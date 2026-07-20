using MessagePack;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.Shared.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the balance of a specific fund.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GetFundBalanceQuery : IQuery<FundBalanceReadModel>
{
    [IgnoreMember] public const string Actor = "FundQuery";
    [IgnoreMember] public const string Verb = "GetFundBalance";
    [IgnoreMember] public const int ErrorId = 1006;

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
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetFundBalanceQuery() { }

    public GetFundBalanceQuery(int fundId)
    {
        FundId = fundId;
        EntityId = new GetFundBalanceParameter(fundId);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFundBalanceQuery(
        ActorSubject subject,         // Key(0)
        IActorEntityId entityId,      // Key(1)
        int fundId)                   // Key(2)
    {
        Subject = subject;
        EntityId = new GetFundBalanceParameter(fundId);
        FundId = fundId;
        ErrorCode = ErrorId;
    }
}