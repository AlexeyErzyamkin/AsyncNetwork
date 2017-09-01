using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Server
{
    //interface IClient
    //{
    //    void Send(byte[] data);
    //}

    //class WeakClient : IClient
    //{
    //    private readonly WeakReference<ClientConnection> _connection;

    //    public WeakClient(ClientConnection connection)
    //    {
    //        _connection = new WeakReference<ClientConnection>(connection);
    //    }

    //    public void Send(byte[] data)
    //    {
    //        if (_connection.TryGetTarget(out ClientConnection connection))
    //        {
    //            connection.Send(data);
    //        }
    //    }
    //}

    class ClientConnection
    {
        private const short Header = 0x0CAE;
        private const int FullHeaderLength = 4;
        private const int BufferSize = FullHeaderLength + 1000;

        public ClientConnection(Guid clientId, Socket socket, Func<ClientConnection, byte[], Task> dataReceivedHandler)
        {
            ClientId = clientId;
            _socket = socket;
            _dataReceivedHandler = dataReceivedHandler;

            _sendBlock = new BufferBlock<byte[]>();

            // _sendBlock = new BufferBlock<byte[]>(new DataflowBlockOptions
            // {
            //     BoundedCapacity = 1
            // });
        }

        public Guid ClientId { get; }

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

                            if (_dataReceivedHandler != null)
                            {
                                await _dataReceivedHandler(this, data);
                            }
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                },
                _cancellationTokenSource.Token
            );

            _sendTask = Task.Run(
                async () =>
                {
                    while (await _sendBlock.OutputAvailableAsync())
                    {
                        byte[] dataToSend = await _sendBlock.ReceiveAsync();
                        if (dataToSend != null)
                        {
                            await SendAsync(dataToSend);
                        }
                    }
                },
                _cancellationTokenSource.Token
            );
        }

        public async Task Stop()
        {
            _cancellationTokenSource.Cancel();

            await Task.WhenAll(_readTask, _sendTask);
        }

        public async Task Send(byte[] data)
        {
            if (data != null)
            {
                await _sendBlock.SendAsync(data);
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
            var size = _readBuffer.Length - _readOffset;

            _socket.BeginReceive(_readBuffer, _readOffset, size, SocketFlags.None, EndCallback, _socket);
            
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

        private Task SendAsync(byte[] data)
        {
            var tcs = new TaskCompletionSource<int>();
            _socket.BeginSend(data, 0, data.Length, SocketFlags.None, EndCallback, _socket);

            return tcs.Task;

            void EndCallback(IAsyncResult asyncResult)
            {
                var socketLocal = (Socket)asyncResult.AsyncState;
                try
                {
                    int bytesRead = socketLocal.EndSend(asyncResult);
                    tcs.TrySetResult(bytesRead);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    tcs.TrySetException(ex);
                }
            }
        }

        public Task DisconnectAsync()
        {
            var tcs = new TaskCompletionSource<int>();
            _socket.BeginDisconnect(false, EndCallback, _socket);

            return tcs.Task;

            void EndCallback(IAsyncResult asyncResult)
            {
                var socket = (Socket)asyncResult.AsyncState;
                try
                {
                    socket.EndDisconnect(asyncResult);
                    tcs.TrySetResult(0);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }
        }

#region Private Fields

        private readonly Guid _clientId;
        private readonly Socket _socket;

        private readonly byte[] _readBuffer = new byte[BufferSize];
        private int _readOffset;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task _readTask;
        private Task _sendTask;

        private readonly Func<ClientConnection, byte[], Task> _dataReceivedHandler;
        private readonly BufferBlock<byte[]> _sendBlock;

#endregion Private Fields

    }
}