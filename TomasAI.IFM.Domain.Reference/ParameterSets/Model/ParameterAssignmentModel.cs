using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Pure eligibility decisions. Callers must supply authoritative version state, not list projections.</summary>
public static class ParameterAssignmentModel
{
    public static ParameterAssignmentRevision Assign(ParameterAssignmentScope scope,
        ParameterSetVersion version, ParameterAssignmentRevision? current, long expectedRevision,
        DateTime nowUtc, string actorIdentity)
    {
        var assignmentId = ParameterAssignmentPolicyModel.AssignmentId(scope);
        if ((current?.Revision ?? 0) != expectedRevision)
            throw new InvalidOperationException("PARAM.REVISION_CONFLICT");
        if (current is not null && (current.Scope != scope || current.AssignmentId != assignmentId))
            throw new ArgumentException("PARAM.SCOPE_INVALID");
        if (version.Status != ParameterVersionStatus.Published)
            throw new InvalidOperationException("PARAM.VERSION_NOT_PUBLISHED");
        if (version.Reference.ComponentCode != scope.ComponentCode || version.Reference.SetId == Guid.Empty || version.Reference.Version <= 0)
            throw new ArgumentException("PARAM.REFERENCE_INVALID");
        if (ParameterCanonicalPayloadModel.Hash(version.PayloadJson) != version.Reference.PayloadSha256)
            throw new ArgumentException("PARAM.PAYLOAD_HASH_MISMATCH");
        ParameterAssignmentPolicyModel.Validate(scope, version);
        if (nowUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(actorIdentity))
            throw new ArgumentException("PARAM.AUDIT_INVALID");
        return new(assignmentId, scope, checked(expectedRevision + 1),
            version.Reference, true, ParameterApplicationPolicy.NextStartup, nowUtc, actorIdentity);
    }

    public static ParameterAssignmentRevision Disable(ParameterAssignmentRevision current,
        long expectedRevision, DateTime nowUtc, string actorIdentity)
    {
        ParameterAssignmentPolicyModel.AssignmentId(current.Scope);
        if (current.Revision != expectedRevision) throw new InvalidOperationException("PARAM.REVISION_CONFLICT");
        if (nowUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(actorIdentity))
            throw new ArgumentException("PARAM.AUDIT_INVALID");
        return current with { Revision = checked(expectedRevision + 1), Enabled = false, CreatedAtUtc = nowUtc, CreatedBy = actorIdentity };
    }
}
