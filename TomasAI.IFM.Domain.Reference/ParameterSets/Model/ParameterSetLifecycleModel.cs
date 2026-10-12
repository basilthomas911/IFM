using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Pure lifecycle decisions for immutable versions.</summary>
public static class ParameterSetLifecycleModel
{
    public static ParameterValidationIssue[] Publish(ParameterSetVersion version,
        ParameterValidationReport validation) 
    {
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(version);
        return (version.Status != ParameterVersionStatus.Draft) switch
{
    true => [new("PARAM.LIFECYCLE_INVALID", "Status", "Only a draft can be published.")],
    false => !string.Equals(version.Reference.PayloadSha256, validation.CandidateSha256, StringComparison.Ordinal) ? [new("PARAM.HASH_MISMATCH", "Payload", "Validation belongs to a different payload.")] : validation.Issues
};
    }
    public static ParameterValidationIssue[] Retire(ParameterSetVersion version, bool assigned) 
    {
        ArgumentNullException.ThrowIfNull(version);
        return (version.Status != ParameterVersionStatus.Published) switch
{
    true => [new("PARAM.LIFECYCLE_INVALID", "Status", "Only a published version can be retired.")],
    false => assigned ? [new("PARAM.VERSION_IN_USE", "Assignments", "Replace or disable active assignments first.")] : []
};
    }
    public static int NextVersion(long actualRevision, long expectedRevision, IEnumerable<int> versions)
    {
        if (actualRevision != expectedRevision) throw new InvalidOperationException("PARAM.REVISION_CONFLICT");
        return checked(versions.DefaultIfEmpty(0).Max() + 1);
    }
}
