using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Legacy identifier for an option trade within an order.</summary>
[MessagePackObject(AllowPrivate = true)]
public record OptionTradeEntityId(
    [property: Key(0)] int OrderId,
    [property: Key(1)] int TradeId) : IActorEntityId
{
    public OptionTradeEntityId() : this(0, 0) { }

    public string Format() => string.Create(null, stackalloc char[64], $"{OrderId}.{TradeId}");

    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [IgnoreMember] public static OptionTradeEntityId Empty => new(0, 0);
    [IgnoreMember] public bool IsValid => OrderId > 0 && TradeId > 0;
}
