using System.Runtime.CompilerServices;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

public sealed partial class EmulatorLedger
{
    readonly Dictionary<Guid, int> _observationIndexes = [];
    int _publishedOrderObservationCount;
    int _observationReader;

    /// <summary>One reader, replaying committed facts from the last confirmed publication cursor.</summary>
    public async IAsyncEnumerable<FrameworkBrokerObservation> ObserveOrders([EnumeratorCancellation] CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _observationReader, 1, 0) != 0)
            throw new InvalidOperationException("Only one emulator order-observation reader is supported.");
        try
        {
            int position;
            lock (_sync) position = _committed.PublishedOrderObservationCount;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                FrameworkBrokerObservation? fact = null;
                lock (_sync)
                {
                    // Read the confirmed head even while a new financial operation has an uncertain save.
                    var history = _committed.Observations;
                    while (position < history.Length)
                    {
                        var next = history[position++];
                        if (next.Kind != FrameworkObservationKind.AccountSnapshot) { fact = next; break; }
                    }
                }
                if (fact is not null)
                {
                    yield return fact;
                    if (Faults.DuplicateExecutionCallbacks && fact.Kind is FrameworkObservationKind.Execution or FrameworkObservationKind.Commission)
                        yield return fact;
                }
                else await _orderEvents.Reader.ReadAsync(token).ConfigureAwait(false);
            }
        }
        finally { Volatile.Write(ref _observationReader, 0); }
    }

    /// <summary>Financial commit and publication cursor use the same recoverable save boundary.</summary>
    public void AcknowledgeOrderObservation(Guid observationId)
    {
        lock (_sync)
        {
            Transact(() =>
            {
                if (!_observationIndexes.TryGetValue(observationId, out var index)
                    || _observations[index].Kind == FrameworkObservationKind.AccountSnapshot)
                    throw new InvalidOperationException("Unknown order observation acknowledgement.");
                if (index < _publishedOrderObservationCount) return false;
                var next = _publishedOrderObservationCount;
                while (next < _observations.Count && _observations[next].Kind == FrameworkObservationKind.AccountSnapshot) next++;
                if (index != next) throw new InvalidOperationException("Order observations must be acknowledged in journal order.");
                _publishedOrderObservationCount = index + 1;
                Persist();
                return true;
            });
        }
    }
}
