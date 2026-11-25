using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Investments.Api.Services.Notifications;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}

/// <summary>Default sender: only logs the subject, so the project runs without an SMTP server.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation("E-mail not sent (SMTP is not configured): {Subject}", subject);
        return Task.CompletedTask;
    }
}

public sealed record SmtpOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string From { get; init; } = string.Empty;
    public string? User { get; init; }
    public string? Password { get; init; }
}

/// <summary>Used only when <c>Smtp:Host</c> is configured.</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        var smtp = options.Value;

        using var client = new SmtpClient(smtp.Host, smtp.Port) { EnableSsl = smtp.EnableSsl };
        if (!string.IsNullOrEmpty(smtp.User))
        {
            client.Credentials = new NetworkCredential(smtp.User, smtp.Password);
        }

        using var message = new MailMessage(smtp.From, to, subject, body);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        await client.SendMailAsync(message, timeout.Token);
    }
}
