using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace remoteServer.Network
{
    public class AsyncTcpListener
    {
        private readonly TcpListener _listener;

        public AsyncTcpListener(IPAddress address, int port)
        {
            _listener = new TcpListener(address, port);
        }

        public void Start() => _listener.Start();

        public Task<TcpClient> AcceptClientAsync() => _listener.AcceptTcpClientAsync();

        public void Stop() => _listener.Stop();
    }
}
