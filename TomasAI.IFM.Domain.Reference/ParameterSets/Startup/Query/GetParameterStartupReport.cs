using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query;

public static class GetParameterStartupReport
{

    extension(GetParameterStartupReportQuery query)
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
            await (context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ParameterSignalStartupReport>(report))).ConfigureAwait(false);
        }
    }
}
