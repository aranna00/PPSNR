using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace PPSNR.Server.Services;

/// <summary>
/// Sends emails using settings provided via configuration/environment variables.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email message.
    /// </summary>
    /// <param name="toEmail">Target recipient email address.</param>
    /// <param name="subject">Subject line.</param>
    /// <param name="htmlBody">HTML body content.</param>
    /// <param name="textBody">Optional plain text body.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(string toEmail, string subject, string htmlBody, string? textBody = null, CancellationToken ct = default);
}

/// <summary>
/// Default SMTP-based implementation of <see cref="IEmailService"/>.
/// Reads settings from configuration keys or environment variables.
/// Preferred keys: EMAIL_HOST, EMAIL_PORT, EMAIL_USERNAME, EMAIL_PASSWORD, EMAIL_FROM, EMAIL_FROM_NAME, EMAIL_ENABLE_SSL.
/// Also supports colon-separated keys under Email: section (e.g., Email:Host).
/// </summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, string? textBody = null, CancellationToken ct = default)
    {
        var host = Get("EMAIL_HOST", "Email:Host");
        var portStr = Get("EMAIL_PORT", "Email:Port");
        var user = Get("EMAIL_USERNAME", "Email:Username");
        var pass = Get("EMAIL_PASSWORD", "Email:Password");
        var from = Get("EMAIL_FROM", "Email:From");
        var fromName = Get("EMAIL_FROM_NAME", "Email:FromName");
        var enableSslStr = Get("EMAIL_ENABLE_SSL", "Email:EnableSsl");

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            throw new InvalidOperationException("Email service is not configured. Please set EMAIL_HOST and EMAIL_FROM in .env or configuration.");
        }

        var port = 25;
        if (!string.IsNullOrWhiteSpace(portStr) && int.TryParse(portStr, out var p)) port = p;
        var enableSsl = true;
        if (!string.IsNullOrWhiteSpace(enableSslStr))
        {
            enableSsl = string.Equals(enableSslStr, "1") || string.Equals(enableSslStr, "true", StringComparison.OrdinalIgnoreCase);
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(user))
        {
            client.Credentials = new NetworkCredential(user, pass);
        }

        using var msg = new MailMessage
        {
            From = new MailAddress(from, string.IsNullOrWhiteSpace(fromName) ? from : fromName),
            Subject = subject,
            Body = string.IsNullOrEmpty(textBody) ? htmlBody : textBody,
            IsBodyHtml = string.IsNullOrEmpty(textBody) // If we have separate text, we attach HTML as alternate view below
        };
        msg.To.Add(new MailAddress(toEmail));

        if (!string.IsNullOrEmpty(textBody))
        {
            var plainView = AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain");
            var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html");
            msg.AlternateViews.Add(plainView);
            msg.AlternateViews.Add(htmlView);
        }

        try
        {
            await client.SendMailAsync(msg, ct);
            _logger.LogInformation("Email sent to {To}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", toEmail);
            throw;
        }
    }

    private string? Get(string primary, string fallback) => _config[primary] ?? _config[fallback];
}
