import React, { useState } from 'react';
import { X, Download, FileCode2 } from 'lucide-react';

export function ExportModal({
  isOpen,
  onClose,
  canvasWidth,
  canvasHeight,
  onExportImage,
  onExportProject
}) {
  const [format, setFormat] = useState('png'); // 'png' | 'jpeg' | 'webp' | 'project'
  const [quality, setQuality] = useState(92);
  const [scale, setScale] = useState(1.0);

  if (!isOpen) return null;

  const outputWidth = Math.round(canvasWidth * scale);
  const outputHeight = Math.round(canvasHeight * scale);

  const handleExport = () => {
    if (format === 'project') {
      onExportProject();
    } else {
      onExportImage({
        format,
        quality: quality / 100,
        scale
      });
    }
    onClose();
  };

  return (
    <div className="ios-modal-backdrop" onClick={onClose}>
      <div className="ios-modal-card" onClick={(e) => e.stopPropagation()}>
        {/* Header */}
        <div className="ios-modal-header">
          <h2 className="ios-modal-title">Export Artwork</h2>
          <button className="ios-icon-button" onClick={onClose}>
            <X size={16} />
          </button>
        </div>

        {/* Body */}
        <div className="ios-modal-body">
          {/* Format Selection */}
          <div>
            <label className="ios-input-label">Export Format</label>
            <div className="ios-segmented-control" style={{ width: '100%' }}>
              {[
                { id: 'png', label: 'PNG (Lossless)' },
                { id: 'jpeg', label: 'JPEG (Photo)' },
                { id: 'webp', label: 'WebP (Web)' },
                { id: 'project', label: '.openimg (Project)' }
              ].map((fmt) => (
                <button
                  key={fmt.id}
                  className={`segmented-item ${format === fmt.id ? 'active' : ''}`}
                  onClick={() => setFormat(fmt.id)}
                  style={{ flex: 1, justifyContent: 'center' }}
                >
                  {fmt.label}
                </button>
              ))}
            </div>
          </div>

          {format !== 'project' && (
            <>
              {/* Scaling */}
              <div>
                <label className="ios-input-label">Resolution Scale</label>
                <div className="ios-segmented-control" style={{ width: '100%' }}>
                  {[0.5, 1.0, 2.0, 3.0].map((s) => (
                    <button
                      key={s}
                      className={`segmented-item ${scale === s ? 'active' : ''}`}
                      onClick={() => setScale(s)}
                      style={{ flex: 1, justifyContent: 'center' }}
                    >
                      {s}x ({Math.round(canvasWidth * s)} × {Math.round(canvasHeight * s)})
                    </button>
                  ))}
                </div>
              </div>

              {/* JPEG / WebP Quality */}
              {(format === 'jpeg' || format === 'webp') && (
                <div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '6px' }}>
                    <label className="ios-input-label" style={{ margin: 0 }}>Quality</label>
                    <span style={{ fontSize: '11px', fontWeight: 600 }}>{quality}%</span>
                  </div>
                  <input
                    type="range"
                    className="ios-slider"
                    style={{ width: '100%' }}
                    min="10"
                    max="100"
                    value={quality}
                    onChange={(e) => setQuality(parseInt(e.target.value))}
                  />
                </div>
              )}

              {/* Details Summary */}
              <div className="ios-group-card" style={{ padding: '12px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px' }}>
                  <span style={{ color: 'var(--theme-text-secondary)' }}>Output Dimensions</span>
                  <strong>{outputWidth} × {outputHeight} px</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', marginTop: '6px' }}>
                  <span style={{ color: 'var(--theme-text-secondary)' }}>Color Space</span>
                  <span>sRGB 24-bit</span>
                </div>
              </div>
            </>
          )}

          {format === 'project' && (
            <div className="ios-group-card" style={{ padding: '14px', display: 'flex', gap: '12px', alignItems: 'center' }}>
              <FileCode2 size={32} color="var(--ios-blue)" />
              <div style={{ fontSize: '12px', color: 'var(--theme-text-secondary)' }}>
                Saves the complete document including all layers, bitmaps, vector text, blend modes, adjustments, and positions into a reloadable <code>.openimg</code> file.
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="ios-modal-footer">
          <button className="ios-button" onClick={onClose}>
            Cancel
          </button>
          <button className="ios-button primary" onClick={handleExport}>
            <Download size={14} strokeWidth={2.5} />
            <span>{format === 'project' ? 'Save Project File' : 'Save Image'}</span>
          </button>
        </div>
      </div>
    </div>
  );
}
