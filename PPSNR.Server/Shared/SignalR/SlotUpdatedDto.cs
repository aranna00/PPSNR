namespace PPSNR.Shared.SignalR;

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
    public int? Width { get; set; }
    public int? Height { get; set; }
}