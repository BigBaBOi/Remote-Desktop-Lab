using System;

namespace remoteServer.Services
{
    public class ServerService
    {
        private readonly DatabaseService _dbService;

        public ServerService()
        {
            _dbService = new DatabaseService();
        }

        public bool Login(string username, string passwordHash)
        {
            return _dbService.ValidateUser(username, passwordHash);
        }

        public bool Register(string username, string passwordHash, out string message)
        {
            return _dbService.RegisterUser(username, passwordHash, out message);
        }

        public int LogSession(string username, string clientIp)
        {
            return _dbService.LogSessionStart(username, clientIp);
        }

        public void LogSessionEnd(int sessionId)
        {
            _dbService.LogSessionEnd(sessionId);
        }
    }
}
