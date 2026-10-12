using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Regime Discovery belongs to the workflow definition and target horizon, before deployment selection.</summary>
public static class WorkflowParameterScopeModel
{
    public const string ConsumerKind = "strategy-workflow";
    public const string Role = "regime-discovery";

    public static ParameterAssignmentScope Create(string workflowDefinitionId, TimeFrameType horizon)
    {
        if (!string.Equals(workflowDefinitionId,IntrinsicTimeStrategyWorkflowDefinition.Id,StringComparison.Ordinal))
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.WORKFLOW_UNSUPPORTED");
#pragma warning restore MA0015
        if (horizon is not (TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly))
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.HORIZON_UNSUPPORTED");
#pragma warning restore MA0015
        var json = ParameterCanonicalPayloadModel.Canonicalize(JsonSerializer.Serialize(new { TargetHorizon = horizon.ToString() }));
        return new(ConsumerKind, workflowDefinitionId, Role, RegimeDiscoveryParameterModel.ComponentCode,
            json, ParameterCanonicalPayloadModel.Hash(json));
    }

    public static TimeFrameType Validate(ParameterAssignmentScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        using var json = JsonDocument.Parse(scope.ScopeJson);
        if (!Enum.TryParse<TimeFrameType>(json.RootElement.GetProperty("TargetHorizon").GetString(), out var horizon))
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.HORIZON_UNSUPPORTED");
#pragma warning restore MA0015
        if (scope != Create(scope.ConsumerId, horizon))
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.SCOPE_INVALID");
#pragma warning restore MA0015
        return horizon;
    }

    public static Guid AssignmentId(ParameterAssignmentScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        Validate(scope);
        var identity = JsonSerializer.Serialize(new { scope.ConsumerKindCode, scope.ConsumerId, scope.Role, scope.ComponentCode, scope.ScopeSha256 });
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0, 16));
    }
}
