using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.QueryParameters;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve external yield curve rates.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetExternalYieldCurveRatesQuery : IQuery<YieldCurveRateReadModel[]>
{
    [IgnoreMember] public const string Actor = "YieldCurveRateQuery";
    [IgnoreMember] public const string Verb = "GetExternalYieldCurveRates";
    [IgnoreMember] public const int ErrorId = 1015;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetExternalYieldCurveRatesQuery()
    {
        EntityId = new GetExternalYieldCurveRatesParameter();
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetExternalYieldCurveRatesQuery(ActorSubject subject, IActorEntityId entityId)
    {
        Subject = subject;
        EntityId = entityId;
        ErrorCode = ErrorId;
    }
}
