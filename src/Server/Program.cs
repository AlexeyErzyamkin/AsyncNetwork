using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        private static readonly IClientConnectionFactory _connectionFactory = new GameClientConnectionFactory();

        static void Main(string[] args)
        {
            //Task.Factory.StartNew(
            //    () =>
            //    {
            //        while (true)
            //        {
            //            if (!_readChannel.IsEmpty && _readChannel.TryDequeue(out ClientData clientData))
            //            {
            //                clientData.Client.Send(clientData.Data);
            //            }
            //        }
            //    },
            //    TaskCreationOptions.LongRunning
            //);

            //for (int threadIndex = 0; threadIndex < 4; ++threadIndex)
            //{
            //    var thread = new Thread(ProcessClientsData);
            //    thread.Start();
            //}

            MainAsync().GetAwaiter().GetResult();
        }

        //private static void ProcessClientsData()
        //{
        //    while (true)
        //    {
        //        if (!_readChannel.IsEmpty && _readChannel.TryDequeue(out ClientData clientData))
        //        {
        //            clientData.Client.Send(clientData.Data);
        //        }
        //    }
        //}

        private static async Task MainAsync()
        {
            var server = new Server(10891, 1000, _connectionFactory);

            await server.Start();

            Console.WriteLine("Press ENTER to exit...");
            Console.ReadLine();
        }

        //private static readonly ConcurrentQueue<ClientData> _readChannel = new ConcurrentQueue<ClientData>();
    }
}
