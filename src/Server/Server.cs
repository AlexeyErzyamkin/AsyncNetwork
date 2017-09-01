using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server
{
    interface IClientConnectionFactory
    {
        ClientConnection Create(Guid clientId, Socket socket);
    }

    class Server
    {
        public Server(int port, int backlog, IClientConnectionFactory connectionFactory)
        {
            _port = port;
            _backlog = backlog;
            _connectionFactory = connectionFactory;
        }

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

                    var clientId = Guid.NewGuid();
                    var connection = _connectionFactory.Create(clientId, clientSocket);
                    connection.Start();

                    // if (!_connections.TryAdd(clientId, connection))
                    // {
                    //     await connection.DisconnectAsync();
                    // }
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

        #region Private Fields

        // private readonly ConcurrentDictionary<Guid, ClientConnection> _connections = new ConcurrentDictionary<Guid, ClientConnection>();
        private readonly int _port;
        private readonly int _backlog;

        private readonly IClientConnectionFactory _connectionFactory;

        #endregion Private Fields

    }
}
