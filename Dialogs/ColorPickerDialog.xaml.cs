using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenImage.Dialogs;

public partial class ColorPickerDialog : Window
{
    private Color _initialColor;
    private Color _selectedColor;
    private bool _isUpdating = false;

    public Color SelectedColor => _selectedColor;

    public ColorPickerDialog(Color initialColor)
    {
        InitializeComponent();
        _initialColor = initialColor;
        _selectedColor = initialColor;

        InitialColorTile.Background = new SolidColorBrush(_initialColor);

        SetColor(initialColor);
    }

    private void SetColor(Color c)
    {
        _isUpdating = true;
        _selectedColor = c;

        CurrentColorTile.Background = new SolidColorBrush(c);

        SliderR.Value = c.R;
        SliderG.Value = c.G;
        SliderB.Value = c.B;

        TxtValR.Text = c.R.ToString();
        TxtValG.Text = c.G.ToString();
        TxtValB.Text = c.B.ToString();

        TxtHex.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        _isUpdating = false;
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;

        byte r = (byte)(SliderR?.Value ?? 0);
        byte g = (byte)(SliderG?.Value ?? 0);
        byte b = (byte)(SliderB?.Value ?? 0);

        _selectedColor = Color.FromRgb(r, g, b);
        CurrentColorTile.Background = new SolidColorBrush(_selectedColor);

        if (TxtValR != null) TxtValR.Text = r.ToString();
        if (TxtValG != null) TxtValG.Text = g.ToString();
        if (TxtValB != null) TxtValB.Text = b.ToString();

        if (TxtHex != null)
        {
            _isUpdating = true;
            TxtHex.Text = $"#{r:X2}{g:X2}{b:X2}";
            _isUpdating = false;
        }
    }

    private void TxtHex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;

        string hex = TxtHex.Text.Trim();
        if (hex.StartsWith("#")) hex = hex[1..];

        if (hex.Length == 6)
        {
            try
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);

                _isUpdating = true;
                _selectedColor = Color.FromRgb(r, g, b);
                CurrentColorTile.Background = new SolidColorBrush(_selectedColor);

                SliderR.Value = r;
                SliderG.Value = g;
                SliderB.Value = b;
                TxtValR.Text = r.ToString();
                TxtValG.Text = g.ToString();
                TxtValB.Text = b.ToString();
                _isUpdating = false;
            }
            catch
            {
                // Ignore incomplete hex
            }
        }
    }

    private void PaletteChip_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string hex)
        {
            try
            {
                var colorObj = (Color)ColorConverter.ConvertFromString(hex);
                SetColor(colorObj);
            }
            catch
            {
                // ignore
            }
        }
    }

    private void InitialColor_Click(object sender, MouseButtonEventArgs e)
    {
        SetColor(_initialColor);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
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
