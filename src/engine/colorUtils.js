/**
 * Converts Hex string (#RRGGBB or #RGB) to RGB object
 */
export function hexToRgb(hex) {
  let cleanHex = hex.replace('#', '');
  if (cleanHex.length === 3) {
    cleanHex = cleanHex.split('').map(c => c + c).join('');
  }
  const num = parseInt(cleanHex, 16);
  return {
    r: (num >> 16) & 255,
    g: (num >> 8) & 255,
    b: num & 255,
    a: 255
  };
}

/**
 * Converts RGB numbers to Hex string
 */
export function rgbToHex(r, g, b) {
  const toHex = (c) => {
    const hex = Math.max(0, Math.min(255, Math.round(c))).toString(16);
    return hex.length === 1 ? '0' + hex : hex;
  };
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`;
}

/**
 * Builds CSS filter string for canvas 2D context or element style
 */
export function buildFilterString(adj = {}) {
  const parts = [];

  // Brightness: default 0 -> CSS 100%
  if (adj.brightness !== 0) {
    const val = 100 + (adj.brightness || 0);
    parts.push(`brightness(${Math.max(0, val)}%)`);
  }

  // Contrast: default 0 -> CSS 100%
  if (adj.contrast !== 0) {
    const val = 100 + (adj.contrast || 0);
    parts.push(`contrast(${Math.max(0, val)}%)`);
  }

  // Saturation & Vibrance: default 0 -> CSS 100%
  const totalSat = (adj.saturation || 0) + (adj.vibrance || 0) * 0.75;
  if (totalSat !== 0) {
    const val = 100 + totalSat;
    parts.push(`saturate(${Math.max(0, val)}%)`);
  }

  // Hue: default 0 -> deg
  if (adj.hue && adj.hue !== 0) {
    parts.push(`hue-rotate(${adj.hue}deg)`);
  }

  // Blur: default 0 -> px
  if (adj.blur && adj.blur > 0) {
    parts.push(`blur(${adj.blur}px)`);
  }

  // Invert
  if (adj.invert) {
    parts.push('invert(100%)');
  }

  // Grayscale
  if (adj.grayscale) {
    parts.push('grayscale(100%)');
  }

  // Sepia
  if (adj.sepia) {
    parts.push('sepia(80%)');
  }

  return parts.length > 0 ? parts.join(' ') : 'none';
}

/**
 * High-performance 4-way Flood Fill algorithm on Canvas context
 */
export function floodFill(ctx, startX, startY, fillColorHex, tolerance = 32) {
  const width = ctx.canvas.width;
  const height = ctx.canvas.height;
  
  if (startX < 0 || startX >= width || startY < 0 || startY >= height) return;

  const imgData = ctx.getImageData(0, 0, width, height);
  const data = imgData.data;

  const targetColor = hexToRgb(fillColorHex);
  const startIndex = (startY * width + startX) * 4;

  const startR = data[startIndex];
  const startG = data[startIndex + 1];
  const startB = data[startIndex + 2];
  const startA = data[startIndex + 3];

  // If clicking on the exact target color already, abort
  if (
    Math.abs(startR - targetColor.r) <= 1 &&
    Math.abs(startG - targetColor.g) <= 1 &&
    Math.abs(startB - targetColor.b) <= 1 &&
    startA === 255
  ) {
    return;
  }

  function matchStartColor(index) {
    const r = data[index];
    const g = data[index + 1];
    const b = data[index + 2];
    const a = data[index + 3];

    return (
      Math.abs(r - startR) <= tolerance &&
      Math.abs(g - startG) <= tolerance &&
      Math.abs(b - startB) <= tolerance &&
      Math.abs(a - startA) <= tolerance
    );
  }

  function colorPixel(index) {
    data[index] = targetColor.r;
    data[index + 1] = targetColor.g;
    data[index + 2] = targetColor.b;
    data[index + 3] = 255;
  }

  const visited = new Uint8Array(width * height);
  const pixelStack = [[startX, startY]];

  while (pixelStack.length > 0) {
    const [x, y] = pixelStack.pop();
    const pixelIndex = y * width + x;

    if (visited[pixelIndex]) continue;
    visited[pixelIndex] = 1;

    const dataIndex = pixelIndex * 4;
    if (!matchStartColor(dataIndex)) continue;

    colorPixel(dataIndex);

    if (x > 0) pixelStack.push([x - 1, y]);
    if (x < width - 1) pixelStack.push([x + 1, y]);
    if (y > 0) pixelStack.push([x, y - 1]);
    if (y < height - 1) pixelStack.push([x, y + 1]);
  }

  ctx.putImageData(imgData, 0, 0);
}
