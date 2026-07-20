using MessagePack;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.Shared.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the closing fund balance for a specific fund on a given date.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices (base query reserves 0..3).
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetClosingFundBalanceQuery : IQuery<FundBalanceReadModel>
{
    [IgnoreMember] public const string Actor = "FundQuery";
    [IgnoreMember] public const string Verb = "GetClosingFundBalance";
    [IgnoreMember] public const int ErrorId = 1007;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember]public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>
    /// Fund identifier to query.
    /// </summary>
    [Key(2)]
    public int FundId { get; init; }

    /// <summary>
    /// As-of value date for which the closing balance is requested.
    /// </summary>
    [Key(3)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetClosingFundBalanceQuery() { }

    public GetClosingFundBalanceQuery(int fundId, DateOnly valueDate) 
    {
        FundId = fundId;
        ValueDate = valueDate;
        EntityId = new GetClosingFundBalanceParameter(fundId, valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// Indices must match the combined keys from <see cref="BaseQuery{TResult}"/> (0..3) and this derived type (4..5).
    /// </summary>
    [SerializationConstructor]
    public GetClosingFundBalanceQuery(
        ActorSubject subject,         // Key(0)
        IActorEntityId entityId,      // Key(1)
        int fundId,                   // Key(2)
        DateOnly valueDate)           // Key(3)
    {
        Subject = subject;
        EntityId = new GetClosingFundBalanceParameter(fundId, valueDate); 
        FundId = fundId;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }

}