using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace ResolumePatchBuilder;

public partial class MainWindow : Window
{
    private string? _sourcePath;
    private IReadOnlyList<PatchEntry> _entries = [];
    private PdfExportOptions _pdfOptions = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose Resolume Advanced Output XML",
            Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _sourcePath = dialog.FileName;
        SourcePathText.Text = _sourcePath;
        ConvertButton.IsEnabled = true;
        SaveCsvButton.IsEnabled = false;
        SavePdfButton.IsEnabled = false;
        SavePixelMapButton.IsEnabled = false;
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
            _entries = ResolumeXmlParser.Parse(_sourcePath);
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
            SaveCsvButton.IsEnabled = _entries.Count > 0;
            SavePdfButton.IsEnabled = _entries.Count > 0;
            SavePixelMapButton.IsEnabled = _entries.Any(entry =>
                entry.PixelWidth.HasValue && entry.PixelHeight.HasValue &&
                int.TryParse(entry.Address, out _) && entry.ColorFormat != "Unknown");
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, exception.Message, "Could not analyze XML", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Analysis failed";
        }
    }

    private void SaveCsvButton_Click(object sender, RoutedEventArgs e)
    {
        CommitFixtureIdEdit();
        SavePatch("csv");
    }

    private void SavePdfButton_Click(object sender, RoutedEventArgs e)
    {
        CommitFixtureIdEdit();
        SavePatch("pdf");
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
            Title = "Save per-pixel DMX map",
            Filter = "CSV pixel map (*.csv)|*.csv",
            FileName = $"{Path.GetFileNameWithoutExtension(_sourcePath)}_pixel_map.csv",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            PatchExporter.SavePixelMapCsv(dialog.FileName, _entries);
            StatusText.Text = $"Saved pixel map {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, exception.Message, "Could not save pixel map", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Pixel map save failed";
        }
    }

    private void CommitFixtureIdEdit()
    {
        PatchGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        PatchGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
    }

    private void SavePatch(string format)
    {
        if (_sourcePath is null || _entries.Count == 0)
        {
            return;
        }

        if (format == "pdf")
        {
            var customizeDialog = new PdfExportWindow(_pdfOptions) { Owner = this };
            if (customizeDialog.ShowDialog() != true)
            {
                return;
            }

            _pdfOptions = customizeDialog.Options;
        }

        var dialog = new SaveFileDialog
        {
            Title = $"Save patch as {format.ToUpperInvariant()}",
            Filter = format == "pdf" ? "PDF document (*.pdf)|*.pdf" : "CSV file (*.csv)|*.csv",
            FileName = $"{Path.GetFileNameWithoutExtension(_sourcePath)}_patch.{format}",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            if (format == "pdf")
            {
                PatchExporter.SavePdf(dialog.FileName, _entries, Path.GetFileName(_sourcePath), _pdfOptions);
            }
            else
            {
                PatchExporter.SaveCsv(dialog.FileName, _entries, Path.GetFileName(_sourcePath));
            }

            StatusText.Text = $"Saved {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Could not save patch", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Save failed";
        }
    }
}
