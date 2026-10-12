using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Completion acknowledges durable source plus the first immutable pricing attempt, not a transient enqueue.</summary>
public interface IOptionTradeEvidenceWriter
{
    ValueTask WriteAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken);
}
