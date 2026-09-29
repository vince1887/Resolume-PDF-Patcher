using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Data;

namespace Cr34teLightPatchBuilder;

public partial class MainWindow : Window
{
    private string? _sourcePath;
    private IReadOnlyList<PatchEntry> _entries = [];
    private PdfExportOptions _pdfOptions = new();

    public MainWindow()
    {
        InitializeComponent();
        ContentRendered += MainWindow_ContentRendered;
    }

    private async void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= MainWindow_ContentRendered;
        if (!AppUpdateService.IsInstalledByMsi())
        {
            return;
        }

        AppRelease? release;
        try
        {
            release = await AppUpdateService.GetNewerReleaseAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException or InvalidDataException)
        {
            System.Diagnostics.Trace.TraceWarning($"Could not check for application updates: {exception}");
            return;
        }

        if (release is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Version {release.Version} is available. Would you like to download and install it now?",
            "CR34TE Light Patch Builder update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            StatusText.Text = $"Downloading version {release.Version}...";
            var installerPath = await AppUpdateService.DownloadInstallerAsync(release);
            AppUpdateService.LaunchInstaller(installerPath);
            Application.Current.Shutdown();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceError($"Update download/install failed: {exception}");
            MessageBox.Show(
                this,
                $"The update could not be started. You can download the latest installer from GitHub Releases.\n\n{exception.Message}",
                "Update unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose Advanced Output XML",
            Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _sourcePath = dialog.FileName;
        SourcePathText.Text = _sourcePath;
        _entries = [];
        ConvertButton.IsEnabled = true;
        SavePdfButton.IsEnabled = false;
        SavePixelMapButton.IsEnabled = false;
        AutoFillIdsButton.IsEnabled = false;
        PatchGrid.ItemsSource = null;
        SummaryText.Text = "Ready to analyze";
        FixtureMetric.Text = "--";
        LumiverseMetric.Text = "--";
        PixelMetric.Text = "--";
        StatusText.Text = "XML selected";
    }

    private void ConvertButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sourcePath is null)
        {
            return;
        }

        try
        {
            _entries = AdvancedOutputXmlParser.Parse(_sourcePath);
            var view = CollectionViewSource.GetDefaultView(_entries);
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PatchEntry.LumiverseGroup)));
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PatchEntry.ArtNetPortGroup)));
            PatchGrid.ItemsSource = view;
            SummaryText.Text = $"{_entries.Count} fixtures  /  {_entries.Select(entry => entry.LumiverseGroup).Distinct().Count()} Lumiverses";
            FixtureMetric.Text = _entries.Count.ToString("N0");
            LumiverseMetric.Text = _entries.Select(entry => entry.LumiverseGroup).Distinct().Count().ToString("N0");
            PixelMetric.Text = _entries
                .Where(entry => entry.PixelWidth.HasValue && entry.PixelHeight.HasValue)
                .Sum(entry => (long)entry.PixelWidth!.Value * entry.PixelHeight!.Value)
                .ToString("N0");
            StatusText.Text = _entries.Count == 0
                ? "No DMX slices found in this XML"
                : "Analysis complete. Review addresses and pixel order before exporting.";
            SavePdfButton.IsEnabled = _entries.Count > 0;
            SavePixelMapButton.IsEnabled = _entries.Any(entry => entry.LayoutCorners.Count == 4);
            AutoFillIdsButton.IsEnabled = _entries.Count > 0;
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, exception.Message, "Could not analyze XML", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Analysis failed";
        }
    }

    private void SavePdfButton_Click(object sender, RoutedEventArgs e)
    {
        CommitFixtureIdEdit();
        SavePatch();
    }

    private void SavePixelMapButton_Click(object sender, RoutedEventArgs e)
    {
        CommitFixtureIdEdit();
        if (_sourcePath is null || _entries.Count == 0)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save XML pixel map as SVG",
            Filter = "SVG pixel map (*.svg)|*.svg",
            FileName = $"{Path.GetFileNameWithoutExtension(_sourcePath)}_pixel_map.svg",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            PatchExporter.SavePixelMapSvg(dialog.FileName, _entries);
            StatusText.Text = $"Saved pixel map {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Could not save pixel map SVG", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Pixel map save failed";
        }
    }

    private void AutoFillIdsButton_Click(object sender, RoutedEventArgs e)
    {
        CommitFixtureIdEdit();
        var filled = FixtureIdGenerator.FillMissing(_entries);
        PatchGrid.Items.Refresh();
        StatusText.Text = filled == 0
            ? "All fixtures already have IDs"
            : $"Auto-filled {filled} blank fixture ID{(filled == 1 ? string.Empty : "s")}";
    }

    private void CommitFixtureIdEdit()
    {
        PatchGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        PatchGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
    }

    private void SavePatch()
    {
        if (_sourcePath is null || _entries.Count == 0)
        {
            return;
        }

        var customizeDialog = new PdfExportWindow(_pdfOptions) { Owner = this };
        if (customizeDialog.ShowDialog() != true)
        {
            return;
        }

        _pdfOptions = customizeDialog.Options;
        var dialog = new SaveFileDialog
        {
            Title = "Save patch as PDF",
            Filter = "PDF document (*.pdf)|*.pdf",
            FileName = $"{Path.GetFileNameWithoutExtension(_sourcePath)}_patch.pdf",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            PatchExporter.SavePdf(dialog.FileName, _entries, Path.GetFileName(_sourcePath), _pdfOptions);
            StatusText.Text = $"Saved {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Could not save patch", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Save failed";
        }
    }
}
