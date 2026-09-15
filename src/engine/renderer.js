import { buildFilterString } from './colorUtils';

/**
 * Transforms screen coordinates (from mouse event) into document canvas space
 */
export function screenToCanvasCoords(clientX, clientY, canvasEl, zoom, panX, panY) {
  if (!canvasEl) return { x: 0, y: 0 };
  const rect = canvasEl.getBoundingClientRect();
  
  // Calculate relative to canvas element top-left
  const x = (clientX - rect.left) / zoom;
  const y = (clientY - rect.top) / zoom;

  return {
    x: Math.round(x),
    y: Math.round(y)
  };
}

/**
 * Composite rendering engine: renders all layers onto target canvas
 */
export function renderComposite(targetCanvas, layers = [], globalAdjustments = {}, options = {}) {
  if (!targetCanvas) return;
  const ctx = targetCanvas.getContext('2d');
  const width = targetCanvas.width;
  const height = targetCanvas.height;

  ctx.save();
  ctx.clearRect(0, 0, width, height);

  // Optional background fill (e.g. solid white or transparent)
  if (options.background && options.background !== 'transparent') {
    ctx.fillStyle = options.background;
    ctx.fillRect(0, 0, width, height);
  }

  // Render each visible layer in sequence (bottom to top)
  for (let i = 0; i < layers.length; i++) {
    const layer = layers[i];
    if (!layer.visible || layer.opacity <= 0) continue;

    ctx.save();
    ctx.globalAlpha = Math.max(0, Math.min(1, layer.opacity));
    ctx.globalCompositeOperation = layer.blendMode || 'source-over';

    // Position and rotation transform
    const centerX = layer.x + layer.width / 2;
    const centerY = layer.y + layer.height / 2;

    if (layer.rotation && layer.rotation !== 0) {
      ctx.translate(centerX, centerY);
      ctx.rotate((layer.rotation * Math.PI) / 180);
      ctx.translate(-centerX, -centerY);
    }

    // Apply adjustments filter
    const combinedAdj = { ...(layer.adjustments || {}), ...globalAdjustments };
    const filterStr = buildFilterString(combinedAdj);
    if (filterStr && filterStr !== 'none') {
      ctx.filter = filterStr;
    }

    // Render based on layer type
    if (layer.type === 'raster' && layer.canvas) {
      ctx.drawImage(layer.canvas, layer.x, layer.y, layer.width, layer.height);
    } else if (layer.type === 'text') {
      ctx.font = `${layer.fontWeight || 'normal'} ${layer.fontSize || 32}px ${layer.fontFamily || '-apple-system, sans-serif'}`;
      ctx.fillStyle = layer.color || '#000000';
      ctx.textAlign = layer.align || 'left';
      ctx.textBaseline = 'top';
      ctx.fillText(layer.text || '', layer.x, layer.y);
    } else if (layer.type === 'shape') {
      renderVectorShape(ctx, layer);
    }

    ctx.restore();
  }

  ctx.restore();
}

/**
 * Vector shape renderer
 */
function renderVectorShape(ctx, layer) {
  const { x, y, width, height, shapeType, fillColor, strokeColor, strokeWidth, borderRadius } = layer;

  ctx.beginPath();
  if (shapeType === 'ellipse') {
    ctx.ellipse(x + width / 2, y + height / 2, width / 2, height / 2, 0, 0, Math.PI * 2);
  } else if (shapeType === 'line') {
    ctx.moveTo(x, y);
    ctx.lineTo(x + width, y + height);
  } else if (shapeType === 'arrow') {
    const headLength = Math.min(24, width * 0.25);
    const endX = x + width;
    const endY = y + height;
    const angle = Math.atan2(height, width);
    ctx.moveTo(x, y);
    ctx.lineTo(endX, endY);
    ctx.lineTo(endX - headLength * Math.cos(angle - Math.PI / 6), endY - headLength * Math.sin(angle - Math.PI / 6));
    ctx.moveTo(endX, endY);
    ctx.lineTo(endX - headLength * Math.cos(angle + Math.PI / 6), endY - headLength * Math.sin(angle + Math.PI / 6));
  } else {
    // Rounded Rectangle / Rectangle
    const r = Math.min(borderRadius || 0, width / 2, height / 2);
    if (r > 0 && ctx.roundRect) {
      ctx.roundRect(x, y, width, height, r);
    } else {
      ctx.rect(x, y, width, height);
    }
  }

  if (fillColor && fillColor !== 'transparent' && shapeType !== 'line' && shapeType !== 'arrow') {
    ctx.fillStyle = fillColor;
    ctx.fill();
  }

  if (strokeWidth > 0 && strokeColor && strokeColor !== 'transparent') {
    ctx.strokeStyle = strokeColor;
    ctx.lineWidth = strokeWidth;
    ctx.stroke();
  }
}
