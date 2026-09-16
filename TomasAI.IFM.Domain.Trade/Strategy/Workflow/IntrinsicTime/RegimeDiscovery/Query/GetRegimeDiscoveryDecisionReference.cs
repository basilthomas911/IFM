using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Reference;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Query;
/// <summary>Handles <see cref="GetRegimeDiscoveryDecisionReferenceQuery"/>.</summary>
public static class GetRegimeDiscoveryDecisionReference
{
    /// <summary>Generates and returns the immutable Regime Discovery decision reference.</summary>
    public static ValueTask ExecuteAsync(this GetRegimeDiscoveryDecisionReferenceQuery query, IRegimeDiscoveryQueryContext services, IQueryActorContext<RegimeDiscoveryQueryActor> context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new RegimeDiscoveryDecisionReferenceGenerator().Generate();
        return context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<RegimeDiscoveryDecisionReferenceDto[]>(result));
    }
}