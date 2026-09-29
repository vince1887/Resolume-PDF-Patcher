using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Cr34teLightPatchBuilder;

public static class PatchExporter
{
    private const int PdfFontSize = 10;
    private static readonly string[] Headers = ["Lumiverse", "Node IP", "Node Port Address", "Subnet", "Universe", "Fixture", "Start address", "Channels", "End address", "Position X", "Position Y", "Angle (degrees)", "Pixels", "Format", "ID"];
    private static readonly string[] PixelHeaders = ["Lumiverse", "Node IP", "Node Port Address", "Universe", "Fixture", "Fixture ID", "Position X", "Position Y", "Angle (degrees)", "Pixel #", "X", "Y", "DMX start", "DMX end", "Color format", "Resolume distribution", "Order note"];

    public static void SaveCsv(string filePath, IReadOnlyList<PatchEntry> entries, string sourceName)
    {
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
        writer.WriteLine(CsvLine(["Source", sourceName]));
        writer.WriteLine(CsvLine(Headers));
        foreach (var entry in entries)
        {
            writer.WriteLine(CsvLine([
                entry.LumiverseName, entry.NodeIP, entry.ArtNetPort, entry.Subnet, entry.Universe,
                entry.Name, entry.Address, entry.Channels, entry.EndAddress,
                Coordinate(entry.PositionX), Coordinate(entry.PositionY), Coordinate(entry.AngleDegrees),
                PixelCount(entry), entry.ColorFormat, entry.FixtureId]));
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
                        Coordinate(entry.PositionX),
                        Coordinate(entry.PositionY),
                        Coordinate(entry.AngleDegrees),
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

    public static void SavePixelMapSvg(string filePath, IReadOnlyList<PatchEntry> entries)
    {
        XNamespace svg = "http://www.w3.org/2000/svg";
        var mappedEntries = entries.Where(entry => entry.LayoutCorners.Count == 4).ToArray();
        if (mappedEntries.Length == 0)
        {
            throw new InvalidOperationException("The XML does not contain fixture layout positions that can be drawn in an SVG pixel map.");
        }

        var allPoints = mappedEntries.SelectMany(entry => entry.LayoutCorners).ToArray();
        var minX = allPoints.Min(point => point.X);
        var minY = allPoints.Min(point => point.Y);
        var maxX = allPoints.Max(point => point.X);
        var maxY = allPoints.Max(point => point.Y);
        var mapSpan = Math.Max(Math.Max(maxX - minX, maxY - minY), 1);
        var padding = Math.Max(mapSpan * 0.04, 1);
        var viewX = minX - padding;
        var viewY = minY - padding;
        var viewWidth = maxX - minX + padding * 2;
        var viewHeight = maxY - minY + padding * 2;
        const double canvasWidth = 1200;
        var canvasHeight = canvasWidth * viewHeight / viewWidth;
        var pixelRadius = Math.Clamp(mapSpan / 250d, 0.7, 3d);
        var labelSize = Math.Clamp(mapSpan / 65d, 5d, 16d);
        var document = new XDocument(
            new XElement(svg + "svg",
                new XAttribute("viewBox", string.Join(" ", new[] { viewX, viewY, viewWidth, viewHeight }.Select(Number))),
                new XAttribute("width", Number(canvasWidth)),
                new XAttribute("height", Number(canvasHeight)),
                new XElement(svg + "title", "Resolume fixture pixel map"),
                new XElement(svg + "desc", "Fixture pixels positioned in Resolume composition space. Fixture IDs, positions, angles, and DMX addresses are attached to each fixture and pixel."),
                new XElement(svg + "rect",
                    new XAttribute("x", Number(viewX)),
                    new XAttribute("y", Number(viewY)),
                    new XAttribute("width", Number(viewWidth)),
                    new XAttribute("height", Number(viewHeight)),
                    new XAttribute("fill", "#ffffff")),
                mappedEntries.Select(entry => CreateSvgFixture(entry, svg, pixelRadius, labelSize, mapSpan))));

        document.Save(filePath);
    }

    public static void SavePdf(string filePath, IReadOnlyList<PatchEntry> entries, string sourceName, PdfExportOptions options)
    {
        var document = new PdfDocument();
        document.Info.Title = options.Title;
        var font = new XFont("Arial", PdfFontSize, XFontStyleEx.Regular);
        var boldFont = new XFont("Arial", PdfFontSize, XFontStyleEx.Bold);
        var titleFont = new XFont("Arial", PdfFontSize + 7, XFontStyleEx.Bold);
        var columns = GetPdfColumns(options);
        var pageWidth = options.Landscape ? 842d : 595d;
        const double margin = 32;
        const double minimumRowHeight = 24;
        const double cellPadding = 4;
        var lineHeight = PdfFontSize * 1.25;
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
        var includeTableHeaderOnNewPage = false;

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
                graphics.DrawString(options.Title, titleFont, accentBrush, new XPoint(margin, y + PdfFontSize + 4));
                y += PdfFontSize + 12;
            }

            if (!string.IsNullOrWhiteSpace(options.Subtitle))
            {
                graphics.DrawString(options.Subtitle, boldFont, XBrushes.DimGray, new XPoint(margin, y + PdfFontSize));
                y += PdfFontSize + 7;
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
                graphics.DrawString(string.Join("    |    ", metadata), font, XBrushes.DimGray, new XPoint(margin, y + PdfFontSize));
                y += PdfFontSize + 8;
            }

            if (includeTableHeaderOnNewPage && columns.Count > 0)
            {
                DrawRow(columns.Select(column => column.Header).ToArray(), true);
            }
        }

        void DrawRow(IReadOnlyList<string> values, bool isHeader)
        {
            currentFont = isHeader ? boldFont : font;
            var wrappedValues = values
                .Select((value, index) => WrapText(value, currentFont, widths[index] - cellPadding * 2, graphics))
                .ToArray();
            var rowHeight = Math.Max(minimumRowHeight, wrappedValues.Max(lines => lines.Count) * lineHeight + cellPadding * 2);
            var x = margin;
            if (isHeader)
            {
                graphics.DrawRectangle(paleAccentBrush, margin, y, widths.Sum(), rowHeight);
            }

            for (var column = 0; column < values.Count; column++)
            {
                for (var line = 0; line < wrappedValues[column].Count; line++)
                {
                    var layout = new XRect(
                        x + cellPadding,
                        y + cellPadding + line * lineHeight,
                        widths[column] - cellPadding * 2,
                        lineHeight);
                    var format = columns[column].Header is "Fixture" or "Node IP"
                        ? XStringFormats.CenterLeft
                        : XStringFormats.Center;
                    graphics.DrawString(wrappedValues[column][line], currentFont, XBrushes.Black, layout, format);
                }
                x += widths[column];
            }

            graphics.DrawLine(XPens.LightGray, margin, y + rowHeight, margin + widths.Sum(), y + rowHeight);
            y += rowHeight;
        }

        AddPage();
        DrawSummarySection(
            "NODE SUMMARY",
            entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.NodeIP))
                .GroupBy(entry => entry.NodeIP, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var lumiverseCount = group.Select(entry => entry.LumiverseName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();
                    var portAddresses = string.Join(", ", group
                        .Select(entry => entry.ArtNetPort)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(port => int.TryParse(port, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : int.MaxValue)
                        .ThenBy(port => port, StringComparer.Ordinal));
                    return $"{group.Key}  |  {lumiverseCount} Lumiverses  |  Node Port Addresses: {portAddresses}";
                }));
        DrawSummarySection(
            "FIXTURE INVENTORY",
            entries
                .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => $"{group.Count()} x {group.Key}"));

        EnsureSpace(minimumRowHeight);
        includeTableHeaderOnNewPage = true;
        if (columns.Count > 0)
        {
            DrawRow(columns.Select(column => column.Header).ToArray(), true);
        }

        foreach (var lumiverseGroup in entries.GroupBy(entry => new { entry.LumiverseName, entry.NodeIP }))
        {
            if (options.ShowLumiverseGroups)
            {
                EnsureSpace(minimumRowHeight);
                DrawGroupRow($"{lumiverseGroup.Key.LumiverseName}  |  {lumiverseGroup.Key.NodeIP}", boldFont, accentBrush);
            }

            foreach (var portGroup in lumiverseGroup.GroupBy(entry => new { entry.ArtNetPort, entry.Subnet, entry.Universe }))
            {
                if (options.ShowArtNetGroups)
                {
                    EnsureSpace(minimumRowHeight);
                    DrawGroupRow($"Node Port Address {portGroup.Key.ArtNetPort}  |  Subnet {portGroup.Key.Subnet}  |  Universe {portGroup.Key.Universe}", boldFont, XBrushes.DimGray);
                }

                foreach (var entry in portGroup)
                {
                    var values = columns.Select(column => column.Value(entry)).ToArray();
                    var rowHeight = GetRowHeight(values, font, widths, cellPadding, lineHeight, graphics, minimumRowHeight);
                    EnsureSpace(rowHeight);
                    DrawRow(values, false);
                }
            }
        }

        DrawFooter();
        document.Save(filePath);

        void EnsureSpace(double height)
        {
            if (y + height > page.Height.Point - margin - PdfFontSize - 12)
            {
                AddPage();
            }
        }

        void DrawGroupRow(string text, XFont groupFont, XBrush brush)
        {
            graphics.DrawRectangle(paleAccentBrush, margin, y, widths.Sum(), minimumRowHeight);
            graphics.DrawString(text, groupFont, brush, new XRect(margin + 5, y + 4, widths.Sum() - 10, minimumRowHeight - 8), XStringFormats.TopLeft);
            y += minimumRowHeight;
        }

        void DrawSummarySection(string title, IEnumerable<string> lines)
        {
            var summaryWidth = page.Width.Point - margin * 2;
            var summaryLineHeight = PdfFontSize * 1.3;
            EnsureSpace(summaryLineHeight + cellPadding * 2);
            graphics.DrawString(title, boldFont, accentBrush, new XRect(margin, y, summaryWidth, summaryLineHeight), XStringFormats.CenterLeft);
            y += summaryLineHeight + cellPadding;

            foreach (var text in lines)
            {
                var wrappedLines = WrapText(text, font, summaryWidth - cellPadding * 2, graphics);
                var summaryHeight = wrappedLines.Count * lineHeight + cellPadding * 2;
                EnsureSpace(summaryHeight);
                for (var line = 0; line < wrappedLines.Count; line++)
                {
                    graphics.DrawString(
                        wrappedLines[line],
                        font,
                        XBrushes.DimGray,
                        new XRect(margin + cellPadding, y + cellPadding + line * lineHeight, summaryWidth - cellPadding * 2, lineHeight),
                        XStringFormats.CenterLeft);
                }

                y += summaryHeight;
            }

            y += cellPadding * 2;
        }

        void DrawFooter()
        {
            graphics.DrawLine(XPens.LightGray, margin, page.Height.Point - margin + 2, page.Width.Point - margin, page.Height.Point - margin + 2);
            if (!string.IsNullOrWhiteSpace(options.Footer))
            {
                graphics.DrawString(options.Footer, font, XBrushes.DimGray,
                    new XRect(margin, page.Height.Point - margin + 5, page.Width.Point - margin * 2 - 50, PdfFontSize + 6), XStringFormats.TopLeft);
            }
            graphics.DrawString(pageNumber.ToString(CultureInfo.InvariantCulture), font, XBrushes.DimGray,
                new XRect(page.Width.Point - margin - 30, page.Height.Point - margin + 5, 30, PdfFontSize + 6), XStringFormats.TopRight);
        }
    }

    private static double GetRowHeight(
        IReadOnlyList<string> values,
        XFont font,
        IReadOnlyList<double> widths,
        double cellPadding,
        double lineHeight,
        XGraphics graphics,
        double minimumRowHeight)
    {
        var maxLines = values
            .Select((value, index) => WrapText(value, font, widths[index] - cellPadding * 2, graphics).Count)
            .Max();
        return Math.Max(minimumRowHeight, maxLines * lineHeight + cellPadding * 2);
    }

    private static List<string> WrapText(string value, XFont font, double maxWidth, XGraphics graphics)
    {
        var lines = new List<string>();
        var paragraphs = value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var paragraph in paragraphs)
        {
            var currentLine = new StringBuilder();
            var words = paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                var candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                if (graphics.MeasureString(candidate, font).Width <= maxWidth)
                {
                    currentLine.Clear();
                    currentLine.Append(candidate);
                    continue;
                }

                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                }

                var textElements = StringInfo.GetTextElementEnumerator(word);
                while (textElements.MoveNext())
                {
                    var element = textElements.GetTextElement();
                    candidate = currentLine.Length == 0 ? element : $"{currentLine}{element}";
                    if (graphics.MeasureString(candidate, font).Width <= maxWidth)
                    {
                        currentLine.Append(element);
                    }
                    else
                    {
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                        currentLine.Append(element);
                    }
                }
            }

            lines.Add(currentLine.ToString());
        }

        return lines;
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
        if (options.ShowPosition) columns.Add(("Position (X, Y)", entry => entry.Position, 1.8));
        if (options.ShowAngle) columns.Add(("Angle", entry => entry.Angle, 1));
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
        "rgbwa" => 5,
        "r" or "l" or "mono" or "monochrome" or "white" => 1,
        _ => null
    };

    private static XElement CreateSvgFixture(PatchEntry entry, XNamespace svg, double pixelRadius, double labelSize, double mapSpan)
    {
        var corners = entry.LayoutCorners;
        var points = string.Join(" ", corners.Select(point => $"{Number(point.X)},{Number(point.Y)}"));
        var centerX = corners.Average(point => point.X);
        var centerY = corners.Average(point => point.Y);
        var color = FixtureColor(entry.Index);
        var group = new XElement(svg + "g",
            new XAttribute("id", $"fixture-{entry.Index}"),
            new XAttribute("data-fixture-id", entry.FixtureId),
            new XAttribute("data-fixture-name", entry.Name),
            new XAttribute("data-lumiverse", entry.LumiverseName),
            new XAttribute("data-node-ip", entry.NodeIP),
            new XAttribute("data-universe", entry.Universe),
            new XAttribute("data-dmx-start", entry.Address),
            new XAttribute("data-dmx-end", entry.EndAddress),
            new XAttribute("data-position-x", entry.PositionX.HasValue ? Number(entry.PositionX.Value) : string.Empty),
            new XAttribute("data-position-y", entry.PositionY.HasValue ? Number(entry.PositionY.Value) : string.Empty),
            new XAttribute("data-angle-degrees", entry.AngleDegrees.HasValue ? Number(entry.AngleDegrees.Value) : string.Empty),
            new XElement(svg + "title", $"ID {DisplayId(entry)} | {entry.Name} | Position {entry.Position} | Angle {entry.Angle} | DMX {entry.Address}-{entry.EndAddress}"),
            new XElement(svg + "polygon",
                new XAttribute("points", points),
                new XAttribute("fill", color),
                new XAttribute("fill-opacity", "0.16"),
                new XAttribute("stroke", color),
                new XAttribute("stroke-width", Number(Math.Max(mapSpan / 500d, 0.5)))));

        var start = int.TryParse(entry.Address, NumberStyles.Integer, CultureInfo.InvariantCulture, out var address)
            ? address
            : (int?)null;
        var components = Components(entry.ColorFormat);
        if (entry.PixelWidth.HasValue && entry.PixelHeight.HasValue && start.HasValue && components.HasValue)
        {
            var pixelIndex = 0;
            for (var y = 0; y < entry.PixelHeight.Value; y++)
            {
                for (var x = 0; x < entry.PixelWidth.Value; x++)
                {
                    pixelIndex++;
                    var u = (x + 0.5) / entry.PixelWidth.Value;
                    var v = (y + 0.5) / entry.PixelHeight.Value;
                    var pixelPoint = Interpolate(corners, u, v);
                    var pixelStart = start.Value + (pixelIndex - 1) * components.Value;
                    var isFirstPixel = pixelIndex == 1;
                    var pointRadius = isFirstPixel ? pixelRadius * 2 : pixelRadius;
                    group.Add(new XElement(svg + "circle",
                        new XAttribute("cx", Number(pixelPoint.X)),
                        new XAttribute("cy", Number(pixelPoint.Y)),
                        new XAttribute("r", Number(pointRadius)),
                        new XAttribute("fill", color),
                        new XAttribute("stroke", isFirstPixel ? "#111111" : "none"),
                        new XAttribute("stroke-width", isFirstPixel ? Number(Math.Max(pixelRadius * 0.45, 0.8)) : "0"),
                        new XAttribute("data-is-first-pixel", isFirstPixel ? "true" : "false"),
                        new XAttribute("data-pixel-index", pixelIndex),
                        new XAttribute("data-x", x + 1),
                        new XAttribute("data-y", y + 1),
                        new XAttribute("data-dmx-start", pixelStart),
                        new XAttribute("data-dmx-end", pixelStart + components.Value - 1),
                        new XElement(svg + "title", $"{(isFirstPixel ? "FIRST PIXEL | " : string.Empty)}ID {DisplayId(entry)} | Pixel {pixelIndex} ({x + 1}, {y + 1}) | DMX {pixelStart}-{pixelStart + components.Value - 1}")));
                }
            }
        }

        if (entry.AngleDegrees.HasValue)
        {
            var angleRadians = entry.AngleDegrees.Value * Math.PI / 180d;
            var lineLength = Math.Max(mapSpan * 0.015, pixelRadius * 4);
            group.Add(new XElement(svg + "line",
                new XAttribute("x1", Number(centerX)),
                new XAttribute("y1", Number(centerY)),
                new XAttribute("x2", Number(centerX + Math.Cos(angleRadians) * lineLength)),
                new XAttribute("y2", Number(centerY + Math.Sin(angleRadians) * lineLength)),
                new XAttribute("stroke", "#111111"),
                new XAttribute("stroke-width", Number(Math.Max(mapSpan / 700d, 0.5)))));
        }

        group.Add(new XElement(svg + "text",
            new XAttribute("x", Number(centerX)),
            new XAttribute("y", Number(centerY)),
            new XAttribute("text-anchor", "middle"),
            new XAttribute("dominant-baseline", "central"),
            new XAttribute("font-family", "Arial, sans-serif"),
            new XAttribute("font-size", Number(labelSize)),
            new XAttribute("font-weight", "bold"),
            new XAttribute("fill", "#111111"),
            new XAttribute("stroke", "#ffffff"),
            new XAttribute("stroke-width", Number(Math.Max(labelSize / 4, 1))),
            new XAttribute("paint-order", "stroke"),
            DisplayId(entry)));

        return group;
    }

    private static FixturePoint Interpolate(IReadOnlyList<FixturePoint> corners, double u, double v)
    {
        var topX = corners[0].X + (corners[1].X - corners[0].X) * u;
        var topY = corners[0].Y + (corners[1].Y - corners[0].Y) * u;
        var bottomX = corners[3].X + (corners[2].X - corners[3].X) * u;
        var bottomY = corners[3].Y + (corners[2].Y - corners[3].Y) * u;
        return new FixturePoint(topX + (bottomX - topX) * v, topY + (bottomY - topY) * v);
    }

    private static string FixtureColor(int index)
    {
        string[] colors = ["#e4572e", "#17a398", "#ffc914", "#4c78a8", "#b279a2", "#f58518", "#54a24b", "#eeca3b"];
        return colors[(index - 1) % colors.Length];
    }

    private static string DisplayId(PatchEntry entry) =>
        string.IsNullOrWhiteSpace(entry.FixtureId) ? entry.Name : entry.FixtureId;

    private static string Coordinate(double? value) =>
        value.HasValue ? Number(value.Value) : "Unknown";

    private static string Number(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
}
