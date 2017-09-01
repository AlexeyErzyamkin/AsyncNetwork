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
            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            var server = new Server(10891, 1000, _connectionFactory);

            await server.Start();
        }
    }
}
