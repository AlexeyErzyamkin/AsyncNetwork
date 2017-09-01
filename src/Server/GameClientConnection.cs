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

        private static async Task DataReceivedHandler(ClientConnection self, byte[] data)
        {
            // Some processing

            await self.Send(data);
        }
    }
}
