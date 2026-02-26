using System;
using MySql.Data.MySqlClient;
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
                        return;
                    }
                }

                // 2. Danh sách các chuỗi kết nối tiềm năng (Timeout 3s để dò nhanh)
                string[] candidates = new string[]
                {
                    "Server=127.0.0.1;Database=RemoteDesktopDB;Uid=root;Pwd=;Connection Timeout=3",
                    "Server=localhost;Database=RemoteDesktopDB;Uid=root;Pwd=;Connection Timeout=3"
                };

                foreach (var connStr in candidates)
                {
                    if (TestConnection(connStr))
                    {
                        _connectionString = connStr;
                        _isInitialized = true;
                        IsDatabaseAvailable = true;
                        return;
                    }
                }

                // Fallback: Dùng chuỗi đầu tiên nhưng đánh dấu là Database không sẵn sàng
                _connectionString = candidates[0];
                _isInitialized = true;
                IsDatabaseAvailable = false;
            }
        }

        private bool TestConnection(string connStr)
        {
            try
            {
                using (var conn = new MySqlConnection(connStr))
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
            // Nếu Database sập, dùng tài khoản Admin cứng (Hardcode) phục vụ cứu hộ/test
            if (!IsDatabaseAvailable)
            {
                return CheckFallbackAdmin(username, passwordHash);
            }

            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    conn.Open();
                    // WITH (NOLOCK) is SQL Server specific, remove it for MySQL
                    string query = "SELECT COUNT(1) FROM Users WHERE Username = @u AND PasswordHash = @p";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = 5; // Giới hạn 5 giây
                        cmd.Parameters.AddWithValue("@u", username);
                        cmd.Parameters.AddWithValue("@p", passwordHash);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch
            {
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
                using (var conn = new MySqlConnection(_connectionString))
                {
                    conn.Open();
                    // 1. Kiểm tra tồn tại
                    string checkQuery = "SELECT COUNT(1) FROM Users WHERE Username = @u";
                    using (var cmd = new MySqlCommand(checkQuery, conn))
                    {
                        cmd.CommandTimeout = 5;
                        cmd.Parameters.AddWithValue("@u", username);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        if (count > 0)
                        {
                            message = "Tên đăng nhập đã tồn tại.";
                            return false;
                        }
                    }

                    // 2. Thêm mới
                    string insertQuery = "INSERT INTO Users (Username, PasswordHash) VALUES (@u, @p)";
                    using (var cmd = new MySqlCommand(insertQuery, conn))
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
                using (var conn = new MySqlConnection(_connectionString))
                {
                    conn.Open();
                    // Insert và lấy ID vừa tạo - MySQL dùng LAST_INSERT_ID()
                    string query = "INSERT INTO SessionHistory (Username, ClientIP, StartTime) VALUES (@u, @ip, NOW()); SELECT LAST_INSERT_ID();";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = 3; // Timeout ngắn cho Log
                        cmd.Parameters.AddWithValue("@u", username);
                        cmd.Parameters.AddWithValue("@ip", clientIp);
                        var result = cmd.ExecuteScalar();
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch
            {
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
                using (var conn = new MySqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = "UPDATE SessionHistory SET EndTime = NOW() WHERE Id = @id";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = 3;
                        cmd.Parameters.AddWithValue("@id", sessionId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch
            {
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
