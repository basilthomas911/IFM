using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Identifies a concrete strategy-position stream for an established trade.</summary>
[MessagePackObject]
public readonly record struct StrategyPositionId(
    [property: Key(0)] TradeEntityId Trade,
    [property: Key(1)] Guid PositionId) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture, $"{Trade.Format()}.{PositionId:N}");
    [IgnoreMember] public bool IsValid => Trade.IsValid && PositionId != Guid.Empty;
}
