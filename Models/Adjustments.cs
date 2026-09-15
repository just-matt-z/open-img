using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenImage.Models;

public class Adjustments : INotifyPropertyChanged
{
    private double _brightness = 0;   // -100 to 100
    private double _contrast = 0;     // -100 to 100
    private double _exposure = 0;     // -100 to 100
    private double _saturation = 0;   // -100 to 100
    private double _vibrance = 0;     // -100 to 100
    private bool _invert = false;
    private bool _grayscale = false;
    private bool _sepia = false;

    public double Brightness
    {
        get => _brightness;
        set { _brightness = value; OnPropertyChanged(); }
    }

    public double Contrast
    {
        get => _contrast;
        set { _contrast = value; OnPropertyChanged(); }
    }

    public double Exposure
    {
        get => _exposure;
        set { _exposure = value; OnPropertyChanged(); }
    }

    public double Saturation
    {
        get => _saturation;
        set { _saturation = value; OnPropertyChanged(); }
    }

    public double Vibrance
    {
        get => _vibrance;
        set { _vibrance = value; OnPropertyChanged(); }
    }

    public bool Invert
    {
        get => _invert;
        set { _invert = value; OnPropertyChanged(); }
    }

    public bool Grayscale
    {
        get => _grayscale;
        set { _grayscale = value; OnPropertyChanged(); }
    }

    public bool Sepia
    {
        get => _sepia;
        set { _sepia = value; OnPropertyChanged(); }
    }

    public bool IsActive => _brightness != 0 || _contrast != 0 || _exposure != 0 || 
                            _saturation != 0 || _vibrance != 0 || _invert || _grayscale || _sepia;

    public void Reset()
    {
        Brightness = 0;
        Contrast = 0;
        Exposure = 0;
        Saturation = 0;
        Vibrance = 0;
        Invert = false;
        Grayscale = false;
        Sepia = false;
    }

    public Adjustments Clone()
    {
        return new Adjustments
        {
            Brightness = this.Brightness,
            Contrast = this.Contrast,
            Exposure = this.Exposure,
            Saturation = this.Saturation,
            Vibrance = this.Vibrance,
            Invert = this.Invert,
            Grayscale = this.Grayscale,
            Sepia = this.Sepia
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
