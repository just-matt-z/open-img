using System.Collections.ObjectModel;
using System.Windows.Media;
using OpenImage.Models;

namespace OpenImage.Engine;

public class HistoryManager
{
    private const int MaxHistory = 40;
    private readonly List<HistoryState> _past = new();
    private readonly List<HistoryState> _future = new();
    private int _stepCounter = 0;

    public ObservableCollection<HistoryState> HistoryList { get; } = new();

    public bool CanUndo => _past.Count > 1;
    public bool CanRedo => _future.Count > 0;
    public int StateCount => _past.Count;

    public void PushState(string description, double width, double height, IEnumerable<Layer> layers, Adjustments adj, ImageSource? thumb = null)
    {
        _stepCounter++;
        var state = HistoryState.Create(description, width, height, layers, adj, thumb, _stepCounter);
        _past.Add(state);

        if (_past.Count > MaxHistory)
        {
            _past.RemoveAt(0);
        }

        _future.Clear();
        RefreshHistoryList();
    }

    public HistoryState? Undo()
    {
        if (!CanUndo) return null;

        var current = _past[^1];
        _past.RemoveAt(_past.Count - 1);
        _future.Add(current);

        var target = _past[^1];
        RefreshHistoryList();
        return CloneState(target);
    }

    public HistoryState? Redo()
    {
        if (!CanRedo) return null;

        var next = _future[^1];
        _future.RemoveAt(_future.Count - 1);
        _past.Add(next);

        RefreshHistoryList();
        return CloneState(next);
    }

    public HistoryState? JumpTo(int index)
    {
        if (index < 0 || index >= _past.Count) return null;

        while (_past.Count - 1 > index)
        {
            var item = _past[^1];
            _past.RemoveAt(_past.Count - 1);
            _future.Add(item);
        }

        RefreshHistoryList();
        return CloneState(_past[^1]);
    }

    public void ClearHistory(double width, double height, IEnumerable<Layer> layers, Adjustments adj, ImageSource? thumb = null)
    {
        _past.Clear();
        _future.Clear();
        _stepCounter = 1;
        var state = HistoryState.Create("Initial State", width, height, layers, adj, thumb, _stepCounter);
        _past.Add(state);
        RefreshHistoryList();
    }

    private void RefreshHistoryList()
    {
        HistoryList.Clear();
        for (int i = 0; i < _past.Count; i++)
        {
            var s = _past[i];
            s.IsCurrentState = (i == _past.Count - 1);
            HistoryList.Add(s);
        }
    }

    private static HistoryState CloneState(HistoryState s)
    {
        var clone = HistoryState.Create(s.Description, s.CanvasWidth, s.CanvasHeight, s.Layers, s.Adjustments, s.Thumbnail, s.StepNumber);
        clone.IsCurrentState = s.IsCurrentState;
        return clone;
    }
}
