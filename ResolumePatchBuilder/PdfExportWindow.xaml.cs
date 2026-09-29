using System.Windows;
using System.Windows.Controls;

namespace ResolumePatchBuilder;

public partial class PdfExportWindow : Window
{
    public PdfExportOptions Options { get; }

    public PdfExportWindow(PdfExportOptions options)
    {
        InitializeComponent();
        Options = options;
        LoadOptions();
    }

    private void LoadOptions()
    {
        TitleInput.Text = Options.Title;
        SubtitleInput.Text = Options.Subtitle;
        FooterInput.Text = Options.Footer;
        AccentInput.Text = Options.AccentHex;
        SelectItem(PageSizeInput, Options.PageSize);
        SelectItem(OrientationInput, Options.Landscape ? "Landscape" : "Portrait");
        SelectItem(FontSizeInput, Options.FontSize.ToString());
        ShowSourceInput.IsChecked = Options.ShowSourceFile;
        ShowDateInput.IsChecked = Options.ShowGeneratedDate;
        ShowLumiverseInput.IsChecked = Options.ShowLumiverseGroups;
        ShowArtNetInput.IsChecked = Options.ShowArtNetGroups;
        FixtureInput.IsChecked = Options.ShowFixture;
        UniverseInput.IsChecked = Options.ShowUniverse;
        StartAddressInput.IsChecked = Options.ShowStartAddress;
        ChannelsInput.IsChecked = Options.ShowChannels;
        EndAddressInput.IsChecked = Options.ShowEndAddress;
        PixelsInput.IsChecked = Options.ShowPixels;
        ColorFormatInput.IsChecked = Options.ShowColorFormat;
        FixtureIdInput.IsChecked = Options.ShowFixtureId;
    }

    private static void SelectItem(ComboBox comboBox, string content)
    {
        comboBox.SelectedItem = comboBox.Items.Cast<ComboBoxItem>().First(item => item.Content?.ToString() == content);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedColumns = new[]
        {
            FixtureInput.IsChecked, UniverseInput.IsChecked, StartAddressInput.IsChecked,
            ChannelsInput.IsChecked, EndAddressInput.IsChecked, PixelsInput.IsChecked,
            ColorFormatInput.IsChecked, FixtureIdInput.IsChecked
        };
        if (!selectedColumns.Any(isSelected => isSelected == true))
        {
            MessageBox.Show(this, "Select at least one table column.", "PDF columns", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var accent = AccentInput.Text.Trim();
        if (accent.Length != 7 || accent[0] != '#' || !accent.Skip(1).All(Uri.IsHexDigit))
        {
            MessageBox.Show(this, "Enter a hex color such as #F99B06.", "Accent color", MessageBoxButton.OK, MessageBoxImage.Information);
            AccentInput.Focus();
            return;
        }

        Options.Title = TitleInput.Text.Trim();
        Options.Subtitle = SubtitleInput.Text.Trim();
        Options.Footer = FooterInput.Text.Trim();
        Options.AccentHex = accent;
        Options.PageSize = ((ComboBoxItem)PageSizeInput.SelectedItem).Content.ToString()!;
        Options.Landscape = ((ComboBoxItem)OrientationInput.SelectedItem).Content?.ToString() == "Landscape";
        Options.FontSize = int.Parse(((ComboBoxItem)FontSizeInput.SelectedItem).Content.ToString()!);
        Options.ShowSourceFile = ShowSourceInput.IsChecked == true;
        Options.ShowGeneratedDate = ShowDateInput.IsChecked == true;
        Options.ShowLumiverseGroups = ShowLumiverseInput.IsChecked == true;
        Options.ShowArtNetGroups = ShowArtNetInput.IsChecked == true;
        Options.ShowFixture = FixtureInput.IsChecked == true;
        Options.ShowUniverse = UniverseInput.IsChecked == true;
        Options.ShowStartAddress = StartAddressInput.IsChecked == true;
        Options.ShowChannels = ChannelsInput.IsChecked == true;
        Options.ShowEndAddress = EndAddressInput.IsChecked == true;
        Options.ShowPixels = PixelsInput.IsChecked == true;
        Options.ShowColorFormat = ColorFormatInput.IsChecked == true;
        Options.ShowFixtureId = FixtureIdInput.IsChecked == true;
        DialogResult = true;
    }
}
