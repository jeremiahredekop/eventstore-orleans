using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventSourceingCoreStuff;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans;

namespace OrleansEventStore
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            await new HostBuilder()
                .UseOrleans(builder =>
                {
                    builder
                        .UseLocalhostClustering()
                        .Configure<ClusterOptions>(options =>
                        {
                            options.ClusterId = "dev";
                            options.ServiceId = "OrleansBasics";
                        })
                        .Configure<EndpointOptions>(options => options.AdvertisedIPAddress = IPAddress.Loopback)
                        
                        .AddCustomStorageBasedLogConsistencyProviderAsDefault()
                        .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(Shipment).Assembly).WithReferences())
                        .AddStartupTask(InitializeEventStoreConnection)
                        .AddMemoryGrainStorage(name: "demo2")
                        .AddMemoryGrainStorageAsDefault();
                })
                .ConfigureServices(services =>
                {
                    services.AddHostedService<ClientHostedService>();
                    services.Configure<ConsoleLifetimeOptions>(options =>
                    {
                        options.SuppressStatusMessages = true;
                    });
                })
                .ConfigureLogging(builder =>
                {
                    builder.AddConsole();
                })
                .ConfigureAppConfiguration(builder => builder.AddJsonFile("appsettings.json"))
                .RunConsoleAsync();
        }

        private static Task InitializeEventStoreConnection(IServiceProvider provider, CancellationToken token)
        {
            var config = provider.GetService<IConfiguration>();
            EventStoreClientBehaviors.InitializeConnection(config["EventStoreUri"], typeof(PickedUp).Assembly,
                config["StorageConnectionString"]);
            return Task.CompletedTask;
        }
    }
}