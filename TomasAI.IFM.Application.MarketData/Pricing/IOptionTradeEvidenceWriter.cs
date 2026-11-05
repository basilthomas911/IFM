using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Completion acknowledges durable source plus the first immutable pricing attempt, not a transient enqueue.</summary>
public interface IOptionTradeEvidenceWriter
{
    ValueTask WriteAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken);

    /// <summary>Accepts the first attempt or its exact retry. Returns false when a different attempt already owns the source identity.</summary>
    async ValueTask<bool> WriteFirstAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken)
    {
        await WriteAsync(evidence, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
