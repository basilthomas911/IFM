using TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query;

public static class PreviewSignalStartupPlan
{

    extension(PreviewSignalStartupPlanQuery query)
    {
        public async ValueTask<ParameterSignalStartupPlan> ExecuteAsync(IParameterStartupQueryServices context, ILogger logger, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(logger);
            context.AccessPolicy.Demand(ParameterCapability.Read);
            var ids = ParameterStartupReader.SupportedScopes().Select(ParameterAssignmentPolicyModel.AssignmentId).ToArray();
            var (assignments, versions) = await context.ConfigurationDb.GetParameterStartupInputsAsync(ids, token).ConfigureAwait(false);
            var snapshot = new ParameterStartupSnapshotModel(query.StartupRunId, assignments, versions.ToDictionary(item => item.Reference));
            var plan = SignalStartupPlanModel.Create(snapshot, SignalStartupPlanModel.ExistingIntradayConsumers());
            return plan;
        }
    }
}
