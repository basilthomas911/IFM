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
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(version);
        var assignmentId = ParameterAssignmentPolicyModel.AssignmentId(scope);
        if ((current?.Revision ?? 0) != expectedRevision)
            throw new InvalidOperationException("PARAM.REVISION_CONFLICT");
        if (current is not null && (current.Scope != scope || current.AssignmentId != assignmentId))
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.SCOPE_INVALID");
#pragma warning restore MA0015
        if (version.Status != ParameterVersionStatus.Published)
            throw new InvalidOperationException("PARAM.VERSION_NOT_PUBLISHED");
        if (!string.Equals(version.Reference.ComponentCode,scope.ComponentCode,StringComparison.Ordinal) || version.Reference.SetId == Guid.Empty || version.Reference.Version <= 0)
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.REFERENCE_INVALID");
#pragma warning restore MA0015
        if (!string.Equals(ParameterCanonicalPayloadModel.Hash(version.PayloadJson),version.Reference.PayloadSha256,StringComparison.Ordinal))
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.PAYLOAD_HASH_MISMATCH");
#pragma warning restore MA0015
        ParameterAssignmentPolicyModel.Validate(scope, version);
        if (nowUtc.Kind != DateTimeKind.Utc)
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.AUDIT_INVALID");
#pragma warning restore MA0015
        if (string.IsNullOrWhiteSpace(actorIdentity))
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.AUDIT_INVALID");
#pragma warning restore MA0015
        return new(assignmentId, scope, checked(expectedRevision + 1),
            version.Reference, true, ParameterApplicationPolicy.NextStartup, nowUtc, actorIdentity);
    }

    public static ParameterAssignmentRevision Disable(ParameterAssignmentRevision current,
        long expectedRevision, DateTime nowUtc, string actorIdentity)
    {
        ArgumentNullException.ThrowIfNull(current);
        ParameterAssignmentPolicyModel.AssignmentId(current.Scope);
        if (current.Revision != expectedRevision) throw new InvalidOperationException("PARAM.REVISION_CONFLICT");
        if (nowUtc.Kind != DateTimeKind.Utc)
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.AUDIT_INVALID");
#pragma warning restore MA0015
        if (string.IsNullOrWhiteSpace(actorIdentity))
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.AUDIT_INVALID");
#pragma warning restore MA0015
        return current with { Revision = checked(expectedRevision + 1), Enabled = false, CreatedAtUtc = nowUtc, CreatedBy = actorIdentity };
    }
}
