using System;
using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketData.QueryParameters;

namespace TomasAI.IFM.Shared.MarketData.Queries;

/// <summary>
/// MessagePack-serializable query to determine whether a yield curve rate exists for a specified value date.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetYieldCurveRateExistsQuery : IQuery<ScalarReadModel<bool>>
{
    [IgnoreMember] public const string Actor = "YieldCurveRateQuery";
    [IgnoreMember] public const string Verb = "GetYieldCurveRateExists";
    [IgnoreMember] public const int ErrorId = 1014;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>
    /// The value (as-of) date to check for an existing yield curve rate.
    /// </summary>
    [Key(2)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetYieldCurveRateExistsQuery() { }

    /// <summary>
    /// Creates a new query instance for the specified value date.
    /// </summary>
    /// <param name="valueDate">The date to check for an existing yield curve rate.</param>
    public GetYieldCurveRateExistsQuery(DateOnly valueDate)
    {
        ValueDate = valueDate;
        EntityId = new GetYieldCurveRateExistsParameter(valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetYieldCurveRateExistsQuery(
        ActorSubject subject,    // Key(0)
        IActorEntityId entityId, // Key(1)
        DateOnly valueDate)      // Key(2)
    {
        Subject = subject;
        EntityId = new GetYieldCurveRateExistsParameter(valueDate);
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}
