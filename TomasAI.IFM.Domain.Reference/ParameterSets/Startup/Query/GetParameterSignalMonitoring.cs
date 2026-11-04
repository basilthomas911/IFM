using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query;

public static class GetParameterSignalMonitoring
{

    extension(GetParameterSignalMonitoringQuery query)
    {
        public async ValueTask ExecuteAsync(IParameterStartupQueryServices context, ILogger logger, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            context.AccessPolicy.Demand(ParameterCapability.Read); token.ThrowIfCancellationRequested();
            var id = new ParameterStartupEntityId(query.RunId);
            var address = new ApplySignalStartupPlanCommand { EntityId = id, RunId = query.RunId, Subject = new ActorSubject(ActorType.Command, ApplySignalStartupPlanCommand.Actor, ApplySignalStartupPlanCommand.Verb, id.Format()) };
            var state = await (context.Startups.LoadStateAsync(address, token)).ConfigureAwait(false); token.ThrowIfCancellationRequested();
            if (!state.Reports.TryGetValue(query.RunId, out var report)) throw new InvalidOperationException("PARAM.STARTUP_REPORT_NOT_RECORDED");
            if (!state.ActiveRuns.TryGetValue(query.RunId, out var run)) throw new InvalidOperationException("PARAM.STARTUP_NOT_FOUND");
            var snapshot = await (TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model.ParameterSignalMonitoringModel.CaptureAsync(run, report.ContractId, context.SignalSnapshots, token)).ConfigureAwait(false);
            await (context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ParameterSignalMonitoringSnapshot>(snapshot))).ConfigureAwait(false);
        }
    }
}
