namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>Provider-neutral account evidence, separate from order mutation.</summary>
public interface IFrameworkBrokerAccount
{
    string AccountAlias { get; }
    long Generation { get; }
    ValueTask<FrameworkAccountSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    ValueTask<FrameworkAccountSnapshot> ResynchronizeAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<FrameworkBrokerObservation> ObserveAsync(CancellationToken cancellationToken = default);
}
