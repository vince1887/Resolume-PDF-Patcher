# CR34TE Light Patch Builder

Analyze Resolume Arena Advanced Output XML and create fixture DMX patch PDFs, patch CSVs, and per-pixel CSV maps.

## Download and install

**[Download the latest Windows installer](https://github.com/vince1887/Resolume-PDF-Patcher/releases/latest/download/CR34TE-Light-Patch-Builder-Setup.msi)**  
Browse [all releases and change notes](https://github.com/vince1887/Resolume-PDF-Patcher/releases).

The x64 MSI installs the application and its runtime dependencies, creates a Start Menu shortcut, and supports upgrades and removal through Windows Installed Apps. Windows requests administrator approval because the app is installed under Program Files.

Installed copies check GitHub Releases at startup. When a newer stable version is available, the app asks before downloading and launching the MSI installer to upgrade. Choose **No** to keep using the current version.

The GitHub repository and its releases must be public for the download links and automatic update checks to work for everyone.

## Build from source

Requirements: Windows, .NET 8 SDK, and the WiX Toolset SDK packages restored by the project.

From this repository's root:

```powershell
.\build.ps1
```

This publishes a self-contained Windows x64 application and builds the MSI. Outputs go under the ignored `artifacts\` folder:

- `artifacts\publish\` — app files bundled into the installer
- `artifacts\installer\CR34TE-Light-Patch-Builder-Setup.msi` — installable app

To build a specific installer version:

```powershell
.\build.ps1 -Version 1.0.5
```

## Publish an update

Update the application version in `Cr34teLightPatchBuilder\Cr34teLightPatchBuilder.csproj`, commit the changes, and click **Push origin** in GitHub Desktop.

The `Build and publish installer` GitHub Actions workflow checks whether that version has already been released. If not, it builds the app and MSI, creates the matching `v<version>` tag and GitHub Release, and attaches the installer with generated notes. No separate tag push is needed. Existing installed copies detect that release on their next launch and offer the update.

## Project layout

```text
.
├── .github/workflows/release.yml     # Tagged-release build and publication
├── Installer/                        # WiX MSI installer source
├── Cr34teLightPatchBuilder/          # WPF app source and Resources/ app icon
├── artifacts/                        # Local build output (git-ignored)
├── build.ps1                         # Reproducible app + MSI build
└── README.md                         # GitHub landing page and downloads
```

See [`Cr34teLightPatchBuilder/README.md`](Cr34teLightPatchBuilder/README.md) for XML mapping and export details.
