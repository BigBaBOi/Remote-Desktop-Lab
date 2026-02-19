using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Runtime.InteropServices;
using Shared.Protocol;
using Shared.DTO;
using Shared.Security;
using Shared.Utils;
using remoteServer.Services;
using System.Threading;

namespace remoteServer.Network
{
    /// <summary>
    /// Quản lý phiên làm việc của một Client kết nối đến Server.
    /// Chịu trách nhiệm xử lý Handshake, Login, Chụp màn hình, Nhận Input, Nhận File.
    /// </summary>
    public class ClientSession
    {
        #region Fields
        
        // Kết nối mạng
        private readonly TcpClient _client;
        private Stream _stream; // Stream mạng (NetworkStream hoặc SslStream)
        
        // Dịch vụ và Trạng thái
        private readonly ServerService _service;
        private readonly X509Certificate2? _serverCertificate;
        private bool _isAuthenticated = false; // Đã đăng nhập hay chưa
        private string _clientIp;
        private int _sessionId = -1; // ID log trong Database
        private CancellationTokenSource _cts; // Token để hủy các luồng chạy nền

        // Encryption (Bảo mật)
        private string _rsaPublicKey;
        private string _rsaPrivateKey;
        private byte[] _aesKey;
        private byte[] _aesIV;
        private bool _isEncrypted = false; // Cờ bật/tắt mã hóa AES

        // Core Components
        private ScreenCaptureService _screenCapture;
        private InputSimulatorService _inputSimulator;

        // Thread Safety
        private readonly System.Threading.SemaphoreSlim _streamLock = new System.Threading.SemaphoreSlim(1, 1);

        #endregion

        #region Properties

        public string ClientInfo => _clientIp + (_isAuthenticated ? " (Đã đăng nhập)" : "");

        #endregion

        #region Constructor

        public ClientSession(TcpClient client, X509Certificate2? certificate)
        {
            _client = client;
            _serverCertificate = certificate;
            
            // Khởi tạo các Service
            _service = new ServerService();
            _screenCapture = new ScreenCaptureService();
            _inputSimulator = new InputSimulatorService();
            _cts = new CancellationTokenSource();
            
            // Tạo cặp khóa RSA mới (Ephemeral Keys) cho mỗi phiên kết nối
            var keys = SecurityHelper.GenerateRsaKeys();
            _rsaPublicKey = keys.PublicKey;
            _rsaPrivateKey = keys.PrivateKey;

            // Lấy địa chỉ IP
            try {
                _clientIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            } catch {
                _clientIp = "Không xác định";
            }
        }

        public override string ToString() => ClientInfo;

        #endregion

        #region Main Process Loop

        /// <summary>
        /// Vòng lặp chính xử lý kết nối Client (Handshake -> Loop Nhận Packet).
        /// </summary>
        public async Task ProcessAsync()
        {
            try
            {
                Stream networkStream = _client.GetStream();

                // 1. Thiết lập SSL/TLS nếu có chứng chỉ Server
                if (_serverCertificate != null)
                {
                    SslStream sslStream = new SslStream(networkStream, false);
                    try
                    {
                        // Xác thực Server với Client (Client không cần chứng chỉ)
                        await sslStream.AuthenticateAsServerAsync(_serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: false);
                        _stream = sslStream;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Lỗi] SSL Handshake thất bại: {ex.Message}");
                        return;
                    }
                }
                else
                {
                    _stream = networkStream;
                }

                // 2. Gửi RSA Public Key cho Client để bắt đầu Handshake E2E
                await SendPublicKeyAsync();

                // 3. Vòng lặp nhận Packet từ Client
                while (_client.Connected && !_cts.Token.IsCancellationRequested)
                {
                    // Đọc Header (Cố định kích thước struct PacketHeader)
                    byte[] headerBuffer = new byte[PacketHeader.Size];
                    int bytesRead = await ReadExactAsync(_stream, headerBuffer, headerBuffer.Length);
                    if (bytesRead == 0) break; // Client đóng kết nối

                    // Chuyển đổi byte[] -> Header Struct
                    GCHandle handle = GCHandle.Alloc(headerBuffer, GCHandleType.Pinned);
                    PacketHeader header;
                    try
                    {
                        header = Marshal.PtrToStructure<PacketHeader>(handle.AddrOfPinnedObject());
                    }
                    finally
                    {
                        handle.Free();
                    }

                    // Đọc Payload (Dữ liệu chính)
                    byte[] payloadBuffer = Array.Empty<byte>();
                    if (header.PayloadLength > 0)
                    {
                        payloadBuffer = new byte[header.PayloadLength];
                        int payloadRead = await ReadExactAsync(_stream, payloadBuffer, header.PayloadLength);
                        if (payloadRead != header.PayloadLength) break; // Đọc thiếu dữ liệu -> Ngắt kết nối
                    }

                    // Giải mã AES Payload nếu đã qua bước Handshake
                    if (_isEncrypted && header.Type != PacketType.Handshake)
                    {
                        try 
                        {
                            payloadBuffer = SecurityHelper.AesDecrypt(payloadBuffer, _aesKey, _aesIV);
                        }
                        catch
                        {
                            Console.WriteLine($"[Lỗi] Giải mã gói tin thất bại. Ngắt kết nối.");
                            break; 
                        }
                    }

                    // Điều hướng xử lý gói tin
                    await HandlePacketAsync(header.Type, payloadBuffer);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session] Lỗi xử lý: {ex.Message}");
            }
            finally
            {
                CleanupSession();
            }
        }

        #endregion

        #region Logic Methods

        /// <summary>
        /// Xử lý logic từng loại gói tin nhận được.
        /// </summary>
        private async Task HandlePacketAsync(PacketType type, byte[] payload)
        {
            switch (type)
            {
                case PacketType.Handshake:
                    // Client gửi AES Key đã mã hóa bằng RSA
                    var handshakeDto = SerializationHelper.Deserialize<HandshakeDto>(payload);
                    if (handshakeDto != null)
                    {
                        _aesKey = SecurityHelper.RsaDecrypt(handshakeDto.EncryptedAesKey, _rsaPrivateKey);
                        _aesIV = SecurityHelper.RsaDecrypt(handshakeDto.EncryptedAesIV, _rsaPrivateKey);
                        _isEncrypted = true; // Bật mã hóa từ thời điểm này
                        Console.WriteLine($"[Bảo mật] Đã thiết lập mã hóa AES với {_clientIp}");
                    }
                    break;

                case PacketType.LoginRequest:
                    await HandleLoginRequest(payload);
                    break;

                case PacketType.RegisterRequest:
                    await HandleRegisterRequest(payload);
                    break;
                
                case PacketType.InputEvent:
                    // Nhận sự kiện chuột/phím và giả lập lên Server
                    if (_isAuthenticated)
                    {
                        var inputDto = SerializationHelper.Deserialize<InputEventDto>(payload);
                        if (inputDto != null) _inputSimulator.ProcessInput(inputDto);
                    }
                    break;

                case PacketType.FileChunk:
                    await HandleFileChunk(payload);
                    break;

                case PacketType.Disconnect:
                    _client.Close();
                    _cts.Cancel();
                    break;
            }
            // Yield để tránh block thread
            await Task.Yield();
        }

        private async Task HandleLoginRequest(byte[] payload)
        {
            bool loginSuccess = false;
            string loginMsg = "Lỗi hệ thống";
            try 
            {
                var loginDto = SerializationHelper.Deserialize<LoginRequestDto>(payload);
                if (loginDto != null)
                {
                    Console.WriteLine($"[Login] Kiểm tra: {loginDto.Username}");
                    
                    // Wrap synchronous DB call in Task.Run
                    loginSuccess = await Task.Run(() => _service.Login(loginDto.Username, loginDto.PasswordHash));
                    
                    loginMsg = loginSuccess ? "OK" : "Sai tên đăng nhập hoặc mật khẩu";
                    if (loginSuccess) 
                    {
                        _isAuthenticated = true;
                        string clientIp = _clientIp;
                        // Async wrapper for logging
                        _sessionId = await Task.Run(() => _service.LogSession(loginDto.Username, clientIp));
                        Console.WriteLine($"[Login] Thành công. SessionID: {_sessionId}. Bắt đầu gửi màn hình.");
                        
                        // Bắt đầu luồng chụp màn hình gửi đi
                        _cts = new CancellationTokenSource();
                        _ = Task.Run(() => SendScreenUpdatesAsync(_cts.Token));
                    }
                }
            } 
            catch (Exception ex) 
            {
                loginMsg = $"Server Error: {ex.Message}";
            }
            
            // Phản hồi kết quả đăng nhập
            var responseDto = new LoginResponseDto { IsSuccess = loginSuccess, Message = loginMsg };
            await SendPacketAsync(PacketType.LoginResponse, SerializationHelper.Serialize(responseDto));
        }

        private async Task HandleRegisterRequest(byte[] payload)
        {
            bool regSuccess = false;
            string regMsg = "Lỗi hệ thống";
            try 
            {
                var regDto = SerializationHelper.Deserialize<RegisterRequestDto>(payload);
                if (regDto != null)
                {
                    // Wrap DB call and handle 'out' parameter
                    var result = await Task.Run(() => 
                    {
                        string msg;
                        bool success = _service.Register(regDto.Username, regDto.PasswordHash, out msg);
                        return new { Success = success, Message = msg };
                    });

                    regSuccess = result.Success;
                    regMsg = result.Message;
                }
            } 
            catch (Exception ex) 
            {
                regMsg = $"Server Error: {ex.Message}";
            }
            
            var regResponse = new RegisterResponseDto { IsSuccess = regSuccess, Message = regMsg };
            await SendPacketAsync(PacketType.RegisterResponse, SerializationHelper.Serialize(regResponse));
        }

        private async Task HandleFileChunk(byte[] payload)
        {
            if (!_isAuthenticated) return;

            var chunkDto = SerializationHelper.Deserialize<FileChunkDto>(payload);
            if (chunkDto != null)
            {
                string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReceivedFiles");
                Directory.CreateDirectory(saveDir);
                string filePath = Path.Combine(saveDir, chunkDto.FileName);
                
                // Ghi file nối tiếp
                using (var fs = new FileStream(filePath, chunkDto.ChunkIndex == 0 ? FileMode.Create : FileMode.Append, FileAccess.Write))
                {
                    await fs.WriteAsync(chunkDto.Data, 0, chunkDto.Data.Length);
                }
                
                if (chunkDto.IsLastChunk)
                {
                    Console.WriteLine($"[File] Đã nhận xong: {chunkDto.FileName} ({chunkDto.FileSize} bytes)");
                }
            }
        }

        /// <summary>
        /// Luồng chạy nền: Chụp màn hình -> Phát hiện thay đổi -> Gửi đi.
        /// </summary>
        private async Task SendScreenUpdatesAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _client.Connected)
            {
                try
                {
                    int w, h, l, t, totalW, totalH;
                    
                    // Chụp màn hình (Dirty Rect)
                    // Hàm này sẽ trả về null nếu màn hình y hệt frame trước
                    byte[] imageBytes = _screenCapture.CaptureScreen(out w, out h, out l, out t, out totalW, out totalH);
                    
                    if (imageBytes != null && imageBytes.Length > 0)
                    {
                        var screenDto = new ScreenFrameDto
                        {
                            ImageData = imageBytes,
                            Width = w,      // Kích thước của mảnh thay đổi (Chunk)
                            Height = h,
                            Left = l,       // Vị trí của mảnh thay đổi
                            Top = t,
                            TotalWidth = totalW, // Kích thước tổng màn hình
                            TotalHeight = totalH,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        };
                        
                        byte[] payload = SerializationHelper.Serialize(screenDto);
                        await SendPacketAsync(PacketType.ScreenFrame, payload);
                    }
                    
                    // Giới hạn FPS ~50 FPS (Delay 20ms)
                    await Task.Delay(20, token); 
                }
                catch
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Gửi một file từ Server đến Client.
        /// </summary>
        public async Task SendFileAsync(string filePath)
        {
            if (!_client.Connected || !File.Exists(filePath)) return;

            try
            {
                string fileName = Path.GetFileName(filePath);
                long fileSize = new FileInfo(filePath).Length;
                int chunkSize = 4096; // 4KB
                byte[] buffer = new byte[chunkSize];
                int chunkIndex = 0;

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    int bytesRead;
                    while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        var chunkDto = new FileChunkDto
                        {
                            FileName = fileName,
                            FileSize = fileSize,
                            ChunkIndex = chunkIndex++,
                            IsLastChunk = (fs.Position == fileSize),
                            Data = new byte[bytesRead]
                        };
                        Array.Copy(buffer, chunkDto.Data, bytesRead);

                        await SendPacketAsync(PacketType.FileChunk, SerializationHelper.Serialize(chunkDto));
                        
                        if (chunkIndex % 10 == 0) await Task.Delay(10); // Throttle
                    }
                }
                Console.WriteLine($"[File] Đã gửi: {fileName} -> Client");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lỗi] Gửi file: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods (Send Packet, Read Stream)

        private async Task SendPublicKeyAsync()
        {
            var pubKeyDto = new PublicKeyDto { PublicKeyXml = _rsaPublicKey };
            await SendPacketAsync(PacketType.Handshake, SerializationHelper.Serialize(pubKeyDto), forcePlain: true);
        }

        private async Task SendPacketAsync(PacketType type, byte[] payload, bool forcePlain = false)
        {
            if (!_client.Connected) return;

            // Mã hóa AES nếu cần
            if (_isEncrypted && !forcePlain)
            {
                payload = SecurityHelper.AesEncrypt(payload, _aesKey, _aesIV);
            }

            // Tạo Header
            var header = new PacketHeader
            {
                Type = type,
                PayloadLength = payload.Length,
                SessionId = new byte[16] 
            };

            // Header -> Byte[]
            int headerSize = Marshal.SizeOf(header);
            byte[] headerBytes = new byte[headerSize];
            IntPtr ptr = Marshal.AllocHGlobal(headerSize);
            try
            {
                Marshal.StructureToPtr(header, ptr, true);
                Marshal.Copy(ptr, headerBytes, 0, headerSize);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            // Gửi dữ liệu (Thread-safe)
            await WriteToStreamAsync(headerBytes, payload);
        }

        private async Task WriteToStreamAsync(byte[] header, byte[] payload)
        {
            await _streamLock.WaitAsync();
            try
            {
                await _stream.WriteAsync(header, 0, header.Length);
                if (payload.Length > 0)
                {
                    await _stream.WriteAsync(payload, 0, payload.Length);
                }
                await _stream.FlushAsync();
            }
            catch
            {
                CleanupSession();
            }
            finally
            {
                _streamLock.Release();
            }
        }

        private async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int length)
        {
            int totalRead = 0;
            while (totalRead < length)
            {
                int read = await stream.ReadAsync(buffer, totalRead, length - totalRead);
                if (read == 0) return 0;
                totalRead += read;
            }
            return totalRead;
        }

        private void CleanupSession()
        {
            if (_sessionId > 0) _service.LogSessionEnd(_sessionId);
            _cts.Cancel();
            _client.Close();
            Console.WriteLine("[Session] Đã kết thúc.");
        }

        #endregion
    }
}
