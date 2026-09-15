import { renderComposite } from './renderer';
import { createCanvas, createRasterLayer, createTextLayer, createShapeLayer } from './layer';

/**
 * Exports the composite canvas to an image file (PNG, JPEG, WebP)
 */
export function exportCompositeImage(canvasWidth, canvasHeight, layers, globalAdjustments, options = {}) {
  const format = options.format || 'png';
  const quality = options.quality !== undefined ? options.quality : 0.92;
  const scale = options.scale || 1.0;

  const exportCanvas = createCanvas(canvasWidth * scale, canvasHeight * scale);
  const ctx = exportCanvas.getContext('2d');
  
  if (scale !== 1.0) {
    ctx.scale(scale, scale);
  }

  // Composite all layers
  renderComposite(exportCanvas, layers, globalAdjustments, {
    background: format === 'jpeg' ? '#ffffff' : 'transparent'
  });

  const mimeType = `image/${format}`;
  const dataUrl = exportCanvas.toDataURL(mimeType, quality);

  // Trigger download
  const link = document.createElement('a');
  link.download = `open-img-${Date.now()}.${format}`;
  link.href = dataUrl;
  link.click();
}

/**
 * Exports the complete document project to a `.openimg` JSON file
 */
export function exportProjectFile(canvasWidth, canvasHeight, layers, globalAdjustments) {
  const projectData = {
    app: 'Open Image',
    version: '1.0.0',
    timestamp: new Date().toISOString(),
    canvasWidth,
    canvasHeight,
    globalAdjustments,
    layers: layers.map(layer => {
      const serializable = { ...layer };
      if (layer.canvas) {
        serializable.bitmapData = layer.canvas.toDataURL('image/png');
        delete serializable.canvas;
      }
      return serializable;
    })
  };

  const jsonStr = JSON.stringify(projectData);
  const blob = new Blob([jsonStr], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  
  const link = document.createElement('a');
  link.download = `project-${Date.now()}.openimg`;
  link.href = url;
  link.click();
  URL.revokeObjectURL(url);
}

/**
 * Imports a `.openimg` JSON file and recreates layers
 */
export async function importProjectFile(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = async (e) => {
      try {
        const data = JSON.parse(e.target.result);
        const restoredLayers = [];

        for (const item of data.layers) {
          if (item.type === 'raster') {
            const canvas = createCanvas(item.width, item.height);
            if (item.bitmapData) {
              await new Promise((resImg) => {
                const img = new Image();
                img.onload = () => {
                  canvas.getContext('2d').drawImage(img, 0, 0);
                  resImg();
                };
                img.src = item.bitmapData;
              });
            }
            restoredLayers.push({
              ...item,
              canvas,
              adjustments: item.adjustments || {}
            });
          } else if (item.type === 'text') {
            restoredLayers.push(createTextLayer(item.name, item.text, item.x, item.y, item));
          } else if (item.type === 'shape') {
            restoredLayers.push(createShapeLayer(item.name, item.shapeType, item.x, item.y, item.width, item.height, item));
          }
        }

        resolve({
          canvasWidth: data.canvasWidth,
          canvasHeight: data.canvasHeight,
          globalAdjustments: data.globalAdjustments,
          layers: restoredLayers
        });
      } catch (err) {
        reject(err);
      }
    };
    reader.onerror = reject;
    reader.readAsText(file);
  });
}
