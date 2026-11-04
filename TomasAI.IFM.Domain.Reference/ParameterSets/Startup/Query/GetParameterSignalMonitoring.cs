using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query;

public static class GetParameterSignalMonitoring
{

    extension(GetParameterSignalMonitoringQuery query)
    {
        public async ValueTask<ParameterSignalMonitoringSnapshot> ExecuteAsync(IParameterStartupQueryServices context, ILogger logger, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            context.AccessPolicy.Demand(ParameterCapability.Read); token.ThrowIfCancellationRequested();
            var (run, report) = await context.ConfigurationDb.GetActiveParameterStartupAsync(query.RunId, token).ConfigureAwait(false);
            if (report is null) throw new InvalidOperationException("PARAM.STARTUP_REPORT_NOT_RECORDED");
            if (run is null) throw new InvalidOperationException("PARAM.STARTUP_NOT_FOUND");
            return await ParameterSignalMonitoringModel.CaptureAsync(run, report.ContractId, context.SignalSnapshots, token).ConfigureAwait(false);
        }
    }
}
