using remoteServer.Services;
using remoteServer.Utils;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace remoteServer
{
    public partial class Form1 : Form
    {
        private ServerService? _server;
        private X509Certificate2? _serverCert;
        private X509Certificate2? _caCert;

        public Form1()
        {
            InitializeComponent();
            Load += Form1_Load;
            FormClosing += Form1_FormClosing;
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            // Không t? ??ng start server ? Load n?a. ??i user nh?n Start.
            Log("?ng d?ng s?n sàng");
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            StopServer();
        }

        // Nút Start nh?n -> t?o CA n?i b? + server cert, kh?i ??ng ServerService
        private void BtnStart_Click(object sender, EventArgs e)
        {
            try
            {
                // T?o CA n?i b? và server cert ???c CA ký. Trong th?c t? nên l?u CA ra file và qu?n lý private key an toàn.
                _caCert = CertUtil.CreateCertificateAuthority("RemoteDesktopLocalCA");
                _serverCert = CertUtil.CreateCertificateSignedByCA(_caCert, "RemoteServer", isServer: true);

                // Kh?i ??ng server, truy?n vào caCert ?? yêu c?u client ch?ng th?c
                _server = new ServerService(IPAddress.Any, 9000, _serverCert, _caCert);
                _server.Start();
                lblStatus.Text = "Status: Running";
                Log("Server started on port 9000 with mutual TLS (CA internal)");
                btnStart.Enabled = false;
                btnStop.Enabled = true;
            }
            catch (Exception ex)
            {
                Log("Start error: " + ex.Message);
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            StopServer();
        }

        private void StopServer()
        {
            if (_server != null)
            {
                _server.Stop();
                _server = null;
                lblStatus.Text = "Status: Stopped";
                Log("Server stopped");
                btnStart.Enabled = true;
                btnStop.Enabled = false;
            }
        }

        // Ghi log ra ListBox (invoke n?u c?n t? thread khác)
        private void Log(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Log(message)));
                return;
            }

            lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        }

    }
}
