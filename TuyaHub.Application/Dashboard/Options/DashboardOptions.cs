namespace TuyaHub.Application.Dashboard.Options;

/// <summary>
/// Configures the read-only web status dashboard (bound from the <c>DashboardOptions</c> section).
/// When <see cref="Enabled"/> is false the host binds no HTTP endpoint and behaves like a plain
/// worker. Overridable via environment (<c>DashboardOptions__Enabled</c> / <c>DashboardOptions__Port</c>).
/// </summary>
public class DashboardOptions
{
    /// <summary>Whether to serve the dashboard and its Server-Sent Events stream.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>TCP port the dashboard listens on when enabled.</summary>
    public int Port { get; set; } = 8080;
}
