using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using SyncServer;

namespace Server
{
    class Program
    {
        //private static readonly IClientConnectionFactory _connectionFactory = new GameClientConnectionFactory();
        private static readonly ConcurrentQueue<ReadData> ReadChannel = new ConcurrentQueue<ReadData>();
        private static readonly ConcurrentQueue<SendData> SendChannel = new ConcurrentQueue<SendData>();

        private static volatile bool _needStop = false;

        private static Thread _processThread;

        static void Main(string[] args)
        {
            //MainAsync().GetAwaiter().GetResult();

            var server = new SyncServer.Server(10891, 1000, null, ReadChannel, SendChannel);
            server.Start();

            _processThread = new Thread(ProcessData);
            _processThread.Start();

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
                if (ReadChannel.TryDequeue(out ReadData data))
                {
                    SendChannel.Enqueue(new SendData { ConnectionId = data.ConnectionId, Data = data.Data });
                }

                Thread.Yield();
            }
        }
    }
}
