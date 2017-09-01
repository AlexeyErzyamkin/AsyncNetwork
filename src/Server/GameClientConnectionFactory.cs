using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    class GameClientConnectionFactory : IClientConnectionFactory
    {
        public ClientConnection Create(Guid clientId, Socket socket)
        {
            return new GameClientConnection(clientId, socket);
        }
    }
}
