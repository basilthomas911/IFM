using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query;
/// <summary>Handles <see cref="GetRiskInvocationQuery"/>.</summary>
public static class GetRiskInvocation
{
    /// <summary>Reads and returns the exact risk invocation observation.</summary>
    public static async ValueTask ExecuteAsync(this GetRiskInvocationQuery query, IRiskQueryContext services, IQueryActorContext<RiskQueryActor> context, CancellationToken cancellationToken)
        => await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<RiskObservation>(await RiskQueryModel.ExactAsync(services, query.Access, query.WorkflowId, query.InvocationId, cancellationToken)));
}