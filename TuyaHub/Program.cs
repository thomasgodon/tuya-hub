using Microsoft.Extensions.Hosting;
using TuyaHub.Application;
using TuyaHub.Application.Dashboard.Options;
using TuyaHub.Dashboard;
using TuyaHub.Infrastructure;

// Read the dashboard flag up front so we can choose the host shape. appsettings.json is always copied
// next to the assembly (AppContext.BaseDirectory), so this resolves regardless of the working directory.
var bootstrap = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();
var dashboard = bootstrap.GetSection(nameof(DashboardOptions)).Get<DashboardOptions>() ?? new DashboardOptions();

if (!dashboard.Enabled)
{
    // Dashboard off: run as a plain worker host with no web server / no listening port.
    var workerBuilder = Host.CreateApplicationBuilder(args);
    workerBuilder.Services.AddApplication();
    workerBuilder.Services.AddInfrastructure(workerBuilder.Configuration);
    await workerBuilder.Build().RunAsync();
    return;
}

// Dashboard on: WebApplication host serving the read-only dashboard on Kestrel, with the background
// services running as hosted services on the same host.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.WebHost.UseUrls($"http://*:{dashboard.Port}");

var app = builder.Build();

app.MapDashboard();

await app.RunAsync();
