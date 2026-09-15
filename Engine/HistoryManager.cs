using System.Collections.ObjectModel;
using OpenImage.Models;

namespace OpenImage.Engine;

public class HistoryManager
{
    private const int MaxHistory = 35;
    private readonly List<HistoryState> _past = new();
    private readonly List<HistoryState> _future = new();

    public ObservableCollection<HistoryState> HistoryList { get; } = new();

    public bool CanUndo => _past.Count > 1;
    public bool CanRedo => _future.Count > 0;

    public void PushState(string description, double width, double height, IEnumerable<Layer> layers, Adjustments adj)
    {
        var state = HistoryState.Create(description, width, height, layers, adj);
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

    private void RefreshHistoryList()
    {
        HistoryList.Clear();
        foreach (var s in _past)
        {
            HistoryList.Add(s);
        }
    }

    private static HistoryState CloneState(HistoryState s)
    {
        return HistoryState.Create(s.Description, s.CanvasWidth, s.CanvasHeight, s.Layers, s.Adjustments);
    }
}
