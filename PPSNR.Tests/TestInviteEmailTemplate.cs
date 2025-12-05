using PPSNR.Server.Services;

namespace PPSNR.Tests;

public sealed class TestInviteEmailTemplate : IInviteEmailTemplate
{
    public (string Subject, string Html, string Text) Build(string ownerName, string pairName, string acceptUrl)
    {
        var subject = $"Invite from {ownerName} to join {pairName}";
        var html = $"<p>{ownerName} invited you to join pair {pairName}. Click <a href=\"{acceptUrl}\">accept</a>.</p>";
        var text = $"{ownerName} invited you to join pair {pairName}. Accept: {acceptUrl}";
        return (subject, html, text);
    }
}
