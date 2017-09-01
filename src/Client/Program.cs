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
        private const int ClientsCount = 1;

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
                var client = new Client(clientIndex, 2000);
                clientTasks[clientIndex] = Task.Run(client.Start);

                //await Task.Delay(_random.Next(100, 500));
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
        private readonly Random _random = new Random();

        public Client(int clientId, int attempts)
        {
            _clientId = clientId;
            _attempts = attempts;
        }

        public async Task Start()
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(IPAddress.Loopback, 10891);
            if (!tcpClient.Connected)
            {
                throw new InvalidOperationException();
            }

            //Console.WriteLine("Client connected: " + _clientId);

            byte[] fullSendBuffer = new byte[GetFullLength(1000)];

            var sw = new Stopwatch();

            using (var stream = tcpClient.GetStream())
            {
                byte[] headerBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)0x0CAE));
                
                for (int attempt = 0; attempt < _attempts; ++attempt)
                {
                    int index = 0;
                    for (int localIndex = 0; localIndex < headerBytes.Length; ++localIndex, ++index)
                    {
                        fullSendBuffer[index] = headerBytes[localIndex];
                    }

                    short length = (short)_random.Next(10, 1000);
                    byte[] lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(length));
                    for (int localIndex = 0; localIndex < lengthBytes.Length; ++localIndex, ++index)
                    {
                        fullSendBuffer[index] = lengthBytes[localIndex];
                    }

                    //sw.Restart();

                    await stream.WriteAsync(fullSendBuffer, 0, GetFullLength(length));
                    await stream.FlushAsync();

                    //sw.Stop();
                    //Console.WriteLine($"Client {_clientId} in {attempt} attempt sent {length} bytes in {sw.Elapsed.TotalMilliseconds} ms");
                    //sw.Restart();

                    var receivedLength = await stream.ReadAsync(fullSendBuffer, 0, GetFullLength(length));

                    //sw.Stop();
                    //Console.WriteLine($"Client {_clientId} in {attempt} attempt received {receivedLength} bytes in {sw.Elapsed.TotalMilliseconds} ms");

                    //await Task.Delay(_random.Next(10, 500));
                }
            }

            //Console.WriteLine("Client done: " + _clientId);
        }

        private static int GetFullLength(int length)
        {
            return 2 + 2 + length;
        }
    }
}
