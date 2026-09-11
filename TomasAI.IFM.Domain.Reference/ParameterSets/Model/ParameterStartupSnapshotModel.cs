using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Freezes exact assignments once for a startup generation. Pending changes cannot alter this snapshot.</summary>
public sealed class ParameterStartupSnapshotModel
{
    readonly Dictionary<Guid, AppliedParameterAssignment> assignments;
    readonly Dictionary<Guid,ParameterAssignmentRevision> scopes = new();
    public bool HasScope(ParameterAssignmentScope scope)=>scopes.ContainsKey(WorkflowParameterScopeModel.AssignmentId(scope));
    public IReadOnlyList<ParameterAssignmentRevision> Scopes=>scopes.Values.OrderBy(x=>x.AssignmentId).ToArray();
    public Guid StartupRunId { get; }
    public string Fingerprint { get; }
    public IReadOnlyList<AppliedParameterAssignment> Assignments => assignments.Values.OrderBy(x => x.Assignment.AssignmentId).ToArray();

    public ParameterStartupSnapshotModel(Guid startupRunId, IEnumerable<ParameterAssignmentRevision> pending,
        IReadOnlyDictionary<ParameterVersionRef, ParameterSetVersion> authoritativeVersions)
    {
        if (startupRunId == Guid.Empty) throw new ArgumentException("PARAM.STARTUP_ID_REQUIRED");
        StartupRunId = startupRunId;
        assignments = new();
        var seen = new HashSet<Guid>();
        foreach (var assignment in pending)
        {
            WorkflowParameterScopeModel.Validate(assignment.Scope);
            if (assignment.AssignmentId != WorkflowParameterScopeModel.AssignmentId(assignment.Scope) || !seen.Add(assignment.AssignmentId))
                throw new ArgumentException("PARAM.ASSIGNMENT_ID_INVALID");
            if (assignment.Revision <= 0 || assignment.ApplicationPolicy != ParameterApplicationPolicy.NextStartup)
                throw new ArgumentException("PARAM.ASSIGNMENT_INVALID");
            scopes.Add(assignment.AssignmentId,assignment);
            if (!assignment.Enabled) continue;
            if (!authoritativeVersions.TryGetValue(assignment.Reference, out var version) || version.Reference != assignment.Reference)
                throw new InvalidOperationException("PARAM.EXACT_VERSION_MISSING");
            // Recheck publication, dependency and scope rules at the activation boundary.
            ParameterAssignmentModel.Assign(assignment.Scope, version, assignment, assignment.Revision,
                assignment.CreatedAtUtc, assignment.CreatedBy);
            assignments.Add(assignment.AssignmentId, new(startupRunId, assignment, version));
        }
        Fingerprint = ParameterCanonicalPayloadModel.Hash(JsonSerializer.Serialize(new {
            Assignments = Scopes
        }));
    }

    public AppliedParameterAssignment? Resolve(ParameterAssignmentScope scope, Guid startupRunId)
    {
        if (startupRunId != StartupRunId) throw new InvalidOperationException("PARAM.STARTUP_GENERATION_MISMATCH");
        return assignments.GetValueOrDefault(WorkflowParameterScopeModel.AssignmentId(scope));
    }
}
