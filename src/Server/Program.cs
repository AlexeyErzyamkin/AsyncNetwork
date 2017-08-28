using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
            => MainAsync().GetAwaiter().GetResult();
        
        private static async Task MainAsync()
        {
            var server = new Server(10891, 1000);

            await server.Start();
        }
    }
}
