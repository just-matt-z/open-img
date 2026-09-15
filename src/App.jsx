import React, { useState, useEffect, useRef, useCallback } from 'react';
import { TOOLS, DEFAULT_ADJUSTMENTS } from './engine/constants';
import { HistoryManager } from './engine/history';
import { 
  createRasterLayer, 
  createTextLayer, 
  createShapeLayer, 
  cloneLayer, 
  generateLayerThumbnail, 
  createCanvas 
} from './engine/layer';
import { exportCompositeImage, exportProjectFile, importProjectFile } from './engine/exporter';
import { TopBar } from './components/TopBar';
import { ToolBar } from './components/ToolBar';
import { ToolOptionsBar } from './components/ToolOptionsBar';
import { CanvasViewport } from './components/CanvasViewport';
import { InspectorPanel } from './components/InspectorPanel';
import { StatusBar } from './components/StatusBar';
import { ExportModal } from './components/ExportModal';
import { NewCanvasModal } from './components/NewCanvasModal';

export default function App() {
  // Theme State: Day Mode (default) or Dark Mode
  const [theme, setTheme] = useState('light');

  // Document Properties
  const [docName, setDocName] = useState('Matterhorn-Sunset.png');
  const [canvasWidth, setCanvasWidth] = useState(1920);
  const [canvasHeight, setCanvasHeight] = useState(1080);
  const [layers, setLayers] = useState([]);
  const [activeLayerId, setActiveLayerId] = useState(null);
  const [globalAdjustments, setGlobalAdjustments] = useState({ ...DEFAULT_ADJUSTMENTS });

  // History Manager
  const historyRef = useRef(new HistoryManager());
  const [historyList, setHistoryList] = useState([]);
  const [canUndo, setCanUndo] = useState(false);
  const [canRedo, setCanRedo] = useState(false);

  // Viewport Zoom & Pan
  const [zoom, setZoom] = useState(0.55);
  const [panX, setPanX] = useState(40);
  const [panY, setPanY] = useState(20);

  // Active Tool & Options
  const [activeTool, setActiveTool] = useState(TOOLS.SELECT);
  const [toolOptions, setToolOptions] = useState({
    brushSize: 28,
    brushOpacity: 100,
    brushHardness: 80,
    eraserSize: 36,
    cropRatio: 'free',
    marqueeShape: 'rect',
    textSize: 48,
    textAlign: 'left',
    shapeType: 'rectangle',
    shapeStrokeWidth: 4,
    fillTolerance: 32
  });

  // Color Swatches
  const [primaryColor, setPrimaryColor] = useState('#007AFF'); // iOS System Blue
  const [secondaryColor, setSecondaryColor] = useState('#FFFFFF');

  // Tool Specific Interactive Overlays
  const [cropRect, setCropRect] = useState(null);
  const [selectionRect, setSelectionRect] = useState(null);

  // Status Bar Live Readouts
  const [cursorPos, setCursorPos] = useState(null);
  const [sampledColor, setSampledColor] = useState(null);

  // Modals
  const [isExportModalOpen, setIsExportModalOpen] = useState(false);
  const [isNewModalOpen, setIsNewModalOpen] = useState(false);
  const fileInputRef = useRef(null);

  // --------------------------------------------------------------------------
  // THEME EFFECT
  // --------------------------------------------------------------------------
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
  }, [theme]);

  // --------------------------------------------------------------------------
  // INITIAL DEMO ARTWORK SETUP
  // --------------------------------------------------------------------------
  useEffect(() => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      // 1. Photo Base Layer
      const baseLayer = createRasterLayer('Matterhorn Sunset', 1920, 1080, img);

      // 2. Stylish iOS 7 Badge Shape Layer
      const badgeShape = createShapeLayer('Frosted Badge', 'rectangle', 80, 80, 520, 150, {
        fillColor: 'rgba(255, 255, 255, 0.25)',
        strokeColor: 'rgba(255, 255, 255, 0.6)',
        strokeWidth: 2,
        borderRadius: 24
      });

      // 3. Vector Text Layer
      const titleLayer = createTextLayer('Title Text', 'Open Image Studio', 110, 110, {
        fontSize: 44,
        fontWeight: '600',
        color: '#FFFFFF'
      });

      // 4. Subtitle Text Layer
      const subLayer = createTextLayer('Subtitle', 'iOS 7 Pro Editing Suite', 112, 170, {
        fontSize: 20,
        fontWeight: '300',
        color: '#EBEBF5'
      });

      const initialLayers = [baseLayer, badgeShape, titleLayer, subLayer];
      setLayers(initialLayers);
      setActiveLayerId(baseLayer.id);

      // Initialize history baseline
      historyRef.current.pushState('Initial Open', 1920, 1080, initialLayers, DEFAULT_ADJUSTMENTS);
      updateHistoryFlags();
    };

    img.onerror = () => {
      // Fallback if image fails: vibrant gradient artwork
      const fallbackCanvas = createCanvas(1920, 1080);
      const ctx = fallbackCanvas.getContext('2d');
      const grad = ctx.createLinearGradient(0, 0, 1920, 1080);
      grad.addColorStop(0, '#5AC8FA');
      grad.addColorStop(0.5, '#007AFF');
      grad.addColorStop(1, '#5856D6');
      ctx.fillStyle = grad;
      ctx.fillRect(0, 0, 1920, 1080);

      const baseLayer = createRasterLayer('Background Gradient', 1920, 1080, fallbackCanvas);
      setLayers([baseLayer]);
      setActiveLayerId(baseLayer.id);
      historyRef.current.pushState('Initial Gradient', 1920, 1080, [baseLayer], DEFAULT_ADJUSTMENTS);
      updateHistoryFlags();
    };

    img.src = '/sample.jpg';
  }, []);

  // --------------------------------------------------------------------------
  // HISTORY HELPERS
  // --------------------------------------------------------------------------
  const updateHistoryFlags = useCallback(() => {
    setCanUndo(historyRef.current.canUndo());
    setCanRedo(historyRef.current.canRedo());
    setHistoryList(historyRef.current.getHistoryList());
  }, []);

  const commitAction = useCallback((desc, updatedLayers = null, newActiveId = null) => {
    const targetLayers = updatedLayers || layers;
    historyRef.current.pushState(desc, canvasWidth, canvasHeight, targetLayers, globalAdjustments);
    if (updatedLayers) setLayers(targetLayers);
    if (newActiveId) setActiveLayerId(newActiveId);
    updateHistoryFlags();
  }, [layers, canvasWidth, canvasHeight, globalAdjustments, updateHistoryFlags]);

  const handleUndo = useCallback(() => {
    const state = historyRef.current.undo();
    if (state) {
      setCanvasWidth(state.canvasWidth);
      setCanvasHeight(state.canvasHeight);
      setLayers(state.layers);
      setGlobalAdjustments(state.globalAdjustments);
      if (state.layers.length > 0 && !state.layers.some(l => l.id === activeLayerId)) {
        setActiveLayerId(state.layers[0].id);
      }
      updateHistoryFlags();
    }
  }, [activeLayerId, updateHistoryFlags]);

  const handleRedo = useCallback(() => {
    const state = historyRef.current.redo();
    if (state) {
      setCanvasWidth(state.canvasWidth);
      setCanvasHeight(state.canvasHeight);
      setLayers(state.layers);
      setGlobalAdjustments(state.globalAdjustments);
      if (state.layers.length > 0 && !state.layers.some(l => l.id === activeLayerId)) {
        setActiveLayerId(state.layers[0].id);
      }
      updateHistoryFlags();
    }
  }, [activeLayerId, updateHistoryFlags]);

  const handleJumpHistory = useCallback((index) => {
    const state = historyRef.current.jumpTo(index);
    if (state) {
      setCanvasWidth(state.canvasWidth);
      setCanvasHeight(state.canvasHeight);
      setLayers(state.layers);
      setGlobalAdjustments(state.globalAdjustments);
      updateHistoryFlags();
    }
  }, [updateHistoryFlags]);

  // --------------------------------------------------------------------------
  // LAYER MANIPULATION
  // --------------------------------------------------------------------------
  const handleSelectLayer = (id) => setActiveLayerId(id);

  const handleUpdateLayer = (id, updates) => {
    setLayers(prev => prev.map(l => {
      if (l.id === id) {
        const updated = { ...l, ...updates };
        updated.thumbnail = generateLayerThumbnail(updated);
        return updated;
      }
      return l;
    }));
  };

  const handleUpdateActiveLayerState = (updates) => {
    if (!activeLayerId) return;
    handleUpdateLayer(activeLayerId, updates);
  };

  const handleAddLayer = () => {
    const newLayer = createRasterLayer(`Layer ${layers.length + 1}`, canvasWidth, canvasHeight);
    const updated = [...layers, newLayer];
    commitAction('Add Layer', updated, newLayer.id);
  };

  const handleDuplicateLayer = (id) => {
    const target = layers.find(l => l.id === id);
    if (!target) return;
    const duplicated = cloneLayer(target);
    const index = layers.findIndex(l => l.id === id);
    const updated = [...layers];
    updated.splice(index + 1, 0, duplicated);
    commitAction('Duplicate Layer', updated, duplicated.id);
  };

  const handleMergeDown = (id) => {
    const index = layers.findIndex(l => l.id === id);
    if (index <= 0) return; // Cannot merge bottom layer down

    const topLayer = layers[index];
    const bottomLayer = layers[index - 1];

    if (bottomLayer.locked) return;

    // Create merged canvas
    const mergedCanvas = createCanvas(bottomLayer.width, bottomLayer.height);
    const ctx = mergedCanvas.getContext('2d');

    if (bottomLayer.canvas) ctx.drawImage(bottomLayer.canvas, 0, 0);

    ctx.save();
    ctx.globalAlpha = topLayer.opacity ?? 1;
    ctx.globalCompositeOperation = topLayer.blendMode || 'source-over';
    if (topLayer.canvas) {
      ctx.drawImage(topLayer.canvas, topLayer.x - bottomLayer.x, topLayer.y - bottomLayer.y);
    }
    ctx.restore();

    const mergedLayer = {
      ...bottomLayer,
      canvas: mergedCanvas,
      thumbnail: null
    };
    mergedLayer.thumbnail = generateLayerThumbnail(mergedLayer);

    const updated = [...layers];
    updated.splice(index - 1, 2, mergedLayer);
    commitAction('Merge Down', updated, mergedLayer.id);
  };

  const handleDeleteLayer = (id) => {
    if (layers.length <= 1) return;
    const updated = layers.filter(l => l.id !== id);
    commitAction('Delete Layer', updated, updated[updated.length - 1].id);
  };

  // --------------------------------------------------------------------------
  // ADJUSTMENTS
  // --------------------------------------------------------------------------
  const handleUpdateAdjustments = (updates) => {
    setGlobalAdjustments(prev => ({ ...prev, ...updates }));
  };

  const handleResetAdjustments = () => {
    setGlobalAdjustments({ ...DEFAULT_ADJUSTMENTS });
    commitAction('Reset Adjustments');
  };

  // --------------------------------------------------------------------------
  // CROP & SELECTION ACTIONS
  // --------------------------------------------------------------------------
  const handleApplyCrop = () => {
    if (!cropRect || cropRect.width < 10 || cropRect.height < 10) return;

    const newWidth = Math.round(cropRect.width);
    const newHeight = Math.round(cropRect.height);

    // Adjust layer offsets relative to crop
    const croppedLayers = layers.map(layer => {
      const cloned = cloneLayer(layer);
      cloned.x = cloned.x - cropRect.x;
      cloned.y = cloned.y - cropRect.y;
      return cloned;
    });

    setCanvasWidth(newWidth);
    setCanvasHeight(newHeight);
    setCropRect(null);
    setActiveTool(TOOLS.SELECT);
    commitAction('Crop Canvas', croppedLayers);
  };

  const handleCancelCrop = () => {
    setCropRect(null);
    setActiveTool(TOOLS.SELECT);
  };

  const handleClearSelection = () => setSelectionRect(null);

  const handleDeleteSelection = () => {
    if (!selectionRect || !activeLayerId) return;
    const targetLayer = layers.find(l => l.id === activeLayerId);
    if (!targetLayer || targetLayer.type !== 'raster' || targetLayer.locked) return;

    const ctx = targetLayer.canvas.getContext('2d');
    const localX = selectionRect.x - targetLayer.x;
    const localY = selectionRect.y - targetLayer.y;

    ctx.save();
    ctx.clearRect(localX, localY, selectionRect.width, selectionRect.height);
    ctx.restore();

    targetLayer.thumbnail = generateLayerThumbnail(targetLayer);
    setSelectionRect(null);
    commitAction('Delete Selection Content');
  };

  // --------------------------------------------------------------------------
  // COLOR SWAPPING & PALETTE
  // --------------------------------------------------------------------------
  const handleSwapColors = () => {
    setPrimaryColor(secondaryColor);
    setSecondaryColor(primaryColor);
  };

  // --------------------------------------------------------------------------
  // NEW DOCUMENT & OPEN FILES
  // --------------------------------------------------------------------------
  const handleCreateNewCanvas = ({ width, height, background }) => {
    const newLayer = createRasterLayer('Background', width, height);
    if (background === 'white') {
      const ctx = newLayer.canvas.getContext('2d');
      ctx.fillStyle = '#ffffff';
      ctx.fillRect(0, 0, width, height);
      newLayer.thumbnail = generateLayerThumbnail(newLayer);
    } else if (background === 'dark') {
      const ctx = newLayer.canvas.getContext('2d');
      ctx.fillStyle = '#1c1c1e';
      ctx.fillRect(0, 0, width, height);
      newLayer.thumbnail = generateLayerThumbnail(newLayer);
    }

    setDocName(`Artwork-${Date.now().toString().slice(-4)}.png`);
    setCanvasWidth(width);
    setCanvasHeight(height);
    setLayers([newLayer]);
    setActiveLayerId(newLayer.id);
    setGlobalAdjustments({ ...DEFAULT_ADJUSTMENTS });
    setCropRect(null);
    setSelectionRect(null);

    historyRef.current = new HistoryManager();
    historyRef.current.pushState('New Document', width, height, [newLayer], DEFAULT_ADJUSTMENTS);
    updateHistoryFlags();
  };

  const handleOpenFile = (file) => {
    if (!file) return;

    // 1. Check if .openimg project file
    if (file.name.endsWith('.openimg')) {
      importProjectFile(file).then(proj => {
        setDocName(file.name.replace('.openimg', '.png'));
        setCanvasWidth(proj.canvasWidth);
        setCanvasHeight(proj.canvasHeight);
        setLayers(proj.layers);
        setActiveLayerId(proj.layers[proj.layers.length - 1]?.id || null);
        setGlobalAdjustments(proj.globalAdjustments || { ...DEFAULT_ADJUSTMENTS });
        historyRef.current = new HistoryManager();
        historyRef.current.pushState('Import Project', proj.canvasWidth, proj.canvasHeight, proj.layers, proj.globalAdjustments);
        updateHistoryFlags();
      }).catch(err => {
        alert('Could not open project file: ' + err.message);
      });
      return;
    }

    // 2. Raster Image file (PNG, JPG, WebP, SVG, BMP)
    const reader = new FileReader();
    reader.onload = (e) => {
      const img = new Image();
      img.onload = () => {
        const layer = createRasterLayer(file.name, img.width, img.height, img);
        setDocName(file.name);
        setCanvasWidth(img.width);
        setCanvasHeight(img.height);
        setLayers([layer]);
        setActiveLayerId(layer.id);
        setGlobalAdjustments({ ...DEFAULT_ADJUSTMENTS });
        historyRef.current = new HistoryManager();
        historyRef.current.pushState('Open Image', img.width, img.height, [layer], DEFAULT_ADJUSTMENTS);
        updateHistoryFlags();
      };
      img.src = e.target.result;
    };
    reader.readAsDataURL(file);
  };

  // --------------------------------------------------------------------------
  // DRAG & DROP + CLIPBOARD PASTE
  // --------------------------------------------------------------------------
  useEffect(() => {
    const handleDragOver = (e) => e.preventDefault();
    const handleDrop = (e) => {
      e.preventDefault();
      if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
        handleOpenFile(e.dataTransfer.files[0]);
      }
    };

    const handlePaste = (e) => {
      const items = e.clipboardData?.items;
      if (!items) return;
      for (const item of items) {
        if (item.type.indexOf('image') !== -1) {
          const blob = item.getAsFile();
          const reader = new FileReader();
          reader.onload = (event) => {
            const img = new Image();
            img.onload = () => {
              const newLayer = createRasterLayer(`Pasted Layer ${layers.length + 1}`, img.width, img.height, img);
              // Center the pasted image on current canvas
              newLayer.x = Math.round((canvasWidth - img.width) / 2);
              newLayer.y = Math.round((canvasHeight - img.height) / 2);
              commitAction('Paste Image', [...layers, newLayer], newLayer.id);
            };
            img.src = event.target.result;
          };
          reader.readAsDataURL(blob);
          break;
        }
      }
    };

    window.addEventListener('dragover', handleDragOver);
    window.addEventListener('drop', handleDrop);
    window.addEventListener('paste', handlePaste);

    return () => {
      window.removeEventListener('dragover', handleDragOver);
      window.removeEventListener('drop', handleDrop);
      window.removeEventListener('paste', handlePaste);
    };
  }, [layers, canvasWidth, canvasHeight, commitAction]);

  // --------------------------------------------------------------------------
  // KEYBOARD SHORTCUTS
  // --------------------------------------------------------------------------
  useEffect(() => {
    const handleKeyDown = (e) => {
      // Ignore shortcut if typing in an input
      if (['INPUT', 'TEXTAREA'].includes(document.activeElement?.tagName)) return;

      const key = e.key.toLowerCase();
      const ctrl = e.ctrlKey || e.metaKey;

      if (ctrl && key === 'z') {
        e.preventDefault();
        if (e.shiftKey) handleRedo();
        else handleUndo();
      } else if (ctrl && key === 'y') {
        e.preventDefault();
        handleRedo();
      } else if (ctrl && key === '0') {
        e.preventDefault();
        // Fit to screen
        setZoom(0.55);
        setPanX(40);
        setPanY(20);
      } else if (ctrl && key === '1') {
        e.preventDefault();
        setZoom(1.0);
      } else if (ctrl && key === 'j') {
        e.preventDefault();
        if (activeLayerId) handleDuplicateLayer(activeLayerId);
      } else if (ctrl && key === 'e') {
        e.preventDefault();
        setIsExportModalOpen(true);
      } else if (ctrl && key === 'n') {
        e.preventDefault();
        setIsNewModalOpen(true);
      } else if (ctrl && key === 'o') {
        e.preventDefault();
        fileInputRef.current?.click();
      } else if (key === 'v') {
        setActiveTool(TOOLS.SELECT);
      } else if (key === 'm') {
        setActiveTool(TOOLS.MARQUEE);
      } else if (key === 'l') {
        setActiveTool(TOOLS.LASSO);
      } else if (key === 'c') {
        setActiveTool(TOOLS.CROP);
      } else if (key === 'b') {
        setActiveTool(TOOLS.BRUSH);
      } else if (key === 'e') {
        setActiveTool(TOOLS.ERASER);
      } else if (key === 'g') {
        setActiveTool(TOOLS.FILL);
      } else if (key === 't') {
        setActiveTool(TOOLS.TEXT);
      } else if (key === 'u') {
        setActiveTool(TOOLS.SHAPE);
      } else if (key === 'i') {
        setActiveTool(TOOLS.EYEDROPPER);
      } else if (key === 'x') {
        handleSwapColors();
      } else if (key === 'd') {
        setPrimaryColor('#000000');
        setSecondaryColor('#FFFFFF');
      } else if (key === 'delete' || key === 'backspace') {
        if (selectionRect) {
          handleDeleteSelection();
        } else if (activeLayerId && layers.length > 1) {
          handleDeleteLayer(activeLayerId);
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [
    handleUndo,
    handleRedo,
    activeLayerId,
    layers,
    selectionRect,
    handleDuplicateLayer,
    handleDeleteLayer
  ]);

  return (
    <div className="app-container">
      {/* Hidden File Picker */}
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*,.openimg"
        style={{ display: 'none' }}
        onChange={(e) => {
          if (e.target.files && e.target.files[0]) {
            handleOpenFile(e.target.files[0]);
          }
        }}
      />

      {/* Top Header */}
      <TopBar
        docName={docName}
        canvasWidth={canvasWidth}
        canvasHeight={canvasHeight}
        zoom={zoom}
        onZoomChange={(z) => setZoom(z)}
        onZoomFit={() => {
          setZoom(0.55);
          setPanX(40);
          setPanY(20);
        }}
        canUndo={canUndo}
        canRedo={canRedo}
        onUndo={handleUndo}
        onRedo={handleRedo}
        onNewClick={() => setIsNewModalOpen(true)}
        onOpenClick={() => fileInputRef.current?.click()}
        onExportClick={() => setIsExportModalOpen(true)}
        theme={theme}
        onToggleTheme={() => setTheme(theme === 'light' ? 'dark' : 'light')}
      />

      {/* Contextual Tool Options Bar */}
      <ToolOptionsBar
        activeTool={activeTool}
        toolOptions={toolOptions}
        onUpdateToolOptions={(updates) => setToolOptions(prev => ({ ...prev, ...updates }))}
        onApplyCrop={handleApplyCrop}
        onCancelCrop={handleCancelCrop}
        onClearSelection={handleClearSelection}
        onDeleteSelection={handleDeleteSelection}
        hasSelection={!!selectionRect}
      />

      {/* Main Workspace Stage */}
      <div className="workspace-container">
        {/* Floating Left Toolbar */}
        <ToolBar
          activeTool={activeTool}
          onSelectTool={(tool) => setActiveTool(tool)}
          primaryColor={primaryColor}
          secondaryColor={secondaryColor}
          onChangePrimaryColor={(c) => setPrimaryColor(c)}
          onChangeSecondaryColor={(c) => setSecondaryColor(c)}
          onSwapColors={handleSwapColors}
        />

        {/* Center Canvas Viewport */}
        <CanvasViewport
          canvasWidth={canvasWidth}
          canvasHeight={canvasHeight}
          layers={layers}
          activeLayerId={activeLayerId}
          globalAdjustments={globalAdjustments}
          activeTool={activeTool}
          toolOptions={toolOptions}
          primaryColor={primaryColor}
          onChangePrimaryColor={(c) => setPrimaryColor(c)}
          zoom={zoom}
          panX={panX}
          panY={panY}
          onPanChange={(x, y) => {
            setPanX(x);
            setPanY(y);
          }}
          onZoomChange={(z) => setZoom(z)}
          onCommitAction={commitAction}
          onUpdateActiveLayerState={handleUpdateActiveLayerState}
          cropRect={cropRect}
          onUpdateCropRect={(rect) => setCropRect(rect)}
          selectionRect={selectionRect}
          onUpdateSelectionRect={(rect) => setSelectionRect(rect)}
          onHoverSampleColor={(info) => {
            setCursorPos({ x: info.x, y: info.y });
            setSampledColor({ hex: info.hex });
          }}
        />

        {/* Floating Right Inspector */}
        <InspectorPanel
          layers={layers}
          activeLayerId={activeLayerId}
          onSelectLayer={handleSelectLayer}
          onUpdateLayer={handleUpdateLayer}
          onAddLayer={handleAddLayer}
          onDuplicateLayer={handleDuplicateLayer}
          onMergeDown={handleMergeDown}
          onDeleteLayer={handleDeleteLayer}
          globalAdjustments={globalAdjustments}
          onUpdateAdjustments={handleUpdateAdjustments}
          onResetAdjustments={handleResetAdjustments}
          historyList={historyList}
          onJumpHistory={handleJumpHistory}
        />
      </div>

      {/* Bottom Status Bar */}
      <StatusBar
        canvasWidth={canvasWidth}
        canvasHeight={canvasHeight}
        zoom={zoom}
        cursorPos={cursorPos}
        sampledColor={sampledColor}
        activeToolName={activeTool.toUpperCase()}
      />

      {/* Modals */}
      <ExportModal
        isOpen={isExportModalOpen}
        onClose={() => setIsExportModalOpen(false)}
        canvasWidth={canvasWidth}
        canvasHeight={canvasHeight}
        onExportImage={(options) => exportCompositeImage(canvasWidth, canvasHeight, layers, globalAdjustments, options)}
        onExportProject={() => exportProjectFile(canvasWidth, canvasHeight, layers, globalAdjustments)}
      />

      <NewCanvasModal
        isOpen={isNewModalOpen}
        onClose={() => setIsNewModalOpen(false)}
        onCreateCanvas={handleCreateNewCanvas}
      />
    </div>
  );
}
