import React, { useState } from 'react';
import { 
  Eye, 
  EyeOff, 
  Lock, 
  Unlock, 
  Plus, 
  Copy, 
  Trash2, 
  RotateCcw,
  Sliders,
  Layers as LayersIcon,
  History as HistoryIcon,
  ArrowDownToLine
} from 'lucide-react';
import { BLEND_MODES } from '../engine/constants';

export function InspectorPanel({
  layers,
  activeLayerId,
  onSelectLayer,
  onUpdateLayer,
  onAddLayer,
  onDuplicateLayer,
  onMergeDown,
  onDeleteLayer,
  globalAdjustments,
  onUpdateAdjustments,
  onResetAdjustments,
  historyList,
  onJumpHistory
}) {
  const [activeTab, setActiveTab] = useState('layers'); // 'layers' | 'adjust' | 'history'

  const activeLayer = layers.find(l => l.id === activeLayerId) || layers[0];

  return (
    <aside className="ios-inspector">
      {/* Segmented Tab Bar */}
      <div className="inspector-header">
        <div className="ios-segmented-control" style={{ width: '100%' }}>
          <button
            className={`segmented-item ${activeTab === 'layers' ? 'active' : ''}`}
            onClick={() => setActiveTab('layers')}
            style={{ flex: 1, justifyContent: 'center' }}
          >
            <LayersIcon size={12} />
            <span>Layers</span>
          </button>
          <button
            className={`segmented-item ${activeTab === 'adjust' ? 'active' : ''}`}
            onClick={() => setActiveTab('adjust')}
            style={{ flex: 1, justifyContent: 'center' }}
          >
            <Sliders size={12} />
            <span>Adjust</span>
          </button>
          <button
            className={`segmented-item ${activeTab === 'history' ? 'active' : ''}`}
            onClick={() => setActiveTab('history')}
            style={{ flex: 1, justifyContent: 'center' }}
          >
            <HistoryIcon size={12} />
            <span>History</span>
          </button>
        </div>
      </div>

      <div className="inspector-content">
        {/* ===================================================================
            TAB 1: LAYERS
           =================================================================== */}
        {activeTab === 'layers' && (
          <>
            {/* Blend Mode & Opacity for Active Layer */}
            {activeLayer && (
              <div className="ios-section">
                <span className="ios-section-title">Layer Properties</span>
                <div className="ios-group-card" style={{ padding: '10px 12px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <span style={{ fontSize: '11px', color: 'var(--theme-text-secondary)', width: '60px' }}>Blend</span>
                    <select
                      className="blend-mode-select"
                      value={activeLayer.blendMode || 'source-over'}
                      onChange={(e) => onUpdateLayer(activeLayer.id, { blendMode: e.target.value })}
                    >
                      {BLEND_MODES.map((b) => (
                        <option key={b.id} value={b.id}>
                          {b.label}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <span style={{ fontSize: '11px', color: 'var(--theme-text-secondary)', width: '60px' }}>Opacity</span>
                    <div className="ios-slider-wrapper" style={{ flex: 1 }}>
                      <input
                        type="range"
                        className="ios-slider"
                        style={{ width: '100%' }}
                        min="0"
                        max="100"
                        value={Math.round((activeLayer.opacity ?? 1.0) * 100)}
                        onChange={(e) => onUpdateLayer(activeLayer.id, { opacity: parseInt(e.target.value) / 100 })}
                      />
                      <span className="slider-value-display">
                        {Math.round((activeLayer.opacity ?? 1.0) * 100)}%
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* Layer Stack (Reversed so top layer appears on top of the list) */}
            <div className="ios-section" style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
              <span className="ios-section-title">Stack ({layers.length})</span>
              <div className="layers-list" style={{ flex: 1 }}>
                {[...layers].reverse().map((layer) => {
                  const isSelected = layer.id === activeLayerId;
                  return (
                    <div
                      key={layer.id}
                      className={`layer-card ${isSelected ? 'active' : ''}`}
                      onClick={() => onSelectLayer(layer.id)}
                    >
                      {/* Layer Thumbnail */}
                      <div className="layer-thumb-box">
                        {layer.thumbnail ? (
                          <img src={layer.thumbnail} alt="" className="layer-thumb-img" />
                        ) : (
                          <div style={{ fontSize: '10px', color: 'var(--theme-text-tertiary)' }}>IMG</div>
                        )}
                      </div>

                      {/* Layer Info */}
                      <div className="layer-info">
                        <div className="layer-title">{layer.name}</div>
                        <div className="layer-type-tag">
                          {layer.type} • {Math.round((layer.opacity ?? 1) * 100)}%
                        </div>
                      </div>

                      {/* Visibility & Lock Controls */}
                      <div className="layer-actions" onClick={(e) => e.stopPropagation()}>
                        <button
                          className="ios-icon-button"
                          style={{ width: '26px', height: '26px' }}
                          onClick={() => onUpdateLayer(layer.id, { visible: !layer.visible })}
                          title={layer.visible ? 'Hide Layer' : 'Show Layer'}
                        >
                          {layer.visible ? (
                            <Eye size={13} strokeWidth={1.75} />
                          ) : (
                            <EyeOff size={13} strokeWidth={1.75} color="var(--theme-text-tertiary)" />
                          )}
                        </button>

                        <button
                          className="ios-icon-button"
                          style={{ width: '26px', height: '26px' }}
                          onClick={() => onUpdateLayer(layer.id, { locked: !layer.locked })}
                          title={layer.locked ? 'Unlock Layer' : 'Lock Layer'}
                        >
                          {layer.locked ? (
                            <Lock size={12} strokeWidth={2} color="var(--ios-orange)" />
                          ) : (
                            <Unlock size={12} strokeWidth={1.75} color="var(--theme-text-tertiary)" />
                          )}
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Bottom Actions */}
            <div className="layers-footer">
              <button className="ios-icon-button" onClick={onAddLayer} title="New Layer (Ctrl+Shift+N)">
                <Plus size={16} strokeWidth={2} />
              </button>
              <button
                className="ios-icon-button"
                onClick={() => activeLayer && onDuplicateLayer(activeLayer.id)}
                disabled={!activeLayer}
                title="Duplicate Layer (Ctrl+J)"
              >
                <Copy size={15} strokeWidth={1.75} />
              </button>
              <button
                className="ios-icon-button"
                onClick={() => activeLayer && onMergeDown(activeLayer.id)}
                disabled={layers.length <= 1 || layers[0]?.id === activeLayerId}
                title="Merge Down (Ctrl+E)"
              >
                <ArrowDownToLine size={15} strokeWidth={1.75} />
              </button>
              <button
                className="ios-icon-button"
                onClick={() => activeLayer && onDeleteLayer(activeLayer.id)}
                disabled={layers.length <= 1}
                title="Delete Layer (Delete)"
              >
                <Trash2 size={15} strokeWidth={1.75} color="var(--ios-red)" />
              </button>
            </div>
          </>
        )}

        {/* ===================================================================
            TAB 2: ADJUSTMENTS (GPU FILTERS)
           =================================================================== */}
        {activeTab === 'adjust' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
            {/* Tone Section */}
            <div className="ios-section">
              <span className="ios-section-title">Light & Tone</span>
              <div className="ios-group-card">
                <AdjustmentRow
                  label="Brightness"
                  min={-100}
                  max={100}
                  value={globalAdjustments.brightness}
                  onChange={(v) => onUpdateAdjustments({ brightness: v })}
                />
                <AdjustmentRow
                  label="Contrast"
                  min={-100}
                  max={100}
                  value={globalAdjustments.contrast}
                  onChange={(v) => onUpdateAdjustments({ contrast: v })}
                />
                <AdjustmentRow
                  label="Exposure"
                  min={-100}
                  max={100}
                  value={globalAdjustments.exposure}
                  onChange={(v) => onUpdateAdjustments({ exposure: v })}
                />
              </div>
            </div>

            {/* Color Section */}
            <div className="ios-section">
              <span className="ios-section-title">Color Balance</span>
              <div className="ios-group-card">
                <AdjustmentRow
                  label="Saturation"
                  min={-100}
                  max={100}
                  value={globalAdjustments.saturation}
                  onChange={(v) => onUpdateAdjustments({ saturation: v })}
                />
                <AdjustmentRow
                  label="Vibrance"
                  min={-100}
                  max={100}
                  value={globalAdjustments.vibrance}
                  onChange={(v) => onUpdateAdjustments({ vibrance: v })}
                />
                <AdjustmentRow
                  label="Hue Angle"
                  min={-180}
                  max={180}
                  value={globalAdjustments.hue}
                  onChange={(v) => onUpdateAdjustments({ hue: v })}
                />
              </div>
            </div>

            {/* Effects & Filters */}
            <div className="ios-section">
              <span className="ios-section-title">Creative Effects</span>
              <div className="ios-group-card">
                <AdjustmentRow
                  label="Gaussian Blur"
                  min={0}
                  max={30}
                  value={globalAdjustments.blur}
                  onChange={(v) => onUpdateAdjustments({ blur: v })}
                />
                <div className="ios-row">
                  <span className="ios-row-label">Grayscale (B&W)</span>
                  <input
                    type="checkbox"
                    checked={globalAdjustments.grayscale}
                    onChange={(e) => onUpdateAdjustments({ grayscale: e.target.checked })}
                  />
                </div>
                <div className="ios-row">
                  <span className="ios-row-label">Invert Colors</span>
                  <input
                    type="checkbox"
                    checked={globalAdjustments.invert}
                    onChange={(e) => onUpdateAdjustments({ invert: e.target.checked })}
                  />
                </div>
                <div className="ios-row">
                  <span className="ios-row-label">Vintage Sepia</span>
                  <input
                    type="checkbox"
                    checked={globalAdjustments.sepia}
                    onChange={(e) => onUpdateAdjustments({ sepia: e.target.checked })}
                  />
                </div>
              </div>
            </div>

            <button className="ios-button destructive" onClick={onResetAdjustments} style={{ alignSelf: 'center' }}>
              <RotateCcw size={12} />
              <span>Reset All Adjustments</span>
            </button>
          </div>
        )}

        {/* ===================================================================
            TAB 3: HISTORY
           =================================================================== */}
        {activeTab === 'history' && (
          <div className="ios-section" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
            <span className="ios-section-title">Timeline ({historyList.length})</span>
            <div className="ios-group-card" style={{ flex: 1, overflowY: 'auto', maxHeight: '420px' }}>
              {historyList.map((item) => (
                <div
                  key={item.index}
                  className="ios-row"
                  onClick={() => onJumpHistory(item.index)}
                  style={{
                    cursor: 'pointer',
                    background: item.isCurrent ? 'rgba(0, 122, 255, 0.08)' : 'transparent',
                    borderLeft: item.isCurrent ? '3px solid var(--ios-blue)' : '3px solid transparent'
                  }}
                >
                  <span style={{ fontWeight: item.isCurrent ? 600 : 400, color: item.isCurrent ? 'var(--ios-blue)' : 'inherit' }}>
                    {item.description}
                  </span>
                  <span style={{ fontSize: '10px', color: 'var(--theme-text-tertiary)' }}>
                    {item.timestamp}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </aside>
  );
}

function AdjustmentRow({ label, min, max, value, onChange }) {
  return (
    <div className="ios-row">
      <span className="ios-row-label" style={{ fontSize: '12px' }}>{label}</span>
      <div className="ios-slider-wrapper">
        <input
          type="range"
          className="ios-slider"
          min={min}
          max={max}
          value={value || 0}
          onChange={(e) => onChange(parseInt(e.target.value))}
        />
        <span className="slider-value-display">{value || 0}</span>
      </div>
    </div>
  );
}
