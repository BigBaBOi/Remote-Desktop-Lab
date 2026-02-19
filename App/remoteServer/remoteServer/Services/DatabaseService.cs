using System;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

namespace remoteServer.Services
{
    /// <summary>
    /// Service quản lý kết nối CSDL SQL Server và các thao tác liên quan đến người dùng/phiên làm việc.
    /// </summary>
    public class DatabaseService
    {
        // Chuỗi kết nối tĩnh (dùng chung cho toàn bộ ứng dụng)
        private static string _connectionString = "";
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();

        public static void ReloadConnectionString()
        {
            lock (_lock)
            {
                _isInitialized = false;
                _connectionString = "";
                // Re-initialize immediately to test
                new DatabaseService(); 
            }
        }

        public DatabaseService()
        {
            InitializeConnectionString();
        }

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
                        Console.WriteLine($"[DB] Loaded form config: {_connectionString}");
                        return;
                    }
                }

                // 2. Tự động dò tìm chuỗi kết nối phù hợp (Timeout thấp để dò nhanh)
                string[] candidates = new string[]
                {
                    "Server=.;Database=RemoteDesktopDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connection Timeout=3",
                    "Server=.\\SQLEXPRESS;Database=RemoteDesktopDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connection Timeout=3",
                    "Server=127.0.0.1;Database=RemoteDesktopDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connection Timeout=3",
                    "Server=Flynn;Database=RemoteDesktopDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connection Timeout=3",
                    "Server=localhost;Database=RemoteDesktopDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connection Timeout=3"
                };

                Console.WriteLine("[DB] Auto-discovering SQL Server...");
                foreach (var connStr in candidates)
                {
                    try
                    {
                        using (var conn = new SqlConnection(connStr))
                        {
                            conn.Open();
                            _connectionString = connStr;
                            _isInitialized = true;
                            Console.WriteLine($"[DB] Success: {connStr}");
                            return;
                        }
                    }
                    catch { /* Continue */ }
                }

                // Fallback
                _connectionString = candidates[0];
                _isInitialized = true;
                Console.WriteLine("[DB] Failed to discover. Using default.");
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
            Console.WriteLine($"[Validate] Checking user: {username}");
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    Console.WriteLine("[Validate] Ket noi DB OK.");
                    // Truy vấn kiểm tra sự tồn tại của cặp User/Pass
                    // Thêm WITH (NOLOCK) để tránh bị treo nếu bảng đang bị khóa bởi tiến trình khác
                    string query = "SELECT COUNT(1) FROM Users WITH (NOLOCK) WHERE Username = @u AND PasswordHash = @p";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = 5; // Giới hạn 5 giây
                        cmd.Parameters.AddWithValue("@u", username);
                        cmd.Parameters.AddWithValue("@p", passwordHash);
                        int count = (int)cmd.ExecuteScalar();
                        Console.WriteLine($"[Validate] Found: {count}");
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Validate] LOI DB: {ex.Message}");
                // Fallback: Tài khoản mặc định 'admin' được 'hardcode' để test nếu DB lỗi hoặc chưa setup
                // Hash này là SHA256 của 'admin123'
                if (username == "admin" && passwordHash == "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9")
                {
                     Console.WriteLine("[Validate] Fallback admin login SUCCESS.");
                     return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Đăng ký người dùng mới.
        /// </summary>
        public bool RegisterUser(string username, string passwordHash, out string message)
        {
            Console.WriteLine($"[Register] Bat dau dang ky user: {username}");
            Console.WriteLine($"[Register] ConnectionString: {_connectionString}");
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    Console.WriteLine("[Register] Ket noi DB thanh cong!");
                    // Kiểm tra user đã tồn tại chưa
                    string checkQuery = "SELECT COUNT(1) FROM Users WITH (NOLOCK) WHERE Username = @u";
                    using (var cmd = new SqlCommand(checkQuery, conn))
                    {
                        cmd.CommandTimeout = 5; // Tránh treo
                        cmd.Parameters.AddWithValue("@u", username);
                        Console.WriteLine($"[Register] Checking user {username}...");
                        int count = (int)cmd.ExecuteScalar();
                        Console.WriteLine($"[Register] Check done. Count={count}");
                        if (count > 0)
                        {
                            message = "Tên đăng nhập đã tồn tại.";
                            return false;
                        }
                    }

                    // Insert User mới
                    string insertQuery = "INSERT INTO Users (Username, PasswordHash) VALUES (@u, @p)";
                    using (var cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.CommandTimeout = 5; // Tránh treo
                        cmd.Parameters.AddWithValue("@u", username);
                        cmd.Parameters.AddWithValue("@p", passwordHash);
                        Console.WriteLine("[Register] Inserting user...");
                        cmd.ExecuteNonQuery();
                        Console.WriteLine("[Register] Insert done.");
                    }
                    
                    message = "Đăng ký thành công!";
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Register] LOI: {ex.Message}");
                message = "Lỗi Database: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Ghi log bắt đầu phiên làm việc mới vào bảng SessionHistory.
        /// </summary>
        /// <param name="username">Người dùng</param>
        /// <param name="clientIp">IP của Client</param>
        /// <returns>ID của phiên làm việc vừa tạo (dùng để update thời gian kết thúc sau này)</returns>
        public int LogSessionStart(string username, string clientIp)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    // Insert và lấy ngay ID vừa tạo bằng SCOPE_IDENTITY()
                    string query = "INSERT INTO SessionHistory (Username, ClientIP, StartTime) VALUES (@u, @ip, GETDATE()); SELECT SCOPE_IDENTITY();";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = 5; // Tránh treo
                        cmd.Parameters.AddWithValue("@u", username);
                        cmd.Parameters.AddWithValue("@ip", clientIp);
                        Console.WriteLine($"[Session] Logging start for {username}...");
                        var result = cmd.ExecuteScalar();
                        Console.WriteLine($"[Session] Logged. ID={result}");
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi DB (LogSessionStart): {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Cập nhật thời gian kết thúc cho phiên làm việc.
        /// </summary>
        /// <param name="sessionId">ID của phiên làm việc</param>
        public void LogSessionEnd(int sessionId)
        {
            if (sessionId <= 0) return;
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = "UPDATE SessionHistory SET EndTime = GETDATE() WHERE Id = @id";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", sessionId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi DB (LogSessionEnd): {ex.Message}");
            }
        }
        
        /// <summary>
        /// Hàm tiện ích để băm chuỗi thành SHA256 (Hex string).
        /// </summary>
        public static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // Chuyển chuỗi thành byte array và băm
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                
                // Chuyển kết quả băm thành chuỗi Hex
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
