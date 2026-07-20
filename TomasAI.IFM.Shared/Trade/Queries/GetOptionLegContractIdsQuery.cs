using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the contract IDs for the option legs associated with a specific trade.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetOptionLegContractIdsQuery : IQuery<string[]>
{
    [IgnoreMember] public const string Actor = "OptionLegContractIdsQuery";
    [IgnoreMember] public const string Verb = "GetOptionLegContractIds";
    [IgnoreMember] public const int ErrorId = 1017;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int TradeId { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetOptionLegContractIdsQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetOptionLegContractIdsQuery(int tradeId)
    {
        TradeId = tradeId;
        EntityId = new GetOptionLegContractIdsParameter(tradeId);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetOptionLegContractIdsQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int tradeId)                // Key(2)
    {
        Subject = subject;
        EntityId = new GetOptionLegContractIdsParameter(tradeId);
        TradeId = tradeId;
        ErrorCode = ErrorId;
    }
}

