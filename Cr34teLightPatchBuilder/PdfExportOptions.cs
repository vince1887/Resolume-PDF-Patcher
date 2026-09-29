namespace Cr34teLightPatchBuilder;

public sealed class PdfExportOptions
{
    public string Title { get; set; } = "CR34TE / Patch Builder";
    public string Subtitle { get; set; } = "DMX PATCH WORKSHEET";
    public string Footer { get; set; } = "Verify against the lighting console patch before show use.";
    public string AccentHex { get; set; } = "#F99B06";
    public string PageSize { get; set; } = "A4";
    public bool Landscape { get; set; } = true;
    public bool ShowSourceFile { get; set; } = true;
    public bool ShowGeneratedDate { get; set; } = true;
    public bool ShowLumiverseGroups { get; set; } = true;
    public bool ShowArtNetGroups { get; set; } = true;
    public bool ShowFixture { get; set; } = true;
    public bool ShowNodeIP { get; set; } = true;
    public bool ShowUniverse { get; set; } = true;
    public bool ShowStartAddress { get; set; } = true;
    public bool ShowChannels { get; set; } = true;
    public bool ShowEndAddress { get; set; } = true;
    public bool ShowPosition { get; set; } = true;
    public bool ShowAngle { get; set; } = true;
    public bool ShowPixels { get; set; } = true;
    public bool ShowColorFormat { get; set; } = true;
    public bool ShowFixtureId { get; set; } = true;
}
