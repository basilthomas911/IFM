using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

[MessagePackObject]
public sealed record RiskHistoryRow([property: Key(0)] int PortfolioId, [property: Key(1)] int FundId,
    [property: Key(2)] DateOnly ValueDate, [property: Key(3)] DateTime EvaluatedAtUtc, [property: Key(4)] Guid WorkflowId,
    [property: Key(5)] Guid InvocationId, [property: Key(6)] long Revision, [property: Key(7)] string Outcome,
    [property: Key(8)] int Units, [property: Key(9)] string Reason, [property: Key(10)] string Horizon,
    [property: Key(11)] long OrderId, [property: Key(12)] string Variant = "");
