using System;
using MessagePack;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.Shared.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the win/loss ratio for a specific fund over a date range.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetFundWinLossRatioQuery : IQuery<FundWinLossRatioReadModel>
{
    [IgnoreMember] public const string Actor = "FundQuery";
    [IgnoreMember] public const string Verb = "GetFundWinLossRatio";
    [IgnoreMember] public const int ErrorId = 1010;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>
    /// Fund identifier to query.
    /// </summary>
    [Key(2)]
    public int FundId { get; init; }

    /// <summary>
    /// Start date (inclusive) of the period to evaluate.
    /// </summary>
    [Key(3)]
    public DateOnly StartDate { get; init; }

    /// <summary>
    /// End date (inclusive) of the period to evaluate.
    /// </summary>
    [Key(4)]
    public DateOnly EndDate { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetFundWinLossRatioQuery() { }

    public GetFundWinLossRatioQuery(int fundId, DateOnly startDate, DateOnly endDate)
    {
        FundId = fundId;
        StartDate = startDate;
        EndDate = endDate;
        EntityId = new GetFundWinLossRatioParameter(fundId, startDate, endDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFundWinLossRatioQuery(
        ActorSubject subject,         // Key(0)
        IActorEntityId entityId,      // Key(1)
        int fundId,                   // Key(2)
        DateOnly startDate,           // Key(3)
        DateOnly endDate)             // Key(4)
    {
        Subject = subject;
        EntityId = new GetFundWinLossRatioParameter(fundId, startDate, endDate);
        FundId = fundId;
        StartDate = startDate;
        EndDate = endDate;
        ErrorCode = ErrorId;
    }
}