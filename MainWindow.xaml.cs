using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenImage.Dialogs;
using OpenImage.Engine;
using OpenImage.Models;

namespace OpenImage;

public partial class MainWindow : Window
{
    private double _canvasWidth = 1920;
    private double _canvasHeight = 1080;
    private string _docName = "Matterhorn-Sunset.png";

    private readonly ObservableCollection<Layer> _layers = new();
    private Layer? _activeLayer;
    private readonly Adjustments _adjustments = new();
    private readonly HistoryManager _history = new();

    private ToolType _activeTool = ToolType.Select;
    private Color _primaryColor = Color.FromRgb(0, 122, 255); // iOS System Blue
    private Color _secondaryColor = Colors.White;

    private double _zoom = 0.6;
    private bool _isPanning = false;
    private Point _panStart;

    private bool _isInteracting = false;
    private Point _lastPoint;
    private Point _dragStart;

    private Rect _selectionRect = Rect.Empty;
    private bool _hasSelection = false;
    private Rect _cropRect = Rect.Empty;
    private Point? _currentCanvasMouse;
    private string? _currentProjectPath = null;

    private Point _cloneSourcePoint = new Point(0, 0);
    private Point _cloneTargetOrigin = new Point(0, 0);
    private Point _lastCloneTarget = new Point(0, 0);
    private Point _lastCloneSource = new Point(0, 0);
    private bool _hasCloneSource = false;

    private GradientStyle _activeGradientStyle = GradientStyle.Linear;
    private Color _gradientColor1 = Color.FromRgb(0, 122, 255);
    private Color _gradientColor2 = Colors.White;



    public MainWindow()
    {
        InitializeComponent();
        LayersListBox.ItemsSource = _layers;
        HistoryListBox.ItemsSource = _history.HistoryList;

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadInitialArtwork();
        UpdateDocInfo();
        UpdateZoomDisplay();
    }

    // =========================================================================
    // INITIAL STARTER DEMO ARTWORK
    // =========================================================================
    private void LoadInitialArtwork()
    {
        _layers.Clear();

        string samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "sample.jpg");
        if (!File.Exists(samplePath))
        {
            samplePath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "sample.jpg");
        }

        if (File.Exists(samplePath))
        {
            try
            {
                var uri = new Uri(samplePath, UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();

                _canvasWidth = bmp.PixelWidth;
                _canvasHeight = bmp.PixelHeight;

                var photoLayer = new Layer("Matterhorn Sunset", _canvasWidth, _canvasHeight, LayerType.Raster);
                var wb = new WriteableBitmap(bmp);
                photoLayer.Bitmap = wb;
                _layers.Add(photoLayer);
            }
            catch
            {
                CreateFallbackGradientLayer();
            }
        }
        else
        {
            CreateFallbackGradientLayer();
        }

        // Add iOS 7 Styled Frosted Badge Shape Layer
        var badge = new Layer("Frosted Badge", 520, 150, LayerType.Shape)
        {
            X = 80,
            Y = 80,
            ShapeType = ShapeType.Rectangle,
            FillColor = Color.FromArgb(64, 255, 255, 255),
            StrokeColor = Color.FromArgb(160, 255, 255, 255),
            StrokeWidth = 1.5,
            CornerRadius = 24
        };
        _layers.Add(badge);

        // Add Vector Text Title
        var title = new Layer("Title Text", 480, 60, LayerType.Text)
        {
            X = 110,
            Y = 110,
            TextContent = "Open Image Studio",
            FontSize = 42,
            TextColor = Colors.White
        };
        _layers.Add(title);

        // Add Vector Subtitle
        var sub = new Layer("Subtitle", 480, 40, LayerType.Text)
        {
            X = 112,
            Y = 170,
            TextContent = "iOS 7 Native Windows .NET 9",
            FontSize = 20,
            TextColor = Color.FromArgb(220, 240, 240, 245)
        };
        _layers.Add(sub);

        _activeLayer = _layers[0];
        LayersListBox.SelectedItem = _activeLayer;
        RenderComposite();
        var initialThumb = CreateCanvasThumbnail();
        _history.PushState("Initial Artwork", _canvasWidth, _canvasHeight, _layers, _adjustments, initialThumb);
        UpdateHistoryButtons();
    }

    private void CreateFallbackGradientLayer()
    {
        _canvasWidth = 1920;
        _canvasHeight = 1080;
        var layer = new Layer("Background Gradient", _canvasWidth, _canvasHeight, LayerType.Raster);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var grad = new LinearGradientBrush(Color.FromRgb(90, 200, 250), Color.FromRgb(0, 122, 255), new Point(0, 0), new Point(1, 1));
            dc.DrawRectangle(grad, null, new Rect(0, 0, _canvasWidth, _canvasHeight));
        }
        var rtb = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        layer.Bitmap = new WriteableBitmap(rtb);
        _layers.Add(layer);
    }

    // =========================================================================
    // RENDERING & COMPOSITION
    // =========================================================================
    private void RenderComposite()
    {
        CanvasBackdrop.Width = _canvasWidth;
        CanvasBackdrop.Height = _canvasHeight;

        var composite = ImageCompositor.RenderComposite(_canvasWidth, _canvasHeight, _layers, _adjustments);
        ImgComposite.Source = composite;
        UpdateOverlays();
    }

    private void Canvas_MouseLeave(object sender, MouseEventArgs e)
    {
        _currentCanvasMouse = null;
        UpdateOverlays();
        CanvasScrollViewer.Cursor = Cursors.Arrow;
    }

    private void UpdateOverlays(Point? mousePos = null)
    {
        OverlayCanvas.Width = _canvasWidth;
        OverlayCanvas.Height = _canvasHeight;
        OverlayCanvas.Children.Clear();

        if (mousePos != null)
        {
            _currentCanvasMouse = mousePos;
        }

        // 1. Transform Gizmo for active layer in Select Tool
        if (_activeTool == ToolType.Select && _activeLayer != null)
        {
            double x = _activeLayer.X;
            double y = _activeLayer.Y;
            double w = _activeLayer.Width;
            double h = _activeLayer.Height;

            // Bounding border
            var box = new System.Windows.Shapes.Rectangle
            {
                Width = Math.Max(1, w),
                Height = Math.Max(1, h),
                Stroke = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
                StrokeThickness = 1.0,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(box, x);
            Canvas.SetTop(box, y);
            OverlayCanvas.Children.Add(box);

            // 8 handles + 1 rotation anchor
            Point[] handles = new Point[]
            {
                new Point(x, y),                 // NW
                new Point(x + w / 2, y),         // N
                new Point(x + w, y),             // NE
                new Point(x + w, y + h / 2),     // E
                new Point(x + w, y + h),         // SE
                new Point(x + w / 2, y + h),     // S
                new Point(x, y + h),             // SW
                new Point(x, y + h / 2),         // W
                new Point(x + w / 2, y - 24)     // Rotation Handle Anchor
            };

            // Stem for rotation handle
            var stem = new System.Windows.Shapes.Line
            {
                X1 = x + w / 2,
                Y1 = y,
                X2 = x + w / 2,
                Y2 = y - 24,
                Stroke = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
                StrokeThickness = 1.0,
                IsHitTestVisible = false
            };
            OverlayCanvas.Children.Add(stem);

            for (int i = 0; i < handles.Length; i++)
            {
                var hp = handles[i];
                bool isRotate = i == handles.Length - 1;
                var handle = new System.Windows.Shapes.Rectangle
                {
                    Width = isRotate ? 8 : 7,
                    Height = isRotate ? 8 : 7,
                    Fill = Brushes.White,
                    Stroke = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
                    StrokeThickness = 1.2,
                    RadiusX = isRotate ? 4 : 1.5,
                    RadiusY = isRotate ? 4 : 1.5,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(handle, hp.X - (isRotate ? 4 : 3.5));
                Canvas.SetTop(handle, hp.Y - (isRotate ? 4 : 3.5));
                OverlayCanvas.Children.Add(handle);
            }
        }

        // 2. Crop Rule-of-Thirds Grid & Darkened Scrim
        if (_activeTool == ToolType.Crop)
        {
            Rect cropRect = _cropRect;
            if (cropRect.IsEmpty || cropRect.Width < 10)
            {
                cropRect = new Rect(0.1 * _canvasWidth, 0.1 * _canvasHeight, 0.8 * _canvasWidth, 0.8 * _canvasHeight);
            }

            var fullRectGeom = new RectangleGeometry(new Rect(0, 0, _canvasWidth, _canvasHeight));
            var cropRectGeom = new RectangleGeometry(cropRect);
            var combinedGeom = new CombinedGeometry(GeometryCombineMode.Exclude, fullRectGeom, cropRectGeom);

            var scrim = new System.Windows.Shapes.Path
            {
                Data = combinedGeom,
                Fill = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
                IsHitTestVisible = false
            };
            OverlayCanvas.Children.Add(scrim);

            var cropBorder = new System.Windows.Shapes.Rectangle
            {
                Width = cropRect.Width,
                Height = cropRect.Height,
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(cropBorder, cropRect.X);
            Canvas.SetTop(cropBorder, cropRect.Y);
            OverlayCanvas.Children.Add(cropBorder);

            // Rule of thirds lines
            for (int i = 1; i <= 2; i++)
            {
                var vLine = new System.Windows.Shapes.Line
                {
                    X1 = cropRect.X + (cropRect.Width * i / 3.0),
                    Y1 = cropRect.Y,
                    X2 = cropRect.X + (cropRect.Width * i / 3.0),
                    Y2 = cropRect.Y + cropRect.Height,
                    Stroke = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                    StrokeThickness = 1.0,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    IsHitTestVisible = false
                };
                OverlayCanvas.Children.Add(vLine);

                var hLine = new System.Windows.Shapes.Line
                {
                    X1 = cropRect.X,
                    Y1 = cropRect.Y + (cropRect.Height * i / 3.0),
                    X2 = cropRect.X + cropRect.Width,
                    Y2 = cropRect.Y + (cropRect.Height * i / 3.0),
                    Stroke = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                    StrokeThickness = 1.0,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    IsHitTestVisible = false
                };
                OverlayCanvas.Children.Add(hLine);
            }
        }

        // 3. Marquee Selection Box
        if ((_activeTool == ToolType.Marquee || _hasSelection) && !_selectionRect.IsEmpty && _selectionRect.Width > 2)
        {
            var marqueeBox = new System.Windows.Shapes.Rectangle
            {
                Width = _selectionRect.Width,
                Height = _selectionRect.Height,
                Stroke = Brushes.White,
                StrokeThickness = 1.0,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                IsHitTestVisible = false
            };
            var marqueeBoxDark = new System.Windows.Shapes.Rectangle
            {
                Width = _selectionRect.Width,
                Height = _selectionRect.Height,
                Stroke = Brushes.Black,
                StrokeThickness = 1.0,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                StrokeDashOffset = 4,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(marqueeBox, _selectionRect.X);
            Canvas.SetTop(marqueeBox, _selectionRect.Y);
            Canvas.SetLeft(marqueeBoxDark, _selectionRect.X);
            Canvas.SetTop(marqueeBoxDark, _selectionRect.Y);
            OverlayCanvas.Children.Add(marqueeBoxDark);
            OverlayCanvas.Children.Add(marqueeBox);
        }

        // 4. Dynamic Brush / Eraser Circular Reticle
        if ((_activeTool == ToolType.Brush || _activeTool == ToolType.Eraser) && _currentCanvasMouse.HasValue)
        {
            var mp = _currentCanvasMouse.Value;
            if (mp.X >= 0 && mp.X <= _canvasWidth && mp.Y >= 0 && mp.Y <= _canvasHeight)
            {
                double radius = SliderBrushSize.Value / 2.0;
                var reticleOuter = new System.Windows.Shapes.Ellipse
                {
                    Width = Math.Max(2, radius * 2),
                    Height = Math.Max(2, radius * 2),
                    Stroke = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                    StrokeThickness = 1.5,
                    IsHitTestVisible = false
                };
                var reticleInner = new System.Windows.Shapes.Ellipse
                {
                    Width = Math.Max(2, radius * 2 - 1),
                    Height = Math.Max(2, radius * 2 - 1),
                    Stroke = Brushes.White,
                    StrokeThickness = 1.0,
                    IsHitTestVisible = false
                };

                Canvas.SetLeft(reticleOuter, mp.X - radius);
                Canvas.SetTop(reticleOuter, mp.Y - radius);
                Canvas.SetLeft(reticleInner, mp.X - radius + 0.5);
                Canvas.SetTop(reticleInner, mp.Y - radius + 0.5);

                OverlayCanvas.Children.Add(reticleOuter);
                OverlayCanvas.Children.Add(reticleInner);
            }
        }

        // 5. Clone Stamp Dynamic Brush Reticle + Source Crosshair
        if (_activeTool == ToolType.CloneStamp && _currentCanvasMouse.HasValue)
        {
            var mp = _currentCanvasMouse.Value;
            if (mp.X >= 0 && mp.X <= _canvasWidth && mp.Y >= 0 && mp.Y <= _canvasHeight)
            {
                double radius = SliderCloneSize.Value / 2.0;
                var reticleOuter = new System.Windows.Shapes.Ellipse
                {
                    Width = Math.Max(2, radius * 2),
                    Height = Math.Max(2, radius * 2),
                    Stroke = new SolidColorBrush(Color.FromArgb(180, 0, 122, 255)),
                    StrokeThickness = 1.5,
                    IsHitTestVisible = false
                };
                var reticleInner = new System.Windows.Shapes.Ellipse
                {
                    Width = Math.Max(2, radius * 2 - 1),
                    Height = Math.Max(2, radius * 2 - 1),
                    Stroke = Brushes.White,
                    StrokeThickness = 1.0,
                    IsHitTestVisible = false
                };

                Canvas.SetLeft(reticleOuter, mp.X - radius);
                Canvas.SetTop(reticleOuter, mp.Y - radius);
                Canvas.SetLeft(reticleInner, mp.X - radius + 0.5);
                Canvas.SetTop(reticleInner, mp.Y - radius + 0.5);

                OverlayCanvas.Children.Add(reticleOuter);
                OverlayCanvas.Children.Add(reticleInner);

                if (_hasCloneSource)
                {
                    Point srcPos = (ChkCloneAligned != null && ChkCloneAligned.IsChecked == true)
                        ? _cloneSourcePoint + (mp - _cloneTargetOrigin)
                        : (_isInteracting ? _cloneSourcePoint + (mp - _dragStart) : _cloneSourcePoint);

                    // Crosshair at source location
                    var crossCircle = new System.Windows.Shapes.Ellipse
                    {
                        Width = 12,
                        Height = 12,
                        Stroke = new SolidColorBrush(Color.FromRgb(255, 59, 48)), // iOS System Red
                        StrokeThickness = 1.5,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(crossCircle, srcPos.X - 6);
                    Canvas.SetTop(crossCircle, srcPos.Y - 6);

                    var hLine = new System.Windows.Shapes.Line
                    {
                        X1 = srcPos.X - 9, Y1 = srcPos.Y,
                        X2 = srcPos.X + 9, Y2 = srcPos.Y,
                        Stroke = new SolidColorBrush(Color.FromRgb(255, 59, 48)),
                        StrokeThickness = 1.5,
                        IsHitTestVisible = false
                    };
                    var vLine = new System.Windows.Shapes.Line
                    {
                        X1 = srcPos.X, Y1 = srcPos.Y - 9,
                        X2 = srcPos.X, Y2 = srcPos.Y + 9,
                        Stroke = new SolidColorBrush(Color.FromRgb(255, 59, 48)),
                        StrokeThickness = 1.5,
                        IsHitTestVisible = false
                    };

                    OverlayCanvas.Children.Add(crossCircle);
                    OverlayCanvas.Children.Add(hLine);
                    OverlayCanvas.Children.Add(vLine);
                }
            }
        }

        // 6. Interactive Gradient Drag Vector Overlay
        if (_activeTool == ToolType.Gradient && _isInteracting && _currentCanvasMouse.HasValue)
        {
            Point p1 = _dragStart;
            Point p2 = _currentCanvasMouse.Value;

            if (_activeGradientStyle == GradientStyle.Linear)
            {
                var lineOuter = new System.Windows.Shapes.Line
                {
                    X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
                    Stroke = Brushes.White, StrokeThickness = 3.0, IsHitTestVisible = false
                };
                var lineInner = new System.Windows.Shapes.Line
                {
                    X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(0, 122, 255)), StrokeThickness = 1.5, IsHitTestVisible = false
                };
                OverlayCanvas.Children.Add(lineOuter);
                OverlayCanvas.Children.Add(lineInner);
            }
            else
            {
                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double radius = Math.Max(2.0, Math.Sqrt(dx * dx + dy * dy));

                var circleOuter = new System.Windows.Shapes.Ellipse
                {
                    Width = radius * 2, Height = radius * 2,
                    Stroke = Brushes.White, StrokeThickness = 2.0,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    IsHitTestVisible = false
                };
                var circleInner = new System.Windows.Shapes.Ellipse
                {
                    Width = radius * 2, Height = radius * 2,
                    Stroke = new SolidColorBrush(Color.FromRgb(0, 122, 255)), StrokeThickness = 1.0,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(circleOuter, p1.X - radius);
                Canvas.SetTop(circleOuter, p1.Y - radius);
                Canvas.SetLeft(circleInner, p1.X - radius);
                Canvas.SetTop(circleInner, p1.Y - radius);
                OverlayCanvas.Children.Add(circleOuter);
                OverlayCanvas.Children.Add(circleInner);
            }

            var handleStart = new System.Windows.Shapes.Ellipse
            {
                Width = 12, Height = 12,
                Fill = new SolidColorBrush(_gradientColor1),
                Stroke = Brushes.White, StrokeThickness = 2.0, IsHitTestVisible = false
            };
            Canvas.SetLeft(handleStart, p1.X - 6);
            Canvas.SetTop(handleStart, p1.Y - 6);
            OverlayCanvas.Children.Add(handleStart);

            var handleEnd = new System.Windows.Shapes.Ellipse
            {
                Width = 12, Height = 12,
                Fill = new SolidColorBrush(_gradientColor2),
                Stroke = Brushes.White, StrokeThickness = 2.0, IsHitTestVisible = false
            };
            Canvas.SetLeft(handleEnd, p2.X - 6);
            Canvas.SetTop(handleEnd, p2.Y - 6);
            OverlayCanvas.Children.Add(handleEnd);
        }
    }

    private void CommitAction(string description)
    {
        RenderComposite();
        var thumb = CreateCanvasThumbnail();
        _history.PushState(description, _canvasWidth, _canvasHeight, _layers, _adjustments, thumb);
        UpdateHistoryButtons();
    }

    private ImageSource? CreateCanvasThumbnail()
    {
        if (ImgComposite.Source is not BitmapSource src) return null;
        try
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawImage(src, new Rect(0, 0, 40, 40));
            }
            var rtb = new RenderTargetBitmap(40, 40, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }
        catch
        {
            return null;
        }
    }

    private void UpdateHistoryButtons()
    {
        BtnUndo.IsEnabled = _history.CanUndo;
        BtnRedo.IsEnabled = _history.CanRedo;
    }

    private void UpdateDocInfo()
    {
        TxtDocInfo.Text = $"{_docName} ({(int)_canvasWidth} × {(int)_canvasHeight} px)";
        TxtStatusDimensions.Text = $"{(int)_canvasWidth} × {(int)_canvasHeight} px";
    }

    private void UpdateZoomDisplay()
    {
        CanvasScale.ScaleX = _zoom;
        CanvasScale.ScaleY = _zoom;
        TxtStatusZoom.Text = $"Zoom: {(int)(_zoom * 100)}%";
    }

    // =========================================================================
    // ZOOM & PAN VIEWPORT INTERACTION
    // =========================================================================
    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            double factor = e.Delta > 0 ? 1.15 : 0.85;
            _zoom = Math.Clamp(_zoom * factor, 0.08, 32.0);
            UpdateZoomDisplay();
        }
    }

    private void ZoomPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && double.TryParse(fe.Tag?.ToString(), out double z))
        {
            _zoom = z;
            UpdateZoomDisplay();
        }
    }

    private void ZoomFit_Click(object sender, RoutedEventArgs e)
    {
        _zoom = 0.6;
        UpdateZoomDisplay();
    }

    // =========================================================================
    // CANVAS MOUSE INTERACTION (DRAWING, MOVING, TOOLS)
    // =========================================================================
    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Middle button or Space for Hand Pan
        if (e.MiddleButton == MouseButtonState.Pressed || Keyboard.IsKeyDown(Key.Space))
        {
            _isPanning = true;
            _panStart = e.GetPosition(CanvasScrollViewer);
            CanvasScrollViewer.Cursor = Cursors.Hand;
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed) return;

        Point pt = e.GetPosition(ImgComposite);
        _dragStart = pt;
        _lastPoint = pt;
        _isInteracting = true;

        if (_activeTool == ToolType.Brush || _activeTool == ToolType.Eraser)
        {
            if (_activeLayer != null && !_activeLayer.IsLocked)
            {
                WriteableBitmap? targetBmp = (_activeLayer.IsEditingMask && _activeLayer.HasMask)
                    ? _activeLayer.MaskBitmap
                    : (_activeLayer.Type == LayerType.Raster ? _activeLayer.Bitmap : null);

                if (targetBmp != null)
                {
                    int localX = (int)(pt.X - _activeLayer.X);
                    int localY = (int)(pt.Y - _activeLayer.Y);
                    int size = (int)SliderBrushSize.Value;
                    double opacity = SliderBrushOpacity.Value / 100.0;
                    double hardness = SliderBrushHardness != null ? SliderBrushHardness.Value / 100.0 : 0.5;
                    bool isEraser = _activeTool == ToolType.Eraser;

                    Color drawColor = (_activeLayer.IsEditingMask && _activeLayer.HasMask)
                        ? (isEraser ? Colors.White : _primaryColor)
                        : _primaryColor;

                    DrawingEngine.DrawBrushStamp(targetBmp, localX, localY, size / 2, drawColor, opacity, (!_activeLayer.IsEditingMask && isEraser), hardness);
                    if (_activeLayer.IsEditingMask) _activeLayer.UpdateMaskThumbnail();
                    RenderComposite();
                }
            }
        }
        else if (_activeTool == ToolType.Marquee)
        {
            _selectionRect = new Rect(pt, pt);
            _hasSelection = false;
            UpdateOverlays(pt);
        }
        else if (_activeTool == ToolType.Crop)
        {
            _cropRect = new Rect(pt, pt);
            UpdateOverlays(pt);
        }
        else if (_activeTool == ToolType.Fill)
        {
            if (_activeLayer != null && !_activeLayer.IsLocked)
            {
                WriteableBitmap? targetBmp = (_activeLayer.IsEditingMask && _activeLayer.HasMask)
                    ? _activeLayer.MaskBitmap
                    : (_activeLayer.Type == LayerType.Raster ? _activeLayer.Bitmap : null);

                if (targetBmp != null)
                {
                    int localX = (int)(pt.X - _activeLayer.X);
                    int localY = (int)(pt.Y - _activeLayer.Y);
                    DrawingEngine.FloodFill(targetBmp, localX, localY, _primaryColor, 32);
                    if (_activeLayer.IsEditingMask) _activeLayer.UpdateMaskThumbnail();
                    else _activeLayer.UpdateThumbnail();
                    CommitAction(_activeLayer.IsEditingMask ? "Mask Fill" : "Flood Fill");
                }
            }
        }
        else if (_activeTool == ToolType.Eyedropper)
        {
            SampleColorAt(pt);
        }
        else if (_activeTool == ToolType.Text)
        {
            var textLayer = new Layer("Text Layer", 300, 60, LayerType.Text)
            {
                X = pt.X,
                Y = pt.Y,
                TextContent = "Open Image Studio",
                FontSize = 36,
                TextColor = _primaryColor
            };
            _layers.Add(textLayer);
            _activeLayer = textLayer;
            LayersListBox.SelectedItem = textLayer;
            CommitAction("Add Text Layer");
            _isInteracting = false;
        }
        else if (_activeTool == ToolType.CloneStamp)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                _cloneSourcePoint = pt;
                _cloneTargetOrigin = pt;
                _hasCloneSource = true;
                if (TxtCloneSourceStatus != null) TxtCloneSourceStatus.Text = $"Source: ({(int)pt.X}, {(int)pt.Y})";
                TxtStatusMessage.Text = $"Clone source set to ({(int)pt.X}, {(int)pt.Y})";
                UpdateOverlays(pt);
                _isInteracting = false;
                return;
            }

            if (!_hasCloneSource)
            {
                if (TxtCloneSourceStatus != null) TxtCloneSourceStatus.Text = "Alt+Click to set source first!";
                TxtStatusMessage.Text = "Hold Alt and Click to sample a clone source point";
                return;
            }

            if (_activeLayer != null && _activeLayer.Type == LayerType.Raster && !_activeLayer.IsLocked && _activeLayer.Bitmap != null)
            {
                if (ChkCloneAligned != null && ChkCloneAligned.IsChecked == true && _cloneTargetOrigin == _cloneSourcePoint)
                {
                    _cloneTargetOrigin = pt;
                }

                Point srcPt = (ChkCloneAligned != null && ChkCloneAligned.IsChecked == true)
                    ? _cloneSourcePoint + (pt - _cloneTargetOrigin)
                    : _cloneSourcePoint;

                _lastCloneTarget = pt;
                _lastCloneSource = srcPt;

                int size = (int)SliderCloneSize.Value;
                double opacity = SliderCloneOpacity.Value / 100.0;
                double hardness = SliderCloneHardness != null ? SliderCloneHardness.Value / 100.0 : 0.5;

                int localTargetX = (int)(pt.X - _activeLayer.X);
                int localTargetY = (int)(pt.Y - _activeLayer.Y);
                int localSourceX = (int)(srcPt.X - _activeLayer.X);
                int localSourceY = (int)(srcPt.Y - _activeLayer.Y);

                DrawingEngine.DrawCloneStamp(_activeLayer.Bitmap, _activeLayer.Bitmap, localTargetX, localTargetY, localSourceX, localSourceY, size / 2, opacity, hardness);
                RenderComposite();
            }
        }
        else if (_activeTool == ToolType.Gradient)
        {
            UpdateOverlays(pt);
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            Point current = e.GetPosition(CanvasScrollViewer);
            CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset - (current.X - _panStart.X));
            CanvasScrollViewer.ScrollToVerticalOffset(CanvasScrollViewer.VerticalOffset - (current.Y - _panStart.Y));
            _panStart = current;
            return;
        }

        Point pt = e.GetPosition(ImgComposite);
        TxtStatusCoords.Text = $"X: {(int)pt.X}, Y: {(int)pt.Y}";

        // Update overlays (brush ring, transform gizmo, etc.)
        UpdateOverlays(pt);

        if (!_isInteracting) return;

        if (_activeTool == ToolType.Marquee)
        {
            _selectionRect = new Rect(
                Math.Min(_dragStart.X, pt.X),
                Math.Min(_dragStart.Y, pt.Y),
                Math.Abs(pt.X - _dragStart.X),
                Math.Abs(pt.Y - _dragStart.Y)
            );
            UpdateOverlays(pt);
            return;
        }
        else if (_activeTool == ToolType.Crop)
        {
            _cropRect = new Rect(
                Math.Min(_dragStart.X, pt.X),
                Math.Min(_dragStart.Y, pt.Y),
                Math.Max(20, Math.Abs(pt.X - _dragStart.X)),
                Math.Max(20, Math.Abs(pt.Y - _dragStart.Y))
            );
            UpdateOverlays(pt);
            return;
        }
        else if ((_activeTool == ToolType.Brush || _activeTool == ToolType.Eraser) && _activeLayer != null)
        {
            WriteableBitmap? targetBmp = (_activeLayer.IsEditingMask && _activeLayer.HasMask)
                ? _activeLayer.MaskBitmap
                : (_activeLayer.Type == LayerType.Raster ? _activeLayer.Bitmap : null);

            if (targetBmp != null)
            {
                Point localLast = new Point(_lastPoint.X - _activeLayer.X, _lastPoint.Y - _activeLayer.Y);
                Point localCurrent = new Point(pt.X - _activeLayer.X, pt.Y - _activeLayer.Y);
                int size = (int)SliderBrushSize.Value;
                double opacity = SliderBrushOpacity.Value / 100.0;
                double hardness = SliderBrushHardness != null ? SliderBrushHardness.Value / 100.0 : 0.5;
                bool isEraser = _activeTool == ToolType.Eraser;

                Color drawColor = (_activeLayer.IsEditingMask && _activeLayer.HasMask)
                    ? (isEraser ? Colors.White : _primaryColor)
                    : _primaryColor;

                DrawingEngine.DrawBrushLine(targetBmp, localLast, localCurrent, size / 2, drawColor, opacity, (!_activeLayer.IsEditingMask && isEraser), hardness);
                _lastPoint = pt;
                if (_activeLayer.IsEditingMask) _activeLayer.UpdateMaskThumbnail();
                RenderComposite();
            }
        }
        else if (_activeTool == ToolType.CloneStamp && _activeLayer != null && _activeLayer.Bitmap != null)
        {
            Point currentTarget = pt;
            Point currentSource = (ChkCloneAligned != null && ChkCloneAligned.IsChecked == true)
                ? _cloneSourcePoint + (pt - _cloneTargetOrigin)
                : _cloneSourcePoint + (pt - _dragStart);

            Point localLastTarget = new Point(_lastCloneTarget.X - _activeLayer.X, _lastCloneTarget.Y - _activeLayer.Y);
            Point localCurTarget = new Point(currentTarget.X - _activeLayer.X, currentTarget.Y - _activeLayer.Y);
            Point localLastSource = new Point(_lastCloneSource.X - _activeLayer.X, _lastCloneSource.Y - _activeLayer.Y);
            Point localCurSource = new Point(currentSource.X - _activeLayer.X, currentSource.Y - _activeLayer.Y);

            int size = (int)SliderCloneSize.Value;
            double opacity = SliderCloneOpacity.Value / 100.0;
            double hardness = SliderCloneHardness != null ? SliderCloneHardness.Value / 100.0 : 0.5;

            DrawingEngine.DrawCloneLine(_activeLayer.Bitmap, _activeLayer.Bitmap, localLastTarget, localCurTarget, localLastSource, localCurSource, size / 2, opacity, hardness);
            _lastCloneTarget = currentTarget;
            _lastCloneSource = currentSource;
            RenderComposite();
        }
        else if (_activeTool == ToolType.Select && _activeLayer != null && !_activeLayer.IsLocked)
        {
            double dx = pt.X - _lastPoint.X;
            double dy = pt.Y - _lastPoint.Y;
            _activeLayer.X += dx;
            _activeLayer.Y += dy;
            _lastPoint = pt;
            RenderComposite();
        }
        else if (_activeTool == ToolType.Eyedropper)
        {
            SampleColorAt(pt);
        }
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            CanvasScrollViewer.Cursor = Cursors.Arrow;
            return;
        }

        if (!_isInteracting) return;
        _isInteracting = false;

        if (_activeTool == ToolType.Marquee)
        {
            _hasSelection = _selectionRect.Width > 5 && _selectionRect.Height > 5;
            UpdateOverlays();
        }
        else if (_activeTool == ToolType.Crop)
        {
            if (_cropRect.Width < 10 || _cropRect.Height < 10)
            {
                _cropRect = new Rect(0.1 * _canvasWidth, 0.1 * _canvasHeight, 0.8 * _canvasWidth, 0.8 * _canvasHeight);
            }
            UpdateOverlays();
        }
        else if (_activeTool == ToolType.Brush || _activeTool == ToolType.Eraser)
        {
            if (_activeLayer != null)
            {
                if (_activeLayer.IsEditingMask && _activeLayer.HasMask)
                {
                    _activeLayer.UpdateMaskThumbnail();
                    CommitAction("Mask Paint");
                }
                else
                {
                    _activeLayer.UpdateThumbnail();
                    CommitAction(_activeTool == ToolType.Brush ? "Brush Stroke" : "Eraser");
                }
            }
        }
        else if (_activeTool == ToolType.CloneStamp)
        {
            _activeLayer?.UpdateThumbnail();
            CommitAction("Clone Stamp");
        }
        else if (_activeTool == ToolType.Gradient)
        {
            Point pEnd = e.GetPosition(ImgComposite);
            double dist = Math.Sqrt(Math.Pow(pEnd.X - _dragStart.X, 2) + Math.Pow(pEnd.Y - _dragStart.Y, 2));
            WriteableBitmap? gradTarget = (_activeLayer != null && !_activeLayer.IsLocked)
                ? (_activeLayer.IsEditingMask && _activeLayer.HasMask ? _activeLayer.MaskBitmap : (_activeLayer.Type == LayerType.Raster ? _activeLayer.Bitmap : null))
                : null;

            if (dist >= 3 && _activeLayer != null && gradTarget != null)
            {
                Point localP1 = new Point(_dragStart.X - _activeLayer.X, _dragStart.Y - _activeLayer.Y);
                Point localP2 = new Point(pEnd.X - _activeLayer.X, pEnd.Y - _activeLayer.Y);

                Rect? localClip = null;
                if (_hasSelection && !_selectionRect.IsEmpty)
                {
                    localClip = new Rect(_selectionRect.X - _activeLayer.X, _selectionRect.Y - _activeLayer.Y, _selectionRect.Width, _selectionRect.Height);
                }

                double opacity = SliderGradientOpacity != null ? SliderGradientOpacity.Value / 100.0 : 1.0;

                if (_activeGradientStyle == GradientStyle.Linear)
                {
                    DrawingEngine.DrawLinearGradient(gradTarget, localP1, localP2, _gradientColor1, _gradientColor2, localClip, opacity);
                }
                else
                {
                    DrawingEngine.DrawRadialGradient(gradTarget, localP1, localP2, _gradientColor1, _gradientColor2, localClip, opacity);
                }

                if (_activeLayer.IsEditingMask) _activeLayer.UpdateMaskThumbnail();
                else _activeLayer.UpdateThumbnail();
                RenderComposite();
                CommitAction(_activeLayer.IsEditingMask ? "Mask Gradient" : $"Render {(_activeGradientStyle == GradientStyle.Linear ? "Linear" : "Radial")} Gradient");
            }
            UpdateOverlays();
        }
        else if (_activeTool == ToolType.Select)
        {
            CommitAction("Move Layer");
        }
        else if (_activeTool == ToolType.Shape)
        {
            Point end = e.GetPosition(ImgComposite);
            double w = Math.Abs(end.X - _dragStart.X);
            double h = Math.Abs(end.Y - _dragStart.Y);
            if (w > 5 && h > 5)
            {
                double x = Math.Min(_dragStart.X, end.X);
                double y = Math.Min(_dragStart.Y, end.Y);
                var shape = new Layer("Rectangle Shape", w, h, LayerType.Shape)
                {
                    X = x,
                    Y = y,
                    ShapeType = ShapeType.Rectangle,
                    FillColor = _primaryColor,
                    StrokeColor = Color.FromRgb(0, 86, 179),
                    StrokeWidth = 2,
                    CornerRadius = 12
                };
                _layers.Add(shape);
                _activeLayer = shape;
                LayersListBox.SelectedItem = shape;
                CommitAction("Add Shape Layer");
            }
        }
    }

    private void SampleColorAt(Point pt)
    {
        if (ImgComposite.Source is WriteableBitmap wb)
        {
            Color c = DrawingEngine.SampleColor(wb, (int)pt.X, (int)pt.Y);
            _primaryColor = c;
            ChipPrimary.Background = new SolidColorBrush(c);
            if (ActiveColorIndicator != null) ActiveColorIndicator.Background = new SolidColorBrush(c);
        }
    }

    // =========================================================================
    // TOOL SELECTION & OPTIONS
    // =========================================================================
    private void Tool_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && Enum.TryParse(rb.Tag?.ToString(), out ToolType tool))
        {
            _activeTool = tool;
            TxtStatusTool.Text = $"Tool: {tool.ToString().ToUpper()}";

            OptionsBrush.Visibility = (tool == ToolType.Brush || tool == ToolType.Eraser) ? Visibility.Visible : Visibility.Collapsed;
            OptionsCrop.Visibility = tool == ToolType.Crop ? Visibility.Visible : Visibility.Collapsed;
            OptionsMarquee.Visibility = tool == ToolType.Marquee ? Visibility.Visible : Visibility.Collapsed;
            OptionsCloneStamp.Visibility = tool == ToolType.CloneStamp ? Visibility.Visible : Visibility.Collapsed;
            OptionsGradient.Visibility = tool == ToolType.Gradient ? Visibility.Visible : Visibility.Collapsed;

            if (tool == ToolType.CloneStamp)
            {
                TxtStatusMessage.Text = _hasCloneSource ? "Clone Stamp: Drag to paint sampled pixels. Alt+Click to re-sample." : "Clone Stamp: Hold Alt and Click canvas to sample source point.";
            }
            else if (tool == ToolType.Gradient)
            {
                TxtStatusMessage.Text = "Gradient Tool: Click and drag across canvas to render gradient.";
            }

            if (tool == ToolType.Crop && (_cropRect.IsEmpty || _cropRect.Width == 0))
            {
                _cropRect = new Rect(0.1 * _canvasWidth, 0.1 * _canvasHeight, 0.8 * _canvasWidth, 0.8 * _canvasHeight);
            }
            UpdateOverlays();
        }
    }

    private void GradientType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && Enum.TryParse(rb.Tag?.ToString(), out GradientStyle style))
        {
            _activeGradientStyle = style;
        }
    }

    private void GradientReverse_Click(object sender, RoutedEventArgs e)
    {
        var temp = _gradientColor1;
        _gradientColor1 = _gradientColor2;
        _gradientColor2 = temp;
        UpdateGradientPreview();
    }

    private void GradientOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtGradientOpacityVal != null) TxtGradientOpacityVal.Text = $"{(int)e.NewValue}%";
    }

    private void GradientPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string preset)
        {
            switch (preset)
            {
                case "Sunrise":
                    _gradientColor1 = Color.FromRgb(255, 45, 85);  // System Pink
                    _gradientColor2 = Color.FromRgb(255, 149, 0); // System Orange
                    break;
                case "Ocean":
                    _gradientColor1 = Color.FromRgb(0, 122, 255);  // System Blue
                    _gradientColor2 = Color.FromRgb(90, 200, 250); // System Teal
                    break;
                case "Sunset":
                    _gradientColor1 = Color.FromRgb(88, 86, 214);  // System Purple
                    _gradientColor2 = Color.FromRgb(255, 45, 85);  // System Pink
                    break;
                case "Lime":
                    _gradientColor1 = Color.FromRgb(52, 199, 89);  // System Green
                    _gradientColor2 = Color.FromRgb(205, 220, 57); // Lime
                    break;
            }
            _primaryColor = _gradientColor1;
            _secondaryColor = _gradientColor2;
            ChipPrimary.Background = new SolidColorBrush(_primaryColor);
            ChipSecondary.Background = new SolidColorBrush(_secondaryColor);
            if (ActiveColorIndicator != null) ActiveColorIndicator.Background = new SolidColorBrush(_primaryColor);
            UpdateGradientPreview();
        }
    }

    private void UpdateGradientPreview()
    {
        if (GradStop1 != null) GradStop1.Color = _gradientColor1;
        if (GradStop2 != null) GradStop2.Color = _gradientColor2;
    }

    private void CloneSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtCloneSizeVal != null) TxtCloneSizeVal.Text = $"{(int)e.NewValue}px";
        UpdateOverlays();
    }

    private void CloneOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtCloneOpacityVal != null) TxtCloneOpacityVal.Text = $"{(int)e.NewValue}%";
    }

    private void CloneHardness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtCloneHardnessVal != null) TxtCloneHardnessVal.Text = $"{(int)e.NewValue}%";
    }

    private void BrushSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtBrushSizeVal != null) TxtBrushSizeVal.Text = $"{(int)e.NewValue}px";
        UpdateOverlays();
    }

    private void BrushOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtBrushOpacityVal != null) TxtBrushOpacityVal.Text = $"{(int)e.NewValue}%";
    }

    private void BrushHardness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtBrushHardnessVal != null) TxtBrushHardnessVal.Text = $"{(int)e.NewValue}%";
    }

    private void BrushPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string preset)
        {
            switch (preset)
            {
                case "Pen":
                    SliderBrushSize.Value = 6;
                    SliderBrushHardness.Value = 100;
                    SliderBrushOpacity.Value = 100;
                    break;
                case "Pencil":
                    SliderBrushSize.Value = 12;
                    SliderBrushHardness.Value = 80;
                    SliderBrushOpacity.Value = 90;
                    break;
                case "Brush":
                    SliderBrushSize.Value = 28;
                    SliderBrushHardness.Value = 50;
                    SliderBrushOpacity.Value = 100;
                    break;
                case "Airbrush":
                    SliderBrushSize.Value = 64;
                    SliderBrushHardness.Value = 0;
                    SliderBrushOpacity.Value = 50;
                    break;
            }
        }
    }

    private void PrimaryChip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var dlg = new ColorPickerDialog(_primaryColor) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _primaryColor = dlg.SelectedColor;
            _gradientColor1 = _primaryColor;
            ChipPrimary.Background = new SolidColorBrush(_primaryColor);
            if (ActiveColorIndicator != null) ActiveColorIndicator.Background = new SolidColorBrush(_primaryColor);
            UpdateGradientPreview();
        }
    }

    private void SecondaryChip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var dlg = new ColorPickerDialog(_secondaryColor) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _secondaryColor = dlg.SelectedColor;
            _gradientColor2 = _secondaryColor;
            ChipSecondary.Background = new SolidColorBrush(_secondaryColor);
            UpdateGradientPreview();
        }
    }

    private void ColorChip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Swap primary and secondary colors (shortcut X)
        var temp = _primaryColor;
        _primaryColor = _secondaryColor;
        _secondaryColor = temp;
        _gradientColor1 = _primaryColor;
        _gradientColor2 = _secondaryColor;
        ChipPrimary.Background = new SolidColorBrush(_primaryColor);
        ChipSecondary.Background = new SolidColorBrush(_secondaryColor);
        if (ActiveColorIndicator != null) ActiveColorIndicator.Background = new SolidColorBrush(_primaryColor);
        UpdateGradientPreview();
    }

    // =========================================================================
    // LAYER MANAGEMENT
    // =========================================================================
    private void LayersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _activeLayer = LayersListBox.SelectedItem as Layer;
        if (_activeLayer != null)
        {
            SliderLayerOpacity.Value = _activeLayer.Opacity * 100;
        }
        UpdateOverlays();
    }

    private void BlendMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_activeLayer != null && CmbBlendMode.SelectedItem is ComboBoxItem item)
        {
            if (Enum.TryParse(item.Content?.ToString(), out LayerBlendMode mode))
            {
                _activeLayer.BlendMode = mode;
                RenderComposite();
            }
        }
    }

    private void LayerOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_activeLayer != null)
        {
            _activeLayer.Opacity = e.NewValue / 100.0;
            RenderComposite();
        }
    }

    private void ToggleVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is Layer l)
        {
            l.IsVisible = !l.IsVisible;
            RenderComposite();
        }
    }

    private void ToggleLock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is Layer l)
        {
            l.IsLocked = !l.IsLocked;
        }
    }

    private void AddLayer_Click(object sender, RoutedEventArgs e)
    {
        var newLayer = new Layer($"Layer {_layers.Count + 1}", _canvasWidth, _canvasHeight, LayerType.Raster);
        _layers.Add(newLayer);
        _activeLayer = newLayer;
        LayersListBox.SelectedItem = newLayer;
        CommitAction("New Layer");
    }

    private void DuplicateLayer_Click(object sender, RoutedEventArgs e)
    {
        if (_activeLayer != null)
        {
            var dup = _activeLayer.Clone();
            int index = _layers.IndexOf(_activeLayer);
            _layers.Insert(index + 1, dup);
            _activeLayer = dup;
            LayersListBox.SelectedItem = dup;
            CommitAction("Duplicate Layer");
        }
    }

    private void MergeDown_Click(object sender, RoutedEventArgs e)
    {
        if (_activeLayer == null || _layers.Count <= 1) return;
        int index = _layers.IndexOf(_activeLayer);
        if (index <= 0) return; // Cannot merge bottom layer down

        var bottom = _layers[index - 1];
        var top = _layers[index];

        var merged = ImageCompositor.RenderComposite(_canvasWidth, _canvasHeight, new[] { bottom, top });
        bottom.Bitmap = merged;
        bottom.UpdateThumbnail();

        _layers.Remove(top);
        _activeLayer = bottom;
        LayersListBox.SelectedItem = bottom;
        CommitAction("Merge Down");
    }

    private void DeleteLayer_Click(object sender, RoutedEventArgs e)
    {
        if (_activeLayer != null && _layers.Count > 1)
        {
            int index = _layers.IndexOf(_activeLayer);
            _layers.Remove(_activeLayer);
            _activeLayer = _layers[Math.Max(0, index - 1)];
            LayersListBox.SelectedItem = _activeLayer;
            CommitAction("Delete Layer");
        }
    }

    private void LayerThumb_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Layer layer)
        {
            layer.IsEditingMask = false;
            _activeLayer = layer;
            LayersListBox.SelectedItem = layer;
            TxtStatusMessage.Text = $"Selected Layer: {layer.Name} (Editing pixels)";
        }
    }

    private void MaskThumb_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Layer layer && layer.HasMask)
        {
            layer.IsEditingMask = true;
            _activeLayer = layer;
            LayersListBox.SelectedItem = layer;
            TxtStatusMessage.Text = $"Selected Mask: {layer.Name} (Black hides, White reveals)";
        }
    }

    private void AddMask_Click(object sender, RoutedEventArgs e)
    {
        if (_activeLayer == null) return;
        if (_activeLayer.HasMask)
        {
            TxtStatusMessage.Text = $"Layer '{_activeLayer.Name}' already has a mask.";
            return;
        }
        _activeLayer.AddMask(revealAll: true);
        _activeLayer.IsEditingMask = true;
        RenderComposite();
        CommitAction("Add Layer Mask");
        TxtStatusMessage.Text = $"Added Mask to '{_activeLayer.Name}'. Paint black to hide, white to reveal.";
    }

    private void RemoveMask_Click(object sender, RoutedEventArgs e)
    {
        if (_activeLayer == null || !_activeLayer.HasMask) return;
        _activeLayer.RemoveMask(apply: true);
        _activeLayer.IsEditingMask = false;
        RenderComposite();
        CommitAction("Apply Layer Mask");
        TxtStatusMessage.Text = $"Applied Mask to '{_activeLayer.Name}'.";
    }

    // =========================================================================
    // ADJUSTMENTS & FILTERS
    // =========================================================================
    private void Adjustments_Changed(object sender, RoutedEventArgs e)
    {
        if (_adjustments == null) return;
        _adjustments.Brightness = SliderBrightness?.Value ?? 0;
        _adjustments.Contrast = SliderContrast?.Value ?? 0;
        _adjustments.Exposure = SliderExposure?.Value ?? 0;
        _adjustments.Saturation = SliderSaturation?.Value ?? 0;
        _adjustments.Vibrance = SliderVibrance?.Value ?? 0;
        _adjustments.Warmth = SliderWarmth?.Value ?? 0;
        _adjustments.Tint = SliderTint?.Value ?? 0;
        _adjustments.Vignette = SliderVignette?.Value ?? 0;
        _adjustments.Grayscale = ChkGrayscale?.IsChecked == true;
        _adjustments.Invert = ChkInvert?.IsChecked == true;
        _adjustments.Sepia = ChkSepia?.IsChecked == true;

        RenderComposite();
    }

    private void ResetAdjustments_Click(object sender, RoutedEventArgs e)
    {
        _adjustments.Reset();
        if (SliderBrightness != null) SliderBrightness.Value = 0;
        if (SliderContrast != null) SliderContrast.Value = 0;
        if (SliderExposure != null) SliderExposure.Value = 0;
        if (SliderSaturation != null) SliderSaturation.Value = 0;
        if (SliderVibrance != null) SliderVibrance.Value = 0;
        if (SliderWarmth != null) SliderWarmth.Value = 0;
        if (SliderTint != null) SliderTint.Value = 0;
        if (SliderVignette != null) SliderVignette.Value = 0;
        if (ChkGrayscale != null) ChkGrayscale.IsChecked = false;
        if (ChkInvert != null) ChkInvert.IsChecked = false;
        if (ChkSepia != null) ChkSepia.IsChecked = false;

        CommitAction("Reset Adjustments");
    }

    private void FilterSharpen_Click(object sender, RoutedEventArgs e)
    {
        if (_activeLayer?.Bitmap != null && !_activeLayer.IsLocked)
        {
            ImageCompositor.ApplySharpen(_activeLayer.Bitmap);
            _activeLayer.UpdateThumbnail();
            CommitAction("Filter: Sharpen");
        }
    }

    private void FilterBlur_Click(object sender, RoutedEventArgs e)
    {
        if (_activeLayer?.Bitmap != null && !_activeLayer.IsLocked)
        {
            ImageCompositor.ApplyGaussianBlur(_activeLayer.Bitmap, 3);
            _activeLayer.UpdateThumbnail();
            CommitAction("Filter: Gaussian Blur");
        }
    }

    private void FilterEdgeDetect_Click(object sender, RoutedEventArgs e)
    {
        if (_activeLayer?.Bitmap != null && !_activeLayer.IsLocked)
        {
            ImageCompositor.ApplyEdgeDetect(_activeLayer.Bitmap);
            _activeLayer.UpdateThumbnail();
            CommitAction("Filter: Edge Detect");
        }
    }

    private void FilterEmboss_Click(object sender, RoutedEventArgs e)
    {
        if (_activeLayer?.Bitmap != null && !_activeLayer.IsLocked)
        {
            ImageCompositor.ApplyEmboss(_activeLayer.Bitmap);
            _activeLayer.UpdateThumbnail();
            CommitAction("Filter: Emboss");
        }
    }

    // =========================================================================
    // TAB NAVIGATION
    // =========================================================================
    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb)
        {
            string tag = rb.Tag?.ToString() ?? "Layers";
            PanelLayers.Visibility = tag == "Layers" ? Visibility.Visible : Visibility.Collapsed;
            PanelAdjust.Visibility = tag == "Adjust" ? Visibility.Visible : Visibility.Collapsed;
            PanelHistory.Visibility = tag == "History" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int index = HistoryListBox.SelectedIndex;
        if (index >= 0)
        {
            var state = _history.JumpTo(index);
            if (state != null)
            {
                ApplyHistoryState(state);
                TxtStatusMessage.Text = $"Jumped to step #{state.StepNumber}: {state.Description}";
            }
        }
    }

    private void ApplyHistoryState(HistoryState state)
    {
        _canvasWidth = state.CanvasWidth;
        _canvasHeight = state.CanvasHeight;
        _layers.Clear();
        foreach (var l in state.Layers) _layers.Add(l.Clone());

        _adjustments.Brightness = state.Adjustments.Brightness;
        _adjustments.Contrast = state.Adjustments.Contrast;
        _adjustments.Exposure = state.Adjustments.Exposure;
        _adjustments.Saturation = state.Adjustments.Saturation;
        _adjustments.Vibrance = state.Adjustments.Vibrance;
        _adjustments.Warmth = state.Adjustments.Warmth;
        _adjustments.Tint = state.Adjustments.Tint;
        _adjustments.Vignette = state.Adjustments.Vignette;
        _adjustments.Grayscale = state.Adjustments.Grayscale;
        _adjustments.Invert = state.Adjustments.Invert;
        _adjustments.Sepia = state.Adjustments.Sepia;
        SyncAdjustmentsUI();

        _activeLayer = _layers.Count > 0 ? _layers[^1] : null;
        LayersListBox.SelectedItem = _activeLayer;
        UpdateHistoryButtons();
        RenderComposite();
        UpdateDocInfo();
    }

    private void SyncAdjustmentsUI()
    {
        if (SliderBrightness != null) SliderBrightness.Value = _adjustments.Brightness;
        if (SliderContrast != null) SliderContrast.Value = _adjustments.Contrast;
        if (SliderExposure != null) SliderExposure.Value = _adjustments.Exposure;
        if (SliderSaturation != null) SliderSaturation.Value = _adjustments.Saturation;
        if (SliderVibrance != null) SliderVibrance.Value = _adjustments.Vibrance;
        if (SliderWarmth != null) SliderWarmth.Value = _adjustments.Warmth;
        if (SliderTint != null) SliderTint.Value = _adjustments.Tint;
        if (SliderVignette != null) SliderVignette.Value = _adjustments.Vignette;
        if (ChkGrayscale != null) ChkGrayscale.IsChecked = _adjustments.Grayscale;
        if (ChkInvert != null) ChkInvert.IsChecked = _adjustments.Invert;
        if (ChkSepia != null) ChkSepia.IsChecked = _adjustments.Sepia;
    }

    private void Snapshot_Click(object sender, RoutedEventArgs e)
    {
        RenderComposite();
        var thumb = CreateCanvasThumbnail();
        _history.PushState($"Snapshot {_history.StateCount}", _canvasWidth, _canvasHeight, _layers, _adjustments, thumb);
        UpdateHistoryButtons();
        TxtStatusMessage.Text = "Created History Snapshot";
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        RenderComposite();
        var thumb = CreateCanvasThumbnail();
        _history.ClearHistory(_canvasWidth, _canvasHeight, _layers, _adjustments, thumb);
        UpdateHistoryButtons();
        TxtStatusMessage.Text = "History cache cleared";
    }

    // =========================================================================
    // FILE ACTIONS (NEW, OPEN, EXPORT, DRAG & DROP)
    // =========================================================================
    private void NewCanvas_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new NewCanvasDialog { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _canvasWidth = dlg.CanvasWidth;
            _canvasHeight = dlg.CanvasHeight;
            _docName = $"Artwork-{DateTime.Now:HHmmss}.png";
            _layers.Clear();

            var baseLayer = new Layer("Background", _canvasWidth, _canvasHeight, LayerType.Raster);
            if (dlg.BackgroundType == "White")
            {
                var wb = baseLayer.Bitmap!;
                DrawingEngine.FloodFill(wb, 0, 0, Colors.White, 0);
            }
            else if (dlg.BackgroundType == "Dark")
            {
                var wb = baseLayer.Bitmap!;
                DrawingEngine.FloodFill(wb, 0, 0, Color.FromRgb(28, 28, 30), 0);
            }

            _layers.Add(baseLayer);
            _activeLayer = baseLayer;
            LayersListBox.SelectedItem = baseLayer;

            _adjustments.Reset();
            UpdateDocInfo();
            CommitAction("New Document");
        }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        SaveProject(saveAs: false);
    }

    private void SaveProject(bool saveAs)
    {
        if (saveAs || string.IsNullOrEmpty(_currentProjectPath))
        {
            var sfd = new SaveFileDialog
            {
                Title = "Save Multi-Layer Project",
                Filter = "Open Image Project (*.openimg)|*.openimg|All Files (*.*)|*.*",
                FileName = Path.GetFileNameWithoutExtension(_docName) + ".openimg"
            };

            if (sfd.ShowDialog() == true)
            {
                _currentProjectPath = sfd.FileName;
                _docName = Path.GetFileName(_currentProjectPath);
            }
            else
            {
                return;
            }
        }

        try
        {
            ProjectFileManager.SaveProject(_currentProjectPath, _canvasWidth, _canvasHeight, _layers, _adjustments, _docName);
            UpdateDocInfo();
            MessageBox.Show($"Project saved successfully to:\n{_currentProjectPath}", "Save Project", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Save failed: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadProjectFromFile(string path)
    {
        try
        {
            var (w, h, loadedLayers, adj, docName) = ProjectFileManager.LoadProject(path);

            _canvasWidth = w;
            _canvasHeight = h;
            _docName = Path.GetFileName(path);
            _currentProjectPath = path;

            _layers.Clear();
            foreach (var l in loadedLayers)
            {
                _layers.Add(l);
            }

            _activeLayer = _layers.Count > 0 ? _layers[^1] : null;
            LayersListBox.SelectedItem = _activeLayer;

            _adjustments.Brightness = adj.Brightness;
            _adjustments.Contrast = adj.Contrast;
            _adjustments.Exposure = adj.Exposure;
            _adjustments.Saturation = adj.Saturation;
            _adjustments.Vibrance = adj.Vibrance;
            _adjustments.Grayscale = adj.Grayscale;
            _adjustments.Invert = adj.Invert;
            _adjustments.Sepia = adj.Sepia;

            if (SliderBrightness != null) SliderBrightness.Value = adj.Brightness;
            if (SliderContrast != null) SliderContrast.Value = adj.Contrast;
            if (SliderExposure != null) SliderExposure.Value = adj.Exposure;
            if (SliderSaturation != null) SliderSaturation.Value = adj.Saturation;
            if (SliderVibrance != null) SliderVibrance.Value = adj.Vibrance;
            if (ChkGrayscale != null) ChkGrayscale.IsChecked = adj.Grayscale;
            if (ChkInvert != null) ChkInvert.IsChecked = adj.Invert;
            if (ChkSepia != null) ChkSepia.IsChecked = adj.Sepia;

            UpdateDocInfo();
            CommitAction("Open Project");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open project: {ex.Message}", "Open Project Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "All Supported (*.openimg;*.png;*.jpg;*.jpeg;*.bmp)|*.openimg;*.png;*.jpg;*.jpeg;*.bmp|Open Image Project (*.openimg)|*.openimg|Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*",
            Title = "Open Artwork or Project"
        };

        if (ofd.ShowDialog() == true)
        {
            string ext = Path.GetExtension(ofd.FileName).ToLower();
            if (ext == ".openimg")
            {
                LoadProjectFromFile(ofd.FileName);
            }
            else
            {
                LoadImageFromFile(ofd.FileName);
            }
        }
    }

    private void LoadImageFromFile(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();

            _canvasWidth = bmp.PixelWidth;
            _canvasHeight = bmp.PixelHeight;
            _docName = Path.GetFileName(path);
            _currentProjectPath = null;

            _layers.Clear();
            var layer = new Layer(_docName, _canvasWidth, _canvasHeight, LayerType.Raster)
            {
                Bitmap = new WriteableBitmap(bmp)
            };
            _layers.Add(layer);
            _activeLayer = layer;
            LayersListBox.SelectedItem = layer;

            _adjustments.Reset();
            UpdateDocInfo();
            CommitAction("Open Image");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open image: {ex.Message}", "Open Image Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ExportDialog(_canvasWidth, _canvasHeight) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            if (dlg.IsIconPack)
            {
                var ofd = new OpenFolderDialog
                {
                    Title = "Select Destination Folder for iOS AppIcon Pack",
                    Multiselect = false
                };

                if (ofd.ShowDialog() == true)
                {
                    try
                    {
                        string targetDir = Path.Combine(ofd.FolderName, "AppIcon.appiconset");
                        Directory.CreateDirectory(targetDir);

                        var iconSizes = new (string filename, int size)[]
                        {
                            ("AppIcon-1024.png", 1024),
                            ("AppIcon-180@3x.png", 180),
                            ("AppIcon-120@2x.png", 120),
                            ("AppIcon-87@3x.png", 87),
                            ("AppIcon-60@3x.png", 60),
                            ("AppIcon-167@2x.png", 167),
                            ("AppIcon-152@2x.png", 152),
                            ("AppIcon-76@1x.png", 76),
                            ("AppIcon-58@2x.png", 58),
                            ("AppIcon-40@2x.png", 40),
                            ("AppIcon-20@2x.png", 20)
                        };

                        foreach (var (filename, size) in iconSizes)
                        {
                            var iconComposite = ImageCompositor.RenderComposite(size, size, _layers, _adjustments, includeCheckerboard: false);
                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(iconComposite));
                            using var fileStream = File.Create(Path.Combine(targetDir, filename));
                            encoder.Save(fileStream);
                        }

                        string contentsJson = """
                        {
                          "images" : [
                            { "size" : "1024x1024", "idiom" : "ios-marketing", "filename" : "AppIcon-1024.png", "scale" : "1x" },
                            { "size" : "60x60", "idiom" : "iphone", "filename" : "AppIcon-180@3x.png", "scale" : "3x" },
                            { "size" : "60x60", "idiom" : "iphone", "filename" : "AppIcon-120@2x.png", "scale" : "2x" },
                            { "size" : "29x29", "idiom" : "iphone", "filename" : "AppIcon-87@3x.png", "scale" : "3x" },
                            { "size" : "20x20", "idiom" : "iphone", "filename" : "AppIcon-60@3x.png", "scale" : "3x" },
                            { "size" : "83.5x83.5", "idiom" : "ipad", "filename" : "AppIcon-167@2x.png", "scale" : "2x" },
                            { "size" : "76x76", "idiom" : "ipad", "filename" : "AppIcon-152@2x.png", "scale" : "2x" },
                            { "size" : "76x76", "idiom" : "ipad", "filename" : "AppIcon-76@1x.png", "scale" : "1x" },
                            { "size" : "29x29", "idiom" : "ipad", "filename" : "AppIcon-58@2x.png", "scale" : "2x" },
                            { "size" : "20x20", "idiom" : "ipad", "filename" : "AppIcon-40@2x.png", "scale" : "2x" }
                          ],
                          "info" : {
                            "author" : "xcode",
                            "version" : 1
                          }
                        }
                        """;
                        File.WriteAllText(Path.Combine(targetDir, "Contents.json"), contentsJson);

                        MessageBox.Show($"Complete iOS App Icon Pack successfully generated in:\n{targetDir}", "iOS Icon Pack Exported", MessageBoxButton.OK, MessageBoxImage.Information);
                        TxtStatusMessage.Text = $"Exported 11 iOS App Icons to {Path.GetFileName(targetDir)}";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Icon pack export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                return;
            }

            string ext = dlg.Format.ToLower() switch
            {
                "jpeg" => "jpg",
                "bmp" => "bmp",
                "tiff" => "tif",
                _ => "png"
            };

            var sfd = new SaveFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(_docName) + $"-export@{dlg.Scale}x." + ext,
                Filter = $"{dlg.Format} Image|*.{ext}|All Files|*.*",
                Title = "Export Artwork"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    double w = _canvasWidth * dlg.Scale;
                    double h = _canvasHeight * dlg.Scale;
                    var finalComposite = ImageCompositor.RenderComposite(w, h, _layers, _adjustments, includeCheckerboard: dlg.Format != "PNG");

                    BitmapEncoder encoder = dlg.Format switch
                    {
                        "JPEG" => new JpegBitmapEncoder { QualityLevel = dlg.Quality },
                        "BMP" => new BmpBitmapEncoder(),
                        "TIFF" => new TiffBitmapEncoder(),
                        _ => new PngBitmapEncoder()
                    };

                    encoder.Frames.Add(BitmapFrame.Create(finalComposite));
                    using var stream = File.Create(sfd.FileName);
                    encoder.Save(stream);

                    MessageBox.Show($"Artwork successfully exported at {dlg.Scale}x ({w:0} × {h:0} px)!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    TxtStatusMessage.Text = $"Exported {Path.GetFileName(sfd.FileName)} ({w:0}×{h:0} px)";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    // Windows Explorer Drag & Drop
    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && !string.IsNullOrEmpty(files[0]))
            {
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext == ".openimg")
                {
                    LoadProjectFromFile(files[0]);
                }
                else
                {
                    LoadImageFromFile(files[0]);
                }
            }
        }
    }

    // Undo & Redo Handlers
    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        var state = _history.Undo();
        if (state != null)
        {
            ApplyHistoryState(state);
            TxtStatusMessage.Text = $"Undo: {state.Description}";
        }
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        var state = _history.Redo();
        if (state != null)
        {
            ApplyHistoryState(state);
            TxtStatusMessage.Text = $"Redo: {state.Description}";
        }
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        // Toggle Day / Dark theme
        if (Background == (SolidColorBrush)FindResource("ThemeBackgroundBrush"))
        {
            Background = new SolidColorBrush(Color.FromRgb(18, 18, 20));
        }
        else
        {
            Background = (SolidColorBrush)FindResource("ThemeBackgroundBrush");
        }
    }

    private void CropRatio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string tag)
        {
            double ratio = tag switch
            {
                "1:1" => 1.0,
                "16:9" => 16.0 / 9.0,
                "4:3" => 4.0 / 3.0,
                _ => 1.0
            };

            double targetW = _canvasWidth * 0.75;
            double targetH = targetW / ratio;

            if (targetH > _canvasHeight * 0.85)
            {
                targetH = _canvasHeight * 0.75;
                targetW = targetH * ratio;
            }

            double x = (_canvasWidth - targetW) / 2;
            double y = (_canvasHeight - targetH) / 2;
            _cropRect = new Rect(x, y, targetW, targetH);
            UpdateOverlays();
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        ToolMarquee.IsChecked = true;
        Tool_Changed(ToolMarquee, null!);
        _selectionRect = new Rect(0, 0, _canvasWidth, _canvasHeight);
        _hasSelection = true;
        UpdateOverlays();
    }

    private void CopySelection_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard();
    }

    private void CopyToClipboard()
    {
        try
        {
            if (_hasSelection && !_selectionRect.IsEmpty && _activeLayer != null && _activeLayer.Bitmap != null)
            {
                int localX = Math.Max(0, (int)(_selectionRect.X - _activeLayer.X));
                int localY = Math.Max(0, (int)(_selectionRect.Y - _activeLayer.Y));
                int w = Math.Min(_activeLayer.Bitmap.PixelWidth - localX, (int)_selectionRect.Width);
                int h = Math.Min(_activeLayer.Bitmap.PixelHeight - localY, (int)_selectionRect.Height);

                if (w > 0 && h > 0)
                {
                    var cropped = new CroppedBitmap(_activeLayer.Bitmap, new Int32Rect(localX, localY, w, h));
                    Clipboard.SetImage(cropped);
                }
            }
            else if (_activeLayer?.Bitmap != null)
            {
                Clipboard.SetImage(_activeLayer.Bitmap);
            }
            else
            {
                var comp = ImageCompositor.RenderComposite(_canvasWidth, _canvasHeight, _layers, _adjustments, includeCheckerboard: true);
                Clipboard.SetImage(comp);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Clipboard copy failed: {ex.Message}", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PasteFromClipboard()
    {
        try
        {
            if (Clipboard.ContainsImage())
            {
                var src = Clipboard.GetImage();
                if (src != null)
                {
                    var wb = new WriteableBitmap(src);
                    string name = $"Pasted Layer {_layers.Count + 1}";
                    var layer = new Layer(name, wb.PixelWidth, wb.PixelHeight, LayerType.Raster)
                    {
                        Bitmap = wb,
                        X = Math.Max(0, (_canvasWidth - wb.PixelWidth) / 2),
                        Y = Math.Max(0, (_canvasHeight - wb.PixelHeight) / 2)
                    };
                    _layers.Add(layer);
                    _activeLayer = layer;
                    LayersListBox.SelectedItem = layer;
                    CommitAction("Paste Image");
                }
            }
            else if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                if (files.Count > 0 && !string.IsNullOrEmpty(files[0]) && File.Exists(files[0]))
                {
                    LoadImageFromFile(files[0]!);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Clipboard paste failed: {ex.Message}", "Paste Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CutToClipboard()
    {
        CopyToClipboard();
        if (_hasSelection)
        {
            DeleteSelection_Click(null!, null!);
        }
        else if (_activeLayer != null)
        {
            DeleteLayer_Click(null!, null!);
        }
    }

    private void ApplyCrop_Click(object sender, RoutedEventArgs e)
    {
        if (_cropRect.Width > 10 && _cropRect.Height > 10)
        {
            int newW = (int)_cropRect.Width;
            int newH = (int)_cropRect.Height;
            double offsetX = _cropRect.X;
            double offsetY = _cropRect.Y;

            foreach (var l in _layers)
            {
                l.X -= offsetX;
                l.Y -= offsetY;
            }

            _canvasWidth = newW;
            _canvasHeight = newH;
            _cropRect = Rect.Empty;
            UpdateDocInfo();
            ToolSelect.IsChecked = true;
            Tool_Changed(ToolSelect, null!);
            CommitAction("Crop Canvas");
        }
    }

    private void CancelCrop_Click(object sender, RoutedEventArgs e)
    {
        _cropRect = Rect.Empty;
        ToolSelect.IsChecked = true;
        Tool_Changed(ToolSelect, null!);
        UpdateOverlays();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        _hasSelection = false;
        _selectionRect = Rect.Empty;
        UpdateOverlays();
    }

    private unsafe void DeleteSelection_Click(object sender, RoutedEventArgs e)
    {
        if (_hasSelection && _activeLayer != null && _activeLayer.Bitmap != null && !_activeLayer.IsLocked)
        {
            var bmp = _activeLayer.Bitmap;
            int localMinX = Math.Max(0, (int)(_selectionRect.X - _activeLayer.X));
            int localMinY = Math.Max(0, (int)(_selectionRect.Y - _activeLayer.Y));
            int localMaxX = Math.Min(bmp.PixelWidth, (int)(_selectionRect.Right - _activeLayer.X));
            int localMaxY = Math.Min(bmp.PixelHeight, (int)(_selectionRect.Bottom - _activeLayer.Y));

            if (localMaxX > localMinX && localMaxY > localMinY)
            {
                bmp.Lock();
                try
                {
                    int stride = bmp.BackBufferStride;
                    byte* buf = (byte*)bmp.BackBuffer.ToPointer();
                    for (int y = localMinY; y < localMaxY; y++)
                    {
                        byte* row = buf + (y * stride);
                        for (int x = localMinX; x < localMaxX; x++)
                        {
                            byte* p = row + (x * 4);
                            p[0] = 0;
                            p[1] = 0;
                            p[2] = 0;
                            p[3] = 0;
                        }
                    }
                    bmp.AddDirtyRect(new Int32Rect(localMinX, localMinY, localMaxX - localMinX, localMaxY - localMinY));
                }
                finally
                {
                    bmp.Unlock();
                }

                _activeLayer.UpdateThumbnail();
                _hasSelection = false;
                _selectionRect = Rect.Empty;
                CommitAction("Clear Selection Area");
            }
        }
    }

    // =========================================================================
    // KEYBOARD SHORTCUTS
    // =========================================================================
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) Redo_Click(null!, null!);
            else Undo_Click(null!, null!);
            e.Handled = true;
        }
        else if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            Redo_Click(null!, null!);
            e.Handled = true;
        }
        else if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            PasteFromClipboard();
            e.Handled = true;
        }
        else if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            CopyToClipboard();
            e.Handled = true;
        }
        else if (e.Key == Key.X && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            CutToClipboard();
            e.Handled = true;
        }
        else if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SelectAll_Click(null!, null!);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ClearSelection_Click(null!, null!);
            e.Handled = true;
        }
        else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            bool saveAs = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            SaveProject(saveAs);
            e.Handled = true;
        }
        else if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            NewCanvas_Click(null!, null!);
            e.Handled = true;
        }
        else if (e.Key == Key.O && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            OpenFile_Click(null!, null!);
            e.Handled = true;
        }
        else if (e.Key == Key.E && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            Export_Click(null!, null!);
            e.Handled = true;
        }
        else if (e.Key == Key.J && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            DuplicateLayer_Click(null!, null!);
            e.Handled = true;
        }
        else if (e.Key == Key.V) { ToolSelect.IsChecked = true; Tool_Changed(ToolSelect, null!); }
        else if (e.Key == Key.M) { ToolMarquee.IsChecked = true; Tool_Changed(ToolMarquee, null!); }
        else if (e.Key == Key.C) { ToolCrop.IsChecked = true; Tool_Changed(ToolCrop, null!); }
        else if (e.Key == Key.B) { ToolBrush.IsChecked = true; Tool_Changed(ToolBrush, null!); }
        else if (e.Key == Key.E) { ToolEraser.IsChecked = true; Tool_Changed(ToolEraser, null!); }
        else if (e.Key == Key.S) { ToolCloneStamp.IsChecked = true; Tool_Changed(ToolCloneStamp, null!); }
        else if (e.Key == Key.G) { ToolGradient.IsChecked = true; Tool_Changed(ToolGradient, null!); }
        else if (e.Key == Key.K) { ToolFill.IsChecked = true; Tool_Changed(ToolFill, null!); }
        else if (e.Key == Key.T) { ToolText.IsChecked = true; Tool_Changed(ToolText, null!); }
        else if (e.Key == Key.U) { ToolShape.IsChecked = true; Tool_Changed(ToolShape, null!); }
        else if (e.Key == Key.I) { ToolEyedropper.IsChecked = true; Tool_Changed(ToolEyedropper, null!); }
        else if (e.Key == Key.X) { ColorChip_MouseDown(null!, null!); }
        else if (e.Key == Key.D)
        {
            _primaryColor = Colors.Black;
            _secondaryColor = Colors.White;
            _gradientColor1 = _primaryColor;
            _gradientColor2 = _secondaryColor;
            if (ChipPrimary != null) ChipPrimary.Background = new SolidColorBrush(_primaryColor);
            if (ChipSecondary != null) ChipSecondary.Background = new SolidColorBrush(_secondaryColor);
            if (ActiveColorIndicator != null) ActiveColorIndicator.Background = new SolidColorBrush(_primaryColor);
            UpdateGradientPreview();
            TxtStatusMessage.Text = "Colors reset to Default Black / White";
        }
        else if (e.Key == Key.Delete)
        {
            if (_hasSelection)
            {
                DeleteSelection_Click(null!, null!);
            }
            else
            {
                DeleteLayer_Click(null!, null!);
            }
            e.Handled = true;
        }
    }
}