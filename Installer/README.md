# Windows installer

Build the current self-contained app publish output first:

```powershell
dotnet publish ..\ResolumePatchBuilder\ResolumePatchBuilder.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -p:PublishDir="..\ResolumePatchBuilder\bin\Release\SharePackage\"
```

Then build this WiX installer project:

```powershell
dotnet build .\PatchBuilderInstaller.wixproj -c Release
```

The MSI is created in `bin\Release`. It installs machine-wide under Program Files, creates a Start Menu shortcut, and supports uninstall through Windows Installed Apps. Windows will ask for administrator approval. The installer bundles the published WPF support files and does not require a separately installed .NET runtime.
