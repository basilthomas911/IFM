using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query;

/// <summary>Loads one immutable parameter startup generation by its run identifier.</summary>
public static class GetParameterStartupRun
{

    extension(GetParameterStartupRunQuery query)
    {
        /// <summary>Returns only the requested startup run so the query response remains bounded.</summary>
        public async ValueTask ExecuteAsync(
            IParameterStartupQueryServices context,
            ILogger logger,
            CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            context.AccessPolicy.Demand(ParameterCapability.Read);
            token.ThrowIfCancellationRequested();
            if (query.RunId == Guid.Empty)
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
                throw new ArgumentException("PARAM.STARTUP_RUN_ID_REQUIRED");
#pragma warning restore MA0015

            var id = new ParameterStartupEntityId(query.RunId);
            var address = new ApplySignalStartupPlanCommand
            {
                EntityId = id, RunId = query.RunId,
                Subject = new ActorSubject(
                    ActorType.Command,
                    ApplySignalStartupPlanCommand.Actor,
                    ApplySignalStartupPlanCommand.Verb,
                    id.Format())
            };
            var state = await (context.Startups.LoadStateAsync(address, token)).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (!state.ActiveRuns.TryGetValue(query.RunId, out var run))
                throw new InvalidOperationException("PARAM.STARTUP_NOT_FOUND");

            await (context.ReplyAsync(
                query.Subject.ThreadId,
                query.Subject.Verb,
                new ServiceOk<ParameterStartupRun>(run))).ConfigureAwait(false);
        }
    }
}
