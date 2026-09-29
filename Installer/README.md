# Windows installer

From the repository root, build the application and MSI together:

```powershell
.\build.ps1
```

The installer is created at `artifacts\installer\CR34TE-Light-Patch-Builder-Setup.msi`. To specify a version, run `.\build.ps1 -Version 1.0.3` from the repository root. The MSI installs machine-wide under Program Files, creates a Start Menu shortcut, and supports upgrades and uninstall through Windows Installed Apps. Windows will ask for administrator approval. The installer bundles the published WPF support files and does not require a separately installed .NET runtime.
