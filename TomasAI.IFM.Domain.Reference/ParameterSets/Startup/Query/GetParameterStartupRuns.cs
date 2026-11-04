using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query;

public static class GetParameterStartupRuns
{

    extension(GetParameterStartupRunsQuery query)
    {
        public async ValueTask ExecuteAsync(IParameterStartupQueryServices context, ILogger logger, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            context.AccessPolicy.Demand(ParameterCapability.Read); token.ThrowIfCancellationRequested();
            var ids = await context.ConfigurationDb.GetParameterStartupRunIdsAsync(Math.Clamp(query.Limit, 1, 25),
                query.AfterCreatedAtUtc, query.AfterRunId, token).ConfigureAwait(false);
            var runs = new List<ParameterStartupRun>();
            foreach (var runId in ids)
            {
                var id = new ParameterStartupEntityId(runId);
                var address = new ApplySignalStartupPlanCommand { EntityId = id, RunId = runId,
                    Subject = new ActorSubject(ActorType.Command, ApplySignalStartupPlanCommand.Actor, ApplySignalStartupPlanCommand.Verb, id.Format()) };
                var state = await context.Startups.LoadStateAsync(address, token).ConfigureAwait(false);
                if (state.ActiveRuns.TryGetValue(runId, out var run)) runs.Add(run);
            }
            await (context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ParameterStartupRun[]>(runs.ToArray()))).ConfigureAwait(false);
        }
    }
}
