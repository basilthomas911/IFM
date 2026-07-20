using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the loss probability distribution data for a specified value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetLossProbabilityDistributionQuery : IQuery<LossProbabilityDistributionDataModel>
{
    [IgnoreMember] public const string Actor = "LossProbabilityDistributionQuery";
    [IgnoreMember] public const string Verb = "GetLossProbabilityDistribution";
    [IgnoreMember] public const int ErrorId = 1032;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetLossProbabilityDistributionQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetLossProbabilityDistributionQuery(DateOnly valueDate)
    {
        ValueDate = valueDate;
        EntityId = new GetLossProbabilityDistributionParameter(valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetLossProbabilityDistributionQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        DateOnly valueDate)         // Key(2)
    {
        Subject = subject;
        EntityId = new GetLossProbabilityDistributionParameter(valueDate);
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}

