using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using WinForms = System.Windows.Forms;

namespace Cr34teLightPatchBuilder;

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
        ShowSourceInput.IsChecked = Options.ShowSourceFile;
        ShowDateInput.IsChecked = Options.ShowGeneratedDate;
        ShowLumiverseInput.IsChecked = Options.ShowLumiverseGroups;
        ShowArtNetInput.IsChecked = Options.ShowArtNetGroups;
        FixtureInput.IsChecked = Options.ShowFixture;
        NodeIPInput.IsChecked = Options.ShowNodeIP;
        UniverseInput.IsChecked = Options.ShowUniverse;
        StartAddressInput.IsChecked = Options.ShowStartAddress;
        ChannelsInput.IsChecked = Options.ShowChannels;
        EndAddressInput.IsChecked = Options.ShowEndAddress;
        PositionInput.IsChecked = Options.ShowPosition;
        AngleInput.IsChecked = Options.ShowAngle;
        PixelsInput.IsChecked = Options.ShowPixels;
        ColorFormatInput.IsChecked = Options.ShowColorFormat;
        FixtureIdInput.IsChecked = Options.ShowFixtureId;
    }

    private static void SelectItem(System.Windows.Controls.ComboBox comboBox, string content)
    {
        comboBox.SelectedItem = comboBox.Items.Cast<ComboBoxItem>().First(item => item.Content?.ToString() == content);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ChooseAccentButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseAccentHex(AccentInput.Text.Trim(), out var initialColor)
            && !TryParseAccentHex(Options.AccentHex, out initialColor))
        {
            initialColor = System.Drawing.Color.FromArgb(249, 155, 6);
        }

        using var dialog = new WinForms.ColorDialog
        {
            Color = initialColor,
            FullOpen = true
        };
        var owner = new WinFormsWindowOwner(new WindowInteropHelper(this).Handle);
        if (dialog.ShowDialog(owner) != WinForms.DialogResult.OK)
        {
            return;
        }

        AccentInput.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
    }

    private static bool TryParseAccentHex(string value, out System.Drawing.Color color)
    {
        color = default;
        if (value.Length != 7 || value[0] != '#' || !value.Skip(1).All(Uri.IsHexDigit))
        {
            return false;
        }

        color = System.Drawing.Color.FromArgb(
            Convert.ToInt32(value.Substring(1, 2), 16),
            Convert.ToInt32(value.Substring(3, 2), 16),
            Convert.ToInt32(value.Substring(5, 2), 16));
        return true;
    }

    private sealed class WinFormsWindowOwner : WinForms.IWin32Window
    {
        public WinFormsWindowOwner(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedColumns = new[]
        {
            FixtureInput.IsChecked, NodeIPInput.IsChecked, UniverseInput.IsChecked, StartAddressInput.IsChecked,
            ChannelsInput.IsChecked, EndAddressInput.IsChecked, PositionInput.IsChecked, AngleInput.IsChecked, PixelsInput.IsChecked,
            ColorFormatInput.IsChecked, FixtureIdInput.IsChecked
        };
        if (!selectedColumns.Any(isSelected => isSelected == true))
        {
            System.Windows.MessageBox.Show(this, "Select at least one table column.", "PDF columns", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var accent = AccentInput.Text.Trim();
        if (accent.Length != 7 || accent[0] != '#' || !accent.Skip(1).All(Uri.IsHexDigit))
        {
            System.Windows.MessageBox.Show(this, "Enter a hex color such as #F99B06.", "Accent color", MessageBoxButton.OK, MessageBoxImage.Information);
            AccentInput.Focus();
            return;
        }

        Options.Title = TitleInput.Text.Trim();
        Options.Subtitle = SubtitleInput.Text.Trim();
        Options.Footer = FooterInput.Text.Trim();
        Options.AccentHex = accent;
        Options.PageSize = ((ComboBoxItem)PageSizeInput.SelectedItem).Content.ToString()!;
        Options.Landscape = ((ComboBoxItem)OrientationInput.SelectedItem).Content?.ToString() == "Landscape";
        Options.ShowSourceFile = ShowSourceInput.IsChecked == true;
        Options.ShowGeneratedDate = ShowDateInput.IsChecked == true;
        Options.ShowLumiverseGroups = ShowLumiverseInput.IsChecked == true;
        Options.ShowArtNetGroups = ShowArtNetInput.IsChecked == true;
        Options.ShowFixture = FixtureInput.IsChecked == true;
        Options.ShowNodeIP = NodeIPInput.IsChecked == true;
        Options.ShowUniverse = UniverseInput.IsChecked == true;
        Options.ShowStartAddress = StartAddressInput.IsChecked == true;
        Options.ShowChannels = ChannelsInput.IsChecked == true;
        Options.ShowEndAddress = EndAddressInput.IsChecked == true;
        Options.ShowPosition = PositionInput.IsChecked == true;
        Options.ShowAngle = AngleInput.IsChecked == true;
        Options.ShowPixels = PixelsInput.IsChecked == true;
        Options.ShowColorFormat = ColorFormatInput.IsChecked == true;
        Options.ShowFixtureId = FixtureIdInput.IsChecked == true;
        DialogResult = true;
    }
}
