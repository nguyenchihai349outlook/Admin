using Aspire.Orleans.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Runtime;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.UseOrleansAspire();

using var host = builder.Build();
await host.StartAsync();

var client = host.Services.GetRequiredService<IClusterClient>();
var grain = client.GetGrain<ICounterGrain>(Guid.NewGuid());

var shutdown = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

while (!shutdown.IsCancellationRequested)
{
    Console.WriteLine($"Count: #{await grain.Increment()}");
    await Task.Delay(1000);
}

await host.StopAsync();

public interface ICounterGrain : IGrainWithGuidKey
{
    ValueTask<int> Increment();
}

public sealed class CounterGrain(
    [PersistentState("count")] IPersistentState<int> count) : ICounterGrain
{
    public async ValueTask<int> Increment()
    {
        var result = ++count.State;
        await count.WriteStateAsync();
        return result;
    }
}

