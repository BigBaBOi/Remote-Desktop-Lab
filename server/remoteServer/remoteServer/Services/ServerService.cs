using remoteServer.Shared;
using remoteServer.Network;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace remoteServer.Services
{
    // ServerService: qu?n lý listener, ch?p nh?n client và qu?n lý sessions
    public class ServerService
    {
        private readonly AsyncTcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<Guid, ClientSession> _sessions = new();
        private readonly X509Certificate2 _serverCert;
        private readonly X509Certificate2? _caCert;

        // N?u mu?n yêu c?u client ch?ng ch? (mutual TLS), truy?n caCert vào ?? validate
        public ServerService(IPAddress bindAddress, int port, X509Certificate2 serverCert, X509Certificate2? caCert = null)
        {
            _listener = new AsyncTcpListener(bindAddress, port);
            _serverCert = serverCert;
            _caCert = caCert;
        }

        // Start server (non-blocking)
        public void Start()
        {
            _listener.Start();
            _ = AcceptLoopAsync();
            Console.WriteLine("Server started");
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
            Console.WriteLine("Server stopped");
        }

        // Vòng accept client b?t ??ng b?
        private async Task AcceptLoopAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var tcp = await _listener.AcceptClientAsync().ConfigureAwait(false);
                    _ = HandleClientAsync(tcp);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine("AcceptLoop error: " + ex);
            }
        }

        // Handle t?ng client: th?c hi?n TLS handshake và kh?i t?o ClientSession
        private async Task HandleClientAsync(TcpClient tcp)
        {
            // Không dispose ssl stream ? ?ây n?u mu?n truy?n ssl vào ClientSession; nh?ng ?? ??n gi?n ta dùng using
            using var ssl = new SslStream(tcp.GetStream(), false, new RemoteCertificateValidationCallback(ValidateClientCertificate));
            try
            {
                bool requireClientCert = _caCert != null;
                // server authenticate b?ng ch?ng ch? n?i b?
                await ssl.AuthenticateAsServerAsync(_serverCert, clientCertificateRequired: requireClientCert, enabledSslProtocols: System.Security.Authentication.SslProtocols.Tls13, checkCertificateRevocation: false).ConfigureAwait(false);

                // N?u client cung c?p certificate, ta có th? l?y thông tin ?? xác th?c user
                if (ssl.RemoteCertificate != null)
                {
                    Console.WriteLine("Client provided certificate: " + ssl.RemoteCertificate.Subject);
                }

                var session = new ClientSession(tcp, ssl);
                _sessions[session.SessionId] = session;
                Console.WriteLine($"Client connected: {session.SessionId}");
                await session.RunAsync(_cts.Token).ConfigureAwait(false);
                _sessions.TryRemove(session.SessionId, out _);
                Console.WriteLine($"Client disconnected: {session.SessionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("HandleClient error: " + ex);
            }
        }

        // Callback ?? validate client certificate theo CA n?i b?
        private bool ValidateClientCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            // N?u không có CA c?u hình, ch?p nh?n t?t c? (fallback)
            if (_caCert == null) return true;

            if (certificate == null)
            {
                Console.WriteLine("No client certificate provided");
                return false;
            }

            try
            {
                var clientCert = new X509Certificate2(certificate);

                // Xây d?ng chain v?i CA n?i b?
                var ch = new X509Chain();
                ch.ChainPolicy.ExtraStore.Add(_caCert);
                ch.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                ch.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

                bool isValid = ch.Build(clientCert);
                if (!isValid)
                {
                    Console.WriteLine("Client certificate chain invalid");
                    return false;
                }

                // Ki?m tra issuer là CA c?u hình
                var issuer = ch.ChainElements[ch.ChainElements.Count - 1].Certificate;
                if (issuer.Thumbprint != _caCert.Thumbprint)
                {
                    Console.WriteLine("Client certificate not signed by configured CA");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ValidateClientCertificate error: " + ex);
                return false;
            }
        }
    }
}
