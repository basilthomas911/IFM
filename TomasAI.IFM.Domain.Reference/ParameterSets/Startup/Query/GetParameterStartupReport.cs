using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query;

public static class GetParameterStartupReport
{

    extension(GetParameterStartupReportQuery query)
    {
        public async ValueTask<ParameterSignalStartupReport> ExecuteAsync(IParameterStartupQueryServices context, ILogger logger, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            context.AccessPolicy.Demand(ParameterCapability.Read); token.ThrowIfCancellationRequested();
            var (_, report) = await context.ConfigurationDb.GetActiveParameterStartupAsync(query.RunId, token).ConfigureAwait(false);
            return report ?? throw new InvalidOperationException("PARAM.STARTUP_REPORT_NOT_RECORDED");
        }
    }
}
