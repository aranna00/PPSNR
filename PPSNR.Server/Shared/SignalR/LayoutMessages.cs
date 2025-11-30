using System.Text.Json;

namespace PPSNR.Shared.SignalR
{
    public class SignalRMessageEnvelope
    {
        public string Type { get; set; } = string.Empty;
        public JsonElement? Data { get; set; }
    }

    public class SlotUpdatedDto
    {
        public Guid Id { get; set; }
        public Guid LayoutId { get; set; }
        public int? Profile { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
        public int? ZIndex { get; set; }
        public bool? Visible { get; set; }
        public string? ImageUrl { get; set; }
        public string? AdditionalProperties { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }

    public class PlacementsResetDto
    {
        // Keep empty for now; envelope Type is sufficient for resets
    }
}

