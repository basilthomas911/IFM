using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

sealed class ActorThreadPoolMetricsState
{
    long _drainedMessageCount;
    int _activeMailboxCount;
    int _measureDrain;

    public long DrainedMessageCount => Volatile.Read(ref _drainedMessageCount);
    public int ActiveMailboxCount => Math.Max(0, Volatile.Read(ref _activeMailboxCount));

    public void RecordMailboxStarted() => Interlocked.Increment(ref _activeMailboxCount);

    public void RecordMailboxCompleted() => Interlocked.Decrement(ref _activeMailboxCount);

    public void BeginDrainMeasurement()
    {
        Interlocked.Exchange(ref _drainedMessageCount, 0);
        Volatile.Write(ref _measureDrain, 1);
    }

    public void RecordMessageCompleted()
    {
        if (Volatile.Read(ref _measureDrain) != 0)
            Interlocked.Increment(ref _drainedMessageCount);
    }
}
