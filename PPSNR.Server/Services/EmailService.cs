using System.Net;
using System.Net.Mail;
using PPSNR.Server.Shared;

namespace PPSNR.Server.Services;

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

    /// <summary>
    ///
    /// </summary>
    /// <param name="config"></param>
    /// <param name="logger"></param>
    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="toEmail"></param>
    /// <param name="subject"></param>
    /// <param name="htmlBody"></param>
    /// <param name="textBody"></param>
    /// <param name="ct"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task SendAsync(string toEmail, string subject, string htmlBody, string? textBody = null, CancellationToken ct = default)
    {
        var host = Get("EMAIL_HOST", "Email:Host");
        var portStr = Get("EMAIL_PORT", "Email:Port");
        var user = Get("EMAIL_USERNAME", "Email:Username");
        var pass = Get("EMAIL_PASSWORD", "Email:Password");
        var fromName = Get("EMAIL_FROM_NAME", "Email:FromName");

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
        {
            throw new InvalidOperationException("Email service is not configured. Please set EMAIL_HOST and EMAIL_FROM in .env or configuration.");
        }

        var port = 587;
        if (!string.IsNullOrWhiteSpace(portStr) && int.TryParse(portStr, out var p)) port = p;

        using var client = new SmtpClient(host, port);

        client.EnableSsl = true;
        client.DeliveryMethod = SmtpDeliveryMethod.Network;
        client.UseDefaultCredentials = false;

        if (!string.IsNullOrWhiteSpace(user))
        {
            client.Credentials = new NetworkCredential(user, pass);
        }

        using var msg = new MailMessage();

        msg.From = new MailAddress(user, string.IsNullOrWhiteSpace(fromName) ? user : fromName);
        msg.Subject = subject;
        msg.Body = string.IsNullOrEmpty(textBody) ? htmlBody : textBody;
        msg.IsBodyHtml = string.IsNullOrEmpty(textBody); // If we have separate text, we attach HTML as alternate view below
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
