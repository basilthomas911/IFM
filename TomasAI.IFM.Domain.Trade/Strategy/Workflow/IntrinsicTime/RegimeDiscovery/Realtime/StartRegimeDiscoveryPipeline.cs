using System.Globalization;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Pipeline;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Realtime;

/// <summary>Owns configuration initialization for one admitted Regime Discovery execution.</summary>
public static class StartRegimeDiscoveryPipeline
{
    public static async ValueTask<PipelineStartResult<ExecuteRegimeDiscoveryPipelineCommand>> StartPipelineAsync(
        this ExecuteRegimeDiscoveryPipelineCommand command,
        IIntrinsicTimeStrategyWorkflowRealtimeContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        TomasAI.IFM.Application.Storage.ConfigurationDb.ResolvedRegimeDiscoveryParameterSet? resolved = null;
        try
        {
            if (context.TimeProvider.GetUtcNow().UtcDateTime >= command.ExpiresAtUtc)
                return PipelineStartResult<ExecuteRegimeDiscoveryPipelineCommand>.Failed("RD.INIT.DEADLINE", "InitializationTimeout",
                    "Regime Discovery initialization reached the workflow deadline.");

            if(context.ParameterRuntime is {Enabled:true,RunId:null})
                return PipelineStartResult<ExecuteRegimeDiscoveryPipelineCommand>.Failed("RD.INIT.PARAMETER_STARTUP_PENDING","ConfigurationUnavailable","The parameter startup generation is not available. This workflow ends here; later triggers can retry.");
            var selected=context.ParameterRuntime?.Resolve(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity.IntrinsicTimeStrategyWorkflowDefinition.Id,command.TargetHorizon);
            if(selected is {IsDisabled:true})
                return PipelineStartResult<ExecuteRegimeDiscoveryPipelineCommand>.Failed("RD.INIT.ASSIGNMENT_DISABLED","ConfigurationUnavailable","The workflow/horizon parameter assignment was disabled for this startup generation.");
            var generic=selected?.Applied;
            resolved=generic is null
                ?await context.ConfigurationDb.ResolveEffectiveRegimeDiscoveryAsync(command.RequestedAtUtc,command.TargetHorizon).ConfigureAwait(false)
                :new TomasAI.IFM.Application.Storage.ConfigurationDb.ResolvedRegimeDiscoveryParameterSet(
                    System.Text.Json.JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(generic.Version.PayloadJson)!,generic.Version.PayloadJson,generic.Version.Reference.PayloadSha256,generic.Version.PublishedAtUtc??generic.Version.CreatedAtUtc);
            if (resolved is null)
                return PipelineStartResult<ExecuteRegimeDiscoveryPipelineCommand>.Failed("RD.INIT.CONFIGURATION_MISSING", "ConfigurationUnavailable",
                    "No published Regime Discovery parameter set is effective for the workflow trigger.",
                    new Dictionary<string, string> { ["TargetHorizon"] = command.TargetHorizon.ToString(), ["RequestedAtUtc"] = command.RequestedAtUtc.ToString("O") });

            var validation = new RegimeDiscoveryParameterSetValidationRules().Execute(resolved.ParameterSet);
            if (validation.Length != 0)
                return PipelineStartResult<ExecuteRegimeDiscoveryPipelineCommand>.Failed(
                    "RD.INIT.CONFIGURATION_INVALID", "ConfigurationInvalid",
                    "The effective Regime Discovery parameter set is invalid.",
                    validation.Select(_ => "RD.CONFIG.INVALID").ToArray(),
                    validation.Select((value, index) => new KeyValuePair<string, string>(
                        $"Validation.{index + 1:D3}", value.ErrorMessage)).ToDictionary())
                    .WithParameterSet(resolved.ParameterSet.ParameterSetId, resolved.ParameterSet.Version,
                        resolved.PayloadSha256);

            var expectedHash = generic is null?RegimeDiscoveryParameterPayload.ComputeSha256(resolved.ParameterSet):RegimeDiscoveryParameterPayload.ComputeSha256(resolved.PayloadJson);
            if (!string.Equals(expectedHash, resolved.PayloadSha256, StringComparison.OrdinalIgnoreCase))
                return PipelineStartResult<ExecuteRegimeDiscoveryPipelineCommand>.Failed(
                    "RD.INIT.CONFIGURATION_HASH", "ConfigurationInvalid",
                    "The effective Regime Discovery parameter hash does not match its payload.")
                    .WithParameterSet(resolved.ParameterSet.ParameterSetId, resolved.ParameterSet.Version,
                        resolved.PayloadSha256);

            var request = RegimeDiscoverySnapshotRequestFactory.Create(
                MarketSeriesIdentity.ForContract(command.TriggerEvent.EntityId.ContractId), resolved.ParameterSet);
            var captured = await context.RegimeDiscoverySnapshotProvider.CaptureAsync(request).ConfigureAwait(false);
            if (!captured.IsSuccess || captured.Snapshot is null)
            {
                var issues = captured.Issues.OrderBy(value => value.SignalKey.TimeFrame)
                    .ThenBy(value => value.Metric).ToArray();
                var reasons = issues.Select(IssueCode).Distinct(StringComparer.Ordinal).ToArray();
                var diagnostics = issues.Take(128).Select((value, index) =>
                    new KeyValuePair<string, string>($"Issue.{index + 1:D3}",
                        $"Metric={value.Metric},TimeFrame={value.SignalKey.TimeFrame},Availability={value.Availability},SignalIdentity={value.SignalIdentity}"))
                    .ToDictionary();
                if(generic is not null)
                {
                    diagnostics["ParameterStartupRunId"]=generic.StartupRunId.ToString();
                    diagnostics["ParameterAssignmentRevision"]=generic.Assignment.Revision.ToString();
                    diagnostics["ParameterPayloadSha256"]=generic.Version.Reference.PayloadSha256;
                }
                return PipelineStartResult<ExecuteRegimeDiscoveryPipelineCommand>.Failed(
                    "RD.INIT.INPUTS_UNAVAILABLE", "InputDataUnavailable",
                    "Required Regime Discovery market signals are unavailable.", reasons, diagnostics)
                    .WithParameterSet(resolved.ParameterSet.ParameterSetId, resolved.ParameterSet.Version,
                        resolved.PayloadSha256);
            }

            return PipelineStartResult<ExecuteRegimeDiscoveryPipelineCommand>.Started(command with
            {
                ParameterSet = resolved.ParameterSet,
                ParameterPayloadSha256 = resolved.PayloadSha256,
                ParameterApplication = generic is null?null:new(generic.StartupRunId,generic.Assignment.AssignmentId,generic.Assignment.Revision),
                Snapshot = captured.Snapshot,
                WorkflowView = command.WorkflowView with
                {
                    RegimeDiscoveryParameterSet = resolved.ParameterSet,
                    RegimeDiscoveryParameterPayloadSha256 = resolved.PayloadSha256,
                    RegimeDiscoveryParameterApplication = generic is null?null:new(generic.StartupRunId,generic.Assignment.AssignmentId,generic.Assignment.Revision),
                    RegimeDiscovery = command.WorkflowView.RegimeDiscovery with
                    {
                        ParameterSetId = resolved.ParameterSet.ParameterSetId,
                        ParameterSetVersion = resolved.ParameterSet.Version,
                        ParameterPayloadSha256 = resolved.PayloadSha256
                    }
                }
            });
        }
        catch (Exception exception)
        {
            context.Logger.LogError(exception, "Regime Discovery initialization failed for workflow {WorkflowId}", command.WorkflowId);
            var diagnosticContext = new Dictionary<string, string>
            {
                ["WorkflowId"] = command.WorkflowId.ToString(),
                ["TargetHorizon"] = command.TargetHorizon.ToString(),
                ["RequestedAtUtc"] = command.RequestedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                ["ExpiresAtUtc"] = command.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture)
            };
            if (resolved is not null)
            {
                diagnosticContext["ParameterSetId"] = resolved.ParameterSet.ParameterSetId.ToString();
                diagnosticContext["ParameterSetVersion"] = resolved.ParameterSet.Version.ToString(CultureInfo.InvariantCulture);
                diagnosticContext["ParameterPayloadSha256"] = resolved.PayloadSha256;
            }
            var failed = PipelineStartResult<ExecuteRegimeDiscoveryPipelineCommand>.Failed(
                "RD.INIT.EXCEPTION", "InitializationFailed",
                PipelineExceptionDiagnostics.Summary("Regime Discovery initialization failed", exception),
                PipelineExceptionDiagnostics.Create(exception, diagnosticContext));
            return resolved is null ? failed : failed.WithParameterSet(
                resolved.ParameterSet.ParameterSetId, resolved.ParameterSet.Version, resolved.PayloadSha256);
        }
    }

    static string IssueCode(RegimeDiscoverySignalObservation observation) => observation.Availability switch
    {
        RegimeDiscoverySignalAvailability.Stale => "RD.DATA.STALE",
        RegimeDiscoverySignalAvailability.NotWarm => "RD.DATA.NOT_WARM",
        RegimeDiscoverySignalAvailability.Invalid => "RD.DATA.INVALID",
        RegimeDiscoverySignalAvailability.FutureTimestamp => "RD.DATA.FUTURE_TIMESTAMP",
        RegimeDiscoverySignalAvailability.SchemaUnsupported => "RD.DATA.SCHEMA_UNSUPPORTED",
        RegimeDiscoverySignalAvailability.CalculationVersionMismatch => "RD.DATA.CALCULATION_VERSION_MISMATCH",
        RegimeDiscoverySignalAvailability.ConfigurationMismatch => "RD.DATA.CONFIGURATION_MISMATCH",
        RegimeDiscoverySignalAvailability.SnapshotConsistencyFailure => "RD.DATA.SNAPSHOT_INCONSISTENT",
        _ => "RD.DATA.REQUIRED_MISSING"
    };
}
