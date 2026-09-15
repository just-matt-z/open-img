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
    private WriteableBitmap? _maskBitmap;
    private ImageSource? _maskThumbnail;
    private bool _isMaskEnabled = true;
    private bool _isEditingMask = false;

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

    public WriteableBitmap? MaskBitmap
    {
        get => _maskBitmap;
        set
        {
            _maskBitmap = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasMask));
            UpdateMaskThumbnail();
        }
    }

    public ImageSource? MaskThumbnail
    {
        get => _maskThumbnail;
        private set { _maskThumbnail = value; OnPropertyChanged(); }
    }

    public bool HasMask => _maskBitmap != null;

    public bool IsMaskEnabled
    {
        get => _isMaskEnabled;
        set { _isMaskEnabled = value; OnPropertyChanged(); }
    }

    public bool IsEditingMask
    {
        get => _isEditingMask;
        set { _isEditingMask = value; OnPropertyChanged(); }
    }

    public void AddMask(bool revealAll = true)
    {
        int w = (int)Math.Max(1, Width);
        int h = (int)Math.Max(1, Height);
        if (_bitmap != null)
        {
            w = _bitmap.PixelWidth;
            h = _bitmap.PixelHeight;
        }

        var mask = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        byte initVal = (byte)(revealAll ? 255 : 0);
        mask.Lock();
        try
        {
            unsafe
            {
                byte* scan0 = (byte*)mask.BackBuffer;
                int stride = mask.BackBufferStride;
                for (int y = 0; y < h; y++)
                {
                    byte* row = scan0 + (y * stride);
                    for (int x = 0; x < w; x++)
                    {
                        byte* p = row + (x * 4);
                        p[0] = initVal;
                        p[1] = initVal;
                        p[2] = initVal;
                        p[3] = 255;
                    }
                }
            }
            mask.AddDirtyRect(new Int32Rect(0, 0, w, h));
        }
        finally
        {
            mask.Unlock();
        }

        MaskBitmap = mask;
        IsMaskEnabled = true;
        IsEditingMask = true;
        UpdateMaskThumbnail();
    }

    public void RemoveMask(bool apply)
    {
        if (apply && _bitmap != null && _maskBitmap != null)
        {
            ApplyMaskToBitmap();
        }
        MaskBitmap = null;
        IsEditingMask = false;
        UpdateThumbnail();
    }

    public unsafe void ApplyMaskToBitmap()
    {
        if (_bitmap == null || _maskBitmap == null) return;
        _bitmap.Lock();
        _maskBitmap.Lock();
        try
        {
            int w = Math.Min(_bitmap.PixelWidth, _maskBitmap.PixelWidth);
            int h = Math.Min(_bitmap.PixelHeight, _maskBitmap.PixelHeight);
            int bStride = _bitmap.BackBufferStride;
            int mStride = _maskBitmap.BackBufferStride;
            byte* bScan = (byte*)_bitmap.BackBuffer;
            byte* mScan = (byte*)_maskBitmap.BackBuffer;

            for (int y = 0; y < h; y++)
            {
                byte* bRow = bScan + (y * bStride);
                byte* mRow = mScan + (y * mStride);
                for (int x = 0; x < w; x++)
                {
                    byte* bp = bRow + (x * 4);
                    byte* mp = mRow + (x * 4);
                    float maskFactor = (0.299f * mp[2] + 0.587f * mp[1] + 0.114f * mp[0]) / 255.0f;
                    bp[3] = (byte)Math.Clamp((int)(bp[3] * maskFactor), 0, 255);
                }
            }
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, w, h));
        }
        finally
        {
            _maskBitmap.Unlock();
            _bitmap.Unlock();
        }
    }

    public void UpdateMaskThumbnail()
    {
        if (_maskBitmap == null)
        {
            MaskThumbnail = null;
            return;
        }

        try
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 48, 48));
                dc.DrawImage(_maskBitmap, new Rect(0, 0, 48, 48));
            }
            var rtb = new RenderTargetBitmap(48, 48, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            MaskThumbnail = rtb;
        }
        catch
        {
        }
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

        if (this.MaskBitmap != null)
        {
            var mwb = new WriteableBitmap(this.MaskBitmap.PixelWidth, this.MaskBitmap.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
            int stride = this.MaskBitmap.PixelWidth * 4;
            int size = stride * this.MaskBitmap.PixelHeight;
            byte[] pixels = new byte[size];
            this.MaskBitmap.CopyPixels(pixels, stride, 0);
            mwb.WritePixels(new Int32Rect(0, 0, mwb.PixelWidth, mwb.PixelHeight), pixels, stride, 0);
            clone.MaskBitmap = mwb;
            clone.IsMaskEnabled = this.IsMaskEnabled;
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
