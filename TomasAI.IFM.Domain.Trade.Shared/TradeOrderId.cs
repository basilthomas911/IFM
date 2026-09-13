using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Identifies one broker-neutral order within its Portfolio and Fund authority.</summary>
[MessagePackObject]
public readonly record struct TradeOrderId(
    [property: Key(0)] int PortfolioId,
    [property: Key(1)] int FundId,
    [property: Key(2)] int OrderId) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture,
        $"{PortfolioId}.{FundId}.{OrderId}");

    [IgnoreMember] public bool IsValid => PortfolioId > 0 && FundId > 0 && OrderId > 0;
}
