using System.Windows;

namespace OpenImage.Dialogs;

public partial class NewCanvasDialog : Window
{
    public double CanvasWidth { get; private set; } = 1920;
    public double CanvasHeight { get; private set; } = 1080;
    public string BackgroundType { get; private set; } = "White";

    public NewCanvasDialog()
    {
        InitializeComponent();
    }

    private void PresetFHD_Click(object sender, RoutedEventArgs e) { TxtWidth.Text = "1920"; TxtHeight.Text = "1080"; }
    private void Preset4K_Click(object sender, RoutedEventArgs e) { TxtWidth.Text = "3840"; TxtHeight.Text = "2160"; }
    private void PresetSquare_Click(object sender, RoutedEventArgs e) { TxtWidth.Text = "1080"; TxtHeight.Text = "1080"; }
    private void PresetStory_Click(object sender, RoutedEventArgs e) { TxtWidth.Text = "1080"; TxtHeight.Text = "1920"; }
    private void PresetWallpaper_Click(object sender, RoutedEventArgs e) { TxtWidth.Text = "2560"; TxtHeight.Text = "1440"; }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(TxtWidth.Text, out double w) && double.TryParse(TxtHeight.Text, out double h))
        {
            CanvasWidth = Math.Clamp(w, 50, 8192);
            CanvasHeight = Math.Clamp(h, 50, 8192);

            if (RbTransparent.IsChecked == true) BackgroundType = "Transparent";
            else if (RbDark.IsChecked == true) BackgroundType = "Dark";
            else BackgroundType = "White";

            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
