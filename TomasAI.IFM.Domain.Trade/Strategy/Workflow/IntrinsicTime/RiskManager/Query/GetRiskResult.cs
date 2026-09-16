using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query;
/// <summary>Handles <see cref="GetRiskResultQuery"/>.</summary>
public static class GetRiskResult
{
    /// <summary>Reads and returns the exact completed risk result.</summary>
    public static async ValueTask ExecuteAsync(this GetRiskResultQuery query, IRiskQueryContext services, IQueryActorContext<RiskQueryActor> context, CancellationToken cancellationToken)
    {
        var observation = await RiskQueryModel.ExactAsync(services, query.Access, query.WorkflowId, query.InvocationId, cancellationToken);
        if (observation.Calculation is not { } result || result.ResultId != query.ResultId)
            throw new KeyNotFoundException("Exact Risk result not found.");
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<RiskAssessmentResult>(result));
    }
}