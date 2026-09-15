namespace OpenImage.Models;

public enum ToolType
{
    Select,     // V (Move / Transform)
    Marquee,    // M (Rectangular Selection)
    Lasso,      // L (Freehand Selection)
    Crop,       // C (Crop & Straighten)
    Brush,      // B (Painting Brush)
    Eraser,     // E (Pixel Eraser)
    Fill,       // G (Paint Bucket / Flood Fill)
    Text,       // T (Vector Text)
    Shape,      // U (Vector Shape)
    Eyedropper  // I (Color Picker)
}

public enum LayerType
{
    Raster,
    Text,
    Shape
}

public enum LayerBlendMode
{
    Normal,
    Multiply,
    Screen,
    Overlay,
    Darken,
    Lighten,
    Difference,
    ColorDodge
}

public enum ShapeType
{
    Rectangle,
    Ellipse,
    Line
}
