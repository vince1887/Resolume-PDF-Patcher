using System.Globalization;

namespace Cr34teLightPatchBuilder;

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
    string Distribution,
    double? PositionX,
    double? PositionY,
    double? AngleDegrees,
    IReadOnlyList<FixturePoint> LayoutCorners)
{
    public string FixtureId { get; set; } = string.Empty;
    public string LumiverseGroup => string.IsNullOrWhiteSpace(NodeIP) || LumiverseName == NodeIP
        ? LumiverseName
        : $"{LumiverseName}  |  {NodeIP}";
    public string ArtNetPortGroup => $"Node Port Address {ArtNetPort}  |  Subnet {Subnet}  |  Universe {Universe}";
    public string PixelSize => PixelWidth.HasValue && PixelHeight.HasValue ? $"{PixelWidth} x {PixelHeight}" : "Unknown";
    public string Position => PositionX.HasValue && PositionY.HasValue
        ? $"{PositionX.Value.ToString("0.##", CultureInfo.InvariantCulture)}, {PositionY.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
        : "Unknown";
    public string Angle => AngleDegrees.HasValue
        ? $"{AngleDegrees.Value.ToString("0.##", CultureInfo.InvariantCulture)}°"
        : "Unknown";
}

public readonly record struct FixturePoint(double X, double Y);
