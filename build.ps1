param(
    [string]$Version = "1.0.2"
)

$ErrorActionPreference = "Stop"
$appProject = Join-Path $PSScriptRoot "ResolumePatchBuilder\ResolumePatchBuilder.csproj"
$installerProject = Join-Path $PSScriptRoot "Installer\PatchBuilderInstaller.wixproj"
$publishDirectory = Join-Path $PSScriptRoot "artifacts\publish"
$installerDirectory = Join-Path $PSScriptRoot "artifacts\installer"

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be numeric, for example 1.0.2."
}

New-Item -ItemType Directory -Force -Path $publishDirectory, $installerDirectory | Out-Null

dotnet publish $appProject -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    "-p:Version=$Version" `
    "-p:PublishDir=$publishDirectory\"
if ($LASTEXITCODE -ne 0) {
    throw "Application publish failed with exit code $LASTEXITCODE."
}

dotnet build $installerProject -c Release `
    "-p:AppPublishDir=$publishDirectory" `
    "-p:ProductVersion=$Version" `
    "-p:OutputPath=$installerDirectory\"
if ($LASTEXITCODE -ne 0) {
    throw "Installer build failed with exit code $LASTEXITCODE."
}

$installer = Join-Path $installerDirectory "CR34TE-Light-Patch-Builder-Setup.msi"
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Installer build completed without producing $installer."
}
Write-Host "Installer ready: $installer"
