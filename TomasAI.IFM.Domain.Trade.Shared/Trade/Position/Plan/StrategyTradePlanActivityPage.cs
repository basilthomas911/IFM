using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>A bounded page of material Trade Plans across strategies for one value date.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
[MessagePackObject]
public sealed record StrategyTradePlanActivityPage(
    [property: Key(0)] StrategyTradePlanSnapshot[] Items,
    [property: Key(1)] byte[]? PagingState);
