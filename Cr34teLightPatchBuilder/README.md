# CR34TE Light | Patch Builder

A Windows desktop utility that reads Resolume Arena Advanced Output XML and creates a grouped fixture DMX patch sheet for technicians.

## Run

Open `Cr34teLightPatchBuilder.csproj` in Visual Studio or VS Code with the .NET 8 SDK and Windows Desktop runtime, then run the project. To build a Windows executable:

```powershell
dotnet publish .\Cr34teLightPatchBuilder.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The published executable is written under `bin\Release\net8.0-windows\win-x64\publish`.

## XML mapping

The parser reads each `DmxScreen`, its `LumiverseId`, output universe, and contained `DmxSlice` entries. It calculates the channel footprint from an explicit leading channel range in the slice name when present; otherwise, it uses embedded pixel width/height and color format (including RGBWA and single-channel luminance). This preserves explicit fixture ranges for modes with additional channels such as a dimmer slider. Rows with incomplete fixture data are marked `Check mapping`; overlapping address ranges on a universe are marked `Address overlap`. Confirm the export against the actual lighting console patch before show use.

The patch preview and PDF group fixtures by Lumiverse/node, then by Node Port Address. The displayed value is `subnet * 16 + universe + 1`, so subnet 0/universe 0 is Node Port Address 1. Fixture DMX addresses remain 1-based. A leading channel range in the fixture name (for example, `1 - 150`) is omitted because the start and end addresses are shown separately; other useful numbers in the fixture name are preserved. The numeric `LumiverseId`, device label, and per-row status are omitted. Packed `TT_IP` node targets are decoded and shown as dotted IPv4 addresses; broadcast targets are labeled `Broadcast`. If the Lumiverse name itself is a valid IPv4 address and no unicast node address is set, that address is used as the displayed node IP. The PDF includes a `Node IP` column by default; it can be toggled in the export settings.

Before saving a PDF, the export settings let you edit the title, subtitle and footer; choose A4/Letter, portrait/landscape, and accent color; toggle source/date/group headings; and select the fixture table columns, including position and angle when present. PDF text uses a fixed 10-point font. Settings remain selected during the current app session. PDF table cells wrap long values, and each row grows to fit its tallest cell.

The PDF begins with a node summary listing each node IP, its distinct Lumiverse count, and its Node Port Addresses, followed by a fixture inventory with counts by fixture name. The detailed patch table follows. The preview's final `ID` column is editable for manually entered fixture IDs; entered values are included in the PDF and pixel-map SVG.

Use `Auto-fill IDs` to give sequential numeric IDs to fixtures whose ID cells are blank; existing IDs are preserved. Fixture position is shown as the center X/Y coordinate from Resolume's `InputRect`; fixture orientation is read from its `orientation` value and converted from radians to degrees. These fields are included in the patch CSV/PDF.

Use `Save Pixel Map` to create an SVG drawing directly from the XML. It includes the fixture layout, pixel locations, IDs, directions, positions, and DMX details. The first logical pixel is drawn larger with a dark outline and identified in its SVG tooltip; confirm the logical pixel sequence against Resolume's distribution and the physical installation before using it to wire or address fixtures.
