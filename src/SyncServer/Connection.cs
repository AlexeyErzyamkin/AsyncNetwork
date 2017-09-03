using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace SyncServer
{
    public class Connection
    {
        //private const int InitialBufferSize = 10;
        private const short Header = 0x0CAE;
        private const int FullHeaderLength = 4;

        public Connection(Guid id, Socket socket)
        {
            ConnectionId = id;

            _socket = socket;
        }

        public Guid ConnectionId { get; }

        public bool TryRead(out byte[] data)
        {
            bool result = false;
            data = null;

            int bytesAvailable = _socket.Available;
            if (bytesAvailable > 0)
            {
                PrepareReadBuffer(bytesAvailable);

                int bytesRead = _socket.Receive(_readBuffer, _readOffset, bytesAvailable, SocketFlags.None);
                _readOffset += bytesRead;

                var dataLength = ParseHeader();
                if (dataLength < 0)
                {
                    throw new InvalidOperationException();
                }

                if (dataLength > 0 && dataLength <= (_readOffset - FullHeaderLength))
                {
                    data = ParseData(dataLength);
                    result = true;
                }
            }

            return result;
        }

        public void Send(byte[] data)
        {
            //_sendQueue.Enqueue(data);
            _socket.Send(data);
        }
        
        #region Private Methods

        private void PrepareReadBuffer(int lengthToAppend)
        {
            if (lengthToAppend == 0)
            {
                return;
            }

            int newSize = _readOffset + lengthToAppend;
            if (_readBuffer == null)
            {
                _readBuffer = new byte[lengthToAppend];
            }
            else if ( _readBuffer.Length < newSize)
            {
                byte[] newReadBuffer = new byte[newSize];
                Array.Copy(_readBuffer, newReadBuffer, _readBuffer.Length);

                _readBuffer = newReadBuffer;
            }
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
            // Optimize this
            var result = new byte[length];
            Array.Copy(_readBuffer, FullHeaderLength, result, 0, length);

            int fullLength = FullHeaderLength + length;
            int nextDataLength = _readOffset - fullLength;
            Array.Copy(_readBuffer, fullLength, _readBuffer, 0, nextDataLength);

            _readOffset = nextDataLength;

            return result;
        }

        #endregion

        #region Private Fields
        
        private readonly Socket _socket;

        private byte[] _readBuffer;
        private int _readOffset;

        private readonly ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();

        #endregion
    }
}
