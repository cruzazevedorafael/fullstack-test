using Investments.Api.Services.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Investments.Api.Tests;

/// <summary>Runs the real API on a throwaway SQLite file, with a fixed clock and an e-mail sender that always fails.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateOnly Today = new(2026, 6, 15);

    private readonly string _database = Path.Combine(Path.GetTempPath(), $"investments-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_database}");
        builder.ConfigureTestServices(services =>
        {
            // 01:30 UTC on the 16th is still the 15th in São Paulo.
            services.AddSingleton<TimeProvider>(new FixedClock(new DateTimeOffset(2026, 6, 16, 1, 30, 0, TimeSpan.Zero)));
            services.AddSingleton<IEmailSender, FailingEmailSender>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        File.Delete(_database);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FailingEmailSender : IEmailSender
    {
        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("SMTP is down");
    }
}
