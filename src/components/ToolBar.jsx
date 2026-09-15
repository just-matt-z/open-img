import React, { useRef } from 'react';
import { 
  Move, 
  Crop, 
  Paintbrush, 
  Eraser, 
  PaintBucket, 
  Type, 
  Square, 
  Pipette, 
  SquareDashed, 
  Lasso,
  ArrowLeftRight
} from 'lucide-react';
import { TOOLS } from '../engine/constants';

export function ToolBar({
  activeTool,
  onSelectTool,
  primaryColor,
  secondaryColor,
  onChangePrimaryColor,
  onChangeSecondaryColor,
  onSwapColors
}) {
  const primaryColorInputRef = useRef(null);
  const secondaryColorInputRef = useRef(null);

  const toolsList = [
    { id: TOOLS.SELECT, label: 'Move / Transform', icon: Move, key: 'V' },
    { id: TOOLS.MARQUEE, label: 'Marquee Selection', icon: SquareDashed, key: 'M' },
    { id: TOOLS.LASSO, label: 'Lasso Selection', icon: Lasso, key: 'L' },
    { id: TOOLS.CROP, label: 'Crop & Straighten', icon: Crop, key: 'C' },
    { id: TOOLS.BRUSH, label: 'Brush Tool', icon: Paintbrush, key: 'B' },
    { id: TOOLS.ERASER, label: 'Eraser Tool', icon: Eraser, key: 'E' },
    { id: TOOLS.FILL, label: 'Paint Bucket', icon: PaintBucket, key: 'G' },
    { id: TOOLS.TEXT, label: 'Text Layer', icon: Type, key: 'T' },
    { id: TOOLS.SHAPE, label: 'Vector Shapes', icon: Square, key: 'U' },
    { id: TOOLS.EYEDROPPER, label: 'Eyedropper', icon: Pipette, key: 'I' },
  ];

  return (
    <aside className="ios-toolbar">
      {toolsList.map(tool => {
        const IconComponent = tool.icon;
        const isActive = activeTool === tool.id;
        return (
          <button
            key={tool.id}
            className={`tool-button ${isActive ? 'active' : ''}`}
            onClick={() => onSelectTool(tool.id)}
            title={`${tool.label} (${tool.key})`}
          >
            <IconComponent size={18} strokeWidth={1.75} />
            <span className="tool-shortcut-badge">{tool.key}</span>
          </button>
        );
      })}

      <div className="toolbar-divider" />

      {/* Dual Color Swatches */}
      <div className="color-chips-container" title="Foreground / Background Colors (X to swap, D for default)">
        <button 
          className="swap-color-btn" 
          onClick={onSwapColors}
          title="Swap Colors (X)"
        >
          <ArrowLeftRight size={8} />
        </button>

        <div 
          className="color-chip foreground" 
          style={{ backgroundColor: primaryColor }}
          onClick={() => primaryColorInputRef.current?.click()}
          title="Change Foreground Color"
        />

        <div 
          className="color-chip background" 
          style={{ backgroundColor: secondaryColor }}
          onClick={() => secondaryColorInputRef.current?.click()}
          title="Change Background Color"
        />

        {/* Hidden HTML Color Pickers */}
        <input 
          ref={primaryColorInputRef}
          type="color" 
          value={primaryColor} 
          onChange={(e) => onChangePrimaryColor(e.target.value)}
          style={{ display: 'none' }}
        />
        <input 
          ref={secondaryColorInputRef}
          type="color" 
          value={secondaryColor} 
          onChange={(e) => onChangeSecondaryColor(e.target.value)}
          style={{ display: 'none' }}
        />
      </div>
    </aside>
  );
}
