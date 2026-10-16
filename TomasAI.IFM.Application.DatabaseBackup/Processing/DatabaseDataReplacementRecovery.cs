using TomasAI.IFM.Application.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Application.DatabaseBackup.Processing;

/// <summary>
/// Coordinates replacement at existing endpoints, preserving rollback data. A crash during
/// a mutating step requires reconciliation; it never causes blind replacement on restart.
/// </summary>
public sealed class DatabaseDataReplacementRecovery(
    IDatabaseReplacementRecoveryPlatform platform,
    IDatabaseReplacementRecoveryJournal journal) : IDatabaseCutoverCapability
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2000",Justification="The bounded cleanup CancellationTokenSource is declared with using and remains alive across rollback awaits.")]
    public async ValueTask<DatabaseCutoverResult> RecoverAsync(DatabaseCutoverRequest request,
        IProgress<DatabaseNativeProgress> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(request);
        if (request.OperationId.Value == Guid.Empty || request.ValidationRevision <= 0 || request.FencingToken <= 0)
            throw new ArgumentException("Recovery requires an approved, validated and fenced restore.", nameof(request));
var lease = await journal.AcquireAsync(request, cancellationToken).ConfigureAwait(false);
await using var leaseDisposal = lease.ConfigureAwait(true);
        var existing = await lease.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing.Request with { FencingToken = request.FencingToken } != request
                || existing.Request.FencingToken > request.FencingToken)
                throw new DatabaseExecutionConflictException("Recovery identity or fencing token conflicts with the durable controller record.");
            if (existing.Stage == DatabaseReplacementStage.Completed && existing.Result?.Succeeded == true)
            {
                await lease.WriteAsync(existing with { Request = request }, cancellationToken).ConfigureAwait(false);
                return existing.Result;
            }
            // A prepared record precedes all active-data writes. An operator can restore
            // this stage only after reconciling an interrupted pre-replacement attempt.
            if (existing.Stage is not (DatabaseReplacementStage.Prepared or DatabaseReplacementStage.ValidatingApplication))
                throw new InvalidOperationException(
                    "An existing recovery requires controller reconciliation before further replacement.");
        }

        // Preparation must not stop writers or mutate active data.
        var rollback = existing?.RollbackReference ?? await platform.PrepareAsync(request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(rollback))
            throw new InvalidOperationException("Replacement requires a durable rollback reference.");
        var record = new DatabaseReplacementRecoveryRecord(request, DatabaseReplacementStage.Prepared, rollback);
        await lease.WriteAsync(record, cancellationToken).ConfigureAwait(false);

        async ValueTask Step(DatabaseReplacementStage stage, Func<CancellationToken, ValueTask> action, int percent)
        {
            record = record with { Stage = stage };
            // Write ahead of mutation. A crash leaves an explicit ambiguous stage.
            await lease.WriteAsync(record, cancellationToken).ConfigureAwait(false);
            progress.Report(new(Domain.SystemAdmin.Shared.DatabaseBackup.Contracts.DatabaseRecoveryPhase.CuttingOver, percent));
            await action(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await (Step(DatabaseReplacementStage.StoppingApplication, ct => platform.StopApplicationAsync(request, ct), 10)).ConfigureAwait(true);
            // Reconciled admission recovery validates current data and never replaces it again.
            if (existing?.Stage != DatabaseReplacementStage.ValidatingApplication)
                await (Step(DatabaseReplacementStage.ReplacingData, ct => platform.ReplaceDataAsync(request, rollback, ct), 30)).ConfigureAwait(true);
            await (Step(DatabaseReplacementStage.ValidatingDatabase, ct => platform.ValidateDatabaseAsync(request, ct), 50)).ConfigureAwait(true);
            await (Step(DatabaseReplacementStage.StartingApplicationInRecovery, ct => platform.StartApplicationInRecoveryAsync(request, ct), 60)).ConfigureAwait(true);
            await (Step(DatabaseReplacementStage.ValidatingApplication, ct => platform.ValidateApplicationAsync(request, ct), 80)).ConfigureAwait(true);
            // Once this starts, writers may have resumed, even if the acknowledgement is lost.
            // Automatic rollback from this point could discard new application writes.
            await (Step(DatabaseReplacementStage.ResumingAdmission, ct => platform.ResumeAdmissionAsync(request, ct), 90)).ConfigureAwait(true);
            var result = new DatabaseCutoverResult(true, true, true, "database-replacement-" + request.OperationId.Format());
            record = record with { Stage = DatabaseReplacementStage.Completed, Result = result };
            await lease.WriteAsync(record, cancellationToken).ConfigureAwait(false);
            progress.Report(new(Domain.SystemAdmin.Shared.DatabaseBackup.Contracts.DatabaseRecoveryPhase.Completed, 100));
            return result;
        }
        catch (Exception failure)
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            if (existing?.Stage == DatabaseReplacementStage.ValidatingApplication
                || record.Stage == DatabaseReplacementStage.ResumingAdmission
                || record.Stage == DatabaseReplacementStage.Completed)
            {
                await lease.WriteAsync(record with { Stage = DatabaseReplacementStage.ReconciliationRequired }, cleanup.Token).ConfigureAwait(false);
                throw new InvalidOperationException("Admission may have resumed; automatic data rollback is prohibited. Controller reconciliation is required.", failure);
            }

            try
            {
                record = record with { Stage = DatabaseReplacementStage.RollingBack };
                await lease.WriteAsync(record, cleanup.Token).ConfigureAwait(false);
                await platform.RollbackAsync(request, rollback, cleanup.Token).ConfigureAwait(false);
                await platform.ValidateRollbackAsync(request, cleanup.Token).ConfigureAwait(false);
                await lease.WriteAsync(record with { Stage = DatabaseReplacementStage.RolledBack }, cleanup.Token).ConfigureAwait(false);
            }
            catch (Exception rollbackFailure)
            {
                await lease.WriteAsync(record with { Stage = DatabaseReplacementStage.ReconciliationRequired }, cleanup.Token).ConfigureAwait(false);
                throw new AggregateException("Replacement and rollback failed; keep writers stopped and reconcile the preserved data.", failure, rollbackFailure);
            }
            throw;
        }
    }
}
