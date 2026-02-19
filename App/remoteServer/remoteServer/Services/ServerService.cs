using System;

namespace remoteServer.Services
{
    /// <summary>
    /// Lớp trung gian (Facade) để giao tiếp với DatabaseService.
    /// Giúp ClientSession không phụ thuộc chặt chẽ vào DatabaseService.
    /// </summary>
    public class ServerService
    {
        private readonly DatabaseService _dbService;

        public ServerService()
        {
            // DatabaseService sẽ tự động khởi tạo kết nối (Auto-discovery) trong constructor này
            _dbService = new DatabaseService();
        }

        /// <summary>
        /// Xác thực đăng nhập.
        /// </summary>
        public bool Login(string username, string passwordHash)
        {
            return _dbService.ValidateUser(username, passwordHash);
        }

        /// <summary>
        /// Đăng ký tài khoản mới.
        /// </summary>
        public bool Register(string username, string passwordHash, out string message)
        {
            return _dbService.RegisterUser(username, passwordHash, out message);
        }

        /// <summary>
        /// Ghi log bắt đầu phiên.
        /// </summary>
        public int LogSession(string username, string clientIp)
        {
            return _dbService.LogSessionStart(username, clientIp);
        }

        /// <summary>
        /// Ghi log kết thúc phiên.
        /// </summary>
        public void LogSessionEnd(int sessionId)
        {
            _dbService.LogSessionEnd(sessionId);
        }
    }
}
