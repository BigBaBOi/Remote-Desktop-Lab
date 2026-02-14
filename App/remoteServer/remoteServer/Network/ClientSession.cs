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
        private readonly TcpClient _client;
        private Stream _stream; // Stream mạng (có thể là NetworkStream hoặc SslStream)
        private readonly ServerService _service;
        private readonly X509Certificate2? _serverCertificate;
        private bool _isAuthenticated = false; // Trạng thái đăng nhập
        private string _clientIp;
        
        // Encryption (Bảo mật)
        private string _rsaPublicKey;
        private string _rsaPrivateKey;
        private byte[] _aesKey;
        private byte[] _aesIV;
        private bool _isEncrypted = false; // Trạng thái mã hóa Packet
        
        // Services & State
        private int _sessionId = -1; // ID log trong Database
        private CancellationTokenSource _cts; // Dùng để hủy các tác vụ nền khi disconnect
        private ScreenCaptureService _screenCapture;
        private InputSimulatorService _inputSimulator;

        public ClientSession(TcpClient client, X509Certificate2? certificate)
        {
            _client = client;
            _serverCertificate = certificate;
            _service = new ServerService();
            _screenCapture = new ScreenCaptureService();
            _inputSimulator = new InputSimulatorService();
            _cts = new CancellationTokenSource();
            
            // Tạo cặp khóa RSA mới cho mỗi session
            var keys = SecurityHelper.GenerateRsaKeys();
            _rsaPublicKey = keys.PublicKey;
            _rsaPrivateKey = keys.PrivateKey;

            try {
                _clientIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            } catch {
                _clientIp = "Unknown";
            }
        }

        /// <summary>
        /// Vòng lặp chính xử lý kết nối Client.
        /// </summary>
        public async Task ProcessAsync()
        {
            try
            {
                Stream networkStream = _client.GetStream();

                // 1. Thiết lập SSL/TLS nếu có chứng chỉ
                if (_serverCertificate != null)
                {
                    SslStream sslStream = new SslStream(networkStream, false);
                    try
                    {
                        // Xác thực Server với Client
                        await sslStream.AuthenticateAsServerAsync(_serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: false);
                        _stream = sslStream;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi SSL: {ex.Message}");
                        return;
                    }
                }
                else
                {
                    _stream = networkStream;
                }

                // 2. Gửi RSA Public Key cho Client để bắt đầu Handshake
                await SendPublicKeyAsync();

                // 3. Vòng lặp nhận Packet từ Client
                while (_client.Connected && !_cts.Token.IsCancellationRequested)
                {
                    // Đọc Header (cố định kích thước)
                    byte[] headerBuffer = new byte[PacketHeader.Size];
                    int bytesRead = await ReadExactAsync(_stream, headerBuffer, headerBuffer.Length);
                    if (bytesRead == 0) break; // Client đóng kết nối

                    // Convert byte[] -> Struct PacketHeader
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

                    // Đọc Payload (dựa trên header.PayloadLength)
                    byte[] payloadBuffer = Array.Empty<byte>();
                    if (header.PayloadLength > 0)
                    {
                        payloadBuffer = new byte[header.PayloadLength];
                        int payloadRead = await ReadExactAsync(_stream, payloadBuffer, header.PayloadLength);
                        if (payloadRead != header.PayloadLength) break; // Lỗi đọc thiếu dữ liệu
                    }

                    // Giải mã Payload nếu đã thiết lập encryption (trừ gói Handshake ban đầu)
                    if (_isEncrypted && header.Type != PacketType.Handshake)
                    {
                        try 
                        {
                            payloadBuffer = SecurityHelper.AesDecrypt(payloadBuffer, _aesKey, _aesIV);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Lỗi giải mã: {ex.Message}");
                            break; // Dữ liệu sai lệch -> ngắt kết nối
                        }
                    }

                    // Xử lý gói tin
                    await HandlePacketAsync(header.Type, payloadBuffer);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi Client: {ex.Message}");
            }
            finally
            {
                // Dọn dẹp: Ghi log kết thúc, hủy task, đóng socket
                if (_sessionId > 0) _service.LogSessionEnd(_sessionId);
                _cts.Cancel();
                _client.Close();
                Console.WriteLine("Client đã ngắt kết nối.");
            }
        }

        /// <summary>
        /// Gửi RSA Public Key (không mã hóa)
        /// </summary>
        private async Task SendPublicKeyAsync()
        {
            var pubKeyDto = new PublicKeyDto { PublicKeyXml = _rsaPublicKey };
            byte[] payload = SerializationHelper.Serialize(pubKeyDto);
            await SendPacketAsync(PacketType.Handshake, payload, forcePlain: true);
        }

        /// <summary>
        /// Xử lý logic từng loại gói tin nhận được.
        /// </summary>
        private async Task HandlePacketAsync(PacketType type, byte[] payload)
        {
            switch (type)
            {
                case PacketType.Handshake:
                    // Nhận AES Key từ Client (được mã hóa bằng RSA Public Key của Server)
                    var handshakeDto = SerializationHelper.Deserialize<HandshakeDto>(payload);
                    if (handshakeDto != null)
                    {
                        // Giải mã RSA để lấy AES Key/IV
                        _aesKey = SecurityHelper.RsaDecrypt(handshakeDto.EncryptedAesKey, _rsaPrivateKey);
                        _aesIV = SecurityHelper.RsaDecrypt(handshakeDto.EncryptedAesIV, _rsaPrivateKey);
                        _isEncrypted = true; // Bật cờ mã hóa cho các gói tin sau
                        Console.WriteLine($"Đã thiết lập mã hóa với {_clientIp}");
                    }
                    break;

                case PacketType.LoginRequest:
                    var loginDto = SerializationHelper.Deserialize<LoginRequestDto>(payload);
                    bool success = false;
                    string msg = "Thất bại";
                    if (loginDto != null)
                    {
                        // Kiểm tra DB
                        success = _service.Login(loginDto.Username, loginDto.PasswordHash);
                        msg = success ? "OK" : "Sai thông tin đăng nhập";
                        if (success) 
                        {
                            _isAuthenticated = true;
                            // Ghi log session vào DB
                            _sessionId = _service.LogSession(loginDto.Username, _clientIp);
                            // Bắt đầu luồng chụp màn hình gửi cho Client
                            _ = Task.Run(() => SendScreenUpdatesAsync(_cts.Token));
                        }
                    }
                    var responseDto = new LoginResponseDto { IsSuccess = success, Message = msg };
                    byte[] responsePayload = SerializationHelper.Serialize(responseDto);
                    await SendPacketAsync(PacketType.LoginResponse, responsePayload);
                    break;
                
                case PacketType.InputEvent:
                    // Nhận sự kiện chuột/phím và giả lập
                    if (_isAuthenticated)
                    {
                        var inputDto = SerializationHelper.Deserialize<InputEventDto>(payload);
                        if (inputDto != null)
                        {
                            _inputSimulator.ProcessInput(inputDto);
                        }
                    }
                    break;

                case PacketType.FileChunk:
                    // Nhận file chia nhỏ
                    if (_isAuthenticated)
                    {
                        var chunkDto = SerializationHelper.Deserialize<FileChunkDto>(payload);
                        if (chunkDto != null)
                        {
                            // Lưu vào thư mục ReceivedFiles
                            string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReceivedFiles");
                            Directory.CreateDirectory(saveDir);
                            string filePath = Path.Combine(saveDir, chunkDto.FileName);
                            
                            // Ghi nối tiếp (Append)
                            using (var fs = new FileStream(filePath, chunkDto.ChunkIndex == 0 ? FileMode.Create : FileMode.Append, FileAccess.Write))
                            {
                                await fs.WriteAsync(chunkDto.Data, 0, chunkDto.Data.Length);
                            }
                            
                            if (chunkDto.IsLastChunk)
                            {
                                Console.WriteLine($"Đã nhận file: {chunkDto.FileName} ({chunkDto.FileSize} bytes)");
                            }
                        }
                    }
                    break;

                case PacketType.Disconnect:
                    _client.Close();
                    _cts.Cancel();
                    break;
            }
            await Task.Yield();
        }
        
        /// <summary>
        /// Luồng chạy nền: Chụp màn hình -> Nén -> Gửi
        /// </summary>
        private async Task SendScreenUpdatesAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _client.Connected)
            {
                try
                {
                    int w, h;
                    // Chụp màn hình
                    byte[] imageBytes = _screenCapture.CaptureScreen(out w, out h);
                    
                    if (imageBytes.Length > 0)
                    {
                        var screenDto = new ScreenFrameDto
                        {
                            ImageData = imageBytes,
                            Width = w,
                            Height = h,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        };
                        
                        byte[] payload = SerializationHelper.Serialize(screenDto);
                        await SendPacketAsync(PacketType.ScreenFrame, payload);
                    }
                    
                    // Giới hạn FPS (~10 FPS) để tránh quá tải CPU và mạng
                    await Task.Delay(100, token); 
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi gửi màn hình: {ex.Message}");
                    break;
                }
            }
        }

        /// <summary>
        /// Gửi gói tin (Đóng gói Header + Payload + Mã hóa AES)
        /// </summary>
        private async Task SendPacketAsync(PacketType type, byte[] payload, bool forcePlain = false)
        {
            if (!_client.Connected) return;

            byte[] processedPayload = payload;
            // Mã hóa Payload nếu cần
            if (_isEncrypted && !forcePlain)
            {
                processedPayload = SecurityHelper.AesEncrypt(payload, _aesKey, _aesIV);
            }

            // Tạo Header
            var header = new PacketHeader
            {
                Type = type,
                PayloadLength = processedPayload.Length,
                SessionId = new byte[16] 
            };

            // Convert Header struct -> byte[]
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

            // Gửi qua stream (Thread-safe)
            try 
            {
                await WriteToStreamAsync(headerBytes, processedPayload);
            }
            catch { _client.Close(); }
            
        }
        
        private readonly System.Threading.SemaphoreSlim _streamLock = new System.Threading.SemaphoreSlim(1, 1);
        
        /// <summary>
        /// Ghi byte raw xuống stream một cách tuần tự (Thread-safe).
        /// </summary>
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
                _client.Close();
                _cts.Cancel();
            }
            finally
            {
                _streamLock.Release();
            }
        }

        /// <summary>
        /// Đọc chính xác N bytes từ stream (tránh trường hợp TCP phân mảnh gói tin).
        /// </summary>
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
    }
}
