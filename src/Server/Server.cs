using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server
{
    class Server
    {
        private readonly ConcurrentBag<ClientConnection> _connections = new ConcurrentBag<ClientConnection>();
        private readonly ConcurrentQueue<byte[]> _readChannel = new ConcurrentQueue<byte[]>();
        private readonly int _port;
        private readonly int _backlog;

        public Server(int port, int backlog)
        {
            _port = port;
            _backlog = backlog;
        }

        public IProducerConsumerCollection<byte[]> Channel

        public async Task Start()
        {
            using (Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                listener.Bind(new IPEndPoint(IPAddress.Any, _port));
                listener.Listen(_backlog);

                Console.WriteLine("Server started on: " + listener.LocalEndPoint);

                while (true)
                {
                    Socket clientSocket = await AcceptAsync(listener);

                    var connection = new ClientConnection(clientSocket, _readChannel);
                    connection.Start();

                    _connections.Add(connection);
                }
            }
        }

        private Task<Socket> AcceptAsync(Socket listener)
        {
            var tcs = new TaskCompletionSource<Socket>();
            listener.BeginAccept(EndCallback, listener);

            return tcs.Task;

            void EndCallback(IAsyncResult asyncResult)
            {
                Socket listenerState = (Socket)asyncResult.AsyncState;

                try
                {
                    Socket client = listenerState.EndAccept(asyncResult);

                    tcs.TrySetResult(client);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }
        }
    }
}
