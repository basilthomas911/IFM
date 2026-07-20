using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the loss probability based on a forward loss ratio and a specific value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetLossProbabilityQuery : IQuery<LossProbabilityDataModel>
{
    [IgnoreMember] public const string Actor = "TradePlanQuery";
    [IgnoreMember] public const string Verb = "GetLossProbability";
    [IgnoreMember] public const int ErrorId = 1031;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public double ForwardLossRatio { get; init; }

    [Key(3)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetLossProbabilityQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetLossProbabilityQuery(double forwardLossRatio, DateOnly valueDate)
    {
        ForwardLossRatio = forwardLossRatio;
        ValueDate = valueDate;
        EntityId = new GetLossProbabilityParameter(forwardLossRatio, valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetLossProbabilityQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        double forwardLossRatio,    // Key(2)
        DateOnly valueDate)         // Key(3)
    {
        Subject = subject;
        EntityId = new GetLossProbabilityParameter(forwardLossRatio, valueDate);
        ForwardLossRatio = forwardLossRatio;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}

