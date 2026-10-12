using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public sealed class DatabaseCutoverReapprovalTests
{
    static DatabaseCutoverExecutionRequestedEvent Prior() => new()
    {
        ValidationRevision = 12, RestorePointId = new("verified-point"), FreshTarget = new("development", "restored"),
        Source = new() { OperationId = new(Guid.NewGuid()), Source = BackupSource.AwsCloud,
            ProtectionSetId = new("read-model-scylla"), OperationKind = DatabaseRecoveryOperationKind.Cutover }
    };
    [Fact]
    public void Failed_cutover_requires_identical_validated_definition()
    {
        var prior = Prior();
        DatabaseCutoverAdmission.ValidateRetry(prior, prior with { Id = Guid.NewGuid() }, DatabaseRecoveryPhase.Failed, true);
        Assert.Throws<DatabaseExecutionConflictException>(() => DatabaseCutoverAdmission.ValidateRetry(
            prior, prior with { RestorePointId = new("other-point") }, DatabaseRecoveryPhase.Failed, true));
        Assert.Throws<DatabaseExecutionConflictException>(() => DatabaseCutoverAdmission.ValidateRetry(
            prior, prior with { ValidationRevision = 13 }, DatabaseRecoveryPhase.Failed, true));
    }
    [Theory]
    [InlineData(DatabaseRecoveryPhase.Completed, true)]
    [InlineData(DatabaseRecoveryPhase.CuttingOver, false)]
    public void Successful_or_active_cutover_cannot_be_readmitted(DatabaseRecoveryPhase phase, bool terminal)
    {
        var prior = Prior();
        Assert.Throws<DatabaseExecutionConflictException>(() => DatabaseCutoverAdmission.ValidateRetry(prior, prior, phase, terminal));
    }
}