namespace PPSNR.Server.Shared;

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