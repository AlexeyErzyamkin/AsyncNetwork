using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    class ClientConnection
    {
        private const short Header = 0x0CAE;
        private const int FullHeaderLength = 4;
        private const int BufferSize = 1000;

        private readonly Socket _socket;

        //private readonly CancellationToken _cancellationToken;
        private readonly byte[] _readBuffer = new byte[BufferSize];
        private int _readOffset;

        private readonly IProducerConsumerCollection<byte[]> _readChannel;
        private readonly ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();

        private Task _readTask;
        // private Task _sendTask;
        
        private readonly object _sendTaskSync = new object();
        private volatile bool _sendTaskWorking;

        public ClientConnection(Socket socket, IProducerConsumerCollection<byte[]> readChannel)
        {
            _socket = socket;
            _readChannel = readChannel;
        }

        public void Start()
        {
            _readTask = Task.Run(
                async () =>
                {
                    try
                    {
                        while (true)
                        {
                            byte[] data = await ReadData();
                            _readChannel.TryAdd(data);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            );
        }

        public void Send(byte[] data)
        {
            _sendQueue.Enqueue(data);

            if (!_sendTaskWorking)
            {
                lock (_sendTaskSync)
                {
                    if (!_sendTaskWorking)
                    {
                        Task
                            .Run(
                                async () =>
                                {
                                    while (_sendQueue.TryDequeue(out byte[] dataToSend))
                                    {
                                        await SendAsync(data);
                                    }
                                })
                            .ContinueWith(
                                state =>
                                {
                                    _sendTaskWorking = false;
                                });
                    }
                }
            }
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
                var socket = (Socket)asyncResult.AsyncState;
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