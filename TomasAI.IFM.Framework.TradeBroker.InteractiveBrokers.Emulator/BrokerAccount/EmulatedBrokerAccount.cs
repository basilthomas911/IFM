using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.BrokerAccount;

/// <summary>Account evidence port backed by the same synthetic cash and position ledger.</summary>
public sealed class EmulatedBrokerAccount(EmulatorLedger ledger) : IFrameworkBrokerAccount
{
    public string AccountAlias => ledger.AccountAlias;
    public long Generation => ledger.Generation;
    public ValueTask<FrameworkAccountSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(ledger.Snapshot()); }
    public ValueTask<FrameworkAccountSnapshot> ResynchronizeAsync(CancellationToken cancellationToken = default) => GetSnapshotAsync(cancellationToken);
    public IAsyncEnumerable<FrameworkBrokerObservation> ObserveAsync(CancellationToken cancellationToken = default) => ledger.ObserveAccount(cancellationToken);
}
