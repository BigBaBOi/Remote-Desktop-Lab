using System;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace remoteServer.Services
{
    /// <summary>
    /// Service quản lý kết nối CSDL SQL Server (Quản lý người dùng, Lịch sử phiên).
    /// Hỗ trợ tự động dò tìm (Auto-Discovery) chuỗi kết nối phù hợp.
    /// </summary>
    public class DatabaseService
    {
        // Chuỗi kết nối tĩnh (dùng chung cho toàn bộ ứng dụng)
        private static string _connectionString = "";
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();

        // Cờ báo hiệu Database có hoạt động hay không (để tránh thử lại liên tục nếu sập)
        public static bool IsDatabaseAvailable { get; private set; } = false;

        public static void ReloadConnectionString()
        {
            lock (_lock)
            {
                _isInitialized = false;
                _connectionString = "";
                // Khởi tạo lại
                new DatabaseService(); 
            }
        }

        public DatabaseService()
        {
            InitializeConnectionString();
        }

        /// <summary>
        /// Khởi tạo chuỗi kết nối. Thử nhiều phương án (Local, SQLEXPRESS, IP...) để tìm Server.
        /// </summary>
        private void InitializeConnectionString()
        {
            if (_isInitialized) return;

            lock (_lock)
            {
                if (_isInitialized) return;

                // 1. Nếu có file config, ưu tiên dùng nó trước
                string configPath = AppDomain.CurrentDomain.BaseDirectory + "db_config.txt";
                if (System.IO.File.Exists(configPath))
                {
                    string fileContent = System.IO.File.ReadAllText(configPath).Trim();
                    if (!string.IsNullOrEmpty(fileContent))
                    {
                        _connectionString = fileContent;
                        _isInitialized = true;
                        IsDatabaseAvailable = TestConnection(_connectionString);
                        Console.WriteLine($"[DB] Đã tải từ Config: {_connectionString} (Status: {IsDatabaseAvailable})");
                        return;
                    }
                }

                // 2. Danh sách các chuỗi kết nối tiềm năng (Timeout 3s để dò nhanh)
                string[] candidates = new string[]
                {
                    "Server=.;Database=RemoteDesktopDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connection Timeout=3",
                    "Server=.\\SQLEXPRESS;Database=RemoteDesktopDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connection Timeout=3",
                    "Server=127.0.0.1;Database=RemoteDesktopDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connection Timeout=3",
                    "Server=localhost;Database=RemoteDesktopDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connection Timeout=3"
                };

                Console.WriteLine("[DB] Đang tự động dò tìm SQL Server...");
                foreach (var connStr in candidates)
                {
                    if (TestConnection(connStr))
                    {
                        _connectionString = connStr;
                        _isInitialized = true;
                        IsDatabaseAvailable = true;
                        Console.WriteLine($"[DB] Kết nối thành công: {connStr}");
                        return;
                    }
                }

                // Fallback: Dùng chuỗi đầu tiên nhưng đánh dấu là Database không sẵn sàng
                _connectionString = candidates[0];
                _isInitialized = true;
                IsDatabaseAvailable = false;
                Console.WriteLine("[DB] Không tìm thấy SQL Server. Chuyển sang chế độ Offline (Chỉ admin mặc định mới đăng nhập được).");
            }
        }

        private bool TestConnection(string connStr)
        {
            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra thông tin đăng nhập của người dùng.
        /// </summary>
        /// <param name="username">Tên đăng nhập</param>
        /// <param name="passwordHash">Mật khẩu đã băm SHA256</param>
        /// <returns>True nếu hợp lệ, False nếu sai</returns>
        public bool ValidateUser(string username, string passwordHash)
        {
            Console.WriteLine($"[Validate] Kiểm tra User: {username}");
            
            // Nếu Database sập, dùng tài khoản Admin cứng (Hardcode) phục vụ cứu hộ/test
            if (!IsDatabaseAvailable)
            {
                return CheckFallbackAdmin(username, passwordHash);
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    // WITH (NOLOCK): Đọc dữ liệu không cần chờ khóa (Tránh Deadlock)
                    string query = "SELECT COUNT(1) FROM Users WITH (NOLOCK) WHERE Username = @u AND PasswordHash = @p";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = 5; // Giới hạn 5 giây
                        cmd.Parameters.AddWithValue("@u", username);
                        cmd.Parameters.AddWithValue("@p", passwordHash);
                        int count = (int)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Validate] Lỗi DB: {ex.Message}. Chuyển sang Fallback Admin.");
                IsDatabaseAvailable = false; // Đánh dấu DB lỗi để lần sau không thử lại ngay
                return CheckFallbackAdmin(username, passwordHash);
            }
        }

        private bool CheckFallbackAdmin(string username, string passwordHash)
        {
            // Hash SHA256 của 'admin123'
            const string adminHash = "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9";
            if (username == "admin" && passwordHash == adminHash)
            {
                 Console.WriteLine("[Validate] Đăng nhập bằng tài khoản Admin khẩn cấp (Fallback).");
                 return true;
            }
            return false;
        }

        /// <summary>
        /// Đăng ký người dùng mới.
        /// </summary>
        public bool RegisterUser(string username, string passwordHash, out string message)
        {
            if (!IsDatabaseAvailable)
            {
                message = "Lỗi: Không kết nối được Database. Vui lòng kiểm tra Server SQL.";
                return false;
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    // 1. Kiểm tra tồn tại
                    string checkQuery = "SELECT COUNT(1) FROM Users WITH (NOLOCK) WHERE Username = @u";
                    using (var cmd = new SqlCommand(checkQuery, conn))
                    {
                        cmd.CommandTimeout = 5;
                        cmd.Parameters.AddWithValue("@u", username);
                        int count = (int)cmd.ExecuteScalar();
                        if (count > 0)
                        {
                            message = "Tên đăng nhập đã tồn tại.";
                            return false;
                        }
                    }

                    // 2. Thêm mới
                    string insertQuery = "INSERT INTO Users (Username, PasswordHash) VALUES (@u, @p)";
                    using (var cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.CommandTimeout = 5;
                        cmd.Parameters.AddWithValue("@u", username);
                        cmd.Parameters.AddWithValue("@p", passwordHash);
                        cmd.ExecuteNonQuery();
                    }
                    
                    message = "Đăng ký thành công!";
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Register] Lỗi: {ex.Message}");
                message = "Lỗi Database: " + ex.Message;
                IsDatabaseAvailable = false;
                return false;
            }
        }

        /// <summary>
        /// Ghi log bắt đầu phiên làm việc vào bảng SessionHistory.
        /// </summary>
        public int LogSessionStart(string username, string clientIp)
        {
            if (!IsDatabaseAvailable) return -1; // Bỏ qua nếu DB lỗi

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    // Insert và lấy ID vừa tạo
                    string query = "INSERT INTO SessionHistory (Username, ClientIP, StartTime) VALUES (@u, @ip, GETDATE()); SELECT SCOPE_IDENTITY();";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = 3; // Timeout ngắn cho Log
                        cmd.Parameters.AddWithValue("@u", username);
                        cmd.Parameters.AddWithValue("@ip", clientIp);
                        var result = cmd.ExecuteScalar();
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogSession] Lỗi ghi log: {ex.Message}");
                // Không đánh dấu IsDatabaseAvailable = false ở đây vì Log không quan trọng, 
                // có thể lỗi tạm thời nhưng Login vẫn cần hoạt động.
                return -1;
            }
        }

        /// <summary>
        /// Cập nhật thời gian kết thúc phiên làm việc.
        /// </summary>
        public void LogSessionEnd(int sessionId)
        {
            if (sessionId <= 0 || !IsDatabaseAvailable) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = "UPDATE SessionHistory SET EndTime = GETDATE() WHERE Id = @id";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = 3;
                        cmd.Parameters.AddWithValue("@id", sessionId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogSessionEnd] Lỗi: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Hàm băm chuỗi SHA256 (Tĩnh).
        /// </summary>
        public static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
