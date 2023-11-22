using Aspire.Orleans.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.UseOrleansAspire();

using var host = builder.Build();
await host.StartAsync();

var client = host.Services.GetRequiredService<IClusterClient>();
var grain = client.GetGrain<IMyGrain>(Guid.NewGuid());

var shutdown = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
while (!shutdown.IsCancellationRequested)
{
    Console.WriteLine($"Ping: #{await grain.Ping()}");
    await Task.Delay(1000);
}

await host.WaitForShutdownAsync();
