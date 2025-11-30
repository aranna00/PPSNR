using System.Text.Json;

namespace PPSNR.Shared.SignalR;

public class SignalRMessageEnvelope
{
    public string Type { get; set; } = string.Empty;
    public JsonElement? Data { get; set; }
}