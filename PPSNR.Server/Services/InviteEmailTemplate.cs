using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace PPSNR.Server.Services;

/// <summary>
/// Builds email subject and body for partner invites. Pluggable for future channels.
/// </summary>
public interface IInviteEmailTemplate
{
    /// <summary>
    /// Builds subject, html and text for an invite.
    /// </summary>
    /// <param name="ownerName">Owner display name.</param>
    /// <param name="pairName">Pair name.</param>
    /// <param name="acceptUrl">Absolute accept URL.</param>
    (string Subject, string Html, string Text) Build(string ownerName, string pairName, string acceptUrl);
}

public sealed class DefaultInviteEmailTemplate : IInviteEmailTemplate
{
    public (string Subject, string Html, string Text) Build(string ownerName, string pairName, string acceptUrl)
    {
        var subject = $"{ownerName} invited you to join '{pairName}'";
        var html =
            "<p>Hello,</p>" +
            $"<p><strong>{System.Net.WebUtility.HtmlEncode(ownerName)}</strong> invited you to collaborate on <strong>{System.Net.WebUtility.HtmlEncode(pairName)}</strong>.</p>" +
            "<p>To accept the invite and start editing your layout, click the link below:</p>" +
            $"<p><a href=\"{acceptUrl}\">Accept invite</a></p>" +
            "<p>If the link doesn't work, copy and paste this URL into your browser:<br/>" +
            $"<code>{System.Net.WebUtility.HtmlEncode(acceptUrl)}</code></p>" +
            "<p>Thanks!</p>";
        var text =
            "Hello,\n\n" +
            $"{ownerName} invited you to collaborate on '{pairName}'.\n\n" +
            $"Accept invite: {acceptUrl}\n\n" +
            "Thanks!";
        return (subject, html, text);
    }
}
