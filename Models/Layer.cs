using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenImage.Models;

public class Layer : INotifyPropertyChanged
{
    private string _name;
    private bool _isVisible = true;
    private bool _locked = false;
    private double _opacity = 1.0;
    private LayerBlendMode _blendMode = LayerBlendMode.Normal;
    private double _x = 0;
    private double _y = 0;
    private double _width;
    private double _height;
    private double _rotation = 0;
    private WriteableBitmap? _bitmap;
    private ImageSource? _thumbnail;

    // Text properties
    private string _textContent = "Text Layer";
    private double _fontSize = 42;
    private Color _textColor = Colors.Black;

    // Shape properties
    private ShapeType _shapeType = ShapeType.Rectangle;
    private Color _fillColor = Color.FromArgb(200, 0, 122, 255);
    private Color _strokeColor = Color.FromArgb(255, 0, 86, 179);
    private double _strokeWidth = 2;
    private double _cornerRadius = 12;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public LayerType Type { get; set; } = LayerType.Raster;

    public Layer(string name, double width, double height, LayerType type = LayerType.Raster)
    {
        _name = name;
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        Type = type;

        if (type == LayerType.Raster)
        {
            _bitmap = new WriteableBitmap((int)_width, (int)_height, 96, 96, PixelFormats.Bgra32, null);
        }
        UpdateThumbnail();
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set { _isVisible = value; OnPropertyChanged(); }
    }

    public bool IsLocked
    {
        get => _locked;
        set { _locked = value; OnPropertyChanged(); }
    }

    public double Opacity
    {
        get => _opacity;
        set { _opacity = Math.Clamp(value, 0.0, 1.0); OnPropertyChanged(); }
    }

    public LayerBlendMode BlendMode
    {
        get => _blendMode;
        set { _blendMode = value; OnPropertyChanged(); }
    }

    public double X
    {
        get => _x;
        set { _x = value; OnPropertyChanged(); }
    }

    public double Y
    {
        get => _y;
        set { _y = value; OnPropertyChanged(); }
    }

    public double Width
    {
        get => _width;
        set { _width = Math.Max(1, value); OnPropertyChanged(); }
    }

    public double Height
    {
        get => _height;
        set { _height = Math.Max(1, value); OnPropertyChanged(); }
    }

    public double Rotation
    {
        get => _rotation;
        set { _rotation = value; OnPropertyChanged(); }
    }

    public WriteableBitmap? Bitmap
    {
        get => _bitmap;
        set { _bitmap = value; OnPropertyChanged(); UpdateThumbnail(); }
    }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        private set { _thumbnail = value; OnPropertyChanged(); }
    }

    public string TextContent
    {
        get => _textContent;
        set { _textContent = value; OnPropertyChanged(); UpdateThumbnail(); }
    }

    public double FontSize
    {
        get => _fontSize;
        set { _fontSize = value; OnPropertyChanged(); }
    }

    public Color TextColor
    {
        get => _textColor;
        set { _textColor = value; OnPropertyChanged(); UpdateThumbnail(); }
    }

    public ShapeType ShapeType
    {
        get => _shapeType;
        set { _shapeType = value; OnPropertyChanged(); UpdateThumbnail(); }
    }

    public Color FillColor
    {
        get => _fillColor;
        set { _fillColor = value; OnPropertyChanged(); UpdateThumbnail(); }
    }

    public Color StrokeColor
    {
        get => _strokeColor;
        set { _strokeColor = value; OnPropertyChanged(); UpdateThumbnail(); }
    }

    public double StrokeWidth
    {
        get => _strokeWidth;
        set { _strokeWidth = value; OnPropertyChanged(); UpdateThumbnail(); }
    }

    public double CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = value; OnPropertyChanged(); }
    }

    public void UpdateThumbnail()
    {
        try
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                // Background
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 48, 48));

                if (Type == LayerType.Raster && _bitmap != null)
                {
                    dc.DrawImage(_bitmap, new Rect(0, 0, 48, 48));
                }
                else if (Type == LayerType.Text)
                {
                    var ft = new FormattedText(
                        "T",
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("-apple-system, Segoe UI, sans-serif"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                        28,
                        new SolidColorBrush(_textColor),
                        96
                    );
                    dc.DrawText(ft, new Point(16, 6));
                }
                else if (Type == LayerType.Shape)
                {
                    var fill = new SolidColorBrush(_fillColor);
                    var stroke = new Pen(new SolidColorBrush(_strokeColor), 1.5);
                    if (_shapeType == ShapeType.Ellipse)
                        dc.DrawEllipse(fill, stroke, new Point(24, 24), 16, 16);
                    else
                        dc.DrawRoundedRectangle(fill, stroke, new Rect(8, 8, 32, 32), 6, 6);
                }
            }

            var rtb = new RenderTargetBitmap(48, 48, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            Thumbnail = rtb;
        }
        catch
        {
            // Ignore thumbnail failure during bulk initialization
        }
    }

    public Layer Clone()
    {
        var clone = new Layer($"{Name} Copy", Width, Height, Type)
        {
            IsVisible = this.IsVisible,
            IsLocked = this.IsLocked,
            Opacity = this.Opacity,
            BlendMode = this.BlendMode,
            X = this.X,
            Y = this.Y,
            Rotation = this.Rotation,
            TextContent = this.TextContent,
            FontSize = this.FontSize,
            TextColor = this.TextColor,
            ShapeType = this.ShapeType,
            FillColor = this.FillColor,
            StrokeColor = this.StrokeColor,
            StrokeWidth = this.StrokeWidth,
            CornerRadius = this.CornerRadius
        };

        if (this.Bitmap != null)
        {
            var wb = new WriteableBitmap(this.Bitmap.PixelWidth, this.Bitmap.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
            int stride = this.Bitmap.PixelWidth * 4;
            int size = stride * this.Bitmap.PixelHeight;
            byte[] pixels = new byte[size];
            this.Bitmap.CopyPixels(pixels, stride, 0);
            wb.WritePixels(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight), pixels, stride, 0);
            clone.Bitmap = wb;
        }

        clone.UpdateThumbnail();
        return clone;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
