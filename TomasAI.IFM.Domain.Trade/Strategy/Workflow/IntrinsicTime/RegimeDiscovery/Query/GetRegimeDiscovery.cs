using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Reference;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Query;
/// <summary>Handles <see cref = "GetRegimeDiscoveryQuery"/>.</summary>
public static class GetRegimeDiscovery
{

    extension(GetRegimeDiscoveryQuery query)
    {
        /// <summary>Reads and returns the Regime Discovery result for a workflow.</summary>
        public async ValueTask ExecuteAsync(IRegimeDiscoveryQueryContext services, IQueryActorContext<RegimeDiscoveryQueryActor> context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            var result = await services.DbFactory.TradeDb.GetRegimeDiscoveryAsync(query.WorkflowId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException($"Regime Discovery result for workflow {query.WorkflowId} was not found.");
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<RegimeDiscoveryReadModel>(result)).ConfigureAwait(false);
        }
    }
}
