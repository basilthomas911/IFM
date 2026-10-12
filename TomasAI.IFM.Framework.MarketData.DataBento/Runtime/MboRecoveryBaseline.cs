namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed class MboRecoveryBaseline
{
    private ulong _lastSequence;

    public bool SnapshotStarted { get; private set; }
    public bool SnapshotComplete { get; private set; }
    public bool LiveBoundaryReached { get; private set; }
    public bool IsReady => SnapshotComplete && LiveBoundaryReached;

    /// <summary>Resets accumulated state for a fresh feed or session generation.</summary>
    public void Reset()
    {
        _lastSequence = 0;
        SnapshotStarted = false;
        SnapshotComplete = false;
        LiveBoundaryReached = false;
    }

    /// <summary>Begins rebuilding the market-by-order snapshot baseline.</summary>
    public void BeginSnapshot()
    {
        Reset();
        SnapshotStarted = true;
    }

    /// <summary>Applies a snapshot record&apos;s sequence to the market-by-order baseline.</summary>
    /// <param name="sequence">The provider record sequence number.</param>
    public void ApplySnapshotRecord(ulong sequence)
    {
        if (!SnapshotStarted || SnapshotComplete)
        {
            throw new InvalidOperationException("MBO snapshot records require an active snapshot.");
        }
        RequireNext(sequence);
    }

    /// <summary>Marks the market-by-order snapshot baseline complete.</summary>
    public void CompleteSnapshot()
    {
        if (!SnapshotStarted)
        {
            throw new InvalidOperationException("An MBO snapshot was not started.");
        }
        SnapshotComplete = true;
    }

    /// <summary>Checks the live record sequence against the established market-by-order baseline.</summary>
    /// <param name="sequence">The provider record sequence number.</param>
    public void ApplyLiveRecord(ulong sequence)
    {
        if (!SnapshotComplete)
        {
            throw new InvalidOperationException("MBO live records require a complete snapshot.");
        }
        RequireNext(sequence);
        LiveBoundaryReached = true;
    }

    private void RequireNext(ulong sequence)
    {
        if (_lastSequence != 0 && sequence != _lastSequence + 1)
        {
            throw new InvalidDataException(
                $"MBO sequence continuity failed: expected {_lastSequence + 1}, received {sequence}.");
        }
        _lastSequence = sequence;
    }
}
