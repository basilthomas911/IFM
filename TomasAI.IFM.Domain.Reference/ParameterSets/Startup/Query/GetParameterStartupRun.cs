using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query;

/// <summary>Loads one immutable parameter startup generation by its run identifier.</summary>
public static class GetParameterStartupRun
{

    extension(GetParameterStartupRunQuery query)
    {
        /// <summary>Returns only the requested startup run so the query response remains bounded.</summary>
        public async ValueTask<ParameterStartupRun> ExecuteAsync(
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

            var (run, _) = await context.ConfigurationDb.GetActiveParameterStartupAsync(query.RunId, token).ConfigureAwait(false);
            return run ?? throw new InvalidOperationException("PARAM.STARTUP_NOT_FOUND");
        }
    }
}
