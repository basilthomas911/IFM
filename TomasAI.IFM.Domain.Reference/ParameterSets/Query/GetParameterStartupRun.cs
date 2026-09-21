using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query;

/// <summary>Loads one immutable parameter startup generation by its run identifier.</summary>
public static class GetParameterStartupRun
{
    /// <summary>Returns only the requested startup run so the query response remains bounded.</summary>
    public static async ValueTask ExecuteAsync(
        this GetParameterStartupRunQuery query,
        IParameterSetQueryContext context,
        ILogger<ParameterSetQueryActor> logger,
        CancellationToken token)
    {
        context.AccessPolicy.Demand(ParameterCapability.Read);
        token.ThrowIfCancellationRequested();
        if (query.RunId == Guid.Empty)
            throw new ArgumentException("PARAM.STARTUP_RUN_ID_REQUIRED", nameof(query));

        var id = ParameterStartupEntityId.Registry;
        var address = new ApplySignalStartupPlanCommand
        {
            EntityId = id,
            Subject = new ActorSubject(
                ActorType.Command,
                ApplySignalStartupPlanCommand.Actor,
                ApplySignalStartupPlanCommand.Verb,
                id.Format())
        };
        var state = await context.Startups.LoadStateAsync(address);
        token.ThrowIfCancellationRequested();
        if (!state.ActiveRuns.TryGetValue(query.RunId, out var run))
            throw new InvalidOperationException("PARAM.STARTUP_NOT_FOUND");

        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceOk<ParameterStartupRun>(run));
    }
}
