using SummerTrip.Core.Dialogue;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SummerTrip.Features.Vn;

public sealed class VNRollbackBuffer : IDisposable
{
    public const int MaximumSnapshots = 100;

    private readonly VNDirector _director;
    private readonly List<VNRollbackSnapshot> _snapshots = new();

    public VNRollbackBuffer(VNDirector director)
    {
        _director = director ?? throw new ArgumentNullException(nameof(director));
        _director.StableLineReached += OnStableLineReached;
        _director.SaveRestored += ResetToCurrent;
    }

    public event Action? AvailabilityChanged;
    public int Count => _snapshots.Count;
    public bool CanRollback => _snapshots.Count > 1;

    public void Dispose()
    {
        _director.StableLineReached -= OnStableLineReached;
        _director.SaveRestored -= ResetToCurrent;
    }

    public async Task<bool> RollbackAsync()
    {
        if (_director.Operation != OperationState.None)
        {
            return false;
        }

        if (_director.Interaction == InteractionState.Typing)
        {
            _director.CompleteCurrentTyping();
        }

        if (
            _director.Interaction is not (
                InteractionState.AwaitingAdvance or InteractionState.AwaitingChoice
            ) ||
            !CanRollback
        )
        {
            return false;
        }

        var removed = _snapshots[^1];
        _snapshots.RemoveAt(_snapshots.Count - 1);
        try
        {
            await _director.RestoreRollbackAsync(_snapshots[^1]);
            AvailabilityChanged?.Invoke();
            return true;
        }
        catch
        {
            _snapshots.Add(removed);
            AvailabilityChanged?.Invoke();
            throw;
        }
    }

    public void ResetToCurrent()
    {
        _snapshots.Clear();
        if (
            _director.Interaction is
                InteractionState.AwaitingAdvance or InteractionState.AwaitingChoice
        )
        {
            _snapshots.Add(_director.CaptureRollbackSnapshot());
        }
        AvailabilityChanged?.Invoke();
    }

    private void OnStableLineReached(DialogueEntry line)
    {
        _snapshots.Add(_director.CaptureRollbackSnapshot());
        if (_snapshots.Count > MaximumSnapshots)
        {
            _snapshots.RemoveRange(0, _snapshots.Count - MaximumSnapshots);
        }
        AvailabilityChanged?.Invoke();
    }
}
