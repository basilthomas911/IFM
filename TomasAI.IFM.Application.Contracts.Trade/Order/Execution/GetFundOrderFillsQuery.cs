using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Execution;

/// <summary>Reads a keyset page of committed orders and fills for a Fund's ValueDate.</summary>
[MessagePackObject]
public sealed record GetFundOrderFillsQuery : IQuery<FundOrderFillsRow[]>
{
    public const string Actor = OrderExecutionActorNames.Query;
    public const string Verb = "GetFundOrderFills";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int PortfolioId { get; init; }
    [Key(3)] public int FundId { get; init; }
    [Key(4)] public DateOnly ValueDate { get; init; }
    [Key(5)] public int AfterOrderId { get; init; }
    [Key(6)] public Guid AfterAttemptId { get; init; }
    [IgnoreMember] public int ErrorCode => 25211;
    [IgnoreMember] public string? QueryParams => null;
}
