export const TOOLS = {
  SELECT: 'select',       // V (Move / Transform)
  MARQUEE: 'marquee',     // M (Rectangular & Elliptical Marquee)
  LASSO: 'lasso',         // L (Freehand selection)
  CROP: 'crop',           // C (Crop & Straighten)
  BRUSH: 'brush',         // B (Painting Brush)
  ERASER: 'eraser',       // E (Pixel Eraser)
  FILL: 'fill',           // G (Paint Bucket / Flood Fill)
  TEXT: 'text',           // T (Vector Text Layer)
  SHAPE: 'shape',         // U (Vector Shapes: Rectangle, Ellipse, Line, Arrow)
  EYEDROPPER: 'eyedropper'// I (Color Sampler & Loupe)
};

export const BLEND_MODES = [
  { id: 'source-over', label: 'Normal' },
  { id: 'multiply', label: 'Multiply' },
  { id: 'screen', label: 'Screen' },
  { id: 'overlay', label: 'Overlay' },
  { id: 'darken', label: 'Darken' },
  { id: 'lighten', label: 'Lighten' },
  { id: 'color-dodge', label: 'Color Dodge' },
  { id: 'color-burn', label: 'Color Burn' },
  { id: 'hard-light', label: 'Hard Light' },
  { id: 'soft-light', label: 'Soft Light' },
  { id: 'difference', label: 'Difference' },
  { id: 'exclusion', label: 'Exclusion' },
  { id: 'hue', label: 'Hue' },
  { id: 'saturation', label: 'Saturation' },
  { id: 'color', label: 'Color' },
  { id: 'luminosity', label: 'Luminosity' }
];

export const CANVAS_PRESETS = [
  { name: 'Full HD (1080p)', width: 1920, height: 1080 },
  { name: '4K Ultra HD', width: 3840, height: 2160 },
  { name: 'Instagram Square (1:1)', width: 1080, height: 1080 },
  { name: 'Instagram Story (9:16)', width: 1080, height: 1920 },
  { name: 'Desktop Wallpaper', width: 2560, height: 1440 },
  { name: 'Photo Print (4x6)', width: 1800, height: 1200 },
  { name: 'Standard Web (1200x800)', width: 1200, height: 800 }
];

export const DEFAULT_ADJUSTMENTS = {
  brightness: 0,   // -100 to 100
  contrast: 0,     // -100 to 100
  exposure: 0,     // -100 to 100
  saturation: 0,   // -100 to 100
  vibrance: 0,     // -100 to 100
  hue: 0,          // -180 to 180
  temperature: 0,  // -100 to 100 (warm/cool)
  tint: 0,         // -100 to 100 (magenta/green)
  blur: 0,         // 0 to 50
  sharpen: 0,      // 0 to 100
  invert: false,
  grayscale: false,
  sepia: false
};

export const IOS_SWATCHES = [
  '#007AFF', // System Blue
  '#5AC8FA', // Sky Teal
  '#4CD964', // System Green
  '#FF9500', // System Orange
  '#FF3B30', // System Red
  '#FF2D55', // System Pink
  '#5856D6', // System Purple
  '#FFCC00', // System Yellow
  '#000000', // Black
  '#8E8E93', // System Gray
  '#C7C7CC', // Light Gray
  '#FFFFFF'  // Pure White
];
