using System.Runtime.CompilerServices;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

public sealed partial class EmulatorLedger
{
    int _accountObservationReader;
    // A new reader can reconstruct the latest wakeup from the durable ledger; no producer waits for it.
    async IAsyncEnumerable<FrameworkBrokerObservation> ObserveLatestAccount([EnumeratorCancellation] CancellationToken token)
    {
        if(Interlocked.CompareExchange(ref _accountObservationReader,1,0)!=0)
            throw new InvalidOperationException("Only one emulator account-observation reader is supported.");
        try
        {
            Guid delivered=Guid.Empty;
            while(true)
            {
                token.ThrowIfCancellationRequested();FrameworkBrokerObservation? latest=null;
                lock(_sync)
                {
                    for(var index=_committed.Observations.Length-1;index>=0;index--)
                        if(_committed.Observations[index].Kind==FrameworkObservationKind.AccountSnapshot)
                        {latest=_committed.Observations[index];break;}
                }
                if(latest is not null && latest.ObservationId!=delivered)
                {delivered=latest.ObservationId;yield return latest;}
                else await _accountEvents.Reader.ReadAsync(token).ConfigureAwait(false);
            }
        }
        finally{Volatile.Write(ref _accountObservationReader,0);}
    }
}
