using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;

namespace ResolumePatchBuilder;

public sealed record AppRelease(Version Version, Uri InstallerUri);

public static class AppUpdateService
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/vince1887/Resolume-PDF-Patcher/releases/latest";
    private const string InstallerFileName = "CR34TE-Light-Patch-Builder-Setup.msi";
    private const string InstallMarkerKey = @"Software\CR34TE Light\Patch Builder";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static bool IsInstalledByMsi()
    {
        using var key = Registry.CurrentUser.OpenSubKey(InstallMarkerKey, writable: false);
        return key?.GetValue("Installed") is int installed && installed == 1;
    }

    public static async Task<AppRelease?> GetNewerReleaseAsync()
    {
        using var response = await HttpClient.GetAsync(LatestReleaseApi);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var release = await JsonDocument.ParseAsync(stream);

        var root = release.RootElement;
        if (!root.TryGetProperty("tag_name", out var tagElement) ||
            !Version.TryParse(tagElement.GetString()?.TrimStart('v', 'V'), out var latestVersion))
        {
            throw new InvalidDataException("The latest GitHub release does not have a valid version tag.");
        }

        var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version
            ?? throw new InvalidDataException("The installed application version could not be determined.");
        if (latestVersion <= currentVersion)
        {
            return null;
        }

        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The latest GitHub release does not contain installer assets.");
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.TryGetProperty("name", out var name) &&
                string.Equals(name.GetString(), InstallerFileName, StringComparison.OrdinalIgnoreCase) &&
                asset.TryGetProperty("browser_download_url", out var url) &&
                Uri.TryCreate(url.GetString(), UriKind.Absolute, out var installerUri) &&
                installerUri.Scheme == Uri.UriSchemeHttps)
            {
                return new AppRelease(latestVersion, installerUri);
            }
        }

        throw new InvalidDataException($"The latest GitHub release is missing {InstallerFileName}.");
    }

    public static async Task<string> DownloadInstallerAsync(AppRelease release)
    {
        var updateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CR34TE Light",
            "Patch Builder",
            "Updates");
        Directory.CreateDirectory(updateDirectory);
        var installerPath = Path.Combine(updateDirectory, $"{release.Version}-{InstallerFileName}");

        using var response = await HttpClient.GetAsync(release.InstallerUri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync();
        await using var destination = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destination);
        if (destination.Length == 0)
        {
            throw new InvalidDataException("The downloaded installer is empty.");
        }

        return installerPath;
    }

    public static void LaunchInstaller(string installerPath)
    {
        var startInfo = new ProcessStartInfo("msiexec.exe", $"/i \"{installerPath}\"")
        {
            UseShellExecute = true,
            Verb = "runas"
        };
        var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Windows could not start the installer.");
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CR34TE-Light-Patch-Builder");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
