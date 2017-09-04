using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SyncServer
{
    public struct ReadData
    {
        public Guid ConnectionId;
        public byte[] Data;
    }

    public struct SendData
    {
        public Guid ConnectionId;
        public byte[] Data;
    }

    public struct ServerOptions
    {
        public int Port;
        public int Backlog;
        public int ReadThreadsCount;
    }

    public class Server
    {
        public Server(ServerOptions options, IConnectionFactory connectionFactory, IProducerConsumerCollection<ReadData> readQueue, IProducerConsumerCollection<SendData> sendQueue)
        {
            _port = options.Port;
            _backlog = options.Backlog;
            _readThreadsCount = options.ReadThreadsCount;

            _connectionFactory = connectionFactory;
            _readQueue = readQueue;
            _sendQueue = sendQueue;
        }

        /// <summary>
        /// Start accepting connections from clients and process their incoming/outgoing data
        /// </summary>
        public void Start()
        {
            _acceptConnectionsSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            _acceptConnectionsThread = new Thread(AcceptConnections);
            _acceptConnectionsThread.Start();

            _processReadsThreads = new Thread[_readThreadsCount];
            for (int threadIndex = 0; threadIndex < _readThreadsCount; ++threadIndex)
            {
                var thread = new Thread(ProcessReads)
                {
                    Name = "Read thread #" + threadIndex
                };
                
                thread.Start();

                _processReadsThreads[threadIndex] = thread;
            }

            _processSendsThread = new Thread(ProcessSends);
            _processSendsThread.Start();
        }

        /// <summary>
        /// Stop accepting new connections and disconnect existing clients
        /// </summary>
        public void Stop()
        {
            _needStop = true;

            _acceptConnectionsThread.Join();
            //_processReadsThread.Join();

            foreach (Thread eachThread in _processReadsThreads)
            {
                eachThread.Join();
            }

            _processSendsThread.Join();

            _acceptConnectionsSocket.Close();
            _acceptConnectionsSocket.Dispose();
        }
        
        #region Private Methods

        private void AcceptConnections()
        {
            _acceptConnectionsSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
            _acceptConnectionsSocket.Listen(_backlog);

            while (!_needStop)
            {
                Socket clientSocket = _acceptConnectionsSocket.Accept();
                //clientSocket.Blocking = false;

                var connectionId = Guid.NewGuid();

                //Connection clientConnection = _connectionFactory.CreateConnection(clientSocket);
                Connection clientConnection = new Connection(connectionId, clientSocket);

                Console.WriteLine($"Client '{connectionId}' connected");

                if (!_connections.TryAdd(connectionId, clientConnection))
                {
                    //
                }

                _connectionsToAccept.Enqueue(clientConnection);

                Thread.Sleep(10);
            }
        }

        private void ProcessReads()
        {
            if (!_connectionsThreadLocal.IsValueCreated)
            {
                _connectionsThreadLocal.Value = new List<Connection>();
            }

            while (!_needStop)
            {
                while (!_connectionsToAccept.IsEmpty && _connectionsToAccept.TryDequeue(out Connection connectionToAccept))
                {
                    _connectionsThreadLocal.Value.Add(connectionToAccept);
                }

                foreach (Connection eachConnection in _connectionsThreadLocal.Value)
                {
                    if (eachConnection.TryRead(out byte[] data))
                    {
                        _readQueue.TryAdd(new ReadData{ ConnectionId = eachConnection.ConnectionId, Data = data});
                    }
                }

                Thread.Yield();
            }
        }

        private void ProcessSends()
        {
            while (!_needStop)
            {
                if (_sendQueue.TryTake(out SendData data))
                {
                    if (_connections.TryGetValue(data.ConnectionId, out Connection connection))
                    {
                        connection.Send(data.Data);
                    }
                }

                Thread.Yield();
            }
        }

        #endregion

        #region Private Fields

        private volatile bool _needStop;

        private readonly int _port;
        private readonly int _backlog;
        private readonly int _readThreadsCount;

        private Socket _acceptConnectionsSocket;
        private Thread _acceptConnectionsThread;

        private Thread[] _processReadsThreads;
        private Thread _processSendsThread;

        private readonly IConnectionFactory _connectionFactory;
        private readonly ConcurrentDictionary<Guid, Connection> _connections = new ConcurrentDictionary<Guid, Connection>();

        private readonly ConcurrentQueue<Connection> _connectionsToAccept = new ConcurrentQueue<Connection>();

        private readonly ThreadLocal<List<Connection>> _connectionsThreadLocal = new ThreadLocal<List<Connection>>();

        private readonly IProducerConsumerCollection<ReadData> _readQueue;
        private readonly IProducerConsumerCollection<SendData> _sendQueue;

        #endregion
    }
}
