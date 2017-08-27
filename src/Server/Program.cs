using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new Server(10891, 1000);

            server.Start2();

            Console.WriteLine("Press ENTER to exit...");
            Console.ReadLine();

            server.Stop();
        }
    }

    class Server
    {
        private readonly ConcurrentBag<Connection> _connections = new ConcurrentBag<Connection>();
        private readonly int _port;
        private readonly int _backlog;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);

        public Server(int port, int backlog)
        {
            _port = port;
            _backlog = backlog;
        }

        public void Start2()
        {
            _stopEvent.Reset();

            Task.Run(
                async () =>
                {
                    try
                    {
                        await Start();
                    }
                    finally
                    {
                        _stopEvent.Set();
                    }
                },
                _cancellationTokenSource.Token
            );
        }

        public async Task Start()
        {
            using (Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                listener.Bind(new IPEndPoint(IPAddress.Any, _port));
                listener.Listen(_backlog);

                Console.WriteLine("Server started on: " + listener.LocalEndPoint);

                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Socket clientSocket = await AcceptAsync(listener);

                    var connection = new Connection(clientSocket, _cancellationTokenSource.Token);
                    connection.Start();

                    _connections.Add(connection);
                }
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();

            _stopEvent.WaitOne();
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

    class Connection
    {
        private const short Header = 0x0CAE;
        private const int FullHeaderLength = 4;
        private const int BufferSize = 1000;

        private readonly Socket _socket;
        private readonly CancellationToken _cancellationToken;
        private readonly byte[] _readBuffer = new byte[BufferSize];
        
        private int _readOffset;

        public Connection(Socket socket, CancellationToken cancellationToken)
        {
            _socket = socket;
            _cancellationToken = cancellationToken;
        }

        public void Start()
        {
            Task.Run(
                async () =>
                {
                    try
                    {
                        while (!_cancellationToken.IsCancellationRequested)
                        {
                            byte[] data = await ReadData();
                            await SendAsync(data);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    await DisconnectAsync();
                },
                _cancellationToken
            );
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
            _socket.BeginReceive(_readBuffer, _readOffset, _readBuffer.Length - _readOffset, SocketFlags.None, EndCallback, _socket);
            
            return tcs.Task;

            void EndCallback(IAsyncResult asyncResult)
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
            }
        }

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
            _socket.BeginSend(data, 0, data.Length, SocketFlags.None, EndCallback, _socket);

            return tcs.Task;

            void EndCallback(IAsyncResult asyncResult)
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
            }
        }

        private async Task DisconnectAsync()
        {
            await Task.Factory.FromAsync(
                _socket.BeginDisconnect(false, null, null),
                _socket.EndDisconnect
            );
        }
    }
}
