using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using OrleansContracts;

namespace OrleansEventStore.Client
{
    class Program
    {
        static int Main(string[] args)
        {
            return RunMainAsync().Result;
        }

        private static async Task<int> RunMainAsync()
        {
            try
            {
                using (var client = await ConnectClient())
                {
                    await DoClientWork(client);
                    Console.ReadKey();
                }

                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine($"\nException while trying to run client: {e.Message}");
                Console.WriteLine("Make sure the silo the client is trying to connect to is running.");
                Console.WriteLine("\nPress any key to exit.");
                Console.ReadKey();
                return 1;
            }
        }

        private static async Task<IClusterClient> ConnectClient()
        {
            IClusterClient client;
            client = new ClientBuilder()
                .UseLocalhostClustering()
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "dev";
                    options.ServiceId = "OrleansBasics";
                })
                .ConfigureLogging(logging => logging.AddConsole())
                .Build();

            await client.Connect();
            Console.WriteLine("Client successfully connected to silo host \n");
            return client;
        }

        private static async Task DoClientWork(IClusterClient client)
        {
            var key = Guid.NewGuid().ToString();
            
            // example of calling grains from the initialized client
            var friend = client.GetGrain<IShipment>(key);
            var status = await friend.GetStatus();
            Console.WriteLine($"{key} status: " + status);

            Console.WriteLine($"{key} pickingup...");
            await friend.Pickup();

            var status2 = await friend.GetStatus();
            Console.WriteLine($"{key} status: " + status2);
            
            Console.WriteLine($"{key} delivering...");
            await friend.Deliver();

            var status3 = await friend.GetStatus();
            Console.WriteLine($"{key} status: " + status3);

            await friend.ForceDeactivateForDemo();

        }
    }
}