using Aspire.Orleans.Server;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.UseOrleansAspire();

using var host = builder.Build();

await host.RunAsync();

//await host.StartAsync();
//var client = host.Services.GetRequiredService<IClusterClient>();
//var grain = client.GetGrain<IMyGrain>(Guid.NewGuid());

//while (true)
//{
//    Console.WriteLine($"Ping: #{await grain.Ping()}");
//    await Task.Delay(1000);
//}
