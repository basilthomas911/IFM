using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
[MessagePackObject]
public sealed record OrderCompositionHistoryRow([property: Key(0)] int PortfolioId, [property: Key(1)] int FundId, [property: Key(2)] DateOnly ValueDate, [property: Key(3)] DateTime OccurredAtUtc, [property: Key(4)] Guid WorkflowId, [property: Key(5)] Guid InvocationId, [property: Key(6)] Guid EventId, [property: Key(7)] short TargetHorizon, [property: Key(8)] byte Outcome, [property: Key(9)] string ReasonCode, [property: Key(10)] Guid ResultId, [property: Key(11)] string ResultSha256);
