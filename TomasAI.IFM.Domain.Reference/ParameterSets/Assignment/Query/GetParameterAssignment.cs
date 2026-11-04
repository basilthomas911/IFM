using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Query.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Model;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Query;

public static class GetParameterAssignment
{

    extension(GetParameterAssignmentQuery query)
    {
        public async ValueTask<ParameterAssignmentSnapshot> ExecuteAsync(IParameterAssignmentQueryServices context, ILogger logger, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(logger);
            context.AccessPolicy.Demand(ParameterCapability.Read); token.ThrowIfCancellationRequested();
            var scope = query.Scope ?? WorkflowParameterScopeModel.Create(query.WorkflowDefinitionId, (TimeFrameType)query.TargetHorizon);
            var identity = new ParameterAssignmentEntityId(ParameterAssignmentPolicyModel.AssignmentId(scope));
            return await context.ConfigurationDb.GetParameterAssignmentSnapshotAsync(identity, scope, token).ConfigureAwait(false);
        }
    }
}
