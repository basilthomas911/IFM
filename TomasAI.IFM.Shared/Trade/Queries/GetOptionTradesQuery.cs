using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve option trades associated with a specific order.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetOptionTradesQuery : IQuery<OptionTradeReadModel[]>
{
    [IgnoreMember] public const string Actor = "OptionTradesQuery";
    [IgnoreMember] public const string Verb = "GetOptionTrades";
    [IgnoreMember] public const int ErrorId = 1023;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int OrderId { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetOptionTradesQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetOptionTradesQuery(int orderId)
    {
        OrderId = orderId;
        EntityId = new GetOptionTradesParameter(orderId);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetOptionTradesQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int orderId)                // Key(2)
    {
        Subject = subject;
        EntityId = new GetOptionTradesParameter(orderId);
        OrderId = orderId;
        ErrorCode = ErrorId;
    }
}
