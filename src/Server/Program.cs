using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

//using SyncServer;

namespace Server
{
    class Program
    {
        //private static readonly IClientConnectionFactory _connectionFactory = new GameClientConnectionFactory();
        private static readonly ConcurrentQueue<SyncServer.ReadData> ReadChannel = new ConcurrentQueue<SyncServer.ReadData>();
        private static readonly ConcurrentQueue<SyncServer.SendData> SendChannel = new ConcurrentQueue<SyncServer.SendData>();

        private static volatile bool _needStop = false;

        private static Thread[] _processThread;

        static void Main(string[] args)
        {
            //MainAsync().GetAwaiter().GetResult();

            var options = new SyncServer.ServerOptions
            {
                Port = 10891,
                Backlog = 1000,
                ReadThreadsCount = 4
            };

            var server = new SyncServer.Server(options, null, ReadChannel, SendChannel);
            server.Start();

            _processThread = new Thread[4];
            for (int threadIndex = 0; threadIndex < 4; ++threadIndex)
            {
                var processThread = new Thread(ProcessData);
                processThread.Start();

                _processThread[threadIndex] = processThread;
            }

            Console.WriteLine("Press ENTER to stop...");
            Console.ReadLine();

            server.Stop();
        }

        //private static async Task MainAsync()
        //{
        //    var server = new Server(10891, 1000, _connectionFactory);

        //    await server.Start();
        //}

        private static void ProcessData()
        {
            while (!_needStop)
            {
                if (ReadChannel.TryDequeue(out SyncServer.ReadData data))
                {
                    SendChannel.Enqueue(new SyncServer.SendData { ConnectionId = data.ConnectionId, Data = data.Data });
                }

                Thread.Yield();
            }
        }
    }
}
