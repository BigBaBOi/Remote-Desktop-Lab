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
    /// <summary>
    /// Lớp lắng nghe kết nối TCP bất đồng bộ.
    /// Chịu trách nhiệm chấp nhận kết nối từ Client và tạo ra các ClientSession.
    /// </summary>
    public class AsyncTcpListener
    {
        private readonly TcpListener _listener;
        private bool _isRunning;
        private X509Certificate2? _serverCertificate;

        /// <summary>
        /// Sự kiện được kích hoạt khi có Client mới kết nối thành công.
        /// </summary>
        public event Action<ClientSession> OnClientConnected;

        public AsyncTcpListener(IPAddress address, int port)
        {
            _listener = new TcpListener(address, port);
            LoadCertificate();
        }

        /// <summary>
        /// Tải chứng chỉ SSL (server.pfx) để mã hóa kết nối.
        /// </summary>
        private void LoadCertificate()
        {
            // Đường dẫn file chứng chỉ
            string certPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server.pfx");
            
            // Nếu không tìm thấy ở thư mục chạy, thử tìm ở thư mục source (chỉ dùng cho môi trường Dev)
            if (!File.Exists(certPath)) {
                 certPath = @"d:\Remote-Desktop-Lab\App\remoteServer\remoteServer\server.pfx"; 
            }

            if (File.Exists(certPath))
            {
                _serverCertificate = CertificateHelper.LoadCertificate(certPath, "password");
                if (_serverCertificate == null)
                {
                    Console.WriteLine("[SSL] Không thể tải server.pfx. Server sẽ chạy ở chế độ KHÔNG BẢO MẬT (Unsecure).");
                }
                else
                {
                    Console.WriteLine($"[SSL] Đã tải chứng chỉ: {_serverCertificate.Subject}");
                }
            }
            else
            {
                Console.WriteLine($"[SSL] Không tìm thấy file {certPath}. Server sẽ chạy ở chế độ KHÔNG BẢO MẬT.");
            }
        }

        /// <summary>
        /// Bắt đầu lắng nghe.
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            _listener.Start();
            _isRunning = true;
            _ = AcceptLoopAsync(); // Chạy vòng lặp chấp nhận kết nối trên luồng nền
            Console.WriteLine("[Listener] Server đã khởi động.");
        }

        /// <summary>
        /// Dừng lắng nghe.
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
        }

        /// <summary>
        /// Vòng lặp liên tục chấp nhận các kết nối mới.
        /// </summary>
        private async Task AcceptLoopAsync()
        {
            while (_isRunning)
            {
                try
                {
                    // Chờ Client kết nối (Non-blocking)
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    
                    Console.WriteLine($"[Listener] Client mới kết nối từ: {client.Client.RemoteEndPoint}");

                    // Tạo Session mới cho Client này
                    ClientSession session = new ClientSession(client, _serverCertificate);
                    
                    // Thông báo cho UI (Server Form) biết
                    OnClientConnected?.Invoke(session);
                    
                    // Bắt đầu xử lý dữ liệu của Session này (chạy song song)
                    _ = session.ProcessAsync();
                }
                catch (ObjectDisposedException) { break; } // Listener bị đóng
                catch (Exception ex)
                {
                    Console.WriteLine($"[Listener] Lỗi chấp nhận kết nối: {ex.Message}");
                }
            }
        }
    }
}
