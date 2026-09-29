# CR34TE Light | Patch Builder

A Windows desktop utility that reads Resolume Arena Advanced Output XML and creates a grouped fixture DMX patch sheet for technicians.

## Run

Open `Cr34teLightPatchBuilder.csproj` in Visual Studio or VS Code with the .NET 8 SDK and Windows Desktop runtime, then run the project. To build a Windows executable:

```powershell
dotnet publish .\Cr34teLightPatchBuilder.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The published executable is written under `bin\Release\net8.0-windows\win-x64\publish`.

## XML mapping

The parser reads each `DmxScreen`, its `LumiverseId`, output universe, and contained `DmxSlice` entries. It uses the slice name, `Start Channel`, and embedded pixel width/height plus color format to calculate the channel footprint. Rows with incomplete fixture data are marked `Check mapping`; overlapping address ranges on a universe are marked `Address overlap`. Confirm the export against the actual lighting console patch before show use.

The patch preview and PDF group fixtures by Lumiverse/node, then by Node Port Address. The displayed value is `subnet * 16 + universe + 1`, so subnet 0/universe 0 is Node Port Address 1. Fixture DMX addresses remain 1-based. A leading channel range in the fixture name (for example, `1 - 150`) is omitted because the start and end addresses are shown separately; other useful numbers in the fixture name are preserved. The numeric `LumiverseId`, device label, and per-row status are omitted. Packed `TT_IP` node targets are decoded and shown as dotted IPv4 addresses; broadcast targets are labeled `Broadcast`. If the Lumiverse name itself is a valid IPv4 address and no unicast node address is set, that address is used as the displayed node IP. The PDF includes a `Node IP` column by default; it can be toggled in the export settings.

Before saving a PDF, the export settings let you edit the title, subtitle and footer; choose A4/Letter, portrait/landscape, font size and accent color; toggle source/date/group headings; and select the fixture table columns. Settings remain selected during the current app session.

The PDF contains the customizable summary patch table only. Export the detailed per-pixel data separately using `Save Pixel Map` as CSV. The preview's final `ID` column is editable for manually entered fixture IDs; entered values are included in the patch CSV, PDF when the ID column is selected, and pixel-map CSV.

`Save Pixel Map` writes one CSV row per logical pixel with its X/Y coordinate and calculated DMX channel range. The CSV preserves Resolume's distribution code and notes the assumed logical row-major sequence; confirm pixel direction/order against the physical installation before using it to wire or address fixtures.
