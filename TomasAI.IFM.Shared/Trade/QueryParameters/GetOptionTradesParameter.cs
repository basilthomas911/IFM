using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve option trades for a specific order.
/// </summary>
[MessagePackObject(false)]
public record GetOptionTradesParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int OrderId { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetOptionTradesParameter() { }

    [SerializationConstructor]
    public GetOptionTradesParameter(int orderId)
    {
        OrderId = orderId;
        QueryParams = $"orderId={OrderId}";
    }

    public string Format()
        => $"{OrderId}";
}
