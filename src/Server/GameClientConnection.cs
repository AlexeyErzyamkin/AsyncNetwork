using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server
{
    class GameClientConnection : ClientConnection
    {
        public GameClientConnection(Guid clientId, Socket socket) :
            base(clientId, socket, DataReceivedHandler)
        {
        }

        private static Task DataReceivedHandler(ClientConnection self, byte[] data)
        {
            // Some processing

            self.Send(data);

            return Task.CompletedTask;
        }
    }
}
