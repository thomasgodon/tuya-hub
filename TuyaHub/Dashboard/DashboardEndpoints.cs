using System.Text;
using TuyaHub.Application.Dashboard;

namespace TuyaHub.Dashboard;

public static class DashboardEndpoints
{
    /// <summary>
    /// Serves the single-page dashboard (wwwroot/index.html) and a Server-Sent Events stream that
    /// pushes the latest <see cref="DashboardSnapshot"/> JSON to connected browsers on every device
    /// state change.
    /// </summary>
    public static WebApplication MapDashboard(this WebApplication app)
    {
        // Static page: "/" -> wwwroot/index.html.
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/events", async (HttpContext context, IDeviceSnapshotBroadcaster broadcaster, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";

            // Flush headers + an SSE comment immediately so the connection opens (and the browser's
            // EventSource fires `onopen`) even before the first snapshot has been published.
            await context.Response.WriteAsync(": connected\n\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);

            var (reader, subscription) = broadcaster.Subscribe();
            using (subscription)
            {
                // Send the most recent snapshot immediately so a fresh connection isn't blank.
                if (broadcaster.Latest is { } latest)
                    await WriteEventAsync(context, latest, cancellationToken);

                try
                {
                    await foreach (var json in reader.ReadAllAsync(cancellationToken))
                        await WriteEventAsync(context, json, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Client disconnected — normal SSE teardown.
                }
            }
        });

        return app;
    }

    private static async Task WriteEventAsync(HttpContext context, string json, CancellationToken cancellationToken)
    {
        // Snapshot JSON is single-line (no indentation), so a single data: frame is safe.
        var frame = Encoding.UTF8.GetBytes($"data: {json}\n\n");
        await context.Response.Body.WriteAsync(frame, cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }
}
