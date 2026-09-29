using System.Globalization;
using System.IO;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Cr34teLightPatchBuilder;

public static class PatchExporter
{
    private static readonly string[] Headers = ["Lumiverse", "Node IP", "Node Port Address", "Subnet", "Universe", "Fixture", "Start address", "Channels", "End address", "Pixels", "Format", "ID"];
    private static readonly string[] PixelHeaders = ["Lumiverse", "Node IP", "Node Port Address", "Universe", "Fixture", "Fixture ID", "Pixel #", "X", "Y", "DMX start", "DMX end", "Color format", "Resolume distribution", "Order note"];

    public static void SaveCsv(string filePath, IReadOnlyList<PatchEntry> entries, string sourceName)
    {
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
        writer.WriteLine(CsvLine(["Source", sourceName]));
        writer.WriteLine(CsvLine(Headers));
        foreach (var entry in entries)
        {
            writer.WriteLine(CsvLine([
                entry.LumiverseName, entry.NodeIP, entry.ArtNetPort, entry.Subnet, entry.Universe,
                entry.Name, entry.Address, entry.Channels, entry.EndAddress, PixelCount(entry), entry.ColorFormat, entry.FixtureId]));
        }
    }

    public static void SavePixelMapCsv(string filePath, IReadOnlyList<PatchEntry> entries)
    {
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
        writer.WriteLine(CsvLine(PixelHeaders));
        foreach (var entry in entries)
        {
            var start = int.TryParse(entry.Address, NumberStyles.Integer, CultureInfo.InvariantCulture, out var address) ? address : (int?)null;
            var components = Components(entry.ColorFormat);
            if (!entry.PixelWidth.HasValue || !entry.PixelHeight.HasValue || !start.HasValue || !components.HasValue)
            {
                continue;
            }

            var pixelIndex = 0;
            for (var y = 1; y <= entry.PixelHeight.Value; y++)
            {
                for (var x = 1; x <= entry.PixelWidth.Value; x++)
                {
                    pixelIndex++;
                    var pixelStart = start.Value + (pixelIndex - 1) * components.Value;
                    writer.WriteLine(CsvLine([
                        entry.LumiverseName,
                        entry.NodeIP,
                        entry.ArtNetPort,
                        entry.Universe,
                        entry.Name,
                        entry.FixtureId,
                        pixelIndex.ToString(CultureInfo.InvariantCulture),
                        x.ToString(CultureInfo.InvariantCulture),
                        y.ToString(CultureInfo.InvariantCulture),
                        pixelStart.ToString(CultureInfo.InvariantCulture),
                        (pixelStart + components.Value - 1).ToString(CultureInfo.InvariantCulture),
                        entry.ColorFormat,
                        entry.Distribution,
                        "Logical row-major; verify physical order against Resolume distribution"]));
                }
            }
        }
    }

    public static void SavePdf(string filePath, IReadOnlyList<PatchEntry> entries, string sourceName, PdfExportOptions options)
    {
        var document = new PdfDocument();
        document.Info.Title = options.Title;
        var font = new XFont("Arial", options.FontSize, XFontStyleEx.Regular);
        var boldFont = new XFont("Arial", options.FontSize, XFontStyleEx.Bold);
        var titleFont = new XFont("Arial", options.FontSize + 7, XFontStyleEx.Bold);
        var columns = GetPdfColumns(options);
        var pageWidth = options.Landscape ? 842d : 595d;
        const double margin = 32;
        const double rowHeight = 24;
        var availableWidth = pageWidth - margin * 2;
        var unitSum = columns.Sum(column => column.WidthUnits);
        var widths = columns.Select(column => availableWidth * column.WidthUnits / unitSum).ToArray();
        var accentColor = ColorFromHex(options.AccentHex);
        var accentBrush = new XSolidBrush(accentColor);
        var paleAccentBrush = new XSolidBrush(XColor.FromArgb(35, accentColor));
        XFont currentFont;
        XGraphics graphics = null!;
        PdfPage page = null!;
        double y = 0;
        var pageNumber = 0;

        void AddPage()
        {
            if (page is not null)
            {
                DrawFooter();
            }

            page = document.AddPage();
            page.Size = options.PageSize == "Letter" ? PdfSharp.PageSize.Letter : PdfSharp.PageSize.A4;
            page.Orientation = options.Landscape ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;
            graphics = XGraphics.FromPdfPage(page);
            pageNumber++;
            y = margin;
            if (!string.IsNullOrWhiteSpace(options.Title))
            {
                graphics.DrawString(options.Title, titleFont, accentBrush, new XPoint(margin, y + options.FontSize + 4));
                y += options.FontSize + 12;
            }

            if (!string.IsNullOrWhiteSpace(options.Subtitle))
            {
                graphics.DrawString(options.Subtitle, boldFont, XBrushes.DimGray, new XPoint(margin, y + options.FontSize));
                y += options.FontSize + 7;
            }

            var metadata = new List<string>();
            if (options.ShowSourceFile)
            {
                metadata.Add(sourceName);
            }
            if (options.ShowGeneratedDate)
            {
                metadata.Add($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}");
            }
            if (metadata.Count > 0)
            {
                graphics.DrawString(string.Join("    |    ", metadata), font, XBrushes.DimGray, new XPoint(margin, y + options.FontSize));
                y += options.FontSize + 8;
            }

            if (columns.Count > 0)
            {
                DrawRow(columns.Select(column => column.Header).ToArray(), true);
            }
        }

        void DrawRow(IReadOnlyList<string> values, bool isHeader)
        {
            var x = margin;
            currentFont = isHeader ? boldFont : font;
            if (isHeader)
            {
                graphics.DrawRectangle(paleAccentBrush, margin, y, widths.Sum(), rowHeight);
            }

            for (var column = 0; column < values.Count; column++)
            {
                var layout = new XRect(x + 4, y + 3, widths[column] - 8, rowHeight - 6);
                graphics.DrawString(values[column], currentFont, XBrushes.Black, layout, XStringFormats.TopLeft);
                x += widths[column];
            }

            graphics.DrawLine(XPens.LightGray, margin, y + rowHeight, margin + widths.Sum(), y + rowHeight);
            y += rowHeight;
        }

        AddPage();
        foreach (var lumiverseGroup in entries.GroupBy(entry => new { entry.LumiverseName, entry.NodeIP }))
        {
            if (options.ShowLumiverseGroups)
            {
                EnsureSpace(rowHeight);
                DrawGroupRow($"{lumiverseGroup.Key.LumiverseName}  |  {lumiverseGroup.Key.NodeIP}", boldFont, accentBrush);
            }

            foreach (var portGroup in lumiverseGroup.GroupBy(entry => new { entry.ArtNetPort, entry.Subnet, entry.Universe }))
            {
                if (options.ShowArtNetGroups)
                {
                    EnsureSpace(rowHeight);
                    DrawGroupRow($"Node Port Address {portGroup.Key.ArtNetPort}  |  Subnet {portGroup.Key.Subnet}  |  Universe {portGroup.Key.Universe}", boldFont, XBrushes.DimGray);
                }

                foreach (var entry in portGroup)
                {
                    EnsureSpace(rowHeight);
                    DrawRow(columns.Select(column => column.Value(entry)).ToArray(), false);
                }
            }
        }

        DrawFooter();
        document.Save(filePath);

        void EnsureSpace(double height)
        {
            if (y + height > page.Height.Point - margin - options.FontSize - 12)
            {
                AddPage();
            }
        }

        void DrawGroupRow(string text, XFont groupFont, XBrush brush)
        {
            graphics.DrawRectangle(paleAccentBrush, margin, y, widths.Sum(), rowHeight);
            graphics.DrawString(text, groupFont, brush, new XRect(margin + 5, y + 4, widths.Sum() - 10, rowHeight - 8), XStringFormats.TopLeft);
            y += rowHeight;
        }

        void DrawFooter()
        {
            graphics.DrawLine(XPens.LightGray, margin, page.Height.Point - margin + 2, page.Width.Point - margin, page.Height.Point - margin + 2);
            if (!string.IsNullOrWhiteSpace(options.Footer))
            {
                graphics.DrawString(options.Footer, font, XBrushes.DimGray,
                    new XRect(margin, page.Height.Point - margin + 5, page.Width.Point - margin * 2 - 50, options.FontSize + 6), XStringFormats.TopLeft);
            }
            graphics.DrawString(pageNumber.ToString(CultureInfo.InvariantCulture), font, XBrushes.DimGray,
                new XRect(page.Width.Point - margin - 30, page.Height.Point - margin + 5, 30, options.FontSize + 6), XStringFormats.TopRight);
        }
    }

    private static List<(string Header, Func<PatchEntry, string> Value, double WidthUnits)> GetPdfColumns(PdfExportOptions options)
    {
        var columns = new List<(string Header, Func<PatchEntry, string> Value, double WidthUnits)>();
        if (options.ShowFixture) columns.Add(("Fixture", entry => entry.Name, 3.2));
        if (options.ShowNodeIP) columns.Add(("Node IP", entry => entry.NodeIP, 1.5));
        if (options.ShowUniverse) columns.Add(("Universe", entry => entry.Universe, 1.1));
        if (options.ShowStartAddress) columns.Add(("Start address", entry => entry.Address, 1.4));
        if (options.ShowChannels) columns.Add(("Channels", entry => entry.Channels, 1.1));
        if (options.ShowEndAddress) columns.Add(("End address", entry => entry.EndAddress, 1.4));
        if (options.ShowPixels) columns.Add(("Pixels", PixelSize, 1.3));
        if (options.ShowColorFormat) columns.Add(("Color format", entry => entry.ColorFormat, 1.4));
        if (options.ShowFixtureId) columns.Add(("ID", entry => entry.FixtureId, 1.2));
        return columns;
    }

    private static string PixelSize(PatchEntry entry) =>
        entry.PixelWidth.HasValue && entry.PixelHeight.HasValue ? $"{entry.PixelWidth} x {entry.PixelHeight}" : "Unknown";

    private static XColor ColorFromHex(string hex)
    {
        var red = Convert.ToByte(hex.Substring(1, 2), 16);
        var green = Convert.ToByte(hex.Substring(3, 2), 16);
        var blue = Convert.ToByte(hex.Substring(5, 2), 16);
        return XColor.FromArgb(255, red, green, blue);
    }

    private static string CsvLine(IEnumerable<string> values) =>
        string.Join(",", values.Select(value => $"\"{value.Replace("\"", "\"\"")}\""));

    private static string PixelCount(PatchEntry entry) =>
        entry.PixelWidth.HasValue && entry.PixelHeight.HasValue
            ? checked(entry.PixelWidth.Value * entry.PixelHeight.Value).ToString(CultureInfo.InvariantCulture)
            : "Unknown";

    private static int? Components(string colorFormat) => colorFormat.ToLowerInvariant() switch
    {
        "rgb" or "bgr" or "cmy" => 3,
        "rgba" or "argb" or "rgbw" => 4,
        "r" or "mono" or "monochrome" or "white" => 1,
        _ => null
    };
}
