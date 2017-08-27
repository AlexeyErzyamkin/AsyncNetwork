using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    class ConnectionState
    {
        public Socket Socket { get; set; }
        public byte[] Buffer = new byte[1000];
    }

    class Program
    {
        static void Main(string[] args)
            => MainAsync().GetAwaiter().GetResult();

        private static async Task MainAsync()
        {
            var server = new Server(10891, 1000);

            await server.Start();
        }
    }

    class Server
    {
        private readonly ConcurrentBag<Connection> _connections = new ConcurrentBag<Connection>();
        private readonly int _port;
        private readonly int _backlog;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public Server(int port, int backlog)
        {
            _port = port;
            _backlog = backlog;
        }

        public async Task Start()
        {
            using (Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                listener.Bind(new IPEndPoint(IPAddress.Any, _port));
                listener.Listen(_backlog);

                Console.WriteLine("Server started on: " + listener.LocalEndPoint);

                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    Socket clientSocket = await AcceptAsync(listener);

                    var connection = new Connection(clientSocket);
                    connection.Start();

                    _connections.Add(connection);
                }
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
        }

        private Task<Socket> AcceptAsync(Socket listener)
        {
            var tcs = new TaskCompletionSource<Socket>();
            var state = new AcceptState
            {
                Listener = listener,
                TaskCompletion = tcs
            };

            listener.BeginAccept(EndAcceptCallback, state);

            return tcs.Task;
        }

        private static void EndAcceptCallback(IAsyncResult asyncResult)
        {
            var state = (AcceptState)asyncResult.AsyncState;

            try
            {
                Socket client = state.Listener.EndAccept(asyncResult);

                state.TaskCompletion.TrySetResult(client);
            }
            catch (Exception ex)
            {
                state.TaskCompletion.TrySetException(ex);
            }
        }

        class AcceptState
        {
            public Socket Listener;
            public TaskCompletionSource<Socket> TaskCompletion;
        }
    }

    class Connection
    {
        private const short Header = 0x0CAE;
        private const int FullHeaderLength = 4;
        private const int BufferSize = 256;

        private readonly Socket _socket;
        private readonly byte[] _readBuffer = new byte[BufferSize];
        
        private int _readOffset;

        public Connection(Socket socket)
        {
            _socket = socket;
        }

        public void Start()
        {
            Task.Run(
                async () =>
                {
                    try
                    {
                        while (true)
                        {
                            byte[] data = await ReadData();
                            await SendAsync(data);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    await DisconnectAsync();
                });
        }

        private async Task<byte[]> ReadData()
        {
            byte[] data = null;
            short length = 0;

            int bytesRead;
            while ((bytesRead = await ReceiveAsync()) > 0)
            {
                _readOffset += bytesRead;

                if (length == 0)
                {
                    length = ParseHeader();
                    if (length < 0)
                    {
                        throw new InvalidOperationException();
                    }
                }

                if (length > 0 && length <= GetBytesRead())
                {
                    data = ParseData(length);

                    break;
                }
            }

            return data;
        }

        private Task<int> ReceiveAsync()
        {
            var tcs = new TaskCompletionSource<int>();

            //var state = new AsyncState
            //{
            //    Socket = _socket,
            //    CompletionSource = tcs
            //};

            _socket.BeginReceive(_readBuffer, _readOffset, _readBuffer.Length - _readOffset, SocketFlags.None,
                asyncResult =>
                {
                    var socket = (Socket) asyncResult.AsyncState;
                    try
                    {
                        int bytesRead = socket.EndReceive(asyncResult);
                        tcs.TrySetResult(bytesRead);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }, _socket);
            
            return tcs.Task;
        }

        //private static void EndReceiveCallback(IAsyncResult asyncResult)
        //{
        //    var state = (AsyncState)asyncResult.AsyncState;

        //    try
        //    {
        //        int bytesRead = state.Socket.EndReceive(asyncResult);
        //        state.CompletionSource.TrySetResult(bytesRead);
        //    }
        //    catch (Exception ex)
        //    {
        //        state.CompletionSource.TrySetException(ex);
        //    }
        //}

        private int GetBytesRead()
        {
            return _readOffset - FullHeaderLength;
        }

        private short ParseHeader()
        {
            if (_readOffset < FullHeaderLength)
            {
                return 0;
            }

            short receivedHeader = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(_readBuffer, 0));
            if (receivedHeader != Header)
            {
                return -1;
            }

            return IPAddress.NetworkToHostOrder(BitConverter.ToInt16(_readBuffer, 2));
        }

        private byte[] ParseData(short length)
        {
            if (GetBytesRead() < length)
            {
                return null;
            }

            // Optimize this
            var result = new byte[length];
            Array.Copy(_readBuffer, FullHeaderLength, result, 0, length);

            int fullLength = FullHeaderLength + length;
            int nextDataLength = _readOffset - fullLength;
            Array.Copy(_readBuffer, fullLength, _readBuffer, 0, nextDataLength);

            _readOffset = nextDataLength;

            return result;
        }

        private Task<int> SendAsync(byte[] data)
        {
            var tcs = new TaskCompletionSource<int>();
            
            _socket.BeginSend(data, 0, data.Length, SocketFlags.None,
                asyncResult =>
                {
                    Socket socket = (Socket)asyncResult.AsyncState;

                    try
                    {
                        int bytesRead = socket.EndSend(asyncResult);
                        tcs.TrySetResult(bytesRead);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }, _socket);

            return tcs.Task;
        }

        //private static void EndSendCallback(IAsyncResult asyncResult)
        //{
        //    var state = (AsyncState)asyncResult.AsyncState;

        //    try
        //    {
        //        int bytesRead = state.Socket.EndSend(asyncResult);
        //        state.CompletionSource.TrySetResult(bytesRead);
        //    }
        //    catch (Exception ex)
        //    {
        //        state.CompletionSource.TrySetException(ex);
        //    }
        //}

        private async Task DisconnectAsync()
        {
            await Task.Factory.FromAsync(
                _socket.BeginDisconnect(false, null, null),
                _socket.EndDisconnect
            );
        }

        //class AsyncState
        //{
        //    public Socket Socket;
        //    public TaskCompletionSource<int> CompletionSource;
        //}
    }
}
