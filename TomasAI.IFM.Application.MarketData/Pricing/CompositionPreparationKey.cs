using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Exact workflow revision and hash of its accepted selection, catalog binding and reservation.</summary>
[MessagePackObject]
public sealed record CompositionPreparationKey([property: Key(0)] Guid WorkflowId,
    [property: Key(1)] long InputRevision, [property: Key(2)] string InputSha256);
