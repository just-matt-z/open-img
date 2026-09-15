import { DEFAULT_ADJUSTMENTS } from './constants';

let layerIdCounter = 1;

/**
 * Generates a unique ID for a layer
 */
export function generateLayerId() {
  return `layer_${Date.now()}_${layerIdCounter++}`;
}

/**
 * Creates an Offscreen / HTMLCanvas with given dimensions
 */
export function createCanvas(width, height) {
  const canvas = document.createElement('canvas');
  canvas.width = Math.max(1, Math.round(width));
  canvas.height = Math.max(1, Math.round(height));
  return canvas;
}

/**
 * Generates a 64x64 data URL thumbnail for a layer
 */
export function generateLayerThumbnail(layer) {
  const thumbCanvas = document.createElement('canvas');
  thumbCanvas.width = 64;
  thumbCanvas.height = 64;
  const ctx = thumbCanvas.getContext('2d');
  ctx.imageSmoothingEnabled = true;

  if (layer.canvas) {
    ctx.drawImage(layer.canvas, 0, 0, 64, 64);
  } else if (layer.type === 'text') {
    ctx.fillStyle = '#f2f2f7';
    ctx.fillRect(0, 0, 64, 64);
    ctx.fillStyle = layer.color || '#007aff';
    ctx.font = 'bold 36px sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText('T', 32, 34);
  } else if (layer.type === 'shape') {
    ctx.fillStyle = '#f2f2f7';
    ctx.fillRect(0, 0, 64, 64);
    ctx.strokeStyle = layer.strokeColor || '#007aff';
    ctx.fillStyle = layer.fillColor || 'rgba(0,122,255,0.2)';
    ctx.lineWidth = 3;
    if (layer.shapeType === 'ellipse') {
      ctx.beginPath();
      ctx.arc(32, 32, 20, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
    } else {
      ctx.fillRect(14, 14, 36, 36);
      ctx.strokeRect(14, 14, 36, 36);
    }
  }

  return thumbCanvas.toDataURL();
}

/**
 * Creates a raster (image/drawing) layer
 */
export function createRasterLayer(name, width, height, initialSource = null) {
  const canvas = createCanvas(width, height);
  const ctx = canvas.getContext('2d');

  if (initialSource) {
    ctx.drawImage(initialSource, 0, 0, width, height);
  }

  const layer = {
    id: generateLayerId(),
    name: name || 'Layer 1',
    type: 'raster',
    visible: true,
    locked: false,
    opacity: 1.0,
    blendMode: 'source-over',
    x: 0,
    y: 0,
    width,
    height,
    rotation: 0,
    canvas,
    adjustments: { ...DEFAULT_ADJUSTMENTS },
    thumbnail: null
  };

  layer.thumbnail = generateLayerThumbnail(layer);
  return layer;
}

/**
 * Creates a text layer
 */
export function createTextLayer(name, text, x, y, options = {}) {
  const layer = {
    id: generateLayerId(),
    name: name || 'Text Layer',
    type: 'text',
    visible: true,
    locked: false,
    opacity: 1.0,
    blendMode: 'source-over',
    x,
    y,
    width: options.width || 320,
    height: options.height || 80,
    rotation: 0,
    text: text || 'Double click to edit',
    fontFamily: options.fontFamily || '-apple-system, Inter, sans-serif',
    fontSize: options.fontSize || 42,
    fontWeight: options.fontWeight || '600',
    color: options.color || '#000000',
    align: options.align || 'left',
    adjustments: { ...DEFAULT_ADJUSTMENTS },
    thumbnail: null
  };

  layer.thumbnail = generateLayerThumbnail(layer);
  return layer;
}

/**
 * Creates a vector shape layer
 */
export function createShapeLayer(name, shapeType, x, y, width, height, options = {}) {
  const layer = {
    id: generateLayerId(),
    name: name || `${shapeType.charAt(0).toUpperCase() + shapeType.slice(1)} 1`,
    type: 'shape',
    shapeType: shapeType || 'rectangle', // 'rectangle' | 'ellipse' | 'line' | 'arrow'
    visible: true,
    locked: false,
    opacity: 1.0,
    blendMode: 'source-over',
    x,
    y,
    width: Math.max(10, width),
    height: Math.max(10, height),
    rotation: 0,
    fillColor: options.fillColor || '#007aff',
    strokeColor: options.strokeColor || '#0056b3',
    strokeWidth: options.strokeWidth || 0,
    borderRadius: options.borderRadius || 12,
    adjustments: { ...DEFAULT_ADJUSTMENTS },
    thumbnail: null
  };

  layer.thumbnail = generateLayerThumbnail(layer);
  return layer;
}

/**
 * Deep clones a layer (copying its internal canvas bitmap)
 */
export function cloneLayer(layer) {
  const cloned = { ...layer, id: generateLayerId(), name: `${layer.name} Copy` };

  if (layer.canvas) {
    const newCanvas = createCanvas(layer.canvas.width, layer.canvas.height);
    const ctx = newCanvas.getContext('2d');
    ctx.drawImage(layer.canvas, 0, 0);
    cloned.canvas = newCanvas;
  }

  cloned.adjustments = { ...layer.adjustments };
  cloned.thumbnail = generateLayerThumbnail(cloned);
  return cloned;
}
