using Microsoft.Extensions.Hosting;
using TuyaHub.Application;
using TuyaHub.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var host = builder.Build();
host.Run();
