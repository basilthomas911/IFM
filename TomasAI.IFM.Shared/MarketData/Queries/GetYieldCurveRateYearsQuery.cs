using MessagePack;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve yield curve rate years data.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetYieldCurveRateYearsQuery : IQuery<YieldCurveRateYearsReadModel>
{
    [IgnoreMember] public const string Actor = "YieldCurveRateQuery";
    [IgnoreMember] public const string Verb = "GetYieldCurveRateYears";
    [IgnoreMember] public const int ErrorId = 1012;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetYieldCurveRateYearsQuery() { }

    /// <summary>
    /// Creates a new query instance.
    /// </summary>
    public GetYieldCurveRateYearsQuery(bool initializeDefaults)
    {
        if (initializeDefaults)
        {
            EntityId = new GetYieldCurveRateYearsParameter();
            ErrorCode = ErrorId;
        }
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetYieldCurveRateYearsQuery(
        ActorSubject subject,    // Key(0)
        IActorEntityId entityId) // Key(1)
    {
        Subject = subject;
        EntityId = new GetYieldCurveRateYearsParameter();
        ErrorCode = ErrorId;
    }
}
