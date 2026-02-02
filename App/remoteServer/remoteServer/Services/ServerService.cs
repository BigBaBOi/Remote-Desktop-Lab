using Shared;
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
    // ServerService: qu?n lí listener, ch?p nh?n client và qu?n lí sessions
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
            // Không dispose ssl stream ? ?ây n?u mu?n truy?n ssl vào ClientSession; nh?ng ? sample ??n gi?n dùng using
            using var ssl = new SslStream(tcp.GetStream(), false, new RemoteCertificateValidationCallback(ValidateClientCertificate));
            try
            {
                bool requireClientCert = _caCert != null;
                // server authenticate b?ng ch?ng ch? n?i b?
                await ssl.AuthenticateAsServerAsync(_serverCert, requireClientCert, System.Security.Authentication.SslProtocols.Tls13, false).ConfigureAwait(false);
                var session = new ClientSession(tcp, ssl);
                _sessions[session.SessionId] = session;
                _ = session.RunAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine("HandleClient error: " + ex);
            }
        }

        private bool ValidateClientCertificate(object? sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            if (_caCert == null) return true; // not requiring client cert
            if (certificate == null) return false;
            // Simplified validation: check thumbprint matches CA issued cert chain root
            try
            {
                var cert2 = new X509Certificate2(certificate);
                // Basic chain build to verify issuer = CA
                using var chain2 = new X509Chain();
                chain2.ChainPolicy.ExtraStore.Add(_caCert);
                chain2.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain2.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                bool ok = chain2.Build(cert2);
                return ok;
            }
            catch { return false; }
        }
    }
}
