using Shared;
using remoteServer.Utils;
using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace remoteServer.Network
{
    // ClientSession ??i di?n cho 1 k?t n?i client
    // Ch?y vòng l?p nh?n không ch?n (async) và x? lí Packet
    public class ClientSession
    {
        private readonly TcpClient _client;
        private readonly SslStream _sslStream;
        private readonly Guid _sessionId;

        public ClientSession(TcpClient client, SslStream sslStream)
        {
            _client = client;
            _sslStream = sslStream;
            _sessionId = Guid.NewGuid();
        }

        public Guid SessionId => _sessionId;

        // RunAsync start vòng nh?n d? li?u - non blocking
        public async Task RunAsync(CancellationToken ct)
        {
            try
            {
                await ReceiveLoopAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine("Session error: " + ex);
            }
            finally
            {
                _sslStream.Close();
                _client.Close();
            }
        }

        // Vòng nh?n packet
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var headerBuf = new byte[PacketHeader.HeaderSize];
            while (!ct.IsCancellationRequested)
            {
                // read header async (non-blocking)
                int read = 0;
                while (read < headerBuf.Length)
                {
                    int r = await _sslStream.ReadAsync(headerBuf, read, headerBuf.Length - read, ct).ConfigureAwait(false);
                    if (r == 0) return; // k?t n?i ?óng
                    read += r;
                }

                var header = PacketHeader.FromBytes(headerBuf);
                if (header.PayloadLength < 0) return;
                var payload = new byte[header.PayloadLength];
                read = 0;
                while (read < payload.Length)
                {
                    int r = await _sslStream.ReadAsync(payload, read, payload.Length - read, ct).ConfigureAwait(false);
                    if (r == 0) return;
                    read += r;
                }

                // ? lab này payload là JSON DTO (ch?a mã hoá)
                await ProcessPacketAsync(header, payload).ConfigureAwait(false);
            }
        }

        // B?n s?a: ProcessPacket s? dùng async và g?i InputSimulator
        private async Task ProcessPacketAsync(PacketHeader header, byte[] payload)
        {
            try
            {
                switch (header.PacketType)
                {
                    case PacketType.LoginRequest:
                        {
                            var dto = JsonSerializer.Deserialize<LoginRequestDto>(payload);
                            Console.WriteLine($"Login attempt: {dto?.Username}");
                            // TODO: validate username/passwordHash
                            var resp = new LoginResponseDto(true, "Welcome");
                            // g?i response không ch?n
                            await SendPacketAsync(PacketType.LoginResponse, JsonSerializer.SerializeToUtf8Bytes(resp)).ConfigureAwait(false);
                        }
                        break;
                    case PacketType.InputEvent:
                        {
                            var evt = JsonSerializer.Deserialize<InputEventDto>(payload);
                            Console.WriteLine($"Input event: {evt?.Type} {evt?.X},{evt?.Y} key={evt?.KeyCode}");
                            // Th?c hi?n mô ph?ng input trên máy b? ?i?u khi?n
                            if (evt != null)
                            {
                                // Ch?y trên thread pool ?? không ch?n loop
                                await Task.Run(() => InputSimulator.SimulateInput(evt)).ConfigureAwait(false);
                            }
                        }
                        break;
                    case PacketType.Heartbeat:
                        // ignore heartbeat
                        break;
                    default:
                        Console.WriteLine("Unhandled packet: " + header.PacketType);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProcessPacket error: " + ex);
            }
        }

        // G?i packet b?t ??ng b?
        public async Task SendPacketAsync(PacketType type, byte[] payload)
        {
            var header = new PacketHeader { PacketType = type, PayloadLength = payload.Length, SessionId = _sessionId };
            var headerBytes = header.ToBytes();
            await _sslStream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
            if (payload.Length > 0)
                await _sslStream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
            await _sslStream.FlushAsync().ConfigureAwait(false);
        }
    }
}
