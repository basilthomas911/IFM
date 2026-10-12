using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[MessagePackObject]
public sealed record CompositionSnapshotResult([property: Key(0)] MarketCompositionSnapshot? Snapshot, [property: Key(1)] OptionPricingFailure? Failure);
