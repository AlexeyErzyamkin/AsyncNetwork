using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    class Program
    {
        private const int ClientsCount = 10;

        private static readonly Random _random = new Random();

        static void Main(string[] args)
            => MainAsync().GetAwaiter().GetResult();
        
        private static async Task MainAsync()
        {
            var sw = new Stopwatch();
            sw.Start();

            Task[] clientTasks = new Task[ClientsCount];
            for (int clientIndex = 0; clientIndex < clientTasks.Length; ++clientIndex)
            {
                var client = new Client(clientIndex, 10, _random);
                clientTasks[clientIndex] = Task.Run(client.Start);
            }

            await Task.WhenAll(clientTasks);

            sw.Stop();

            Console.WriteLine("ET: " + sw.Elapsed);

            Console.WriteLine("Press ENTER to continue...");
            Console.ReadLine();
        }
    }

    class Client
    {
        private readonly int _clientId;
        private readonly int _attempts;
        private readonly Random _random;

        public Client(int clientId, int attempts, Random random)
        {
            _clientId = clientId;
            _attempts = attempts;
            _random = random;
        }

        public async Task Start()
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("192.168.1.5", 10891);

            Console.WriteLine("Client connected: " + _clientId);

            var sw = new Stopwatch();

            using (var stream = tcpClient.GetStream())
            {
                byte[] headerBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)0x0CAE));
                
                for (int attempt = 0; attempt < _attempts; ++attempt)
                {
                    int length = _random.Next(10, 100);
                    byte[] sendBuffer = new byte[length];
                    _random.NextBytes(sendBuffer);

                    byte[] lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)sendBuffer.Length));
                    byte[] fullSendBuffer = new byte[4 + sendBuffer.Length];

                    using (var ms = new MemoryStream(fullSendBuffer))
                    {
                        ms.Write(headerBytes, 0, headerBytes.Length);
                        ms.Write(lengthBytes, 0, lengthBytes.Length);
                        ms.Write(sendBuffer, 0, sendBuffer.Length);
                    }
                    
                    sw.Restart();

                    await stream.WriteAsync(fullSendBuffer, 0, fullSendBuffer.Length);
                    await stream.FlushAsync();

                    byte[] readBuffer = new byte[fullSendBuffer.Length];
                    await stream.ReadAsync(readBuffer, 0, readBuffer.Length);

                    sw.Stop();
                    Console.WriteLine($"Client {_clientId} in {attempt} attempt sent {length} bytes: {sw.Elapsed}");

                    await Task.Delay(_random.Next(100, 500));
                }
            }

            Console.WriteLine("Client done: " + _clientId);
        }
    }
}
