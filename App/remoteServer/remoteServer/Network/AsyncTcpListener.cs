using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Shared.Utils;
using System.IO;
using System.Windows.Forms;

namespace remoteServer.Network
{
    public class AsyncTcpListener
    {
        private readonly TcpListener _listener;
        private bool _isRunning;
        private X509Certificate2? _serverCertificate;

        public event Action<ClientSession> OnClientConnected; // Event notify UI

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
                if (_serverCertificate == null)
                {
                    MessageBox.Show($"Failed to load existing server.pfx!\nPassword may be incorrect or file corrupted.\nServer will run in UNSECURE mode.", "Certificate Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    MessageBox.Show($"Certificate LOADED successfully:\n{_serverCertificate?.Subject}", "Server Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                string msg = $"[WARNING] server.pfx NOT FOUND at:\n{certPath}\n\nServer will run in UNSECURE mode (Plain TCP).\nTo fix: Copy server.pfx to this folder.";
                MessageBox.Show(msg, "Certificate Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    OnClientConnected?.Invoke(session);
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
