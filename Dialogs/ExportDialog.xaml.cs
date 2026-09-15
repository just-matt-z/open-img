using System.Windows;

namespace OpenImage.Dialogs;

public partial class ExportDialog : Window
{
    private readonly double _origWidth;
    private readonly double _origHeight;

    public string Format { get; private set; } = "PNG";
    public double Scale { get; private set; } = 1.0;
    public int Quality { get; private set; } = 92;
    public bool IsIconPack { get; private set; } = false;

    public ExportDialog(double width, double height)
    {
        InitializeComponent();
        _origWidth = width;
        _origHeight = height;
        UpdateDimensions();
    }

    private void ExportMode_Changed(object sender, RoutedEventArgs e)
    {
        if (StandardExportPanel == null || IconPackPanel == null) return;
        IsIconPack = RbIconPackExport?.IsChecked == true;
        StandardExportPanel.Visibility = IsIconPack ? Visibility.Collapsed : Visibility.Visible;
        IconPackPanel.Visibility = IsIconPack ? Visibility.Visible : Visibility.Collapsed;
        BtnExport.Content = IsIconPack ? "Generate Icon Pack" : "Export Image";
    }

    private void Format_Changed(object sender, RoutedEventArgs e)
    {
        if (QualityPanel == null) return;
        if (RbJpg?.IsChecked == true)
        {
            Format = "JPEG";
            QualityPanel.Visibility = Visibility.Visible;
        }
        else if (RbBmp?.IsChecked == true)
        {
            Format = "BMP";
            QualityPanel.Visibility = Visibility.Collapsed;
        }
        else if (RbTiff?.IsChecked == true)
        {
            Format = "TIFF";
            QualityPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            Format = "PNG";
            QualityPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void Scale_Changed(object sender, RoutedEventArgs e)
    {
        if (RbScaleHalf?.IsChecked == true) Scale = 0.5;
        else if (RbScale2?.IsChecked == true) Scale = 2.0;
        else if (RbScale3?.IsChecked == true) Scale = 3.0;
        else Scale = 1.0;
        UpdateDimensions();
    }

    private void SliderQuality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Quality = (int)e.NewValue;
        if (TxtQualityVal != null) TxtQualityVal.Text = $"{Quality}%";
    }

    private void UpdateDimensions()
    {
        if (TxtDimensions != null)
        {
            int w = (int)Math.Round(_origWidth * Scale);
            int h = (int)Math.Round(_origHeight * Scale);
            TxtDimensions.Text = $"{w} × {h} px";
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
