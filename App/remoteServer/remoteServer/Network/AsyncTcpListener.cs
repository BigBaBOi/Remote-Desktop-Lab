using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Shared.Utils;
using System.IO;

namespace remoteServer.Network
{
    public class AsyncTcpListener
    {
        private readonly TcpListener _listener;
        private bool _isRunning;
        private X509Certificate2? _serverCertificate;

        public AsyncTcpListener(IPAddress address, int port)
        {
            _listener = new TcpListener(address, port);
            LoadCertificate();
        }

        private void LoadCertificate()
        {
            // Path to PFX file - in a real app, strict path management is needed
            string certPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server.pfx");
            // If running from IDE, it might be in project root, but let's assume it's copied to bin or absolute path we generated
            if (!File.Exists(certPath)) {
                 certPath = @"d:\Remote-Desktop-Lab\App\remoteServer\remoteServer\server.pfx"; 
            }

            if (File.Exists(certPath))
            {
                _serverCertificate = CertificateHelper.LoadCertificate(certPath, "password");
                Console.WriteLine($"Certificate loaded: {_serverCertificate?.Subject}");
            }
            else
            {
                Console.WriteLine("Warning: server.pfx not found. SSL will fail.");
            }
        }

        public void Start()
        {
            if (_isRunning) return;
            _listener.Start();
            _isRunning = true;
            _ = AcceptLoopAsync();
        }

        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
        }

        private async Task AcceptLoopAsync()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    // Pass certificate to session
                    ClientSession session = new ClientSession(client, _serverCertificate);
                    _ = session.ProcessAsync();
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"Accept Error: {ex.Message}");
                }
            }
        }
    }
}
