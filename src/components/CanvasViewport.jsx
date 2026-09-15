import React, { useRef, useEffect, useState, useCallback } from 'react';
import { TOOLS } from '../engine/constants';
import { renderComposite, screenToCanvasCoords } from '../engine/renderer';
import { floodFill, hexToRgb, rgbToHex } from '../engine/colorUtils';
import { createShapeLayer, createTextLayer, generateLayerThumbnail } from '../engine/layer';

export function CanvasViewport({
  canvasWidth,
  canvasHeight,
  layers,
  activeLayerId,
  globalAdjustments,
  activeTool,
  toolOptions,
  primaryColor,
  onChangePrimaryColor,
  zoom,
  panX,
  panY,
  onPanChange,
  onZoomChange,
  onCommitAction,
  onUpdateActiveLayerState,
  cropRect,
  onUpdateCropRect,
  selectionRect,
  onUpdateSelectionRect,
  onHoverSampleColor
}) {
  const containerRef = useRef(null);
  const mainCanvasRef = useRef(null);
  const overlayCanvasRef = useRef(null);

  // Interaction State
  const [isInteracting, setIsInteracting] = useState(false);
  const [isPanning, setIsPanning] = useState(false);
  const [panStart, setPanStart] = useState({ x: 0, y: 0 });
  const [brushCursor, setBrushCursor] = useState({ visible: false, x: 0, y: 0 });
  const [loupe, setLoupe] = useState({ visible: false, x: 0, y: 0, color: '#000000' });
  const [transformHandle, setTransformHandle] = useState(null); // null | 'move' | 'nw' | 'ne' | 'se' | 'sw' | 'n' | 's' | 'e' | 'w' | 'rot'
  const [dragOrigin, setDragOrigin] = useState({ x: 0, y: 0 });
  const [initialLayerBounds, setInitialLayerBounds] = useState(null);
  const [tempShapePreview, setTempShapePreview] = useState(null);

  const activeLayer = layers.find(l => l.id === activeLayerId);

  // --------------------------------------------------------------------------
  // RENDER COMPOSITE TO MAIN CANVAS
  // --------------------------------------------------------------------------
  useEffect(() => {
    const canvas = mainCanvasRef.current;
    if (!canvas) return;
    renderComposite(canvas, layers, globalAdjustments);
  }, [layers, globalAdjustments, canvasWidth, canvasHeight]);

  // --------------------------------------------------------------------------
  // WHEEL ZOOM & PAN (Center pinned)
  // --------------------------------------------------------------------------
  const handleWheel = useCallback((e) => {
    e.preventDefault();
    if (e.ctrlKey || e.metaKey) {
      // Zoom
      const zoomFactor = e.deltaY < 0 ? 1.15 : 0.85;
      const newZoom = Math.min(32, Math.max(0.08, zoom * zoomFactor));

      if (containerRef.current) {
        const rect = containerRef.current.getBoundingClientRect();
        const mouseX = e.clientX - rect.left;
        const mouseY = e.clientY - rect.top;

        // Keep point under cursor stationary
        const newPanX = mouseX - (mouseX - panX) * (newZoom / zoom);
        const newPanY = mouseY - (mouseY - panY) * (newZoom / zoom);

        onZoomChange(newZoom);
        onPanChange(newPanX, newPanY);
      }
    } else {
      // Pan
      onPanChange(panX - e.deltaX, panY - e.deltaY);
    }
  }, [zoom, panX, panY, onZoomChange, onPanChange]);

  // --------------------------------------------------------------------------
  // MOUSE DOWN HANDLER
  // --------------------------------------------------------------------------
  const handleMouseDown = (e) => {
    // Space or Middle mouse button for Hand Pan
    if (e.button === 1 || e.spaceKey || e.altKey) {
      setIsPanning(true);
      setPanStart({ x: e.clientX - panX, y: e.clientY - panY });
      return;
    }

    if (e.button !== 0) return; // Only primary mouse button

    const canvasCoords = screenToCanvasCoords(e.clientX, e.clientY, mainCanvasRef.current, zoom, panX, panY);
    setIsInteracting(true);
    setDragOrigin(canvasCoords);

    // 1. SELECT / MOVE TOOL
    if (activeTool === TOOLS.SELECT && activeLayer && !activeLayer.locked) {
      setInitialLayerBounds({
        x: activeLayer.x,
        y: activeLayer.y,
        width: activeLayer.width,
        height: activeLayer.height,
        rotation: activeLayer.rotation || 0
      });
      // Handle determination will be set via handle attributes if clicking handle
    }

    // 2. BRUSH / ERASER TOOL
    if ((activeTool === TOOLS.BRUSH || activeTool === TOOLS.ERASER) && activeLayer && activeLayer.type === 'raster' && !activeLayer.locked) {
      const layerCtx = activeLayer.canvas.getContext('2d');
      layerCtx.save();
      const localX = canvasCoords.x - activeLayer.x;
      const localY = canvasCoords.y - activeLayer.y;

      const isEraser = activeTool === TOOLS.ERASER;
      const size = isEraser ? toolOptions.eraserSize : toolOptions.brushSize;
      const opacity = toolOptions.brushOpacity / 100;
      const hardness = toolOptions.brushHardness / 100;

      layerCtx.lineCap = 'round';
      layerCtx.lineJoin = 'round';
      layerCtx.lineWidth = size;

      if (isEraser) {
        layerCtx.globalCompositeOperation = 'destination-out';
        layerCtx.strokeStyle = 'rgba(0,0,0,1)';
      } else {
        layerCtx.globalCompositeOperation = 'source-over';
        layerCtx.strokeStyle = primaryColor;
        layerCtx.globalAlpha = opacity;
      }

      layerCtx.beginPath();
      layerCtx.moveTo(localX, localY);
      layerCtx.lineTo(localX + 0.1, localY + 0.1);
      layerCtx.stroke();
      layerCtx.restore();

      renderComposite(mainCanvasRef.current, layers, globalAdjustments);
    }

    // 3. PAINT BUCKET (FILL)
    if (activeTool === TOOLS.FILL && activeLayer && activeLayer.type === 'raster' && !activeLayer.locked) {
      const localX = canvasCoords.x - activeLayer.x;
      const localY = canvasCoords.y - activeLayer.y;
      const layerCtx = activeLayer.canvas.getContext('2d');
      floodFill(layerCtx, localX, localY, primaryColor, toolOptions.fillTolerance);
      activeLayer.thumbnail = generateLayerThumbnail(activeLayer);
      renderComposite(mainCanvasRef.current, layers, globalAdjustments);
      onCommitAction('Paint Bucket Fill');
    }

    // 4. EYEDROPPER
    if (activeTool === TOOLS.EYEDROPPER) {
      sampleColorAt(e.clientX, e.clientY);
    }

    // 5. TEXT TOOL
    if (activeTool === TOOLS.TEXT) {
      const newText = prompt('Enter text for layer:', 'Open Image Studio');
      if (newText) {
        const textLayer = createTextLayer(`Text "${newText.slice(0, 10)}"`, newText, canvasCoords.x, canvasCoords.y, {
          fontSize: toolOptions.textSize,
          color: primaryColor,
          align: toolOptions.textAlign
        });
        onCommitAction('Add Text Layer', [...layers, textLayer], textLayer.id);
      }
      setIsInteracting(false);
    }

    // 6. CROP TOOL
    if (activeTool === TOOLS.CROP && !cropRect) {
      onUpdateCropRect({
        x: canvasCoords.x,
        y: canvasCoords.y,
        width: 10,
        height: 10
      });
    }

    // 7. MARQUEE SELECTION
    if (activeTool === TOOLS.MARQUEE) {
      onUpdateSelectionRect({
        x: canvasCoords.x,
        y: canvasCoords.y,
        width: 0,
        height: 0,
        shape: toolOptions.marqueeShape
      });
    }
  };

  // --------------------------------------------------------------------------
  // MOUSE MOVE HANDLER
  // --------------------------------------------------------------------------
  const handleMouseMove = (e) => {
    // 1. Hand Pan
    if (isPanning) {
      onPanChange(e.clientX - panStart.x, e.clientY - panStart.y);
      return;
    }

    const canvasCoords = screenToCanvasCoords(e.clientX, e.clientY, mainCanvasRef.current, zoom, panX, panY);

    // Update Status Bar Hover Color
    if (canvasCoords.x >= 0 && canvasCoords.x < canvasWidth && canvasCoords.y >= 0 && canvasCoords.y < canvasHeight) {
      const ctx = mainCanvasRef.current?.getContext('2d');
      if (ctx) {
        try {
          const pixel = ctx.getImageData(canvasCoords.x, canvasCoords.y, 1, 1).data;
          onHoverSampleColor({
            x: canvasCoords.x,
            y: canvasCoords.y,
            hex: rgbToHex(pixel[0], pixel[1], pixel[2])
          });
        } catch {}
      }
    }

    // Brush Cursor Indicator
    if (activeTool === TOOLS.BRUSH || activeTool === TOOLS.ERASER) {
      const size = activeTool === TOOLS.BRUSH ? toolOptions.brushSize : toolOptions.eraserSize;
      setBrushCursor({
        visible: true,
        x: e.clientX,
        y: e.clientY,
        size: size * zoom
      });
    } else {
      if (brushCursor.visible) setBrushCursor({ visible: false, x: 0, y: 0 });
    }

    // Eyedropper Live Loupe
    if (activeTool === TOOLS.EYEDROPPER && isInteracting) {
      sampleColorAt(e.clientX, e.clientY);
    }

    if (!isInteracting) return;

    // A. BRUSH / ERASER CONTINUOUS STROKE
    if ((activeTool === TOOLS.BRUSH || activeTool === TOOLS.ERASER) && activeLayer && activeLayer.type === 'raster' && !activeLayer.locked) {
      const layerCtx = activeLayer.canvas.getContext('2d');
      const localX = canvasCoords.x - activeLayer.x;
      const localY = canvasCoords.y - activeLayer.y;

      const isEraser = activeTool === TOOLS.ERASER;
      const size = isEraser ? toolOptions.eraserSize : toolOptions.brushSize;
      const opacity = toolOptions.brushOpacity / 100;

      layerCtx.save();
      layerCtx.lineCap = 'round';
      layerCtx.lineJoin = 'round';
      layerCtx.lineWidth = size;

      if (isEraser) {
        layerCtx.globalCompositeOperation = 'destination-out';
        layerCtx.strokeStyle = 'rgba(0,0,0,1)';
      } else {
        layerCtx.globalCompositeOperation = 'source-over';
        layerCtx.strokeStyle = primaryColor;
        layerCtx.globalAlpha = opacity;
      }

      layerCtx.lineTo(localX, localY);
      layerCtx.stroke();
      layerCtx.beginPath();
      layerCtx.moveTo(localX, localY);
      layerCtx.restore();

      renderComposite(mainCanvasRef.current, layers, globalAdjustments);
    }

    // B. TRANSFORM / MOVE LAYER
    if (activeTool === TOOLS.SELECT && activeLayer && initialLayerBounds) {
      const dx = canvasCoords.x - dragOrigin.x;
      const dy = canvasCoords.y - dragOrigin.y;

      if (!transformHandle || transformHandle === 'move') {
        onUpdateActiveLayerState({
          x: Math.round(initialLayerBounds.x + dx),
          y: Math.round(initialLayerBounds.y + dy)
        });
      } else if (transformHandle === 'se') {
        onUpdateActiveLayerState({
          width: Math.max(10, Math.round(initialLayerBounds.width + dx)),
          height: Math.max(10, Math.round(initialLayerBounds.height + dy))
        });
      } else if (transformHandle === 'e') {
        onUpdateActiveLayerState({
          width: Math.max(10, Math.round(initialLayerBounds.width + dx))
        });
      } else if (transformHandle === 's') {
        onUpdateActiveLayerState({
          height: Math.max(10, Math.round(initialLayerBounds.height + dy))
        });
      } else if (transformHandle === 'rot') {
        const centerX = initialLayerBounds.x + initialLayerBounds.width / 2;
        const centerY = initialLayerBounds.y + initialLayerBounds.height / 2;
        const angle = Math.atan2(canvasCoords.y - centerY, canvasCoords.x - centerX) * (180 / Math.PI) - 90;
        onUpdateActiveLayerState({
          rotation: Math.round(angle)
        });
      }
    }

    // C. SHAPE DRAG PREVIEW
    if (activeTool === TOOLS.SHAPE) {
      const width = canvasCoords.x - dragOrigin.x;
      const height = canvasCoords.y - dragOrigin.y;
      setTempShapePreview({
        x: width >= 0 ? dragOrigin.x : canvasCoords.x,
        y: height >= 0 ? dragOrigin.y : canvasCoords.y,
        width: Math.abs(width),
        height: Math.abs(height)
      });
    }

    // D. MARQUEE SELECTION
    if (activeTool === TOOLS.MARQUEE) {
      const width = canvasCoords.x - dragOrigin.x;
      const height = canvasCoords.y - dragOrigin.y;
      onUpdateSelectionRect({
        x: width >= 0 ? dragOrigin.x : canvasCoords.x,
        y: height >= 0 ? dragOrigin.y : canvasCoords.y,
        width: Math.abs(width),
        height: Math.abs(height),
        shape: toolOptions.marqueeShape
      });
    }

    // E. CROP RECT DRAG
    if (activeTool === TOOLS.CROP) {
      const width = canvasCoords.x - dragOrigin.x;
      const height = canvasCoords.y - dragOrigin.y;
      onUpdateCropRect({
        x: width >= 0 ? dragOrigin.x : canvasCoords.x,
        y: height >= 0 ? dragOrigin.y : canvasCoords.y,
        width: Math.abs(width),
        height: Math.abs(height)
      });
    }
  };

  // --------------------------------------------------------------------------
  // MOUSE UP HANDLER
  // --------------------------------------------------------------------------
  const handleMouseUp = () => {
    if (isPanning) {
      setIsPanning(false);
      return;
    }

    if (!isInteracting) return;
    setIsInteracting(false);

    // Commit Brush / Eraser
    if (activeTool === TOOLS.BRUSH || activeTool === TOOLS.ERASER) {
      if (activeLayer) {
        activeLayer.thumbnail = generateLayerThumbnail(activeLayer);
        onCommitAction(activeTool === TOOLS.BRUSH ? 'Brush Stroke' : 'Eraser');
      }
    }

    // Commit Move / Transform
    if (activeTool === TOOLS.SELECT && activeLayer) {
      setInitialLayerBounds(null);
      setTransformHandle(null);
      onCommitAction('Transform Layer');
    }

    // Commit Shape Creation
    if (activeTool === TOOLS.SHAPE && tempShapePreview && tempShapePreview.width > 5) {
      const newShape = createShapeLayer(
        `${toolOptions.shapeType.toUpperCase()} Shape`,
        toolOptions.shapeType,
        tempShapePreview.x,
        tempShapePreview.y,
        tempShapePreview.width,
        tempShapePreview.height,
        {
          fillColor: primaryColor,
          strokeColor: '#000000',
          strokeWidth: toolOptions.shapeStrokeWidth
        }
      );
      setTempShapePreview(null);
      onCommitAction('Create Shape', [...layers, newShape], newShape.id);
    }

    setLoupe({ visible: false, x: 0, y: 0, color: '#000000' });
  };

  // --------------------------------------------------------------------------
  // COLOR SAMPLING (EYEDROPPER)
  // --------------------------------------------------------------------------
  const sampleColorAt = (clientX, clientY) => {
    const canvasCoords = screenToCanvasCoords(clientX, clientY, mainCanvasRef.current, zoom, panX, panY);
    const ctx = mainCanvasRef.current?.getContext('2d');
    if (!ctx) return;

    try {
      const pixel = ctx.getImageData(canvasCoords.x, canvasCoords.y, 1, 1).data;
      const hex = rgbToHex(pixel[0], pixel[1], pixel[2]);
      onChangePrimaryColor(hex);
      setLoupe({
        visible: true,
        x: clientX,
        y: clientY,
        color: hex
      });
    } catch {}
  };

  // Cursor style
  let cursorClass = 'crosshair';
  if (isPanning) cursorClass = 'grabbing';
  else if (activeTool === TOOLS.SELECT) cursorClass = 'default';
  else if (activeTool === TOOLS.TEXT) cursorClass = 'text';

  return (
    <div
      ref={containerRef}
      className="viewport-area"
      onWheel={handleWheel}
      onMouseDown={handleMouseDown}
      onMouseMove={handleMouseMove}
      onMouseUp={handleMouseUp}
      onMouseLeave={handleMouseUp}
      style={{ cursor: cursorClass }}
    >
      {/* Canvas Transform Stage */}
      <div
        className="canvas-transform-wrapper checkerboard-bg"
        style={{
          width: `${canvasWidth}px`,
          height: `${canvasHeight}px`,
          transform: `translate(${panX}px, ${panY}px) scale(${zoom})`
        }}
      >
        {/* Main Render Composite Canvas */}
        <canvas
          ref={mainCanvasRef}
          width={canvasWidth}
          height={canvasHeight}
          className="main-render-canvas"
        />

        {/* Dynamic Shape Drag Preview */}
        {tempShapePreview && (
          <div
            style={{
              position: 'absolute',
              left: `${tempShapePreview.x}px`,
              top: `${tempShapePreview.y}px`,
              width: `${tempShapePreview.width}px`,
              height: `${tempShapePreview.height}px`,
              backgroundColor: primaryColor,
              border: `${toolOptions.shapeStrokeWidth}px solid #000000`,
              borderRadius: toolOptions.shapeType === 'ellipse' ? '50%' : '8px',
              opacity: 0.7,
              pointerEvents: 'none'
            }}
          />
        )}

        {/* Interactive Transform Gizmo for Active Layer (Move Tool) */}
        {activeTool === TOOLS.SELECT && activeLayer && !activeLayer.locked && (
          <div
            className="transform-gizmo"
            style={{
              left: `${activeLayer.x}px`,
              top: `${activeLayer.y}px`,
              width: `${activeLayer.width}px`,
              height: `${activeLayer.height}px`,
              transform: `rotate(${activeLayer.rotation || 0}deg)`,
              transformOrigin: 'center center'
            }}
          >
            {/* Rotation Handle */}
            <div className="gizmo-rot-line" />
            <div
              className="gizmo-handle rot"
              onMouseDown={(e) => {
                e.stopPropagation();
                setTransformHandle('rot');
                setIsInteracting(true);
                setDragOrigin(screenToCanvasCoords(e.clientX, e.clientY, mainCanvasRef.current, zoom, panX, panY));
              }}
            />

            {/* 8 Bounding Box Scale Handles */}
            <div className="gizmo-handle nw" onMouseDown={() => setTransformHandle('nw')} />
            <div className="gizmo-handle n" onMouseDown={() => setTransformHandle('n')} />
            <div className="gizmo-handle ne" onMouseDown={() => setTransformHandle('ne')} />
            <div className="gizmo-handle e" onMouseDown={() => setTransformHandle('e')} />
            <div className="gizmo-handle se" onMouseDown={() => setTransformHandle('se')} />
            <div className="gizmo-handle s" onMouseDown={() => setTransformHandle('s')} />
            <div className="gizmo-handle sw" onMouseDown={() => setTransformHandle('sw')} />
            <div className="gizmo-handle w" onMouseDown={() => setTransformHandle('w')} />
          </div>
        )}

        {/* Marquee Selection Box (Marching Ants) */}
        {selectionRect && selectionRect.width > 2 && (
          <div
            style={{
              position: 'absolute',
              left: `${selectionRect.x}px`,
              top: `${selectionRect.y}px`,
              width: `${selectionRect.width}px`,
              height: `${selectionRect.height}px`,
              border: '1px dashed #007AFF',
              borderRadius: selectionRect.shape === 'ellipse' ? '50%' : '0',
              pointerEvents: 'none',
              boxShadow: '0 0 0 1px rgba(255, 255, 255, 0.8)'
            }}
          />
        )}

        {/* Crop Grid Overlay */}
        {activeTool === TOOLS.CROP && cropRect && (
          <div
            className="crop-overlay"
            style={{
              left: `${cropRect.x}px`,
              top: `${cropRect.y}px`,
              width: `${cropRect.width}px`,
              height: `${cropRect.height}px`
            }}
          >
            <div className="crop-grid">
              <div className="crop-grid-cell" />
              <div className="crop-grid-cell" />
              <div className="crop-grid-cell" />
              <div className="crop-grid-cell" />
              <div className="crop-grid-cell" />
              <div className="crop-grid-cell" />
              <div className="crop-grid-cell" />
              <div className="crop-grid-cell" />
              <div className="crop-grid-cell" />
            </div>
          </div>
        )}
      </div>

      {/* Live Brush Size Cursor Indicator */}
      {brushCursor.visible && (
        <div
          style={{
            position: 'fixed',
            left: `${brushCursor.x}px`,
            top: `${brushCursor.y}px`,
            width: `${brushCursor.size}px`,
            height: `${brushCursor.size}px`,
            transform: 'translate(-50%, -50%)',
            border: '1px solid rgba(0, 0, 0, 0.6)',
            boxShadow: '0 0 0 1px rgba(255, 255, 255, 0.8)',
            borderRadius: '50%',
            pointerEvents: 'none',
            zIndex: 999
          }}
        />
      )}

      {/* Eyedropper Magnifying Loupe */}
      {loupe.visible && (
        <div
          style={{
            position: 'fixed',
            left: `${loupe.x + 18}px`,
            top: `${loupe.y - 48}px`,
            width: '42px',
            height: '42px',
            borderRadius: '50%',
            backgroundColor: loupe.color,
            border: '3px solid #ffffff',
            boxShadow: '0 4px 14px rgba(0, 0, 0, 0.3)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#ffffff',
            fontSize: '9px',
            fontWeight: 600,
            textShadow: '0 1px 2px rgba(0,0,0,0.8)',
            pointerEvents: 'none',
            zIndex: 999
          }}
        >
          {loupe.color}
        </div>
      )}
    </div>
  );
}
