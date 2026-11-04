using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query;

public static class GetParameterStartupRuns
{

    extension(GetParameterStartupRunsQuery query)
    {
        public async ValueTask<ParameterStartupRun[]> ExecuteAsync(IParameterStartupQueryServices context, ILogger logger, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            context.AccessPolicy.Demand(ParameterCapability.Read); token.ThrowIfCancellationRequested();
            return await context.ConfigurationDb.GetActiveParameterStartupPageAsync(query.Limit,
                query.AfterCreatedAtUtc, query.AfterRunId, token).ConfigureAwait(false);
        }
    }
}
