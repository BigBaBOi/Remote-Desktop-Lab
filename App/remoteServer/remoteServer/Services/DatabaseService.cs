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
        // Chuỗi kết nối mặc định. Trong thực tế nên để trong file config.
        private readonly string _connectionString = "Server=(localdb)\\mssqllocaldb;Database=RemoteDesktopDB;Trusted_Connection=True;MultipleActiveResultSets=true";

        public DatabaseService()
        {
            // Cho phép ghi đè chuỗi kết nối qua biến môi trường (hữu ích khi deploy)
            var envConn = Environment.GetEnvironmentVariable("REMOTE_DESKTOP_DB_CONN");
            if (!string.IsNullOrEmpty(envConn))
            {
                _connectionString = envConn;
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
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    // Truy vấn kiểm tra sự tồn tại của cặp User/Pass
                    string query = "SELECT COUNT(1) FROM Users WHERE Username = @u AND PasswordHash = @p";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@u", username);
                        cmd.Parameters.AddWithValue("@p", passwordHash);
                        int count = (int)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi DB (ValidateUser): {ex.Message}");
                // Fallback: Tài khoản mặc định 'admin' được 'hardcode' để test nếu DB lỗi hoặc chưa setup
                // Hash này là SHA256 của 'admin123'
                if (username == "admin" && passwordHash == "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9")
                {
                     return true;
                }
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
                        cmd.Parameters.AddWithValue("@u", username);
                        cmd.Parameters.AddWithValue("@ip", clientIp);
                        var result = cmd.ExecuteScalar();
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
