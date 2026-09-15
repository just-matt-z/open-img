import React from 'react';

export function StatusBar({
  canvasWidth,
  canvasHeight,
  zoom,
  cursorPos,
  sampledColor,
  activeToolName
}) {
  return (
    <footer className="ios-statusbar">
      <div className="statusbar-left">
        <span>Tool: <strong>{activeToolName}</strong></span>
        <span>Canvas: <strong>{canvasWidth} × {canvasHeight} px</strong></span>
        <span>Zoom: <strong>{Math.round(zoom * 100)}%</strong></span>
      </div>

      <div className="statusbar-right">
        {cursorPos && (
          <span>X: <strong>{cursorPos.x}</strong>, Y: <strong>{cursorPos.y}</strong></span>
        )}

        {sampledColor && (
          <div className="color-sample-preview" title="Color under cursor">
            <div className="color-sample-dot" style={{ backgroundColor: sampledColor.hex }} />
            <span>{sampledColor.hex}</span>
          </div>
        )}

        <span style={{ color: 'var(--theme-text-tertiary)' }}>Open Image Studio • Ready</span>
      </div>
    </footer>
  );
}
