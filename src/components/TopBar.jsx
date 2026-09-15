import React from 'react';
import { 
  Undo2, 
  Redo2, 
  Download, 
  Plus, 
  FolderOpen, 
  Sun, 
  Moon,
  Maximize2
} from 'lucide-react';

export function TopBar({
  docName = 'untitled-1.png',
  canvasWidth,
  canvasHeight,
  zoom,
  onZoomChange,
  onZoomFit,
  canUndo,
  canRedo,
  onUndo,
  onRedo,
  onNewClick,
  onOpenClick,
  onExportClick,
  theme,
  onToggleTheme
}) {
  const zoomPercent = Math.round(zoom * 100);

  return (
    <header className="ios-topbar">
      {/* Left: App Branding & File Actions */}
      <div className="topbar-left">
        <div className="app-brand">
          <svg width="24" height="24" viewBox="0 0 100 100" style={{ borderRadius: '6px' }}>
            <rect width="100" height="100" rx="22" fill="#007AFF" />
            <path d="M25 65 L45 42 L60 58 L70 48 L85 65 Z" fill="#FFFFFF" />
            <circle cx="40" cy="32" r="8" fill="#FFFFFF" />
          </svg>
          <span>Open Image</span>
          <span className="app-brand-badge">PRO</span>
        </div>

        <div className="ios-segmented-control" style={{ marginLeft: '6px' }}>
          <button className="segmented-item" onClick={onNewClick} title="New Document (Ctrl+N)">
            <Plus size={13} strokeWidth={2} />
            <span>New</span>
          </button>
          <button className="segmented-item" onClick={onOpenClick} title="Open Image (Ctrl+O)">
            <FolderOpen size={13} strokeWidth={2} />
            <span>Open</span>
          </button>
        </div>

        <div className="doc-info" style={{ marginLeft: '8px' }}>
          <span>{docName}</span>
          <span className="doc-info-dim">({canvasWidth} × {canvasHeight}px)</span>
        </div>
      </div>

      {/* Center: Zoom Controls */}
      <div className="topbar-center">
        <div className="ios-segmented-control">
          <button 
            className={`segmented-item ${zoomPercent === 50 ? 'active' : ''}`}
            onClick={() => onZoomChange(0.5)}
          >
            50%
          </button>
          <button 
            className={`segmented-item ${zoomPercent === 100 ? 'active' : ''}`}
            onClick={() => onZoomChange(1.0)}
          >
            100%
          </button>
          <button 
            className={`segmented-item ${zoomPercent === 200 ? 'active' : ''}`}
            onClick={() => onZoomChange(2.0)}
          >
            200%
          </button>
          <button 
            className="segmented-item"
            onClick={onZoomFit}
            title="Fit to Screen (Ctrl+0)"
          >
            <Maximize2 size={12} strokeWidth={2} />
            <span>Fit</span>
          </button>
        </div>
      </div>

      {/* Right: History & Export Actions */}
      <div className="topbar-right">
        <button 
          className="ios-icon-button" 
          onClick={onUndo} 
          disabled={!canUndo} 
          title="Undo (Ctrl+Z)"
        >
          <Undo2 size={16} strokeWidth={2} />
        </button>

        <button 
          className="ios-icon-button" 
          onClick={onRedo} 
          disabled={!canRedo} 
          title="Redo (Ctrl+Y)"
        >
          <Redo2 size={16} strokeWidth={2} />
        </button>

        <button 
          className="ios-icon-button" 
          onClick={onToggleTheme} 
          title={`Switch to ${theme === 'light' ? 'Dark' : 'Day'} Mode`}
        >
          {theme === 'light' ? <Moon size={16} strokeWidth={2} /> : <Sun size={16} strokeWidth={2} />}
        </button>

        <button 
          className="ios-button primary" 
          onClick={onExportClick} 
          title="Export Image (Ctrl+E)"
        >
          <Download size={14} strokeWidth={2.5} />
          <span>Export</span>
        </button>
      </div>
    </header>
  );
}
