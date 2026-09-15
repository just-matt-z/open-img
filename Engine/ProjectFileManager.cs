using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenImage.Models;

namespace OpenImage.Engine;

public class ProjectManifest
{
    public string FormatVersion { get; set; } = "1.0";
    public double CanvasWidth { get; set; }
    public double CanvasHeight { get; set; }
    public string DocumentName { get; set; } = "Untitled.openimg";
    public AdjustmentsData Adjustments { get; set; } = new();
    public List<LayerData> Layers { get; set; } = new();
}

public class AdjustmentsData
{
    public double Brightness { get; set; }
    public double Contrast { get; set; }
    public double Exposure { get; set; }
    public double Saturation { get; set; }
    public double Vibrance { get; set; }
    public double Warmth { get; set; }
    public double Tint { get; set; }
    public double Vignette { get; set; }
    public bool Grayscale { get; set; }
    public bool Invert { get; set; }
    public bool Sepia { get; set; }
}

public class LayerData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Raster";
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    public double Opacity { get; set; } = 1.0;
    public string BlendMode { get; set; } = "Normal";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }

    // Text
    public string TextContent { get; set; } = "";
    public double FontSize { get; set; } = 36;
    public string TextColorHex { get; set; } = "#FFFFFFFF";

    // Shape
    public string ShapeType { get; set; } = "Rectangle";
    public string FillColorHex { get; set; } = "#FF007AFF";
    public string StrokeColorHex { get; set; } = "#FF0056B3";
    public double StrokeWidth { get; set; } = 2;
    public double CornerRadius { get; set; } = 12;

    public string? BitmapEntry { get; set; }
    public bool HasMask { get; set; } = false;
    public bool IsMaskEnabled { get; set; } = true;
    public string? MaskEntry { get; set; }
}

public static class ProjectFileManager
{
    public static void SaveProject(string filePath, double width, double height, IEnumerable<Layer> layers, Adjustments adj, string docName)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(filePath)) File.Delete(filePath);

        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

        var manifest = new ProjectManifest
        {
            CanvasWidth = width,
            CanvasHeight = height,
            DocumentName = docName,
            Adjustments = new AdjustmentsData
            {
                Brightness = adj.Brightness,
                Contrast = adj.Contrast,
                Exposure = adj.Exposure,
                Saturation = adj.Saturation,
                Vibrance = adj.Vibrance,
                Warmth = adj.Warmth,
                Tint = adj.Tint,
                Vignette = adj.Vignette,
                Grayscale = adj.Grayscale,
                Invert = adj.Invert,
                Sepia = adj.Sepia
            }
        };

        foreach (var layer in layers)
        {
            var ld = new LayerData
            {
                Id = layer.Id,
                Name = layer.Name,
                Type = layer.Type.ToString(),
                IsVisible = layer.IsVisible,
                IsLocked = layer.IsLocked,
                Opacity = layer.Opacity,
                BlendMode = layer.BlendMode.ToString(),
                X = layer.X,
                Y = layer.Y,
                Width = layer.Width,
                Height = layer.Height,
                Rotation = layer.Rotation,
                TextContent = layer.TextContent,
                FontSize = layer.FontSize,
                TextColorHex = $"#{layer.TextColor.A:X2}{layer.TextColor.R:X2}{layer.TextColor.G:X2}{layer.TextColor.B:X2}",
                ShapeType = layer.ShapeType.ToString(),
                FillColorHex = $"#{layer.FillColor.A:X2}{layer.FillColor.R:X2}{layer.FillColor.G:X2}{layer.FillColor.B:X2}",
                StrokeColorHex = $"#{layer.StrokeColor.A:X2}{layer.StrokeColor.R:X2}{layer.StrokeColor.G:X2}{layer.StrokeColor.B:X2}",
                StrokeWidth = layer.StrokeWidth,
                CornerRadius = layer.CornerRadius,
                HasMask = layer.HasMask,
                IsMaskEnabled = layer.IsMaskEnabled
            };

            if (layer.Type == LayerType.Raster && layer.Bitmap != null)
            {
                string entryName = $"layers/layer_{layer.Id}.png";
                ld.BitmapEntry = entryName;
                var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(layer.Bitmap));
                encoder.Save(entryStream);
            }

            if (layer.HasMask && layer.MaskBitmap != null)
            {
                string maskEntryName = $"masks/mask_{layer.Id}.png";
                ld.MaskEntry = maskEntryName;
                var maskEntry = archive.CreateEntry(maskEntryName, CompressionLevel.Fastest);
                using var maskStream = maskEntry.Open();
                var maskEncoder = new PngBitmapEncoder();
                maskEncoder.Frames.Add(BitmapFrame.Create(layer.MaskBitmap));
                maskEncoder.Save(maskStream);
            }

            manifest.Layers.Add(ld);
        }

        var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Fastest);
        using (var manifestStream = manifestEntry.Open())
        {
            JsonSerializer.Serialize(manifestStream, manifest, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public static (double width, double height, List<Layer> layers, Adjustments adjustments, string docName) LoadProject(string filePath)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("manifest.json");
        if (manifestEntry == null) throw new InvalidOperationException("Invalid .openimg file: manifest.json not found.");

        ProjectManifest? manifest;
        using (var manifestStream = manifestEntry.Open())
        {
            manifest = JsonSerializer.Deserialize<ProjectManifest>(manifestStream);
        }

        if (manifest == null) throw new InvalidOperationException("Failed to deserialize manifest.json");

        var adj = new Adjustments
        {
            Brightness = manifest.Adjustments.Brightness,
            Contrast = manifest.Adjustments.Contrast,
            Exposure = manifest.Adjustments.Exposure,
            Saturation = manifest.Adjustments.Saturation,
            Vibrance = manifest.Adjustments.Vibrance,
            Warmth = manifest.Adjustments.Warmth,
            Tint = manifest.Adjustments.Tint,
            Vignette = manifest.Adjustments.Vignette,
            Grayscale = manifest.Adjustments.Grayscale,
            Invert = manifest.Adjustments.Invert,
            Sepia = manifest.Adjustments.Sepia
        };

        var layers = new List<Layer>();

        foreach (var ld in manifest.Layers)
        {
            LayerType lt = Enum.TryParse(ld.Type, out LayerType parsedType) ? parsedType : LayerType.Raster;
            var layer = new Layer(ld.Name, ld.Width, ld.Height, lt)
            {
                Id = ld.Id,
                IsVisible = ld.IsVisible,
                IsLocked = ld.IsLocked,
                Opacity = ld.Opacity,
                BlendMode = Enum.TryParse(ld.BlendMode, out LayerBlendMode bm) ? bm : LayerBlendMode.Normal,
                X = ld.X,
                Y = ld.Y,
                Rotation = ld.Rotation,
                TextContent = ld.TextContent,
                FontSize = ld.FontSize,
                ShapeType = Enum.TryParse(ld.ShapeType, out ShapeType st) ? st : ShapeType.Rectangle,
                StrokeWidth = ld.StrokeWidth,
                CornerRadius = ld.CornerRadius,
                IsMaskEnabled = ld.IsMaskEnabled
            };

            try { layer.TextColor = (Color)ColorConverter.ConvertFromString(ld.TextColorHex); } catch { }
            try { layer.FillColor = (Color)ColorConverter.ConvertFromString(ld.FillColorHex); } catch { }
            try { layer.StrokeColor = (Color)ColorConverter.ConvertFromString(ld.StrokeColorHex); } catch { }

            if (lt == LayerType.Raster && !string.IsNullOrEmpty(ld.BitmapEntry))
            {
                var bmpEntry = archive.GetEntry(ld.BitmapEntry);
                if (bmpEntry != null)
                {
                    using var bmpStream = bmpEntry.Open();
                    using var ms = new MemoryStream();
                    bmpStream.CopyTo(ms);
                    ms.Position = 0;

                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.StreamSource = ms;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    bi.Freeze();

                    layer.Bitmap = new WriteableBitmap(bi);
                }
            }

            if (ld.HasMask && !string.IsNullOrEmpty(ld.MaskEntry))
            {
                var maskEntry = archive.GetEntry(ld.MaskEntry);
                if (maskEntry != null)
                {
                    using var maskStream = maskEntry.Open();
                    using var msMask = new MemoryStream();
                    maskStream.CopyTo(msMask);
                    msMask.Position = 0;

                    var biMask = new BitmapImage();
                    biMask.BeginInit();
                    biMask.StreamSource = msMask;
                    biMask.CacheOption = BitmapCacheOption.OnLoad;
                    biMask.EndInit();
                    biMask.Freeze();

                    layer.MaskBitmap = new WriteableBitmap(biMask);
                    layer.UpdateMaskThumbnail();
                }
            }

            layer.UpdateThumbnail();
            layers.Add(layer);
        }

        return (manifest.CanvasWidth, manifest.CanvasHeight, layers, adj, manifest.DocumentName);
    }
}
