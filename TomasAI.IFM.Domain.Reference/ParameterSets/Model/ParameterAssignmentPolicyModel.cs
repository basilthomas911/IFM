using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Dispatches component-specific assignment identity, scope, and payload validation.</summary>
public static class ParameterAssignmentPolicyModel
{
    public static Guid AssignmentId(ParameterAssignmentScope scope) => scope.ComponentCode switch
    {
        RegimeDiscoveryParameterModel.ComponentCode => WorkflowParameterScopeModel.AssignmentId(scope),
        IronCondorMarketSelectionParameterModel.ComponentCode or VerticalSpreadMarketSelectionParameterModel.ComponentCode
            => OptionSpreadStrategyParameterScopeModel.AssignmentId(scope),
        _ => throw new ArgumentException("PARAM.COMPONENT_UNSUPPORTED")
    };

    public static void Validate(ParameterAssignmentScope scope, ParameterSetVersion version)
    {
        if (version.Reference.ComponentCode != scope.ComponentCode) throw new ArgumentException("PARAM.REFERENCE_INVALID");
        var descriptor = ParameterComponentModelRegistry.Get(scope.ComponentCode);
        if (descriptor.Validate(version.PayloadJson, version.SchemaVersion)
            .Any(issue => issue.Severity == ParameterIssueSeverity.Error))
            throw new ArgumentException("PARAM.VERSION_INVALID");
        using var document = JsonDocument.Parse(version.PayloadJson);
        if (!document.RootElement.TryGetProperty("ParameterSetId", out var setId)
            || setId.GetGuid() != version.Reference.SetId
            || !document.RootElement.TryGetProperty("Version", out var payloadVersion)
            || payloadVersion.GetInt32() != version.Reference.Version)
            throw new ArgumentException("PARAM.REFERENCE_INVALID");
        if (scope.ComponentCode == RegimeDiscoveryParameterModel.ComponentCode)
        {
            var horizon = WorkflowParameterScopeModel.Validate(scope);
            var payload = JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(version.PayloadJson)!;
            if (payload.TargetHorizon != horizon) throw new ArgumentException("PARAM.HORIZON_MISMATCH");
        }
        else OptionSpreadStrategyParameterScopeModel.Validate(scope);
    }
}
