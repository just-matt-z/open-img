import React from 'react';
import { TOOLS } from '../engine/constants';
import { Check, X, AlignLeft, AlignCenter, AlignRight } from 'lucide-react';

export function ToolOptionsBar({
  activeTool,
  toolOptions,
  onUpdateToolOptions,
  onApplyCrop,
  onCancelCrop,
  onClearSelection,
  onDeleteSelection,
  hasSelection
}) {
  return (
    <div className="ios-tool-options">
      {/* BRUSH & ERASER OPTIONS */}
      {(activeTool === TOOLS.BRUSH || activeTool === TOOLS.ERASER) && (
        <>
          <div className="tool-option-group">
            <span className="tool-option-label">Size</span>
            <div className="ios-slider-wrapper">
              <input
                type="range"
                className="ios-slider"
                min="1"
                max="250"
                value={activeTool === TOOLS.BRUSH ? toolOptions.brushSize : toolOptions.eraserSize}
                onChange={(e) => {
                  const val = parseInt(e.target.value);
                  onUpdateToolOptions(activeTool === TOOLS.BRUSH ? { brushSize: val } : { eraserSize: val });
                }}
              />
              <span className="slider-value-display">
                {activeTool === TOOLS.BRUSH ? toolOptions.brushSize : toolOptions.eraserSize}px
              </span>
            </div>
          </div>

          <div className="tool-option-group">
            <span className="tool-option-label">Opacity</span>
            <div className="ios-slider-wrapper">
              <input
                type="range"
                className="ios-slider"
                min="1"
                max="100"
                value={toolOptions.brushOpacity}
                onChange={(e) => onUpdateToolOptions({ brushOpacity: parseInt(e.target.value) })}
              />
              <span className="slider-value-display">{toolOptions.brushOpacity}%</span>
            </div>
          </div>

          <div className="tool-option-group">
            <span className="tool-option-label">Hardness</span>
            <div className="ios-slider-wrapper">
              <input
                type="range"
                className="ios-slider"
                min="0"
                max="100"
                value={toolOptions.brushHardness}
                onChange={(e) => onUpdateToolOptions({ brushHardness: parseInt(e.target.value) })}
              />
              <span className="slider-value-display">{toolOptions.brushHardness}%</span>
            </div>
          </div>
        </>
      )}

      {/* CROP OPTIONS */}
      {activeTool === TOOLS.CROP && (
        <>
          <div className="tool-option-group">
            <span className="tool-option-label">Aspect</span>
            <div className="ios-segmented-control">
              {['free', '1:1', '4:3', '16:9', 'original'].map((ratio) => (
                <button
                  key={ratio}
                  className={`segmented-item ${toolOptions.cropRatio === ratio ? 'active' : ''}`}
                  onClick={() => onUpdateToolOptions({ cropRatio: ratio })}
                >
                  {ratio.toUpperCase()}
                </button>
              ))}
            </div>
          </div>

          <div className="tool-option-group" style={{ marginLeft: 'auto', gap: '8px' }}>
            <button className="ios-button destructive" onClick={onCancelCrop}>
              <X size={13} />
              <span>Cancel</span>
            </button>
            <button className="ios-button primary" onClick={onApplyCrop}>
              <Check size={13} strokeWidth={2.5} />
              <span>Apply Crop</span>
            </button>
          </div>
        </>
      )}

      {/* MARQUEE & LASSO OPTIONS */}
      {(activeTool === TOOLS.MARQUEE || activeTool === TOOLS.LASSO) && (
        <>
          {activeTool === TOOLS.MARQUEE && (
            <div className="tool-option-group">
              <span className="tool-option-label">Shape</span>
              <div className="ios-segmented-control">
                <button
                  className={`segmented-item ${toolOptions.marqueeShape === 'rect' ? 'active' : ''}`}
                  onClick={() => onUpdateToolOptions({ marqueeShape: 'rect' })}
                >
                  Rectangular
                </button>
                <button
                  className={`segmented-item ${toolOptions.marqueeShape === 'ellipse' ? 'active' : ''}`}
                  onClick={() => onUpdateToolOptions({ marqueeShape: 'ellipse' })}
                >
                  Elliptical
                </button>
              </div>
            </div>
          )}

          {hasSelection && (
            <div className="tool-option-group" style={{ marginLeft: 'auto', gap: '8px' }}>
              <button className="ios-button" onClick={onClearSelection}>
                Clear Selection
              </button>
              <button className="ios-button destructive" onClick={onDeleteSelection}>
                Delete Content
              </button>
            </div>
          )}
        </>
      )}

      {/* TEXT OPTIONS */}
      {activeTool === TOOLS.TEXT && (
        <>
          <div className="tool-option-group">
            <span className="tool-option-label">Size</span>
            <div className="ios-slider-wrapper">
              <input
                type="range"
                className="ios-slider"
                min="14"
                max="120"
                value={toolOptions.textSize}
                onChange={(e) => onUpdateToolOptions({ textSize: parseInt(e.target.value) })}
              />
              <span className="slider-value-display">{toolOptions.textSize}px</span>
            </div>
          </div>

          <div className="tool-option-group">
            <span className="tool-option-label">Align</span>
            <div className="ios-segmented-control">
              <button
                className={`segmented-item ${toolOptions.textAlign === 'left' ? 'active' : ''}`}
                onClick={() => onUpdateToolOptions({ textAlign: 'left' })}
              >
                <AlignLeft size={13} />
              </button>
              <button
                className={`segmented-item ${toolOptions.textAlign === 'center' ? 'active' : ''}`}
                onClick={() => onUpdateToolOptions({ textAlign: 'center' })}
              >
                <AlignCenter size={13} />
              </button>
              <button
                className={`segmented-item ${toolOptions.textAlign === 'right' ? 'active' : ''}`}
                onClick={() => onUpdateToolOptions({ textAlign: 'right' })}
              >
                <AlignRight size={13} />
              </button>
            </div>
          </div>
        </>
      )}

      {/* SHAPE OPTIONS */}
      {activeTool === TOOLS.SHAPE && (
        <>
          <div className="tool-option-group">
            <span className="tool-option-label">Type</span>
            <div className="ios-segmented-control">
              {['rectangle', 'ellipse', 'line', 'arrow'].map((type) => (
                <button
                  key={type}
                  className={`segmented-item ${toolOptions.shapeType === type ? 'active' : ''}`}
                  onClick={() => onUpdateToolOptions({ shapeType: type })}
                >
                  {type.charAt(0).toUpperCase() + type.slice(1)}
                </button>
              ))}
            </div>
          </div>

          <div className="tool-option-group">
            <span className="tool-option-label">Border</span>
            <div className="ios-slider-wrapper">
              <input
                type="range"
                className="ios-slider"
                min="0"
                max="24"
                value={toolOptions.shapeStrokeWidth}
                onChange={(e) => onUpdateToolOptions({ shapeStrokeWidth: parseInt(e.target.value) })}
              />
              <span className="slider-value-display">{toolOptions.shapeStrokeWidth}px</span>
            </div>
          </div>
        </>
      )}

      {/* FILL OPTIONS */}
      {activeTool === TOOLS.FILL && (
        <div className="tool-option-group">
          <span className="tool-option-label">Tolerance</span>
          <div className="ios-slider-wrapper">
            <input
              type="range"
              className="ios-slider"
              min="1"
              max="128"
              value={toolOptions.fillTolerance}
              onChange={(e) => onUpdateToolOptions({ fillTolerance: parseInt(e.target.value) })}
            />
            <span className="slider-value-display">{toolOptions.fillTolerance}</span>
          </div>
        </div>
      )}

      {/* MOVE/TRANSFORM OPTIONS */}
      {activeTool === TOOLS.SELECT && (
        <div className="tool-option-group">
          <span className="tool-option-label">Transform</span>
          <span style={{ fontSize: '11px', color: 'var(--theme-text-secondary)' }}>
            Drag to move layer • Use corner handles to scale & rotate • Hold Shift for aspect ratio
          </span>
        </div>
      )}

      {/* EYEDROPPER OPTIONS */}
      {activeTool === TOOLS.EYEDROPPER && (
        <div className="tool-option-group">
          <span className="tool-option-label">Eyedropper</span>
          <span style={{ fontSize: '11px', color: 'var(--theme-text-secondary)' }}>
            Click anywhere on canvas to sample color into active palette
          </span>
        </div>
      )}
    </div>
  );
}
