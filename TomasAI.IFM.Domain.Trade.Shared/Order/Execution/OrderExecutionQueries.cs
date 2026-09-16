using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using NewTradeOrderId = TomasAI.IFM.Domain.Trade.Shared.TradeOrderId;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Execution;

/// <summary>Reads durable execution evidence for accepted Trade Orders.</summary>
public interface IOrderExecutionQueryApi
{
    /// <summary>Gets one exact execution attempt and its fills, costs, and terminal state.</summary>
    ValueTask<ServiceResult<OrderExecutionDefinition>> GetAsync(
        NewTradeOrderId tradeOrderId,
        Guid executionAttemptId,
        CancellationToken cancellationToken = default);
}

[MessagePackObject]
public sealed record GetOrderExecutionQuery : IQuery<OrderExecutionDefinition>
{
    public const string Actor = OrderExecutionActorNames.Query;
    public const string Verb = "GetOrderExecution";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public NewTradeOrderId TradeOrderId { get; init; }
    [Key(3)] public Guid ExecutionAttemptId { get; init; }
    [IgnoreMember] public int ErrorCode => 25210;
    [IgnoreMember] public string? QueryParams => null;
}
