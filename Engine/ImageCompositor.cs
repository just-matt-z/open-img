using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenImage.Models;

namespace OpenImage.Engine;

public static class ImageCompositor
{
    /// <summary>
    /// Composites all visible layers into a single high-performance RenderTargetBitmap/WriteableBitmap,
    /// supporting industry-standard mathematical blend modes (Multiply, Screen, Overlay, etc.)
    /// </summary>
    public static WriteableBitmap RenderComposite(double width, double height, IEnumerable<Layer> layers, Adjustments? adjustments = null, bool includeCheckerboard = false)
    {
        int pixelWidth = Math.Max(1, (int)Math.Round(width));
        int pixelHeight = Math.Max(1, (int)Math.Round(height));

        var visibleLayers = layers.Where(l => l.IsVisible && l.Opacity > 0).ToList();

        // Check if any layer uses a non-normal blend mode
        bool hasAdvancedBlendModes = visibleLayers.Any(l => l.BlendMode != LayerBlendMode.Normal);

        WriteableBitmap compositeWb;

        if (!hasAdvancedBlendModes)
        {
            // Fast single-pass DirectX visual render
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                if (includeCheckerboard)
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pixelWidth, pixelHeight));
                }

                foreach (var layer in visibleLayers)
                {
                    RenderLayerToContext(dc, layer);
                }
            }

            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            compositeWb = new WriteableBitmap(rtb);
        }
        else
        {
            // Multi-pass mathematical pixel compositing
            compositeWb = new WriteableBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Bgra32, null);

            if (includeCheckerboard)
            {
                // Fill base with white
                FillSolidColor(compositeWb, 255, 255, 255, 255);
            }

            foreach (var layer in visibleLayers)
            {
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    RenderLayerToContext(dc, layer);
                }

                var layerRtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
                layerRtb.Render(dv);

                var layerWb = new WriteableBitmap(layerRtb);
                BlendBuffers(compositeWb, layerWb, layer.BlendMode);
            }
        }

        // Apply Adjustments (Brightness, Contrast, Saturation, Invert, Grayscale)
        if (adjustments != null && adjustments.IsActive)
        {
            ApplyAdjustments(compositeWb, adjustments);
        }

        return compositeWb;
    }

    private static void RenderLayerToContext(DrawingContext dc, Layer layer)
    {
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

    /// <summary>
    /// Fills buffer with a solid color
    /// </summary>
    private static unsafe void FillSolidColor(WriteableBitmap bmp, byte r, byte g, byte b, byte a)
    {
        bmp.Lock();
        try
        {
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            int stride = bmp.BackBufferStride;
            byte* buffer = (byte*)bmp.BackBuffer.ToPointer();

            for (int y = 0; y < height; y++)
            {
                byte* row = buffer + (y * stride);
                for (int x = 0; x < width; x++)
                {
                    byte* p = row + (x * 4);
                    p[0] = b;
                    p[1] = g;
                    p[2] = r;
                    p[3] = a;
                }
            }
            bmp.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            bmp.Unlock();
        }
    }

    /// <summary>
    /// Blends top buffer onto bottom buffer using mathematical blend mode formulas
    /// </summary>
    public static unsafe void BlendBuffers(WriteableBitmap dstBmp, WriteableBitmap srcBmp, LayerBlendMode mode)
    {
        dstBmp.Lock();
        srcBmp.Lock();
        try
        {
            int width = Math.Min(dstBmp.PixelWidth, srcBmp.PixelWidth);
            int height = Math.Min(dstBmp.PixelHeight, srcBmp.PixelHeight);

            int dstStride = dstBmp.BackBufferStride;
            int srcStride = srcBmp.BackBufferStride;

            byte* dstBuf = (byte*)dstBmp.BackBuffer.ToPointer();
            byte* srcBuf = (byte*)srcBmp.BackBuffer.ToPointer();

            for (int y = 0; y < height; y++)
            {
                byte* dstRow = dstBuf + (y * dstStride);
                byte* srcRow = srcBuf + (y * srcStride);

                for (int x = 0; x < width; x++)
                {
                    byte* s = srcRow + (x * 4);
                    byte* d = dstRow + (x * 4);

                    float srcA = s[3] / 255.0f;
                    if (srcA <= 0.0001f) continue; // Completely transparent source pixel

                    float dstA = d[3] / 255.0f;

                    float srcB = s[0] / 255.0f;
                    float srcG = s[1] / 255.0f;
                    float srcR = s[2] / 255.0f;

                    float dstB = d[0] / 255.0f;
                    float dstG = d[1] / 255.0f;
                    float dstR = d[2] / 255.0f;

                    if (mode == LayerBlendMode.Normal)
                    {
                        // Standard Alpha Compositing (Porter-Duff Source Over)
                        float outAlpha = srcA + dstA * (1.0f - srcA);
                        if (outAlpha > 0.0001f)
                        {
                            d[0] = (byte)Math.Clamp((int)Math.Round(((srcB * srcA) + (dstB * dstA * (1.0f - srcA))) / outAlpha * 255.0f), 0, 255);
                            d[1] = (byte)Math.Clamp((int)Math.Round(((srcG * srcA) + (dstG * dstA * (1.0f - srcA))) / outAlpha * 255.0f), 0, 255);
                            d[2] = (byte)Math.Clamp((int)Math.Round(((srcR * srcA) + (dstR * dstA * (1.0f - srcA))) / outAlpha * 255.0f), 0, 255);
                            d[3] = (byte)Math.Clamp((int)Math.Round(outAlpha * 255.0f), 0, 255);
                        }
                    }
                    else
                    {
                        // Mathematical blend mode
                        float blendR = BlendChannel(dstR, srcR, mode);
                        float blendG = BlendChannel(dstG, srcG, mode);
                        float blendB = BlendChannel(dstB, srcB, mode);

                        float outAlpha = srcA + dstA * (1.0f - srcA);
                        if (outAlpha > 0.0001f)
                        {
                            // W3C Alpha Blending: (srcA * (1 - dstA) * src + srcA * dstA * blend + (1 - srcA) * dstA * dst) / outAlpha
                            float blendedR = (srcA * (1.0f - dstA) * srcR + srcA * dstA * blendR + (1.0f - srcA) * dstA * dstR) / outAlpha;
                            float blendedG = (srcA * (1.0f - dstA) * srcG + srcA * dstA * blendG + (1.0f - srcA) * dstA * dstG) / outAlpha;
                            float blendedB = (srcA * (1.0f - dstA) * srcB + srcA * dstA * blendB + (1.0f - srcA) * dstA * dstB) / outAlpha;

                            d[0] = (byte)Math.Clamp((int)Math.Round(blendedB * 255.0f), 0, 255);
                            d[1] = (byte)Math.Clamp((int)Math.Round(blendedG * 255.0f), 0, 255);
                            d[2] = (byte)Math.Clamp((int)Math.Round(blendedR * 255.0f), 0, 255);
                            d[3] = (byte)Math.Clamp((int)Math.Round(outAlpha * 255.0f), 0, 255);
                        }
                    }
                }
            }

            dstBmp.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            srcBmp.Unlock();
            dstBmp.Unlock();
        }
    }

    private static float BlendChannel(float cb, float cs, LayerBlendMode mode)
    {
        return mode switch
        {
            LayerBlendMode.Multiply => cb * cs,
            LayerBlendMode.Screen => 1.0f - (1.0f - cb) * (1.0f - cs),
            LayerBlendMode.Overlay => cb <= 0.5f ? 2.0f * cb * cs : 1.0f - 2.0f * (1.0f - cb) * (1.0f - cs),
            LayerBlendMode.Darken => Math.Min(cb, cs),
            LayerBlendMode.Lighten => Math.Max(cb, cs),
            LayerBlendMode.ColorDodge => cs >= 1.0f ? 1.0f : Math.Min(1.0f, cb / Math.Max(0.0001f, 1.0f - cs)),
            LayerBlendMode.ColorBurn => cs <= 0.0f ? 0.0f : 1.0f - Math.Min(1.0f, (1.0f - cb) / Math.Max(0.0001f, cs)),
            LayerBlendMode.HardLight => cs <= 0.5f ? 2.0f * cb * cs : 1.0f - 2.0f * (1.0f - cb) * (1.0f - cs),
            LayerBlendMode.SoftLight => cs <= 0.5f
                ? cb - (1.0f - 2.0f * cs) * cb * (1.0f - cb)
                : cb + (2.0f * cs - 1.0f) * (((cb <= 0.25f) ? ((16.0f * cb - 12.0f) * cb + 4.0f) * cb : MathF.Sqrt(cb)) - cb),
            LayerBlendMode.Difference => Math.Abs(cb - cs),
            LayerBlendMode.Exclusion => cb + cs - 2.0f * cb * cs,
            _ => cs
        };
    }

    /// <summary>
    /// In-place high-performance color manipulation on composite buffer
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
            float warmth = (float)(adj.Warmth / 200.0f);
            float tint = (float)(adj.Tint / 200.0f);
            float vignetteStrength = (float)(adj.Vignette / 100.0f);
            bool invert = adj.Invert;
            bool gray = adj.Grayscale;
            bool sepia = adj.Sepia;

            float centerX = width / 2.0f;
            float centerY = height / 2.0f;
            float maxDistSq = centerX * centerX + centerY * centerY;

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

                    if (a == 0) continue;

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

                    // Saturation & Vibrance
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

                    // Color Temperature (Warmth)
                    if (warmth != 0)
                    {
                        r += warmth * 0.35f;
                        g += warmth * 0.10f;
                        b -= warmth * 0.35f;
                    }

                    // Tint (Green vs Magenta)
                    if (tint != 0)
                    {
                        r += tint * 0.25f;
                        g -= tint * 0.25f;
                        b += tint * 0.25f;
                    }

                    // Lens Vignette
                    if (vignetteStrength > 0.001f)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        float distSq = dx * dx + dy * dy;
                        float normDist = distSq / maxDistSq;
                        float vigFactor = 1.0f - (normDist * vignetteStrength * 0.75f);
                        r *= vigFactor;
                        g *= vigFactor;
                        b *= vigFactor;
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

    /// <summary>
    /// Applies a 3x3 spatial convolution kernel to a WriteableBitmap
    /// </summary>
    public static unsafe void ApplyConvolutionKernel(WriteableBitmap bmp, float[] kernel, float factor = 1.0f, float bias = 0.0f)
    {
        if (bmp == null || kernel == null || kernel.Length < 9) return;

        int width = bmp.PixelWidth;
        int height = bmp.PixelHeight;

        uint[] srcCopy = new uint[width * height];
        bmp.Lock();
        try
        {
            byte* scan0 = (byte*)bmp.BackBuffer;
            int stride = bmp.BackBufferStride;

            for (int y = 0; y < height; y++)
            {
                byte* row = scan0 + (y * stride);
                for (int x = 0; x < width; x++)
                {
                    srcCopy[y * width + x] = *(uint*)(row + (x * 4));
                }
            }

            for (int y = 1; y < height - 1; y++)
            {
                byte* dstRow = scan0 + (y * stride);

                for (int x = 1; x < width - 1; x++)
                {
                    float r = 0, g = 0, b = 0;
                    int k = 0;

                    for (int ky = -1; ky <= 1; ky++)
                    {
                        int py = y + ky;
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int px = x + kx;
                            uint pixel = srcCopy[py * width + px];
                            float kw = kernel[k++];

                            r += ((pixel >> 16) & 0xFF) * kw;
                            g += ((pixel >> 8) & 0xFF) * kw;
                            b += (pixel & 0xFF) * kw;
                        }
                    }

                    byte* dst = dstRow + (x * 4);
                    dst[0] = (byte)Math.Clamp((int)(b * factor + bias), 0, 255);
                    dst[1] = (byte)Math.Clamp((int)(g * factor + bias), 0, 255);
                    dst[2] = (byte)Math.Clamp((int)(r * factor + bias), 0, 255);
                }
            }

            bmp.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            bmp.Unlock();
        }
    }

    /// <summary>
    /// Applies high-pass unsharp filter to crisp edges
    /// </summary>
    public static void ApplySharpen(WriteableBitmap bmp)
    {
        float[] kernel = {
             0, -1,  0,
            -1,  5, -1,
             0, -1,  0
        };
        ApplyConvolutionKernel(bmp, kernel, 1.0f, 0.0f);
    }

    /// <summary>
    /// Applies edge detection filter
    /// </summary>
    public static void ApplyEdgeDetect(WriteableBitmap bmp)
    {
        float[] kernel = {
            -1, -1, -1,
            -1,  8, -1,
            -1, -1, -1
        };
        ApplyConvolutionKernel(bmp, kernel, 1.0f, 0.0f);
    }

    /// <summary>
    /// Applies relief emboss filter
    /// </summary>
    public static void ApplyEmboss(WriteableBitmap bmp)
    {
        float[] kernel = {
            -2, -1,  0,
            -1,  1,  1,
             0,  1,  2
        };
        ApplyConvolutionKernel(bmp, kernel, 1.0f, 128.0f);
    }

    /// <summary>
    /// Fast 2-pass separable box Gaussian blur
    /// </summary>
    public static unsafe void ApplyGaussianBlur(WriteableBitmap bmp, int radius = 3)
    {
        if (bmp == null || radius < 1) return;
        int width = bmp.PixelWidth;
        int height = bmp.PixelHeight;

        uint[] src = new uint[width * height];
        uint[] intermediate = new uint[width * height];

        bmp.Lock();
        try
        {
            byte* scan0 = (byte*)bmp.BackBuffer;
            int stride = bmp.BackBufferStride;

            for (int y = 0; y < height; y++)
            {
                byte* row = scan0 + (y * stride);
                for (int x = 0; x < width; x++)
                {
                    src[y * width + x] = *(uint*)(row + (x * 4));
                }
            }

            int kernelSize = radius * 2 + 1;
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int px = Math.Clamp(x + k, 0, width - 1);
                        uint pixel = src[rowOffset + px];
                        a += (int)((pixel >> 24) & 0xFF);
                        r += (int)((pixel >> 16) & 0xFF);
                        g += (int)((pixel >> 8) & 0xFF);
                        b += (int)(pixel & 0xFF);
                    }
                    intermediate[rowOffset + x] = ((uint)(a / kernelSize) << 24) |
                                                  ((uint)(r / kernelSize) << 16) |
                                                  ((uint)(g / kernelSize) << 8) |
                                                  (uint)(b / kernelSize);
                }
            }

            for (int y = 0; y < height; y++)
            {
                byte* row = scan0 + (y * stride);
                for (int x = 0; x < width; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int py = Math.Clamp(y + k, 0, height - 1);
                        uint pixel = intermediate[py * width + x];
                        a += (int)((pixel >> 24) & 0xFF);
                        r += (int)((pixel >> 16) & 0xFF);
                        g += (int)((pixel >> 8) & 0xFF);
                        b += (int)(pixel & 0xFF);
                    }
                    byte* p = row + (x * 4);
                    p[0] = (byte)(b / kernelSize);
                    p[1] = (byte)(g / kernelSize);
                    p[2] = (byte)(r / kernelSize);
                    p[3] = (byte)(a / kernelSize);
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
