using System.Buffers.Binary;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace ResolumePatchBuilder;

public static class ResolumeXmlParser
{
    public static IReadOnlyList<PatchEntry> Parse(string filePath)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(filePath, settings);
        var document = XDocument.Load(reader);
        var entries = new List<PatchEntry>();

        foreach (var screen in document.Descendants().Where(element => element.Name.LocalName == "DmxScreen"))
        {
            var configuredLumiverseName = FindGroupValue(screen, "Params", "Name");
            var screenElementName = Attribute(screen, "name");
            var lumiverseName = configuredLumiverseName ?? screenElementName ?? "Unnamed Lumiverse";
            var universe = FindNamedValue(screen, "OutputDeviceDmx", "Universe") ?? "Unknown";
            var subnet = FindNamedValue(screen, "OutputDeviceDmx", "Subnet") ?? "0";
            var nodeIP = GetNodeIP(
                FindNamedValue(screen, "OutputDeviceDmx", "TargetIP"),
                configuredLumiverseName,
                screenElementName);
            var artNetPort = int.TryParse(subnet, out var subnetNumber) && int.TryParse(universe, out var universeNumber)
                ? Math.Max(0, subnetNumber * 16 + universeNumber - 1).ToString(CultureInfo.InvariantCulture)
                : "Unknown";
            var slices = screen.Descendants().Where(element => element.Name.LocalName == "DmxSlice");

            foreach (var slice in slices)
            {
                var sliceName = ParameterValue(slice, "Name") ?? $"Slice {entries.Count + 1}";
                var startText = FindGroupValue(slice, "Input", "Start Channel");
                var start = ParsePositiveInteger(startText);
                var channelCount = GetChannelCount(slice);
                var end = start.HasValue && channelCount.HasValue ? start.Value + channelCount.Value - 1 : (int?)null;
                var pixelData = GetPixelData(slice);

                entries.Add(new PatchEntry(
                    entries.Count + 1,
                    lumiverseName,
                    nodeIP,
                    subnet,
                    sliceName,
                    universe,
                    artNetPort,
                    start?.ToString(CultureInfo.InvariantCulture) ?? "Unknown",
                    channelCount?.ToString(CultureInfo.InvariantCulture) ?? "Unknown",
                    end?.ToString(CultureInfo.InvariantCulture) ?? "Unknown",
                    pixelData.Width,
                    pixelData.Height,
                    pixelData.ColorFormat,
                    pixelData.Distribution));
            }
        }

        return entries;
    }

    private static string GetNodeIP(string? target, string? configuredLumiverseName, string? screenElementName)
    {
        var parts = target?.Split((char[]?)null, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        foreach (var part in parts)
        {
            if (part.Contains('.') && System.Net.IPAddress.TryParse(part, out var targetAddress) &&
                targetAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return targetAddress.ToString();
            }
        }

        if (parts.Length >= 3 && uint.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var packed))
        {
            var addressBytes = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(addressBytes, packed);
            return new System.Net.IPAddress(addressBytes).ToString();
        }

        foreach (var name in new[] { configuredLumiverseName, screenElementName })
        {
            if (name?.Contains('.') == true && System.Net.IPAddress.TryParse(name, out var lumiverseAddress) &&
                lumiverseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return lumiverseAddress.ToString();
            }
        }

        return parts.FirstOrDefault() == "TT_BROADCAST" ? "Broadcast" : string.Empty;
    }

    private static int? GetChannelCount(XElement slice)
    {
        var data = GetPixelData(slice);
        return data.Width.HasValue && data.Height.HasValue && data.Components.HasValue
            ? checked(data.Width.Value * data.Height.Value * data.Components.Value)
            : null;
    }

    private static (int? Width, int? Height, int? Components, string ColorFormat, string Distribution) GetPixelData(XElement slice)
    {
        var pixels = slice.Descendants().FirstOrDefault(element => element.Name.LocalName == "ParamFixturePixels");
        if (pixels is null)
        {
            return (null, null, null, "Unknown", "Unknown");
        }

        var width = ParsePositiveInteger(NestedParameterValue(pixels, "Width"));
        var height = ParsePositiveInteger(NestedParameterValue(pixels, "Height"));
        var format = NestedParameterValue(pixels, "Color Format")?.Trim().ToLowerInvariant() ?? "Unknown";
        var components = format switch
        {
            "rgb" or "bgr" or "cmy" => 3,
            "rgba" or "argb" or "rgbw" => 4,
            "r" or "mono" or "monochrome" or "white" => 1,
            _ => (int?)null
        };

        return (width, height, components, format, NestedParameterValue(pixels, "Distribution") ?? "Unknown");
    }

    private static string? NestedParameterValue(XElement parent, string name) =>
        parent.Descendants()
            .Where(element => element.Name.LocalName is "Param" or "ParamRange" or "ParamChoice")
            .FirstOrDefault(element => Attribute(element, "name") == name) is { } parameter
                ? Attribute(parameter, "value")
                : null;

    private static string? FindNamedValue(XElement parent, string containerName, string parameterName)
    {
        var container = parent.Descendants().FirstOrDefault(element => element.Name.LocalName == containerName);
        return container is null ? null : NestedParameterValue(container, parameterName);
    }

    private static string? FindGroupValue(XElement parent, string groupName, string parameterName)
    {
        var group = parent.Descendants().FirstOrDefault(element =>
            element.Name.LocalName == "Params" && Attribute(element, "name") == groupName);
        return group is null ? null : NestedParameterValue(group, parameterName);
    }

    private static string? ParameterValue(XElement parent, string parameterName) => NestedParameterValue(parent, parameterName);

    private static string? Attribute(XElement element, string name) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value;

    private static int? ParsePositiveInteger(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : null;
}
