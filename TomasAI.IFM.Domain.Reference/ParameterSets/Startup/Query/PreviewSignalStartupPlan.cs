using TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query.Model;
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
        public async ValueTask ExecuteAsync(IParameterStartupQueryServices context, ILogger logger, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(logger);
            context.AccessPolicy.Demand(ParameterCapability.Read);
            var snapshot = await (ParameterStartupReader.ReadAsync(query.StartupRunId, context.ConfigurationDb, context.Assignments, context.StateRepository, token)).ConfigureAwait(false);
            var plan = SignalStartupPlanModel.Create(snapshot, SignalStartupPlanModel.ExistingIntradayConsumers());
            await (context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ParameterSignalStartupPlan>(plan))).ConfigureAwait(false);
        }
    }
}
