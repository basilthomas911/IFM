using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the fund ID from an order ID.
/// </summary>
[MessagePackObject(false)]
public record GetFundIdFromOrderIdParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int OrderId { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFundIdFromOrderIdParameter() { }

    [SerializationConstructor]
    public GetFundIdFromOrderIdParameter(int orderId)
    {
        OrderId = orderId;
        QueryParams = $"orderId={OrderId}";
    }

    public string Format()
        => $"{OrderId}";
}
