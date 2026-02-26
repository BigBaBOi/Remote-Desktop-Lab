using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Concurrent;
using Shared.Protocol;
using Shared.DTO;
using Shared.Security;
using Shared.Utils;

namespace remoteClient.Network
{
    /// <summary>
    /// Lớp quản lý kết nối từ Client đến Server.
    /// Bao gồm: Kết nối TCP/SSL, Handshake bảo mật, gửi/nhận Packet.
    /// </summary>
    public class ClientConnection
    {
        private TcpClient _client;
        private Stream _stream; // Stream mạng chính (có thể là NetworkStream hoặc SslStream)
        private bool _isConnected;

        // Encryption (Thông tin mã hóa AES)
        private byte[] _aesKey;
        private byte[] _aesIV;
        private bool _isEncrypted = false;

        // Task để đợi Handshake hoàn tất (dùng cho việc chờ đợi ở màn hình Login)
        private TaskCompletionSource<bool> _handshakeTcs = new TaskCompletionSource<bool>();
        public Task HandshakeComplete => _handshakeTcs.Task;

        // Quản lý các FileAck đang đợi
        private readonly ConcurrentDictionary<string, TaskCompletionSource<FileAckDto>> _pendingFileAcks = new ConcurrentDictionary<string, TaskCompletionSource<FileAckDto>>();

        // Sự kiện để UI lắng nghe
        public event Action<string> OnError; // Khi có lỗi kết nối
        public event Action<PacketType, byte[]> OnPacketReceived; // Khi nhận gói tin

        public bool IsConnected => _isConnected && _client != null && _client.Connected;

        /// <summary>
        /// Kết nối đến Server và thiết lập SSL/TLS.
        /// </summary>
        public async Task<bool> ConnectAsync(string ip, int port)
        {
            try
            {
                _handshakeTcs = new TaskCompletionSource<bool>(); // Reset trạng thái handshake
                _client = new TcpClient();

                // 1. Kết nối TCP thuần
                await _client.ConnectAsync(ip, port);

                Stream networkStream = _client.GetStream();

                // 2. Nâng cấp lên SSL/TLS
                // Callback ValidateServerCertificate đang trả về true để chấp nhận chứng chỉ tự ký (Self-signed)
                SslStream sslStream = new SslStream(
                    networkStream,
                    false,
                    new RemoteCertificateValidationCallback(CertificateHelper.ValidateServerCertificate),
                    null
                );

                try
                {
                    // Thực hiện bắt tay SSL với Server
                    // TargetHost phải khớp với CN trong chứng chỉ (nhưng ta ignore lỗi nên tạm để string nào cũng được)
                    await sslStream.AuthenticateAsClientAsync("RemoteDesktopServer");
                    _stream = sslStream;
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Lỗi SSL: {ex.Message}");
                    _client.Close();
                    return false;
                }

                _isConnected = true;

                // Bắt đầu vòng lặp nhận dữ liệu nền
                _ = ProcessAsync();
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Lỗi kết nối: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gửi một gói tin đến Server.
        /// </summary>
        public async Task SendPacketAsync(PacketType type, byte[] payload)
        {
            if (!IsConnected) return;

            byte[] processedPayload = payload;
            // Mã hóa AES payload nếu trạng thái đã được bật (trừ handshake ban đầu)
            if (_isEncrypted && type != PacketType.Handshake)
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

            // Chuyển đổi Header struct -> byte[]
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

            // Ghi xuống NetworkStream
            await WriteToStreamAsync(headerBytes, processedPayload);
        }

        // Khóa Semaphore để đảm bảo thread-safe khi ghi dữ liệu (tránh tranh chấp luồng)
        private readonly System.Threading.SemaphoreSlim _streamLock = new System.Threading.SemaphoreSlim(1, 1);

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
                Disconnect();
            }
            finally
            {
                _streamLock.Release();
            }
        }

        /// <summary>
        /// Gửi file đến Server (Chia nhỏ thành các Chunk 4KB) với hỗ trợ Resume.
        /// </summary>
        public async Task SendFileAsync(string filePath)
        {
            if (!IsConnected || !File.Exists(filePath)) return;

            try
            {
                string fileName = Path.GetFileName(filePath);
                long fileSize = new FileInfo(filePath).Length;
                int chunkSize = 4096; // Kích thước mỗi gói tin: 4KB
                int totalChunks = (int)Math.Ceiling((double)fileSize / chunkSize);
                string fileId = Guid.NewGuid().ToString();

                // 1. Gửi FileMeta
                var metaDto = new FileMetaDto
                {
                    FileId = fileId,
                    FileName = fileName,
                    TotalSize = fileSize,
                    ChunkSize = chunkSize,
                    TotalChunks = totalChunks,
                    Checksum = ""
                };

                var ackTcs = new TaskCompletionSource<FileAckDto>();
                _pendingFileAcks.TryAdd(fileId, ackTcs);

                byte[] metaPayload = SerializationHelper.Serialize(metaDto);
                await SendPacketAsync(PacketType.FileMeta, metaPayload);

                // 2. Chờ FileAck từ Server xác nhận (Timeout 30s)
                var timeoutTask = Task.Delay(30000);
                var completedTask = await Task.WhenAny(ackTcs.Task, timeoutTask);

                _pendingFileAcks.TryRemove(fileId, out _);

                if (completedTask != ackTcs.Task)
                {
                    OnError?.Invoke($"Lỗi gửi file: Timeout không nhận được phản hồi FileAck từ Server.");
                    return;
                }

                var ack = ackTcs.Task.Result;
                if (ack.IsComplete)
                {
                    // Đã nhận đủ trước đó
                    return;
                }

                int startChunkIndex = ack.LastReceivedChunkIndex + 1;

                // 3. Gửi các chunk còn lại
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    long startPosition = (long)startChunkIndex * chunkSize;
                    if (startPosition < fileSize)
                    {
                        fs.Seek(startPosition, SeekOrigin.Begin);
                    }

                    byte[] buffer = new byte[chunkSize];
                    int chunkIndex = startChunkIndex;
                    int bytesRead;

                    while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        bool isLast = (fs.Position == fileSize);
                        byte[] dataToSend = new byte[bytesRead];
                        Array.Copy(buffer, dataToSend, bytesRead);

                        var chunkDto = new FileChunkDto
                        {
                            FileId = fileId,
                            FileName = fileName,
                            FileSize = fileSize,
                            ChunkIndex = chunkIndex++,
                            IsLastChunk = isLast,
                            Data = dataToSend
                        };

                        byte[] payload = SerializationHelper.Serialize(chunkDto);
                        await SendPacketAsync(PacketType.FileChunk, payload);

                        // Delay nhỏ để tránh spam mạng quá nhanh gây nghẽn
                        if (chunkIndex % 10 == 0) await Task.Delay(10);
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Lỗi gửi file: {ex.Message}");
            }
        }

        /// <summary>
        /// Vòng lặp nhận dữ liệu từ Server.
        /// </summary>
        private async Task ProcessAsync()
        {
            try
            {
                while (IsConnected)
                {
                    // 1. Đọc Header
                    byte[] headerBuffer = new byte[PacketHeader.Size];
                    int bytesRead = await ReadExactAsync(_stream, headerBuffer, headerBuffer.Length);
                    if (bytesRead == 0) break; // Server đóng kết nối

                    // Parse Header
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

                    // 2. Đọc Payload
                    byte[] payloadBuffer = Array.Empty<byte>();
                    if (header.PayloadLength > 0)
                    {
                        payloadBuffer = new byte[header.PayloadLength];
                        int payloadRead = await ReadExactAsync(_stream, payloadBuffer, header.PayloadLength);
                        if (payloadRead != header.PayloadLength) break;
                    }

                    // 3. Giải mã Payload (AES)
                    if (_isEncrypted && header.Type != PacketType.Handshake)
                    {
                        try
                        {
                            payloadBuffer = SecurityHelper.AesDecrypt(payloadBuffer, _aesKey, _aesIV);
                        }
                        catch { continue; } // Bỏ qua gói lỗi
                    }

                    // 4. Xử lý Handshake đặc biệt
                    if (header.Type == PacketType.Handshake)
                    {
                        var pubKeyDto = SerializationHelper.Deserialize<PublicKeyDto>(payloadBuffer);
                        if (pubKeyDto != null && !_isEncrypted)
                        {
                            // Nhận Public Key -> Tạo AES Key -> Mã hóa AES Key -> Gửi lại Server
                            await PerformHandshake(pubKeyDto.PublicKeyXml);
                            continue;
                        }
                    }

                    // 4.5. Xử lý gói FileAck nội bộ cho ClientConnection (phần Gửi File)
                    if (header.Type == PacketType.FileAck)
                    {
                        var ackDto = SerializationHelper.Deserialize<FileAckDto>(payloadBuffer);
                        if (ackDto != null && _pendingFileAcks.TryGetValue(ackDto.FileId, out var tcs))
                        {
                            tcs.TrySetResult(ackDto);
                        }
                        continue;
                    }

                    // 5. Bắn sự kiện ra ngoài cho UI xử lý (FileChunk, FileMeta từ Server gửi tới v.v..)
                    OnPacketReceived?.Invoke(header.Type, payloadBuffer);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Lỗi nhận dữ liệu: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Thực hiện quy trình Handshake: Tạo AES Key, mã hóa bằng RSA Public Key của Server và gửi đi.
        /// </summary>
        private async Task PerformHandshake(string serverPublicKey)
        {
            try
            {
                // Tạo AES Key/IV ngẫu nhiên
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.GenerateKey();
                    aes.GenerateIV();
                    _aesKey = aes.Key;
                    _aesIV = aes.IV;
                }

                // Mã hóa AES Key/IV bằng RSA Public Key của Server
                var handshakeDto = new HandshakeDto
                {
                    EncryptedAesKey = SecurityHelper.RsaEncrypt(_aesKey, serverPublicKey),
                    EncryptedAesIV = SecurityHelper.RsaEncrypt(_aesIV, serverPublicKey)
                };

                byte[] payload = SerializationHelper.Serialize(handshakeDto);
                await SendPacketAsync(PacketType.Handshake, payload);
                _isEncrypted = true; // Bật cờ mã hóa phía Client
                _handshakeTcs.TrySetResult(true); // Handshake hoàn tất
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Handshake thất bại: {ex.Message}");
                Disconnect();
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

        public void Disconnect()
        {
            _isConnected = false;
            _client?.Close();
        }
    }
}
