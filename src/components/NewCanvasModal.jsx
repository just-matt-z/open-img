import React, { useState } from 'react';
import { X, Plus, Sparkles } from 'lucide-react';
import { CANVAS_PRESETS } from '../engine/constants';

export function NewCanvasModal({
  isOpen,
  onClose,
  onCreateCanvas
}) {
  const [width, setWidth] = useState(1920);
  const [height, setHeight] = useState(1080);
  const [bgType, setBgType] = useState('white'); // 'white' | 'transparent' | 'dark'

  if (!isOpen) return null;

  const handleSelectPreset = (preset) => {
    setWidth(preset.width);
    setHeight(preset.height);
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    onCreateCanvas({
      width: Math.max(100, Math.min(8192, parseInt(width) || 1920)),
      height: Math.max(100, Math.min(8192, parseInt(height) || 1080)),
      background: bgType
    });
    onClose();
  };

  return (
    <div className="ios-modal-backdrop" onClick={onClose}>
      <div className="ios-modal-card" onClick={(e) => e.stopPropagation()}>
        {/* Header */}
        <div className="ios-modal-header">
          <h2 className="ios-modal-title">New Artwork Canvas</h2>
          <button className="ios-icon-button" onClick={onClose}>
            <X size={16} />
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          {/* Body */}
          <div className="ios-modal-body">
            {/* Presets */}
            <div>
              <label className="ios-input-label">Quick Presets</label>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '6px', maxHeight: '140px', overflowY: 'auto' }}>
                {CANVAS_PRESETS.map((preset) => (
                  <button
                    key={preset.name}
                    type="button"
                    className="ios-button"
                    style={{
                      justifyContent: 'flex-start',
                      background: width === preset.width && height === preset.height ? 'rgba(0, 122, 255, 0.12)' : 'var(--theme-bg)',
                      border: '0.5px solid var(--theme-hairline)',
                      padding: '8px 10px',
                      borderRadius: '8px'
                    }}
                    onClick={() => handleSelectPreset(preset)}
                  >
                    <div style={{ textAlign: 'left' }}>
                      <div style={{ fontSize: '12px', fontWeight: 500 }}>{preset.name}</div>
                      <div style={{ fontSize: '10px', color: 'var(--theme-text-tertiary)' }}>
                        {preset.width} × {preset.height} px
                      </div>
                    </div>
                  </button>
                ))}
              </div>
            </div>

            {/* Custom Dimensions */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
              <div>
                <label className="ios-input-label">Width (px)</label>
                <input
                  type="number"
                  className="ios-text-input"
                  min="100"
                  max="8192"
                  value={width}
                  onChange={(e) => setWidth(e.target.value)}
                  required
                />
              </div>

              <div>
                <label className="ios-input-label">Height (px)</label>
                <input
                  type="number"
                  className="ios-text-input"
                  min="100"
                  max="8192"
                  value={height}
                  onChange={(e) => setHeight(e.target.value)}
                  required
                />
              </div>
            </div>

            {/* Canvas Background */}
            <div>
              <label className="ios-input-label">Canvas Background</label>
              <div className="ios-segmented-control" style={{ width: '100%' }}>
                {[
                  { id: 'white', label: 'White' },
                  { id: 'transparent', label: 'Transparent' },
                  { id: 'dark', label: 'Dark Studio' }
                ].map((bg) => (
                  <button
                    key={bg.id}
                    type="button"
                    className={`segmented-item ${bgType === bg.id ? 'active' : ''}`}
                    onClick={() => setBgType(bg.id)}
                    style={{ flex: 1, justifyContent: 'center' }}
                  >
                    {bg.label}
                  </button>
                ))}
              </div>
            </div>
          </div>

          {/* Footer */}
          <div className="ios-modal-footer">
            <button type="button" className="ios-button" onClick={onClose}>
              Cancel
            </button>
            <button type="submit" className="ios-button primary">
              <Plus size={14} strokeWidth={2.5} />
              <span>Create Canvas</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
