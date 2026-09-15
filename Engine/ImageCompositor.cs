using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenImage.Models;

namespace OpenImage.Engine;

public static class ImageCompositor
{
    /// <summary>
    /// Composites all visible layers into a single high-performance RenderTargetBitmap/WriteableBitmap
    /// </summary>
    public static WriteableBitmap RenderComposite(double width, double height, IEnumerable<Layer> layers, Adjustments? adjustments = null, bool includeCheckerboard = false)
    {
        int pixelWidth = Math.Max(1, (int)Math.Round(width));
        int pixelHeight = Math.Max(1, (int)Math.Round(height));

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            // Background fill
            if (includeCheckerboard)
            {
                // Draw white background
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pixelWidth, pixelHeight));
            }

            // Render layers bottom-to-top
            foreach (var layer in layers)
            {
                if (!layer.IsVisible || layer.Opacity <= 0) continue;

                dc.PushOpacity(layer.Opacity);

                var transformGroup = new TransformGroup();
                if (layer.Rotation != 0)
                {
                    double centerX = layer.X + layer.Width / 2;
                    double centerY = layer.Y + layer.Height / 2;
                    transformGroup.Children.Add(new RotateTransform(layer.Rotation, centerX, centerY));
                }
                transformGroup.Children.Add(new TranslateTransform(layer.X, layer.Y));
                dc.PushTransform(transformGroup);

                if (layer.Type == LayerType.Raster && layer.Bitmap != null)
                {
                    dc.DrawImage(layer.Bitmap, new Rect(0, 0, layer.Width, layer.Height));
                }
                else if (layer.Type == LayerType.Text)
                {
                    var ft = new FormattedText(
                        layer.TextContent,
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("-apple-system, Segoe UI, sans-serif"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                        Math.Max(8, layer.FontSize),
                        new SolidColorBrush(layer.TextColor),
                        96
                    );
                    dc.DrawText(ft, new Point(0, 0));
                }
                else if (layer.Type == LayerType.Shape)
                {
                    var fillBrush = new SolidColorBrush(layer.FillColor);
                    var strokePen = layer.StrokeWidth > 0 ? new Pen(new SolidColorBrush(layer.StrokeColor), layer.StrokeWidth) : null;

                    if (layer.ShapeType == ShapeType.Ellipse)
                    {
                        dc.DrawEllipse(fillBrush, strokePen, new Point(layer.Width / 2, layer.Height / 2), layer.Width / 2, layer.Height / 2);
                    }
                    else if (layer.ShapeType == ShapeType.Line)
                    {
                        dc.DrawLine(strokePen ?? new Pen(fillBrush, 2), new Point(0, 0), new Point(layer.Width, layer.Height));
                    }
                    else
                    {
                        dc.DrawRoundedRectangle(fillBrush, strokePen, new Rect(0, 0, layer.Width, layer.Height), layer.CornerRadius, layer.CornerRadius);
                    }
                }

                dc.Pop(); // Transform
                dc.Pop(); // Opacity
            }
        }

        var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);

        var compositeWb = new WriteableBitmap(rtb);

        // Apply Adjustments (Brightness, Contrast, Saturation, Invert, Grayscale)
        if (adjustments != null && adjustments.IsActive)
        {
            ApplyAdjustments(compositeWb, adjustments);
        }

        return compositeWb;
    }

    /// <summary>
    /// In-place high-performance GPU-style color manipulation on composite buffer
    /// </summary>
    public static unsafe void ApplyAdjustments(WriteableBitmap bmp, Adjustments adj)
    {
        bmp.Lock();
        try
        {
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            int stride = bmp.BackBufferStride;
            byte* buffer = (byte*)bmp.BackBuffer.ToPointer();

            float bright = (float)(adj.Brightness / 100.0f);
            float contrast = (float)Math.Pow((100.0f + adj.Contrast) / 100.0f, 2);
            float sat = (float)((100.0f + adj.Saturation + adj.Vibrance * 0.75f) / 100.0f);
            bool invert = adj.Invert;
            bool gray = adj.Grayscale;
            bool sepia = adj.Sepia;

            for (int y = 0; y < height; y++)
            {
                byte* row = buffer + (y * stride);

                for (int x = 0; x < width; x++)
                {
                    byte* p = row + (x * 4);
                    float b = p[0] / 255.0f;
                    float g = p[1] / 255.0f;
                    float r = p[2] / 255.0f;
                    byte a = p[3];

                    if (a == 0) continue; // Skip fully transparent pixels

                    // Brightness
                    if (bright != 0)
                    {
                        r += bright;
                        g += bright;
                        b += bright;
                    }

                    // Contrast
                    if (contrast != 1.0f)
                    {
                        r = (r - 0.5f) * contrast + 0.5f;
                        g = (g - 0.5f) * contrast + 0.5f;
                        b = (b - 0.5f) * contrast + 0.5f;
                    }

                    // Saturation
                    if (sat != 1.0f || gray)
                    {
                        float luminance = 0.299f * r + 0.587f * g + 0.114f * b;
                        if (gray)
                        {
                            r = luminance;
                            g = luminance;
                            b = luminance;
                        }
                        else
                        {
                            r = luminance + (r - luminance) * sat;
                            g = luminance + (g - luminance) * sat;
                            b = luminance + (b - luminance) * sat;
                        }
                    }

                    // Invert
                    if (invert)
                    {
                        r = 1.0f - r;
                        g = 1.0f - g;
                        b = 1.0f - b;
                    }

                    // Sepia
                    if (sepia)
                    {
                        float tr = 0.393f * r + 0.769f * g + 0.189f * b;
                        float tg = 0.349f * r + 0.686f * g + 0.168f * b;
                        float tb = 0.272f * r + 0.534f * g + 0.131f * b;
                        r = tr;
                        g = tg;
                        b = tb;
                    }

                    p[0] = (byte)Math.Clamp((int)(b * 255), 0, 255);
                    p[1] = (byte)Math.Clamp((int)(g * 255), 0, 255);
                    p[2] = (byte)Math.Clamp((int)(r * 255), 0, 255);
                }
            }

            bmp.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            bmp.Unlock();
        }
    }
}
