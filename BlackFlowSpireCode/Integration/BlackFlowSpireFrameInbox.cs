namespace BlackFlowSpire.Integration;

internal sealed class BlackFlowSpireFrameInbox<T>
{
    private readonly Dictionary<(long Command, int Index), TaskCompletionSource<T>> _frames = [];
    private (long Command, int Index) _read;
    private bool _closed;

    public void Offer(long command, int index, T frame)
    {
        if (_closed) return;
        if (_frames.TryGetValue((command, index), out var existing)) existing.TrySetResult(frame);
        else if ((command, index).CompareTo(_read) > 0) Slot(command, index).TrySetResult(frame);
    }

    public async Task<T> Next(long command, int index)
    {
        if (_closed) throw new OperationCanceledException();
        var key = (command, index);
        if (key.CompareTo(_read) <= 0) throw new InvalidOperationException("Forest frame already consumed.");
        _read = key;
        try { return await Slot(command, index).Task.WaitAsync(TimeSpan.FromSeconds(90)); }
        finally { _frames.Remove(key); }
    }

    public void Complete(long command)
    {
        _read = (command, int.MaxValue);
        foreach (var key in _frames.Keys.Where(key => key.Command <= command).ToArray())
            _frames.Remove(key);
    }

    public void Cancel()
    {
        _closed = true;
        foreach (var slot in _frames.Values) slot.TrySetCanceled();
        _frames.Clear();
    }

    private TaskCompletionSource<T> Slot(long command, int index)
    {
        if (!_frames.TryGetValue((command, index), out var slot))
            _frames.Add((command, index), slot = new(TaskCreationOptions.RunContinuationsAsynchronously));
        return slot;
    }
}
