using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions;

namespace TomasAI.IFM.Application.MarketData.OptionChainCache;

/// <summary>Confirms runtime option ownership has been retired after provider workers stop.</summary>
public interface IOptionFeedSessionCleanup
{
    /// <summary>Closes session resources without deleting committed financial subscription intent.</summary>
    Task CloseSessionAsync(CancellationToken cancellationToken);
}

/// <summary>Coordinates option-chain maintenance and durable-runtime cleanup before the feed-stop receipt.</summary>
public sealed class OptionFeedSessionCleanup(OptionChainBackgroundUpdater chains,
    DurableCompositionRuntime durable, QualifiedCompositionDiscovery discovery, IndividualOptionRiskReader? riskReader = null) : IOptionFeedSessionCleanup
{
    /// <inheritdoc />
    public async Task CloseSessionAsync(CancellationToken cancellationToken)
    {
        List<Exception> failures = [];
        try { await chains.CloseSessionAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception error) { failures.Add(error); }
        try { await durable.CloseSessionAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception error) { failures.Add(error); }
        discovery.CloseSession();
        riskReader?.CloseSession();
        if (failures.Count > 0) throw new AggregateException("Option-feed session cleanup failed.", failures);
    }
}
