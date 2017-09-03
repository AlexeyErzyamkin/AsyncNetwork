using System.Net.Sockets;

namespace SyncServer
{
    public interface IConnectionFactory
    {
        Connection CreateConnection(Socket socket);
    }
}
