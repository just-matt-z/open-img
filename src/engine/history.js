import { cloneLayer } from './layer';

const MAX_HISTORY_STATES = 40;

export class HistoryManager {
  constructor() {
    this.past = [];
    this.future = [];
  }

  /**
   * Captures a snapshot of current document state
   */
  pushState(description, canvasWidth, canvasHeight, layers, globalAdjustments) {
    // Deep clone layers to preserve bitmap state
    const clonedLayers = layers.map(l => cloneLayer(l));
    const snapshot = {
      description: description || 'Edit',
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
      canvasWidth,
      canvasHeight,
      layers: clonedLayers,
      globalAdjustments: { ...globalAdjustments }
    };

    this.past.push(snapshot);
    if (this.past.length > MAX_HISTORY_STATES) {
      this.past.shift();
    }
    // Clear redo history on new action
    this.future = [];
  }

  canUndo() {
    return this.past.length > 1;
  }

  canRedo() {
    return this.future.length > 0;
  }

  undo(currentState) {
    if (!this.canUndo()) return null;
    const current = this.past.pop();
    this.future.push(current);
    const targetState = this.past[this.past.length - 1];
    return this.cloneState(targetState);
  }

  redo() {
    if (!this.canRedo()) return null;
    const nextState = this.future.pop();
    this.past.push(nextState);
    return this.cloneState(nextState);
  }

  jumpTo(index) {
    if (index < 0 || index >= this.past.length) return null;
    // Move states between past and future
    while (this.past.length - 1 > index) {
      this.future.push(this.past.pop());
    }
    const targetState = this.past[this.past.length - 1];
    return this.cloneState(targetState);
  }

  getHistoryList() {
    return this.past.map((state, idx) => ({
      index: idx,
      description: state.description,
      timestamp: state.timestamp,
      isCurrent: idx === this.past.length - 1
    }));
  }

  cloneState(state) {
    return {
      canvasWidth: state.canvasWidth,
      canvasHeight: state.canvasHeight,
      layers: state.layers.map(l => cloneLayer(l)),
      globalAdjustments: { ...state.globalAdjustments }
    };
  }
}
