using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;
/// <summary>One process consumes an immutable, durably applied startup generation. No polling or recovery worker.</summary>
public sealed class ParameterRuntimeSnapshotModel(bool enabled) : IParameterRuntimeSnapshot
{
    ParameterStartupSnapshotModel? current;
    public bool Enabled => enabled;
    public Guid? RunId => Volatile.Read(ref current)?.StartupRunId;
    public void Apply(ParameterStartupRun run)
    {
        var snapshot = new ParameterStartupSnapshotModel(run.RunId, run.Scopes, run.Versions.ToDictionary(x => x.Reference));
        if (run.Plan.AssignmentFingerprint != snapshot.Fingerprint)
            throw new InvalidDataException("PARAM.STARTUP_FINGERPRINT_MISMATCH");
        var previous = Interlocked.CompareExchange(ref current, snapshot, null);
        if (previous is not null && previous.StartupRunId != run.RunId)
            throw new InvalidOperationException("PARAM.STARTUP_ALREADY_APPLIED");
    }
    public void Clear() => Interlocked.Exchange(ref current, null);
    public ParameterRuntimeResolution Resolve(string workflowDefinitionId, TimeFrameType horizon)
        => Resolve(WorkflowParameterScopeModel.Create(workflowDefinitionId, horizon));
    public ParameterRuntimeResolution Resolve(ParameterAssignmentScope scope)
    {
        if (!Enabled) return new(false, false, null);
        var value = Volatile.Read(ref current) ?? throw new InvalidOperationException("PARAM.STARTUP_NOT_APPLIED");
        var applied = value.Resolve(scope, value.StartupRunId);
        return new(value.HasScope(scope), value.HasScope(scope) && applied is null, applied);
    }
}
