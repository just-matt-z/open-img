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

        _history.PushState("Initial Artwork", _canvasWidth, _canvasHeight, _layers, _adjustments);
        UpdateHistoryButtons();
        RenderComposite();
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
    }

    private void CommitAction(string description)
    {
        _history.PushState(description, _canvasWidth, _canvasHeight, _layers, _adjustments);
        UpdateHistoryButtons();
        RenderComposite();
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
            if (_activeLayer != null && _activeLayer.Type == LayerType.Raster && !_activeLayer.IsLocked && _activeLayer.Bitmap != null)
            {
                int localX = (int)(pt.X - _activeLayer.X);
                int localY = (int)(pt.Y - _activeLayer.Y);
                int size = (int)SliderBrushSize.Value;
                double opacity = SliderBrushOpacity.Value / 100.0;
                bool isEraser = _activeTool == ToolType.Eraser;

                DrawingEngine.DrawBrushStamp(_activeLayer.Bitmap, localX, localY, size / 2, _primaryColor, opacity, isEraser);
                RenderComposite();
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
            if (_activeLayer != null && _activeLayer.Type == LayerType.Raster && !_activeLayer.IsLocked && _activeLayer.Bitmap != null)
            {
                int localX = (int)(pt.X - _activeLayer.X);
                int localY = (int)(pt.Y - _activeLayer.Y);
                DrawingEngine.FloodFill(_activeLayer.Bitmap, localX, localY, _primaryColor, 32);
                _activeLayer.UpdateThumbnail();
                CommitAction("Flood Fill");
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
        else if ((_activeTool == ToolType.Brush || _activeTool == ToolType.Eraser) && _activeLayer != null && _activeLayer.Bitmap != null)
        {
            Point localLast = new Point(_lastPoint.X - _activeLayer.X, _lastPoint.Y - _activeLayer.Y);
            Point localCurrent = new Point(pt.X - _activeLayer.X, pt.Y - _activeLayer.Y);
            int size = (int)SliderBrushSize.Value;
            double opacity = SliderBrushOpacity.Value / 100.0;
            bool isEraser = _activeTool == ToolType.Eraser;

            DrawingEngine.DrawBrushLine(_activeLayer.Bitmap, localLast, localCurrent, size / 2, _primaryColor, opacity, isEraser);
            _lastPoint = pt;
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
            _activeLayer?.UpdateThumbnail();
            CommitAction(_activeTool == ToolType.Brush ? "Brush Stroke" : "Eraser");
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

            if (tool == ToolType.Crop && (_cropRect.IsEmpty || _cropRect.Width == 0))
            {
                _cropRect = new Rect(0.1 * _canvasWidth, 0.1 * _canvasHeight, 0.8 * _canvasWidth, 0.8 * _canvasHeight);
            }
            UpdateOverlays();
        }
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

    private void PrimaryChip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var dlg = new ColorPickerDialog(_primaryColor) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _primaryColor = dlg.SelectedColor;
            ChipPrimary.Background = new SolidColorBrush(_primaryColor);
            if (ActiveColorIndicator != null) ActiveColorIndicator.Background = new SolidColorBrush(_primaryColor);
        }
    }

    private void SecondaryChip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var dlg = new ColorPickerDialog(_secondaryColor) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _secondaryColor = dlg.SelectedColor;
            ChipSecondary.Background = new SolidColorBrush(_secondaryColor);
        }
    }

    private void ColorChip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Swap primary and secondary colors (shortcut X)
        var temp = _primaryColor;
        _primaryColor = _secondaryColor;
        _secondaryColor = temp;
        ChipPrimary.Background = new SolidColorBrush(_primaryColor);
        ChipSecondary.Background = new SolidColorBrush(_secondaryColor);
        if (ActiveColorIndicator != null) ActiveColorIndicator.Background = new SolidColorBrush(_primaryColor);
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
        if (ChkGrayscale != null) ChkGrayscale.IsChecked = false;
        if (ChkInvert != null) ChkInvert.IsChecked = false;
        if (ChkSepia != null) ChkSepia.IsChecked = false;

        CommitAction("Reset Adjustments");
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
                _canvasWidth = state.CanvasWidth;
                _canvasHeight = state.CanvasHeight;
                _layers.Clear();
                foreach (var l in state.Layers) _layers.Add(l.Clone());
                _adjustments.Brightness = state.Adjustments.Brightness;
                _adjustments.Contrast = state.Adjustments.Contrast;
                _adjustments.Saturation = state.Adjustments.Saturation;

                _activeLayer = _layers.Count > 0 ? _layers[^1] : null;
                LayersListBox.SelectedItem = _activeLayer;
                UpdateHistoryButtons();
                RenderComposite();
            }
        }
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

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*",
            Title = "Open Artwork"
        };

        if (ofd.ShowDialog() == true)
        {
            LoadImageFromFile(ofd.FileName);
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
            string ext = dlg.Format.ToLower() switch
            {
                "jpeg" => "jpg",
                "bmp" => "bmp",
                _ => "png"
            };

            var sfd = new SaveFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(_docName) + "-export." + ext,
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
                        _ => new PngBitmapEncoder()
                    };

                    encoder.Frames.Add(BitmapFrame.Create(finalComposite));
                    using var stream = File.Create(sfd.FileName);
                    encoder.Save(stream);

                    MessageBox.Show("Artwork successfully exported!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
            if (files != null && files.Length > 0)
            {
                LoadImageFromFile(files[0]);
            }
        }
    }

    // Undo & Redo Handlers
    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        var state = _history.Undo();
        if (state != null)
        {
            _canvasWidth = state.CanvasWidth;
            _canvasHeight = state.CanvasHeight;
            _layers.Clear();
            foreach (var l in state.Layers) _layers.Add(l.Clone());
            _activeLayer = _layers.Count > 0 ? _layers[^1] : null;
            LayersListBox.SelectedItem = _activeLayer;
            UpdateHistoryButtons();
            RenderComposite();
        }
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        var state = _history.Redo();
        if (state != null)
        {
            _canvasWidth = state.CanvasWidth;
            _canvasHeight = state.CanvasHeight;
            _layers.Clear();
            foreach (var l in state.Layers) _layers.Add(l.Clone());
            _activeLayer = _layers.Count > 0 ? _layers[^1] : null;
            LayersListBox.SelectedItem = _activeLayer;
            UpdateHistoryButtons();
            RenderComposite();
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
        else if (e.Key == Key.G) { ToolFill.IsChecked = true; Tool_Changed(ToolFill, null!); }
        else if (e.Key == Key.T) { ToolText.IsChecked = true; Tool_Changed(ToolText, null!); }
        else if (e.Key == Key.U) { ToolShape.IsChecked = true; Tool_Changed(ToolShape, null!); }
        else if (e.Key == Key.I) { ToolEyedropper.IsChecked = true; Tool_Changed(ToolEyedropper, null!); }
        else if (e.Key == Key.X) { ColorChip_MouseDown(null!, null!); }
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