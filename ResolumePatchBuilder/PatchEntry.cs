namespace ResolumePatchBuilder;

public sealed record PatchEntry(
    int Index,
    string LumiverseName,
    string NodeIP,
    string Subnet,
    string Name,
    string Universe,
    string ArtNetPort,
    string Address,
    string Channels,
    string EndAddress,
    int? PixelWidth,
    int? PixelHeight,
    string ColorFormat,
    string Distribution)
{
    public string FixtureId { get; set; } = string.Empty;
    public string LumiverseGroup => string.IsNullOrWhiteSpace(NodeIP) || LumiverseName == NodeIP
        ? LumiverseName
        : $"{LumiverseName}  |  {NodeIP}";
    public string ArtNetPortGroup => $"Node Port Address {ArtNetPort}  |  Subnet {Subnet}  |  Universe {Universe}";
    public string PixelSize => PixelWidth.HasValue && PixelHeight.HasValue ? $"{PixelWidth} x {PixelHeight}" : "Unknown";
}
