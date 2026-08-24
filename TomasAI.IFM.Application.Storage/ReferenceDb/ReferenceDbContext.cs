using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.JobScheduler;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>
/// reference database constructor
/// </summary>
/// <param name="connectionSettings"></param>
/// <param name="dbFactory"></param>
public class ReferenceDbContext(
    IDbConnectionSettings connectionSettings, 
    IDbContextFactory dbFactory, 
    ISequenceIdGenerator sequenceIdGenerator,
    ILogger<DbProvider> logger) 
    : ObjectDataRepository<ReferenceDbContext>(connectionSettings["ReferenceDbConnection"], logger), IReferenceDbContext
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);
    readonly ISequenceIdGenerator _sequenceIdGenerator = IsArgumentNull.Set(sequenceIdGenerator);
    public const string ReferenceDbConnection = "ReferenceDbConnection";
    const string ScheduledJobProjectionName = "scheduled_job_by_name_v3";
    const string ScheduledJobIdOwnershipScope = "job-id";
    const string ScheduledJobNameOwnershipScope = "job-name";
    const char ProjectionScopeSeparator = '\u001f';
    const int MaxReservationRotationAttempts = 8;
    internal Func<string, int, Task>? ScheduledJobBackfillReservationInsertedForTestingAsync { get; set; }
    internal Func<Task>? ScheduledJobCanonicalMutationSubmittingForTestingAsync { get; set; }

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override ReferenceDbContext Database => this;

    static bool MapToBoolean(IObjectDataRecord e)
        => e.GetBool(0);

    static Guid MapToGuid(IObjectDataRecord e)
        => e.GetGuid(0);

    static string MapToString(IObjectDataRecord e)
        => e.GetString(0);

    static ReferenceProjectionState MapToReferenceProjectionState(IObjectDataRecord e)
        => new(e.GetGuid(0), e.GetBool(1));

    static ReferenceProjectionMutationJournalEntry MapToReferenceProjectionMutationJournalEntry(
        IObjectDataRecord e)
        => new(e.GetString(0), e.GetGuid(1), e.GetDateTime(2));

    static LookupTypeReadModel MapToLookupType(IObjectDataRecord e)
        => new(
            lookupTypeName: e.GetString(0),
            shortCode: e.GetString(1),
            orderId: e.GetInt(2),
            description: e.GetString(3),
            createdOn: e.GetDateTime(4),
            createdBy: e.GetString(5)
        );

    static LookupTypeNameReadModel MapToLookupTypeName(IObjectDataRecord e)
        => new(
            lookupTypeName: e.GetString(0)
        );

    static ScheduledJobReadModel MapToScheduledJob<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            jobId: e.GetInt(0),
            jobName: e.GetString(1),
            jobSchedule: e.GetEnum<JobScheduleType>(2),
            jobScheduleDate: e.GetDateTime(3),
            jobScheduleInterval: e.GetDouble(4),
            taskName: e.GetString(5),
            taskEnabled: e.GetBool(6),
            createdOn: e.GetDateTime(7),
            createdBy: e.GetString(8),
            updatedOn: e.GetDateTime(9),
            updatedBy: e.GetString(10)
        );

    static ScheduledJobDaysOfWeekReadModel MapToScheduledJobDaysOfWeek<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            jobId: e.GetInt(0),
            monday: e.GetBool(1),
            tuesday: e.GetBool(2),
            wednesday: e.GetBool(3),
            thursday: e.GetBool(4),
            friday: e.GetBool(5),
            saturday: e.GetBool(6),
            sunday: e.GetBool(7)
        );

    static MDIForwardLossRatioReadModel MapToMDIForwardLossRatio<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            trendDirection: e.GetEnum<IntrinsicTimeTrendType>(0),
            tradeType: e.GetEnum<TradeType>(1),
            mdi: e.GetInt(2),
            forwardLossRatio: e.GetDouble(3),
            createdBy: e.GetString(4),
            createdOn: e.GetDateTime(5),
            updatedBy: e.GetString(6),
            updatedOn: e.GetDateTime(7)
        );

    static LookupTypeShortCodeReadModel MapToLookupTypeShortCode<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            shortCode: e.GetString(0),
            orderId: e.GetInt(1)
        );

    static ScheduledJobIdRow MapToJobId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(e.GetInt(0));

    static ScheduledJobProjectionRow MapToScheduledJobProjectionRow(IObjectDataRecord e)
        => new(
            new ScheduledJobProjectionKey(e.GetString(0), e.GetInt(1)),
            e.IsNull(2) ? null : e.GetGuid(2));

    static ScheduledJobReservation MapToScheduledJobReservation(IObjectDataRecord e)
        => new(
            e.GetInt(0),
            e.IsNull(1) ? null : e.GetGuid(1));

    static ScheduledJobWriteOwnership MapToScheduledJobWriteOwnership(IObjectDataRecord e)
        => new(
            e.GetString(0),
            e.GetString(1),
            e.GetGuid(2),
            e.GetDateTime(3));

    static void ValidateMdiForwardLossRatioLogicalKeys(
        IEnumerable<MDIForwardLossRatioReadModel> mdiForwardLossRatios)
    {
        var logicalKeys = new HashSet<MdiForwardLossRatioLogicalKey>();
        foreach (var ratio in mdiForwardLossRatios)
        {
            var key = new MdiForwardLossRatioLogicalKey(
                ratio.TrendDirection.ToStringFast(),
                ratio.TradeType.ToStringFast(),
                ratio.MDI);
            if (!logicalKeys.Add(key))
            {
                throw new ArgumentException(
                    $"MDI forward-loss-ratio batch contains duplicate key " +
                    $"({key.TrendDirection}, {key.TradeType}, {key.Mdi}).",
                    nameof(mdiForwardLossRatios));
            }
        }
    }

    internal static string GetScheduledJobProjectionScope(string scheduledJobName)
        => FormattableString.Invariant(
            $"{ScheduledJobProjectionName}{ProjectionScopeSeparator}{scheduledJobName.Length}:{scheduledJobName}");

    static async Task<int?> GetScheduledJobProjectionIdAsync(
        IObjectRepository db,
        string scheduledJobName,
        CancellationToken cancellationToken = default)
        => (await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobId)}", ReferenceDbCql.GetScheduledJobId)
            .SetParameters(new GetScheduledJobId(scheduledJobName))
            .ExecuteSingleAsync(MapToJobId!, cancellationToken))?.Value;

    static async Task<ScheduledJobReservation?> ReadScheduledJobReservationAsync(
        IObjectRepository db,
        string scheduledJobName)
        => await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobReservationV3)}", ReferenceDbCql.GetScheduledJobReservationV3)
            .SetParameters(new GetScheduledJobReservationV3(scheduledJobName))
            .ExecuteSingleAsync<ScheduledJobReservation?>(
                static row => MapToScheduledJobReservation(row));

    static ScheduledJobWriteOperation CreateScheduledJobWriteOperation()
        => new(Guid.NewGuid(), DateTime.UtcNow, []);

    static (string ScopeType, string ScopeKey) GetScheduledJobIdOwnershipScope(int scheduledJobId)
        => (ScheduledJobIdOwnershipScope,
            scheduledJobId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    static (string ScopeType, string ScopeKey) GetScheduledJobNameOwnershipScope(string scheduledJobName)
        => (ScheduledJobNameOwnershipScope, scheduledJobName);

    static async Task<ScheduledJobWriteOwnership?> ReadScheduledJobWriteOwnershipAsync(
        IObjectRepository db,
        string scopeType,
        string scopeKey)
        => await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobWriteOwnershipV3)}", ReferenceDbCql.GetScheduledJobWriteOwnershipV3)
            .SetParameters(new GetScheduledJobWriteOwnershipV3(scopeType, scopeKey))
            .ExecuteSingleAsync<ScheduledJobWriteOwnership?>(
                static row => MapToScheduledJobWriteOwnership(row));

    static async Task ClaimScheduledJobWriteScopesAsync(
        IObjectRepository db,
        ScheduledJobWriteOperation operation,
        IEnumerable<(string ScopeType, string ScopeKey)> scopes)
    {
        foreach (var scope in scopes
            .Distinct()
            .OrderBy(static scope => scope.ScopeType, StringComparer.Ordinal)
            .ThenBy(static scope => scope.ScopeKey, StringComparer.Ordinal))
        {
            if (operation.Ownerships.Any(ownership =>
                string.Equals(ownership.ScopeType, scope.ScopeType, StringComparison.Ordinal) &&
                string.Equals(ownership.ScopeKey, scope.ScopeKey, StringComparison.Ordinal)))
            {
                continue;
            }

            var ownership = new ScheduledJobWriteOwnership(
                scope.ScopeType,
                scope.ScopeKey,
                operation.OperationId,
                operation.StartedOn);
            try
            {
                var applied = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.ClaimScheduledJobWriteOwnershipV3)}", ReferenceDbCql.ClaimScheduledJobWriteOwnershipV3)
                    .SetParameters(new ClaimScheduledJobWriteOwnershipV3(
                        ownership.ScopeType,
                        ownership.ScopeKey,
                        ownership.OperationId,
                        ownership.StartedOn))
                    .ExecuteSingleAsync(MapToBoolean!);
                if (applied != true)
                {
                    throw new StorageException(
                        $"Scheduled-job {scope.ScopeType} scope '{scope.ScopeKey}' is already being modified; retry the write.");
                }
                operation.Ownerships.Add(ownership);
            }
            catch
            {
                // A timed-out LWT may have applied. Cleanup is conditional on this
                // operation ID and includes the attempted scope.
                await TryReleaseScheduledJobWritesAsync(
                    db,
                    [.. operation.Ownerships, ownership]).ConfigureAwait(false);
                throw;
            }
        }
    }

    static async Task ReleaseScheduledJobWriteOwnershipAsync(
        IObjectRepository db,
        ScheduledJobWriteOwnership ownership)
    {
        try
        {
            var applied = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.ReleaseScheduledJobWriteOwnershipV3)}", ReferenceDbCql.ReleaseScheduledJobWriteOwnershipV3)
                .SetParameters(new ReleaseScheduledJobWriteOwnershipV3(
                    ownership.ScopeType,
                    ownership.ScopeKey,
                    ownership.OperationId))
                .ExecuteSingleAsync(MapToBoolean!);
            if (applied == true)
                return;
        }
        catch
        {
            var currentAfterFailure = await ReadScheduledJobWriteOwnershipAsync(
                db,
                ownership.ScopeType,
                ownership.ScopeKey).ConfigureAwait(false);
            if (currentAfterFailure is null ||
                currentAfterFailure.Value.OperationId != ownership.OperationId)
            {
                return;
            }
            throw;
        }

        var current = await ReadScheduledJobWriteOwnershipAsync(
            db,
            ownership.ScopeType,
            ownership.ScopeKey).ConfigureAwait(false);
        if (current is null || current.Value.OperationId != ownership.OperationId)
            return;

        throw new StorageException(
            $"Scheduled-job {ownership.ScopeType} scope '{ownership.ScopeKey}' ownership could not be released.");
    }

    static async Task ReleaseScheduledJobWritesAsync(
        IObjectRepository db,
        IEnumerable<ScheduledJobWriteOwnership> ownerships)
    {
        foreach (var ownership in ownerships.Reverse())
            await ReleaseScheduledJobWriteOwnershipAsync(db, ownership).ConfigureAwait(false);
    }

    static async Task TryReleaseScheduledJobWritesAsync(
        IObjectRepository db,
        IEnumerable<ScheduledJobWriteOwnership> ownerships)
    {
        foreach (var ownership in ownerships.Reverse())
        {
            try
            {
                await ReleaseScheduledJobWriteOwnershipAsync(db, ownership).ConfigureAwait(false);
            }
            catch
            {
                // An unresolved exact row remains fail-closed until explicit
                // writers-drained stale recovery.
            }
        }
    }

    static async Task<ReferenceProjectionState?> GetProjectionStateAsync(
        IObjectRepository db,
        string projectionName,
        CancellationToken cancellationToken = default)
    {
        var states = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetReferenceProjectionStateV3)}", ReferenceDbCql.GetReferenceProjectionStateV3)
            .SetParameters(new GetReferenceProjectionStateV3(projectionName))
            .ExecuteQueryAsync(MapToReferenceProjectionState!, cancellationToken);
        return states.Count == 0 ? null : states.First();
    }

    static async Task<ReferenceProjectionReadToken?> GetScopedProjectionReadTokenAsync(
        IObjectRepository db,
        string projectionName,
        string scopeName,
        CancellationToken cancellationToken = default)
    {
        var projectionState = await GetProjectionStateAsync(db, projectionName, cancellationToken);
        if (projectionState is not { Completed: true })
            return null;

        var scopeState = await GetProjectionStateAsync(db, scopeName, cancellationToken);
        var activeScopeMutations = await GetProjectionMutationsAsync(db, scopeName, cancellationToken);
        if (activeScopeMutations.Count != 0)
            return null;

        return scopeState switch
        {
            null => new ReferenceProjectionReadToken(projectionState.Value.Generation, null),
            { Completed: true } => new ReferenceProjectionReadToken(
                projectionState.Value.Generation,
                scopeState.Value.Generation),
            _ => null
        };
    }

    static async Task<bool> IsScopedProjectionReadTokenValidAsync(
        IObjectRepository db,
        string projectionName,
        string scopeName,
        ReferenceProjectionReadToken token,
        CancellationToken cancellationToken = default)
    {
        var projectionState = await GetProjectionStateAsync(db, projectionName, cancellationToken);
        if (projectionState is not { Completed: true } ||
            projectionState.Value.Generation != token.ProjectionGeneration ||
            (await GetProjectionMutationsAsync(db, scopeName, cancellationToken)).Count != 0)
        {
            return false;
        }

        var scopeState = await GetProjectionStateAsync(db, scopeName, cancellationToken);
        return token.ScopeGeneration.HasValue
            ? scopeState is { Completed: true } &&
                scopeState.Value.Generation == token.ScopeGeneration.Value
            : scopeState is null;
    }

    static Task<ICollection<Guid>> GetProjectionMutationsAsync(
        IObjectRepository db,
        string projectionName,
        CancellationToken cancellationToken = default)
        => db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetReferenceProjectionMutationsV3)}", ReferenceDbCql.GetReferenceProjectionMutationsV3)
            .SetParameters(new GetReferenceProjectionMutationsV3(projectionName))
            .ExecuteQueryAsync(MapToGuid, cancellationToken);

    static async Task ClearScopedProjectionStatesAsync(
        IObjectRepository db,
        IReadOnlyCollection<string> projectionNames,
        CancellationToken cancellationToken)
    {
        var prefixes = projectionNames
            .Select(static projectionName => $"{projectionName}{ProjectionScopeSeparator}")
            .ToArray();
        var scopedStateNames = new List<string>();
        await foreach (var stateName in db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetReferenceProjectionStateNamesV3All)}", ReferenceDbCql.GetReferenceProjectionStateNamesV3All)
            .ExecuteStreamAsync(MapToString, cancellationToken))
        {
            if (prefixes.Any(prefix => stateName.StartsWith(prefix, StringComparison.Ordinal)))
                scopedStateNames.Add(stateName);
        }

        if (scopedStateNames.Count != 0)
        {
            await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteReferenceProjectionStateV3)}", ReferenceDbCql.DeleteReferenceProjectionStateV3)
                .SetParameters(scopedStateNames.Select(static stateName =>
                    new DeleteReferenceProjectionStateV3(stateName)))
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    static async Task RecoverVerifiedInactiveProjectionMutationsAsync(
        IObjectRepository db,
        IReadOnlyCollection<string> projectionNames,
        DateTime staleOperationCutoffUtc,
        CancellationToken cancellationToken)
    {
        var prefixes = projectionNames
            .Select(static projectionName => $"{projectionName}{ProjectionScopeSeparator}")
            .ToArray();
        var staleMutations = new List<ReferenceProjectionMutationJournalEntry>();
        await foreach (var mutation in db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetReferenceProjectionMutationsV3All)}", ReferenceDbCql.GetReferenceProjectionMutationsV3All)
            .ExecuteStreamAsync(MapToReferenceProjectionMutationJournalEntry, cancellationToken))
        {
            var isRelevant = projectionNames.Contains(mutation.ProjectionName, StringComparer.Ordinal) ||
                prefixes.Any(prefix => mutation.ProjectionName.StartsWith(prefix, StringComparison.Ordinal));
            if (isRelevant &&
                ProjectionMutationSafety.AsUtc(mutation.StartedOn) <= staleOperationCutoffUtc)
            {
                staleMutations.Add(mutation);
            }
        }

        if (staleMutations.Count == 0)
            return;

        // The caller has explicitly asserted these writers cannot resume. Invalidate every
        // journaled scope before removing its exact marker/ownership row; a partial cleanup
        // therefore remains on canonical fallback and is safe to replay.
        await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InvalidateReferenceProjectionStateV3)}", ReferenceDbCql.InvalidateReferenceProjectionStateV3)
            .SetParameters(staleMutations
                .Select(static mutation => mutation.ProjectionName)
                .Distinct(StringComparer.Ordinal)
                .Select(projectionName => new InvalidateReferenceProjectionStateV3(
                    Guid.NewGuid(),
                    projectionName)))
            .ExecuteCommandAsync(cancellationToken);

        foreach (var mutation in staleMutations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.ReleaseReferenceProjectionOwnershipV3)}", ReferenceDbCql.ReleaseReferenceProjectionOwnershipV3)
                .SetParameters(new ReleaseReferenceProjectionOwnershipV3(
                    mutation.ProjectionName,
                    mutation.MutationId))
                .ExecuteScalarAsync(MapToBoolean!);
        }

        await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteReferenceProjectionMutationV3)}", ReferenceDbCql.DeleteReferenceProjectionMutationV3)
            .SetParameters(staleMutations.Select(static mutation =>
                new DeleteReferenceProjectionMutationV3(
                    mutation.ProjectionName,
                    mutation.MutationId)))
            .ExecuteCommandAsync(cancellationToken);
    }

    static async Task RecoverVerifiedInactiveScheduledJobWritesAsync(
        IObjectRepository db,
        DateTime staleOperationCutoffUtc,
        CancellationToken cancellationToken)
    {
        await foreach (var ownership in db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobWriteOwnershipsV3All)}", ReferenceDbCql.GetScheduledJobWriteOwnershipsV3All)
            .ExecuteStreamAsync(MapToScheduledJobWriteOwnership, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ProjectionMutationSafety.AsUtc(ownership.StartedOn) > staleOperationCutoffUtc)
                continue;

            await ReleaseScheduledJobWriteOwnershipAsync(db, ownership).ConfigureAwait(false);
        }
    }

    static async Task<ReferenceProjectionMutation> SuspendProjectionAsync(
        IObjectRepository db,
        string projectionName,
        string? inheritedReadyProjectionName = null)
    {
        var generation = Guid.NewGuid();
        var ownsWriteOwnership = false;
        var ownershipClaimSubmissionStarted = false;
        var stateActivationConfirmed = false;
        await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertReferenceProjectionMutationV3)}", ReferenceDbCql.InsertReferenceProjectionMutationV3)
            .SetParameters(new InsertReferenceProjectionMutationV3(
                projectionName,
                generation,
                DateTime.UtcNow))
            .ExecuteCommandAsync();
        try
        {
            ownershipClaimSubmissionStarted = true;
            ownsWriteOwnership = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.ClaimReferenceProjectionOwnershipV3)}", ReferenceDbCql.ClaimReferenceProjectionOwnershipV3)
                .SetParameters(new ClaimReferenceProjectionOwnershipV3(
                    projectionName,
                    generation,
                    DateTime.UtcNow))
                .ExecuteScalarAsync(MapToBoolean!);
            var state = await GetProjectionStateAsync(db, projectionName);
            var activeMutations = await GetProjectionMutationsAsync(db, projectionName);
            var markerIsExclusive = ProjectionMutationSafety.HasExclusiveMarker(activeMutations, generation);
            if (!ownsWriteOwnership || !markerIsExclusive)
            {
                // Poison whichever owner is current. This also poisons a newly claimed epoch when
                // an older contender's marker survives an ownership handoff.
                _ = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.FlagReferenceProjectionOwnershipConflictV3)}", ReferenceDbCql.FlagReferenceProjectionOwnershipConflictV3)
                    .SetParameters(new FlagReferenceProjectionOwnershipConflictV3(projectionName))
                    .ExecuteScalarAsync(MapToBoolean!);
            }

            var inheritedState = state is null && inheritedReadyProjectionName is not null
                ? await GetProjectionStateAsync(db, inheritedReadyProjectionName)
                : null;
            var restoreReady = ownsWriteOwnership &&
                markerIsExclusive &&
                (state is { Completed: true } || inheritedState is { Completed: true });

            await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InvalidateReferenceProjectionStateV3)}", ReferenceDbCql.InvalidateReferenceProjectionStateV3)
                .SetParameters(new InvalidateReferenceProjectionStateV3(generation, projectionName))
                .ExecuteCommandAsync();
            stateActivationConfirmed = true;
            return new(generation, restoreReady, ownsWriteOwnership);
        }
        catch
        {
            try
            {
                await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InvalidateReferenceProjectionStateV3)}", ReferenceDbCql.InvalidateReferenceProjectionStateV3)
                    .SetParameters(new InvalidateReferenceProjectionStateV3(generation, projectionName))
                    .ExecuteCommandAsync(CancellationToken.None);
                stateActivationConfirmed = true;
            }
            catch
            {
                // Continue with exact ownership cleanup. State invalidation and ownership
                // release are independent safety barriers.
            }

            var ownershipResolved = !ownershipClaimSubmissionStarted ||
                await TryConfirmProjectionOwnershipReleasedOrAbsentAsync(
                    db,
                    projectionName,
                    generation).ConfigureAwait(false);
            if (ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted: false,
                ownershipReleaseOrAbsenceConfirmed: ownershipResolved,
                activationResponseConfirmed: stateActivationConfirmed))
            {
                try
                {
                    await DeleteProjectionMutationAsync(db, projectionName, generation);
                }
                catch
                {
                    // Retaining a marker is the safe failure mode.
                }
            }
            throw;
        }
    }

    static async Task<bool> CompleteProjectionAsync(
        IObjectRepository db,
        string projectionName,
        Guid generation)
        => await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.CompleteReferenceProjectionStateV3)}", ReferenceDbCql.CompleteReferenceProjectionStateV3)
            .SetParameters(new CompleteReferenceProjectionStateV3(
                DateTime.UtcNow,
                projectionName,
                generation))
            .ExecuteScalarAsync(MapToBoolean!);

    static async Task<bool> TryCompleteProjectionAsync(
        IObjectRepository db,
        string projectionName,
        ReferenceProjectionMutation mutation)
    {
        var activeMutations = await GetProjectionMutationsAsync(db, projectionName);
        if (!mutation.OwnsWriteOwnership ||
            !ProjectionMutationSafety.HasExclusiveMarker(activeMutations, mutation.Generation) ||
            !await CompleteProjectionAsync(db, projectionName, mutation.Generation))
        {
            return false;
        }

        var releasedWithoutConflict = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.ReleaseReferenceProjectionOwnershipIfSafeV3)}", ReferenceDbCql.ReleaseReferenceProjectionOwnershipIfSafeV3)
            .SetParameters(new ReleaseReferenceProjectionOwnershipV3(projectionName, mutation.Generation))
            .ExecuteScalarAsync(MapToBoolean!);
        if (ProjectionMutationSafety.CanPublishReady(
            operationSucceeded: true,
            ownsWriteEpoch: mutation.OwnsWriteOwnership,
            wasReadyOrExactlyReconciled: true,
            markerIsExclusive: true,
            generationStillMatches: true,
            ownershipReleasedWithoutConflict: releasedWithoutConflict))
        {
            return true;
        }

        // Completion happens while our marker still gates readers. A failed safe release means
        // another writer overlapped, so revoke completion before the marker can disappear.
        await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InvalidateReferenceProjectionStateV3)}", ReferenceDbCql.InvalidateReferenceProjectionStateV3)
            .SetParameters(new InvalidateReferenceProjectionStateV3(mutation.Generation, projectionName))
            .ExecuteCommandAsync(CancellationToken.None);
        await ReleaseProjectionOwnershipAsync(db, projectionName, mutation);
        return false;
    }

    static Task DeleteProjectionMutationAsync(
        IObjectRepository db,
        string projectionName,
        Guid generation)
        => db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteReferenceProjectionMutationV3)}", ReferenceDbCql.DeleteReferenceProjectionMutationV3)
            .SetParameters(new DeleteReferenceProjectionMutationV3(projectionName, generation))
            .ExecuteCommandAsync();

    static async Task RestoreProjectionAsync(
        IObjectRepository db,
        string projectionName,
        ReferenceProjectionMutation mutation)
    {
        var restored = mutation.RestoreReady &&
            await TryCompleteProjectionAsync(db, projectionName, mutation);
        if (!restored)
        {
            await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InvalidateReferenceProjectionStateV3)}", ReferenceDbCql.InvalidateReferenceProjectionStateV3)
                .SetParameters(new InvalidateReferenceProjectionStateV3(mutation.Generation, projectionName))
                .ExecuteCommandAsync(CancellationToken.None);
            await ReleaseProjectionOwnershipAsync(db, projectionName, mutation);
        }
        await DeleteProjectionMutationAsync(db, projectionName, mutation.Generation);
    }

    static async Task AbandonProjectionAsync(
        IObjectRepository db,
        string projectionName,
        ReferenceProjectionMutation mutation)
    {
        await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InvalidateReferenceProjectionStateV3)}", ReferenceDbCql.InvalidateReferenceProjectionStateV3)
            .SetParameters(new InvalidateReferenceProjectionStateV3(mutation.Generation, projectionName))
            .ExecuteCommandAsync(CancellationToken.None);
        await ReleaseProjectionOwnershipAsync(db, projectionName, mutation);
        await DeleteProjectionMutationAsync(db, projectionName, mutation.Generation);
    }

    static async Task ReleaseProjectionOwnershipAsync(
        IObjectRepository db,
        string projectionName,
        ReferenceProjectionMutation mutation)
    {
        if (!mutation.OwnsWriteOwnership)
            return;

        _ = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.ReleaseReferenceProjectionOwnershipV3)}", ReferenceDbCql.ReleaseReferenceProjectionOwnershipV3)
            .SetParameters(new ReleaseReferenceProjectionOwnershipV3(
                projectionName,
                mutation.Generation))
            .ExecuteScalarAsync(MapToBoolean!);
    }

    static async Task<bool> TryConfirmProjectionOwnershipReleasedOrAbsentAsync(
        IObjectRepository db,
        string projectionName,
        Guid mutationId)
    {
        try
        {
            // A successful LWT response confirms that this mutation either released
            // ownership or was not the current owner. The applied value is immaterial.
            _ = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.ReleaseReferenceProjectionOwnershipV3)}", ReferenceDbCql.ReleaseReferenceProjectionOwnershipV3)
                .SetParameters(new ReleaseReferenceProjectionOwnershipV3(
                    projectionName,
                    mutationId))
                .ExecuteScalarAsync(MapToBoolean!);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static Task FinishProjectionMutationAsync(
        IObjectRepository db,
        string projectionName,
        ReferenceProjectionMutation mutation,
        bool succeeded)
        => succeeded
            ? RestoreProjectionAsync(db, projectionName, mutation)
            : AbandonProjectionAsync(db, projectionName, mutation);

    static async Task<ReferenceProjectionMutation> JoinProjectionGroupAsync(
        IObjectRepository db,
        string projectionName)
    {
        var generation = Guid.NewGuid();
        var ownsWriteOwnership = false;
        var ownershipClaimSubmissionStarted = false;
        await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertReferenceProjectionMutationV3)}", ReferenceDbCql.InsertReferenceProjectionMutationV3)
            .SetParameters(new InsertReferenceProjectionMutationV3(
                projectionName,
                generation,
                DateTime.UtcNow))
            .ExecuteCommandAsync();
        try
        {
            ownershipClaimSubmissionStarted = true;
            ownsWriteOwnership = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.ClaimReferenceProjectionOwnershipV3)}", ReferenceDbCql.ClaimReferenceProjectionOwnershipV3)
                .SetParameters(new ClaimReferenceProjectionOwnershipV3(
                    projectionName,
                    generation,
                    DateTime.UtcNow))
                .ExecuteScalarAsync(MapToBoolean!);
            var activeMutations = await GetProjectionMutationsAsync(db, projectionName);
            if (!ownsWriteOwnership ||
                !ProjectionMutationSafety.HasExclusiveMarker(activeMutations, generation))
            {
                _ = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.FlagReferenceProjectionOwnershipConflictV3)}", ReferenceDbCql.FlagReferenceProjectionOwnershipConflictV3)
                    .SetParameters(new FlagReferenceProjectionOwnershipConflictV3(projectionName))
                    .ExecuteScalarAsync(MapToBoolean!);
            }

            // The group journal coordinates normal scoped writes with a whole-projection backfill.
            // It deliberately does not invalidate global readiness for unrelated normal writes.
            return new(generation, RestoreReady: false, ownsWriteOwnership);
        }
        catch
        {
            var ownershipResolved = !ownershipClaimSubmissionStarted ||
                await TryConfirmProjectionOwnershipReleasedOrAbsentAsync(
                    db,
                    projectionName,
                    generation).ConfigureAwait(false);
            if (ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted: false,
                ownershipReleaseOrAbsenceConfirmed: ownershipResolved))
            {
                try
                {
                    await DeleteProjectionMutationAsync(db, projectionName, generation);
                }
                catch
                {
                    // Retain the journal if its delete is itself ambiguous.
                }
            }
            throw;
        }
    }

    static async Task FinishProjectionGroupMutationAsync(
        IObjectRepository db,
        string projectionName,
        ReferenceProjectionMutation mutation)
    {
        if (mutation.OwnsWriteOwnership)
        {
            var releasedWithoutConflict = await db
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.ReleaseReferenceProjectionOwnershipIfSafeV3)}", ReferenceDbCql.ReleaseReferenceProjectionOwnershipIfSafeV3)
                .SetParameters(new ReleaseReferenceProjectionOwnershipV3(
                    projectionName,
                    mutation.Generation))
                .ExecuteScalarAsync(MapToBoolean!);
            if (!releasedWithoutConflict)
            {
                // Await the exact LWT response before deleting the group journal.
                // If this request is ambiguous, the journal must survive.
                _ = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.ReleaseReferenceProjectionOwnershipV3)}", ReferenceDbCql.ReleaseReferenceProjectionOwnershipV3)
                    .SetParameters(new ReleaseReferenceProjectionOwnershipV3(
                        projectionName,
                        mutation.Generation))
                    .ExecuteScalarAsync(MapToBoolean!);
            }
        }
        await DeleteProjectionMutationAsync(db, projectionName, mutation.Generation);
    }

    static async Task<ReferenceProjectionWriteState> SuspendProjectionScopesAsync(
        IObjectRepository db,
        string projectionName,
        IEnumerable<string> scopeNames)
    {
        var distinctScopeNames = scopeNames
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var groupMutation = await JoinProjectionGroupAsync(db, projectionName);
        var scopeMutations = new List<ReferenceProjectionScopedMutation>(distinctScopeNames.Length);
        try
        {
            foreach (var scopeName in distinctScopeNames)
            {
                scopeMutations.Add(new ReferenceProjectionScopedMutation(
                    scopeName,
                    await SuspendProjectionAsync(db, scopeName, projectionName)));
            }
            return new ReferenceProjectionWriteState(projectionName, groupMutation, scopeMutations);
        }
        catch
        {
            foreach (var scopeMutation in scopeMutations)
                await AbandonProjectionAsync(db, scopeMutation.ScopeName, scopeMutation.Mutation);
            await FinishProjectionGroupMutationAsync(db, projectionName, groupMutation);
            throw;
        }
    }

    static async Task FinishProjectionScopesAsync(
        IObjectRepository db,
        ReferenceProjectionWriteState state,
        bool succeeded)
    {
        Exception? firstError = null;
        foreach (var scopeMutation in state.ScopeMutations)
        {
            try
            {
                await FinishProjectionMutationAsync(
                    db,
                    scopeMutation.ScopeName,
                    scopeMutation.Mutation,
                    succeeded);
            }
            catch (Exception exception)
            {
                firstError ??= exception;
            }
        }

        if (firstError is null)
        {
            try
            {
                await FinishProjectionGroupMutationAsync(db, state.ProjectionName, state.GroupMutation);
            }
            catch (Exception exception)
            {
                firstError = exception;
            }
        }

        if (firstError is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstError).Throw();
    }

    static Task FinishProjectionScopesAfterMutationAttemptAsync(
        IObjectRepository db,
        ReferenceProjectionWriteState state,
        bool succeeded,
        bool targetMutationSubmissionStarted)
        => succeeded || ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted)
            ? FinishProjectionScopesAsync(db, state, succeeded)
            : Task.CompletedTask;

    static Task<ReferenceProjectionWriteState> SuspendScheduledJobProjectionAsync(
        IObjectRepository db,
        IEnumerable<string> scheduledJobNames)
        => SuspendProjectionScopesAsync(
            db,
            ScheduledJobProjectionName,
            scheduledJobNames.Select(GetScheduledJobProjectionScope));

    static async Task<Guid> ReserveScheduledJobNameAsync(
        IObjectRepository db,
        string scheduledJobName,
        int scheduledJobId,
        CancellationToken cancellationToken = default)
    {
        var insertedReservationToken = Guid.NewGuid();
        await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertScheduledJobByNameV3)}", ReferenceDbCql.InsertScheduledJobByNameV3)
            .SetParameters(new InsertScheduledJobByNameV3(
                scheduledJobName,
                scheduledJobId,
                insertedReservationToken))
            .ExecuteCommandAsync(cancellationToken);

        for (var attempt = 0; attempt < MaxReservationRotationAttempts; attempt++)
        {
            var reservation = await ReadScheduledJobReservationAsync(db, scheduledJobName)
                .ConfigureAwait(false)
                ?? throw new StorageException(
                    $"Scheduled job name '{scheduledJobName}' could not establish its uniqueness reservation.");
            if (reservation.JobId != scheduledJobId)
            {
                throw new StorageException(
                    $"Scheduled job name '{scheduledJobName}' is already assigned to job {reservation.JobId}.");
            }
            if (reservation.ReservationToken is not { } currentReservationToken)
            {
                throw new StorageException(
                    $"Scheduled job name '{scheduledJobName}' has a legacy tokenless reservation. " +
                    "Repair it only while scheduled-job writers are drained.");
            }
            if (currentReservationToken == insertedReservationToken)
                return insertedReservationToken;

            var replacementReservationToken = Guid.NewGuid();
            var rotated = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.RotateScheduledJobNameV3Reservation)}", ReferenceDbCql.RotateScheduledJobNameV3Reservation)
                .SetParameters(new RotateScheduledJobNameV3Reservation(
                    replacementReservationToken,
                    scheduledJobName,
                    scheduledJobId,
                    currentReservationToken))
                .ExecuteSingleAsync(MapToBoolean!);
            if (rotated == true)
                return replacementReservationToken;
        }

        throw new StorageException(
            $"Scheduled job name '{scheduledJobName}' reservation changed too frequently; retry the write.");
    }

    static async Task ReleaseScheduledJobNameReservationAsync(
        IObjectRepository db,
        string scheduledJobName,
        int scheduledJobId,
        Guid reservationToken)
    {
        try
        {
            var applied = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.ReleaseScheduledJobNameV3)}", ReferenceDbCql.ReleaseScheduledJobNameV3)
                .SetParameters(new ReleaseScheduledJobNameV3(
                    scheduledJobName,
                    scheduledJobId,
                    reservationToken))
                .ExecuteSingleAsync(MapToBoolean!);
            if (applied == true)
                return;
        }
        catch
        {
            var currentAfterFailure = await ReadScheduledJobReservationAsync(db, scheduledJobName)
                .ConfigureAwait(false);
            if (currentAfterFailure is null)
                return;
            throw;
        }

        var current = await ReadScheduledJobReservationAsync(db, scheduledJobName)
            .ConfigureAwait(false);
        if (current is null)
            return;

        throw new StorageException(
            $"Scheduled job name '{scheduledJobName}' reservation changed or could not be released; " +
            "the mutation remains fail-closed for stale recovery.");
    }

    static async Task<bool> ResolvePreCanonicalScheduledJobDestinationAsync(
        IObjectRepository db,
        string scheduledJobName,
        int scheduledJobId,
        bool reservationSubmissionStarted)
    {
        if (!reservationSubmissionStarted)
            return true;

        ScheduledJobReservation? current;
        try
        {
            // This verification deliberately has no caller cancellation token. The
            // name/ID ownership is still held and must not be released until an
            // ambiguously submitted reservation is classified.
            current = await ReadScheduledJobReservationAsync(db, scheduledJobName)
                .ConfigureAwait(false);
        }
        catch
        {
            return false;
        }

        if (current is null || current.Value.JobId != scheduledJobId)
            return true;
        if (current.Value.ReservationToken is not { } currentToken)
            return false;

        try
        {
            await ReleaseScheduledJobNameReservationAsync(
                db,
                scheduledJobName,
                scheduledJobId,
                currentToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    async Task EnsureScheduledJobNameProjectionAsync(
        IObjectRepository db,
        string scheduledJobName,
        int scheduledJobId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reservationToken = Guid.NewGuid();
        var inserted = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertScheduledJobByNameV3)}", ReferenceDbCql.InsertScheduledJobByNameV3)
            .SetParameters(new InsertScheduledJobByNameV3(
                scheduledJobName,
                scheduledJobId,
                reservationToken))
            .ExecuteSingleAsync(MapToBoolean!);
        if (inserted != true)
            return;

        if (ScheduledJobBackfillReservationInsertedForTestingAsync is { } insertedHook)
            await insertedHook(scheduledJobName, scheduledJobId).ConfigureAwait(false);

        // Do not let cancellation strand a candidate after its LWT was acknowledged.
        // A same-owner writer rotates to a new token, so this exact compensation can
        // never delete a newer reservation incarnation.
        var canonical = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJob)}", ReferenceDbCql.GetScheduledJob)
            .SetParameters(new GetScheduledJob(scheduledJobId))
            .ExecuteSingleAsync(MapToScheduledJob!);
        if (canonical is not null &&
            string.Equals(canonical.JobName, scheduledJobName, StringComparison.Ordinal))
        {
            return;
        }

        await ReleaseScheduledJobNameReservationAsync(
            db,
            scheduledJobName,
            scheduledJobId,
            reservationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// return db reader/writer properties
    /// </summary>
    public IReferenceDbReadContext DbReader => this;
    public IReferenceDbWriteContext DbWriter => this;

    /// <summary>
    /// delete lookup type by name
    /// </summary>
    /// <param name="lookupTypeId"></param>
    /// <returns></returns>
    public async Task DeleteLookupTypeAsync(LookupTypeId lookupTypeId)
    {
        await _dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteLookupType)}", ReferenceDbCql.DeleteLookupType)
               .SetParameters(new DeleteLookupType(lookupTypeId.LookupTypeName, lookupTypeId.OrderId))
               .ExecuteCommandAsync();

        var lookupTypes = await GetLookupTypeAsync(lookupTypeId.LookupTypeName);
        if (lookupTypes?.Count  > 0)
        {
            var orderId = 0;
            foreach (var e in lookupTypes)
                await UpdateLookupTypeAsync(e.Id, e with { OrderId = orderId++, CreatedOn = DateTime.Now });
        }
    }

    /// <summary>
    /// delete scheduled job
    /// </summary>
    /// <param name="scheduledJobId"></param>
    /// <returns></returns>
    public async Task DeleteScheduledJobAsync(int scheduledJobId)
    {
        var db = _dbFactory.ReferenceDb;
        var writeOperation = CreateScheduledJobWriteOperation();
        var targetMutationSubmissionStarted = false;
        try
        {
            await ClaimScheduledJobWriteScopesAsync(
                db,
                writeOperation,
                [GetScheduledJobIdOwnershipScope(scheduledJobId)]).ConfigureAwait(false);

            // The job-ID ownership is acquired before observing the canonical name.
            var existing = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJob)}", ReferenceDbCql.GetScheduledJob)
                .SetParameters(new GetScheduledJob(scheduledJobId))
                .ExecuteSingleAsync(MapToScheduledJob!);
            if (existing is null)
            {
                await ReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships)
                    .ConfigureAwait(false);
                return;
            }

            await ClaimScheduledJobWriteScopesAsync(
                db,
                writeOperation,
                [GetScheduledJobNameOwnershipScope(existing.JobName)]).ConfigureAwait(false);

            var confirmed = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJob)}", ReferenceDbCql.GetScheduledJob)
                .SetParameters(new GetScheduledJob(scheduledJobId))
                .ExecuteSingleAsync(MapToScheduledJob!);
            if (confirmed is null ||
                !string.Equals(confirmed.JobName, existing.JobName, StringComparison.Ordinal))
            {
                throw new StorageException(
                    $"Scheduled job {scheduledJobId} changed while its name ownership was being acquired.");
            }

            var projectionState = await SuspendScheduledJobProjectionAsync(db, [existing.JobName]);
            var succeeded = false;
            try
            {
                var reservationToken = await ReserveScheduledJobNameAsync(
                    db,
                    existing.JobName,
                    scheduledJobId).ConfigureAwait(false);

                if (ScheduledJobCanonicalMutationSubmittingForTestingAsync is { } mutationSubmitting)
                    await mutationSubmitting().ConfigureAwait(false);

                var queuedCommands = new List<object>
                {
                    db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteScheduledJob)}", ReferenceDbCql.DeleteScheduledJob)
                        .SetParameters(new DeleteScheduledJob(scheduledJobId))
                        .QueueCommand(),
                    db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteScheduledJobDays)}", ReferenceDbCql.DeleteScheduledJobDays)
                        .SetParameters(new DeleteScheduledJobDays(scheduledJobId))
                        .QueueCommand()
                };
                targetMutationSubmissionStarted = true;
                await ProjectionMutationSafety.ExecuteCanonicalMutationThenReleaseReservationAsync(
                    () => db.ExecuteQueuedCommandsAsync(queuedCommands),
                    () => ReleaseScheduledJobNameReservationAsync(
                        db,
                        existing.JobName,
                        scheduledJobId,
                        reservationToken)).ConfigureAwait(false);
                succeeded = true;
            }
            finally
            {
                await FinishProjectionScopesAfterMutationAttemptAsync(
                    db,
                    projectionState,
                    succeeded,
                    targetMutationSubmissionStarted).ConfigureAwait(false);
            }

            await ReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships)
                .ConfigureAwait(false);
        }
        catch
        {
            if (!targetMutationSubmissionStarted)
                await TryReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// return next seed id by seed type
    /// </summary>
    /// <param name="seedType"></param>
    /// <returns></returns>
    public async Task<int> GetNextSeedIdAsync(string seedType)
        => checked((int)await _sequenceIdGenerator
            .GetSequenceIdAsync(SequenceNameExtensions.ParseSequenceName(seedType))
            .ConfigureAwait(false));

    public async Task<int> GetNextSeedIdAsync(string seedType, CancellationToken cancellationToken)
        => checked((int)await _sequenceIdGenerator
            .GetSequenceIdAsync(
                SequenceNameExtensions.ParseSequenceName(seedType),
                cancellationToken)
            .ConfigureAwait(false));

    /// <summary>
    /// return current seed id for selected seed type
    /// </summary>
    /// <param name="seedType"></param>
    /// <returns></returns>
    public async Task<int> GetCurrentSeedIdAsync(string seedType)
        => checked((int)await _sequenceIdGenerator
            .GetHighWatermarkAsync(SequenceNameExtensions.ParseSequenceName(seedType))
            .ConfigureAwait(false));

    public async Task<int> GetCurrentSeedIdAsync(string seedType, CancellationToken cancellationToken)
        => checked((int)await _sequenceIdGenerator
            .GetHighWatermarkAsync(
                SequenceNameExtensions.ParseSequenceName(seedType),
                cancellationToken)
            .ConfigureAwait(false));

    /// <summary>
    /// return lookup type from lookup type id
    /// </summary>
    /// <param name="lookupTypeId"
    /// <returns></returns>
    public async Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId)
       => await _dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypeById)}", ReferenceDbCql.GetLookupTypeById)
                .SetParameters(new GetLookupTypeById(lookupTypeId.LookupTypeName, lookupTypeId.OrderId))
                .ExecuteSingleAsync(MapToLookupType!);

    public async Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId, CancellationToken cancellationToken)
       => await _dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypeById)}", ReferenceDbCql.GetLookupTypeById)
                .SetParameters(new GetLookupTypeById(lookupTypeId.LookupTypeName, lookupTypeId.OrderId))
                .ExecuteSingleAsync(MapToLookupType!, cancellationToken);

    /// <summary>
    /// return lookup types from lookup type name
    /// </summary>
    /// <param name="lookupTypeName"></param>
    /// <returns></returns>
    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName)
       => await _dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupType)}", ReferenceDbCql.GetLookupType)
                .SetParameters(new GetLookupType(lookupTypeName))
                .ExecuteQueryAsync(MapToLookupType!);

    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName, CancellationToken cancellationToken)
       => await _dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupType)}", ReferenceDbCql.GetLookupType)
                .SetParameters(new GetLookupType(lookupTypeName))
                .ExecuteQueryAsync(MapToLookupType!, cancellationToken);

    /// <summary>
    /// return all lookup types 
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync()
       => await _dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypes)}", ReferenceDbCql.GetLookupTypes)
               .ExecuteQueryAsync(MapToLookupType!);

    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync(CancellationToken cancellationToken)
       => await _dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypes)}", ReferenceDbCql.GetLookupTypes)
               .ExecuteQueryAsync(MapToLookupType!, cancellationToken);

    /// <summary>
    /// return all lookup type names
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<string>> GetLookupTypeNamesAsync()
       => [.. (await _dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypeNames)}", ReferenceDbCql.GetLookupTypeNames)
               .ExecuteQueryAsync(MapToLookupTypeName!)).Select(e => e.LookupTypeName)];

    public async Task<ICollection<string>> GetLookupTypeNamesAsync(CancellationToken cancellationToken)
       => [.. (await _dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypeNames)}", ReferenceDbCql.GetLookupTypeNames)
               .ExecuteQueryAsync(MapToLookupTypeName!, cancellationToken)).Select(e => e.LookupTypeName)];

    /// <summary>
    /// return all lookup type short codes by lookup type
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName)
       => await _dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypeShortCodes)}", ReferenceDbCql.GetLookupTypeShortCodes)
                .SetParameters(new GetLookupType(lookupTypeName))
                .ExecuteQueryAsync(MapToLookupTypeShortCode!);

    public async Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName, CancellationToken cancellationToken)
       => await _dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypeShortCodes)}", ReferenceDbCql.GetLookupTypeShortCodes)
                .SetParameters(new GetLookupType(lookupTypeName))
                .ExecuteQueryAsync(MapToLookupTypeShortCode!, cancellationToken);

    /// <summary>
    /// Checks a lookup-type partition without allocating a LINQ iterator or closure.
    /// </summary>
    public async Task<bool> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode)
    {
        var values = await GetLookupTypeShortCodesAsync(lookupTypeName);
        foreach (var value in values)
            if (string.Equals(value.ShortCode, shortCode, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public async Task<bool> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode, CancellationToken cancellationToken)
    {
        var values = await GetLookupTypeShortCodesAsync(lookupTypeName, cancellationToken);
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(value.ShortCode, shortCode, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// return scheduled job id
    /// </summary>
    /// <param name="scheduledJobName"></param>
    /// <returns></returns>
    public async Task<int> GetScheduledJobIdAsync(string scheduledJobName)
    {
        var db = _dbFactory.ReferenceDb;
        var scopeName = GetScheduledJobProjectionScope(scheduledJobName);
        var readToken = await GetScopedProjectionReadTokenAsync(
            db,
            ScheduledJobProjectionName,
            scopeName);
        if (readToken is not null)
        {
            var jobId = await GetScheduledJobProjectionIdAsync(db, scheduledJobName);
            if (await IsScopedProjectionReadTokenValidAsync(
                db,
                ScheduledJobProjectionName,
                scopeName,
                readToken.Value))
                return jobId ?? 0;
        }

        var jobs = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobs)}", ReferenceDbCql.GetScheduledJobs)
            .ExecuteQueryAsync(MapToScheduledJob!);
        var legacyMatches = jobs
            .Where(job => string.Equals(job.JobName, scheduledJobName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (legacyMatches.Length == 0)
            return 0;
        if (legacyMatches.Length > 1)
        {
            throw new StorageException(
                $"Scheduled job name '{scheduledJobName}' is assigned to more than one canonical job.");
        }

        var legacyMatch = legacyMatches[0];
        return legacyMatch.JobId;
    }

    public async Task<int> GetScheduledJobIdAsync(string scheduledJobName, CancellationToken cancellationToken)
    {
        var db = _dbFactory.ReferenceDb;
        var scopeName = GetScheduledJobProjectionScope(scheduledJobName);
        var readToken = await GetScopedProjectionReadTokenAsync(
            db, ScheduledJobProjectionName, scopeName, cancellationToken);
        if (readToken is not null)
        {
            var jobId = await GetScheduledJobProjectionIdAsync(db, scheduledJobName, cancellationToken);
            if (await IsScopedProjectionReadTokenValidAsync(
                db, ScheduledJobProjectionName, scopeName, readToken.Value, cancellationToken))
                return jobId ?? 0;
        }

        var jobs = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobs)}", ReferenceDbCql.GetScheduledJobs)
            .ExecuteQueryAsync(MapToScheduledJob!, cancellationToken);
        var legacyMatches = jobs
            .Where(job => string.Equals(job.JobName, scheduledJobName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (legacyMatches.Length == 0)
            return 0;
        if (legacyMatches.Length > 1)
            throw new StorageException(
                $"Scheduled job name '{scheduledJobName}' is assigned to more than one canonical job.");
        return legacyMatches[0].JobId;
    }

    /// <summary>
    /// return list of scheduled jobs
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync()
    {
        var db =  _dbFactory.ReferenceDb;
        var scheduledJobs = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobs)}", ReferenceDbCql.GetScheduledJobs)
               .ExecuteQueryAsync(MapToScheduledJob!);
        foreach (var e in scheduledJobs)
        {
            var jobDaysOfWeek = await GetScheduledJobDaysAsync(e.JobId);
            if (jobDaysOfWeek is not null)
                e.DaysOfWeek = jobDaysOfWeek;
        }
        return scheduledJobs;

         Task<ScheduledJobDaysOfWeekReadModel?> GetScheduledJobDaysAsync(int jobId)
            =>  db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobDays)}", ReferenceDbCql.GetScheduledJobDays)
                .SetParameters(new GetScheduledJobDays(jobId))
                .ExecuteSingleAsync(MapToScheduledJobDaysOfWeek!);
    }

    /// <summary>
    /// return mdi forward loss ratio data
    /// </summary>
    /// <param name="trendDirection"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
        => await _dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetMDIForwardLossRatios)}", ReferenceDbCql.GetMDIForwardLossRatios)
               .SetParameters(new GetMDIForwardLossRatios(trendDirection.ToStringFast(), tradeType.ToStringFast()))
               .ExecuteQueryAsync(MapToMDIForwardLossRatio!);

    public async Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(
        IntrinsicTimeTrendType trendDirection,
        TradeType tradeType,
        CancellationToken cancellationToken)
        => await _dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetMDIForwardLossRatios)}", ReferenceDbCql.GetMDIForwardLossRatios)
               .SetParameters(new GetMDIForwardLossRatios(trendDirection.ToStringFast(), tradeType.ToStringFast()))
               .ExecuteQueryAsync(MapToMDIForwardLossRatio!, cancellationToken);

    /// <summary>
    /// insert lookup types
    /// </summary>
    /// <param name="lookupTypes"></param>
    /// <returns></returns>
    public async Task InsertLookupTypeAsync(LookupTypeReadModel e)
        => await _dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertLookupType)}", ReferenceDbCql.InsertLookupType)
               .SetParameters(new InsertLookupType(e.LookupTypeName, e.ShortCode, e.OrderId, e.Description, e.CreatedOn, e.CreatedBy))
               .ExecuteCommandAsync();

    public async Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync(CancellationToken cancellationToken)
    {
        var db = _dbFactory.ReferenceDb;
        var scheduledJobs = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobs)}", ReferenceDbCql.GetScheduledJobs)
            .ExecuteQueryAsync(MapToScheduledJob!, cancellationToken);
        foreach (var e in scheduledJobs)
        {
            var jobDaysOfWeek = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobDays)}", ReferenceDbCql.GetScheduledJobDays)
                .SetParameters(new GetScheduledJobDays(e.JobId))
                .ExecuteSingleAsync(MapToScheduledJobDaysOfWeek!, cancellationToken);
            if (jobDaysOfWeek is not null)
                e.DaysOfWeek = jobDaysOfWeek;
        }
        return scheduledJobs;
    }

    /// <summary>
    /// insert scheduled job
    /// </summary>
    /// <param name="e">scheduled job</param>
    /// <returns></returns>
    public async Task InsertScheduledJobAsync(ScheduledJobReadModel e)
    {
        var db = _dbFactory.ReferenceDb;
        var writeOperation = CreateScheduledJobWriteOperation();
        var targetMutationSubmissionStarted = false;
        var destinationReservationSubmissionStarted = false;
        var jobId = 0;
        try
        {
            // The name scope is the only stable identity available before a new ID
            // is allocated, so it must be claimed before reading the reservation.
            await ClaimScheduledJobWriteScopesAsync(
                db,
                writeOperation,
                [GetScheduledJobNameOwnershipScope(e.JobName)]).ConfigureAwait(false);

            var reservation = await ReadScheduledJobReservationAsync(db, e.JobName)
                .ConfigureAwait(false);
            if (reservation is { ReservationToken: null })
            {
                throw new StorageException(
                    $"Scheduled job name '{e.JobName}' has a legacy tokenless reservation. " +
                    "Repair it only while scheduled-job writers are drained.");
            }

            jobId = reservation?.JobId ?? await GetNextSeedIdAsync("ScheduledJobId");
            if (jobId <= 0)
            {
                throw new StorageException(
                    $"Scheduled job name '{e.JobName}' resolves to invalid job ID {jobId}.");
            }

            await ClaimScheduledJobWriteScopesAsync(
                db,
                writeOperation,
                [GetScheduledJobIdOwnershipScope(jobId)]).ConfigureAwait(false);

            var duplicate = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJob)}", ReferenceDbCql.GetScheduledJob)
                .SetParameters(new GetScheduledJob(jobId))
                .ExecuteSingleAsync(MapToScheduledJob!);
            if (duplicate is not null)
            {
                throw new StorageException(
                    string.Equals(duplicate.JobName, e.JobName, StringComparison.Ordinal)
                        ? $"Scheduled job name '{e.JobName}' is already assigned to job {jobId}."
                        : $"Scheduled job name '{e.JobName}' ambiguously resolves to job {jobId}, " +
                          $"whose canonical name is '{duplicate.JobName}'.");
            }

            var projectionState = await SuspendScheduledJobProjectionAsync(db, [e.JobName]);
            var succeeded = false;
            try
            {
                // A reused same-owner reservation gets a fresh epoch while the
                // distributed name and ID scopes remain exclusively owned.
                destinationReservationSubmissionStarted = true;
                _ = await ReserveScheduledJobNameAsync(
                    db,
                    e.JobName,
                    jobId).ConfigureAwait(false);

                if (ScheduledJobCanonicalMutationSubmittingForTestingAsync is { } mutationSubmitting)
                    await mutationSubmitting().ConfigureAwait(false);

                var queuedCommands = new List<object>
                {
                    db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertScheduledJob)}", ReferenceDbCql.InsertScheduledJob)
                        .SetParameters(new InsertScheduledJob(jobId, e.JobName, e.JobSchedule.ToStringFast(), e.JobScheduleDate, e.JobScheduleInterval, e.TaskName, e.TaskEnabled, e.CreatedOn, e.CreatedBy))
                        .QueueCommand()
                };

                if (e.DaysOfWeek is not null)
                {
                    queuedCommands.Add(
                    db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertScheduledJobDays)}", ReferenceDbCql.InsertScheduledJobDays)
                          .SetParameters(new InsertScheduledJobDays(jobId, e.DaysOfWeek.Monday, e.DaysOfWeek.Tuesday, e.DaysOfWeek.Wednesday, e.DaysOfWeek.Thursday, e.DaysOfWeek.Friday, e.DaysOfWeek.Saturday, e.DaysOfWeek.Sunday))
                        .QueueCommand());
                }
                targetMutationSubmissionStarted = true;
                await db.ExecuteQueuedCommandsAsync(queuedCommands).ConfigureAwait(false);
                succeeded = true;
            }
            finally
            {
                await FinishProjectionScopesAfterMutationAttemptAsync(
                    db,
                    projectionState,
                    succeeded,
                    targetMutationSubmissionStarted).ConfigureAwait(false);
            }

            await ReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships)
                .ConfigureAwait(false);
        }
        catch
        {
            if (!targetMutationSubmissionStarted)
            {
                var ownershipCanBeReleased = await ResolvePreCanonicalScheduledJobDestinationAsync(
                    db,
                    e.JobName,
                    jobId,
                    destinationReservationSubmissionStarted).ConfigureAwait(false);

                if (ownershipCanBeReleased)
                {
                    await TryReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships)
                        .ConfigureAwait(false);
                }
            }
            throw;
        }
    }

    /// <summary>
    /// insert scheduled job
    /// </summary>
    /// <param name="e">scheduled job</param>
    /// <returns></returns>
    public async Task UpdateScheduledJobAsync(ScheduledJobReadModel e)
    {
        var db = _dbFactory.ReferenceDb;
        var writeOperation = CreateScheduledJobWriteOperation();
        var targetMutationSubmissionStarted = false;
        var destinationReservationSubmissionStarted = false;
        try
        {
            await ClaimScheduledJobWriteScopesAsync(
                db,
                writeOperation,
                [GetScheduledJobIdOwnershipScope(e.JobId)]).ConfigureAwait(false);

            var existing = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJob)}", ReferenceDbCql.GetScheduledJob)
                .SetParameters(new GetScheduledJob(e.JobId))
                .ExecuteSingleAsync(MapToScheduledJob!);
            if (existing is null)
            {
                throw new StorageException(
                    $"Scheduled job {e.JobId} does not exist; update was not submitted.");
            }

            await ClaimScheduledJobWriteScopesAsync(
                db,
                writeOperation,
                new[] { existing.JobName, e.JobName }
                    .Distinct(StringComparer.Ordinal)
                    .Select(GetScheduledJobNameOwnershipScope)).ConfigureAwait(false);

            var confirmed = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJob)}", ReferenceDbCql.GetScheduledJob)
                .SetParameters(new GetScheduledJob(e.JobId))
                .ExecuteSingleAsync(MapToScheduledJob!);
            if (confirmed is null ||
                !string.Equals(confirmed.JobName, existing.JobName, StringComparison.Ordinal))
            {
                throw new StorageException(
                    $"Scheduled job {e.JobId} changed while its name ownership was being acquired.");
            }

            var projectionState = await SuspendScheduledJobProjectionAsync(
                db,
                string.Equals(existing.JobName, e.JobName, StringComparison.Ordinal)
                    ? [e.JobName]
                    : [existing.JobName, e.JobName]);
            var succeeded = false;
            try
            {
                Guid? oldNameReservationToken = null;
                if (!string.Equals(existing.JobName, e.JobName, StringComparison.Ordinal))
                {
                    oldNameReservationToken = await ReserveScheduledJobNameAsync(
                        db,
                        existing.JobName,
                        e.JobId).ConfigureAwait(false);
                }

                if (!string.Equals(existing.JobName, e.JobName, StringComparison.Ordinal))
                    destinationReservationSubmissionStarted = true;
                _ = await ReserveScheduledJobNameAsync(
                    db,
                    e.JobName,
                    e.JobId).ConfigureAwait(false);

                if (ScheduledJobCanonicalMutationSubmittingForTestingAsync is { } mutationSubmitting)
                    await mutationSubmitting().ConfigureAwait(false);

                var queuedCommands = new List<object>
                {
                    db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteScheduledJob)}", ReferenceDbCql.DeleteScheduledJob)
                        .SetParameters(new DeleteScheduledJob(e.JobId))
                        .QueueCommand(),
                    db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteScheduledJobDays)}", ReferenceDbCql.DeleteScheduledJobDays)
                        .SetParameters(new DeleteScheduledJobDays(e.JobId))
                        .QueueCommand(),
                    db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertScheduledJob)}", ReferenceDbCql.InsertScheduledJob)
                        .SetParameters(new InsertScheduledJob(e.JobId, e.JobName, e.JobSchedule.ToStringFast(), e.JobScheduleDate, e.JobScheduleInterval, e.TaskName, e.TaskEnabled, e.CreatedOn, e.CreatedBy))
                        .QueueCommand()
                };

                if (e.DaysOfWeek != null)
                {
                    queuedCommands.Add(
                    db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertScheduledJobDays)}", ReferenceDbCql.InsertScheduledJobDays)
                          .SetParameters(new InsertScheduledJobDays(e.JobId, e.DaysOfWeek.Monday, e.DaysOfWeek.Tuesday, e.DaysOfWeek.Wednesday, e.DaysOfWeek.Thursday, e.DaysOfWeek.Friday, e.DaysOfWeek.Saturday, e.DaysOfWeek.Sunday))
                           .QueueCommand());
                }

                targetMutationSubmissionStarted = true;
                await ProjectionMutationSafety.ExecuteCanonicalMutationThenReleaseReservationAsync(
                    () => db.ExecuteQueuedCommandsAsync(queuedCommands),
                    oldNameReservationToken.HasValue
                        ? () => ReleaseScheduledJobNameReservationAsync(
                            db,
                            existing.JobName,
                            e.JobId,
                            oldNameReservationToken.Value)
                        : static () => Task.CompletedTask).ConfigureAwait(false);
                succeeded = true;
            }
            finally
            {
                await FinishProjectionScopesAfterMutationAttemptAsync(
                    db,
                    projectionState,
                    succeeded,
                    targetMutationSubmissionStarted).ConfigureAwait(false);
            }

            await ReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships)
                .ConfigureAwait(false);
        }
        catch
        {
            if (!targetMutationSubmissionStarted)
            {
                var ownershipCanBeReleased = await ResolvePreCanonicalScheduledJobDestinationAsync(
                    db,
                    e.JobName,
                    e.JobId,
                    destinationReservationSubmissionStarted).ConfigureAwait(false);

                if (ownershipCanBeReleased)
                {
                    await TryReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships)
                        .ConfigureAwait(false);
                }
            }
            throw;
        }
    }
    
    /// <summary>
    /// update lookup type
    /// </summary>
    /// <param name="id"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task UpdateLookupTypeAsync(LookupTypeId id, LookupTypeReadModel e)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.ReferenceDb;

        queuedCommands.Add(
        db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteLookupType)}", ReferenceDbCql.DeleteLookupType)
               .SetParameters(new DeleteLookupType(id.LookupTypeName, id.OrderId))
               .QueueCommand());

        queuedCommands.Add(
        db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertLookupType)}", ReferenceDbCql.InsertLookupType)
               .SetParameters(new InsertLookupType(e.LookupTypeName, e.ShortCode, e.OrderId, e.Description, e.CreatedOn, e.CreatedBy))
               .QueueCommand());
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

     /// <summary>
    /// insert mdi forward loss ratio
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel e)
        => await _dbFactory.ReferenceDb
              .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertMDIForwardLossRatio)}", ReferenceDbCql.InsertMDIForwardLossRatio)
              .SetParameters(new InsertMDIForwardLossRatio(e.MDI, e.TrendDirection.ToStringFast(), e.TradeType.ToStringFast(), e.ForwardLossRatio, e.CreatedBy, e.CreatedOn, e.UpdatedBy, e.UpdatedOn))
              .ExecuteCommandAsync().ConfigureAwait(false);

    /// <summary>
    /// insert mdi forward loss ratios
    /// </summary>
    /// <param name="mdiForwardLossRatios"></param>
    /// <returns></returns>
    public async Task InsertMDIForwardLossRatiosAsync(ICollection<MDIForwardLossRatioReadModel> mdiForwardLossRatios)
    {
        ValidateMdiForwardLossRatioLogicalKeys(mdiForwardLossRatios);
        await _dbFactory.ReferenceDb
            .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertMDIForwardLossRatio)}", ReferenceDbCql.InsertMDIForwardLossRatio)
            .SetParameters(mdiForwardLossRatios.Select(o => new InsertMDIForwardLossRatio(o.MDI, o.TrendDirection.ToStringFast(), o.TradeType.ToStringFast(), o.ForwardLossRatio, o.CreatedBy, o.CreatedOn, o.UpdatedBy, o.UpdatedOn)))
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// delete mdi forward loss ratio
    /// </summary>
    /// <param name="trendDirection"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task DeleteMDIForwardLossRatioAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
        =>  await _dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteMDIForwardLossRatio)}", ReferenceDbCql.DeleteMDIForwardLossRatio)
                .SetParameters(new DeleteMDIForwardLossRatio(trendDirection.ToStringFast(), tradeType.ToStringFast()))
                .ExecuteCommandAsync();

    /// <summary>
    /// update mdi forward loss ratio
    /// </summary>
    /// <param name="mdiForwardLossRatio"></param>
    /// <returns></returns>
    public async Task UpdateMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel mdiForwardLossRatio)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.ReferenceDb;

        // Delete the existing record
        queuedCommands.Add(
            db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteMDIForwardLossRatio)}", ReferenceDbCql.DeleteMDIForwardLossRatio)
                .SetParameters(new DeleteMDIForwardLossRatio(mdiForwardLossRatio.TrendDirection.ToStringFast(), mdiForwardLossRatio.TradeType.ToStringFast()))
                .QueueCommand());

        // Insert the updated record
        queuedCommands.Add(
            db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertMDIForwardLossRatio)}", ReferenceDbCql.InsertMDIForwardLossRatio)
                .SetParameters(new InsertMDIForwardLossRatio(mdiForwardLossRatio.MDI, mdiForwardLossRatio.TrendDirection.ToStringFast(), mdiForwardLossRatio.TradeType.ToStringFast(), mdiForwardLossRatio.ForwardLossRatio, mdiForwardLossRatio.CreatedBy, mdiForwardLossRatio.CreatedOn, mdiForwardLossRatio.UpdatedBy, mdiForwardLossRatio.UpdatedOn))
                .QueueCommand());
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// Idempotently rebuilds the ReferenceDb V2 query projections from their canonical tables.
    /// This method is intended for an operator-controlled migration before V2-only reads are deployed.
    /// </summary>
    public async Task<ReferenceProjectionBackfillResult> BackfillQueryProjectionsV2Async(
        int batchSize = 256,
        CancellationToken cancellationToken = default,
        DateTime? staleOperationCutoffUtc = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ProjectionMutationSafety.ValidateStaleOperationCutoffUtc(
            staleOperationCutoffUtc,
            nameof(staleOperationCutoffUtc));

        var db = _dbFactory.ReferenceDb;
        if (staleOperationCutoffUtc is { } verifiedInactiveCutoffUtc)
        {
            await RecoverVerifiedInactiveScheduledJobWritesAsync(
                db,
                verifiedInactiveCutoffUtc,
                cancellationToken).ConfigureAwait(false);
            await RecoverVerifiedInactiveProjectionMutationsAsync(
                db,
                [ScheduledJobProjectionName],
                verifiedInactiveCutoffUtc,
                cancellationToken);
        }
        long scheduledJobCount = 0;
        ReferenceProjectionMutation? scheduledJobMutation = null;
        var published = false;
        var targetMutationSubmissionStarted = false;
        try
        {
            scheduledJobMutation = await SuspendProjectionAsync(
                db,
                ScheduledJobProjectionName);

            var scheduledJobs = new Dictionary<string, int>(StringComparer.Ordinal);
            var scheduledJobNamesById = new Dictionary<int, string>();
            await foreach (var row in db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobs)}", ReferenceDbCql.GetScheduledJobs)
                .ExecuteStreamAsync(MapToScheduledJob!, cancellationToken))
            {
                if (scheduledJobs.TryGetValue(row.JobName, out var existingJobId)
                    && existingJobId != row.JobId)
                {
                    throw new StorageException(
                        $"Scheduled job name '{row.JobName}' is assigned to jobs {existingJobId} and {row.JobId}.");
                }
                if (scheduledJobNamesById.TryGetValue(row.JobId, out var existingJobName)
                    && !string.Equals(existingJobName, row.JobName, StringComparison.Ordinal))
                {
                    throw new StorageException(
                        $"Scheduled job {row.JobId} has canonical names '{existingJobName}' and '{row.JobName}'.");
                }
                scheduledJobs[row.JobName] = row.JobId;
                scheduledJobNamesById[row.JobId] = row.JobName;
                scheduledJobCount++;
            }

            foreach (var scheduledJob in scheduledJobs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                targetMutationSubmissionStarted = true;
                await EnsureScheduledJobNameProjectionAsync(
                    db,
                    scheduledJob.Key,
                    scheduledJob.Value,
                    cancellationToken);
            }

            var reconciliation = await ReconcileQueryProjectionsV2Async(cancellationToken);
            if (!reconciliation.IsConsistent)
            {
                throw new StorageException(
                    "ReferenceDb V2 projection backfill did not reconcile " +
                    $"(jobs missing={reconciliation.MissingScheduledJobs}, " +
                    $"jobs unexpected={reconciliation.UnexpectedScheduledJobs}, " +
                    $"jobs tokenless={reconciliation.TokenlessScheduledJobReservations}). " +
                    "Replay the backfill before cutover.");
            }

            // The whole-projection rebuild supersedes any older per-bucket/per-name readiness
            // overrides. A concurrent scoped writer has a group marker and poisons this backfill's
            // ownership epoch, so global cutover cannot publish over that deletion.
            targetMutationSubmissionStarted = true;
            await ClearScopedProjectionStatesAsync(
                db,
                [ScheduledJobProjectionName],
                cancellationToken);

            var scheduledJobCompleted = await TryCompleteProjectionAsync(
                db,
                ScheduledJobProjectionName,
                scheduledJobMutation.Value);
            if (!scheduledJobCompleted)
            {
                throw new StorageException(
                    "ReferenceDb V2 projection cutover was superseded by a concurrent mutation. " +
                    "The affected projection remains on canonical fallback; replay the backfill.");
            }

            await DeleteProjectionMutationAsync(
                db,
                ScheduledJobProjectionName,
                scheduledJobMutation.Value.Generation);
            published = true;
            return new ReferenceProjectionBackfillResult(scheduledJobCount);
        }
        finally
        {
            if (!published && ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted))
            {
                var cleanupTasks = new List<Task>(1);
                if (scheduledJobMutation.HasValue)
                {
                    cleanupTasks.Add(AbandonProjectionAsync(
                        db,
                        ScheduledJobProjectionName,
                        scheduledJobMutation.Value));
                }
                if (cleanupTasks.Count != 0)
                    await Task.WhenAll(cleanupTasks);
            }
        }

    }

    /// <summary>
    /// Compares canonical ReferenceDb keys with the V2 projection keys after a backfill.
    /// </summary>
    public async Task<ReferenceProjectionReconciliationResult> ReconcileQueryProjectionsV2Async(
        CancellationToken cancellationToken = default)
    {
        var db = _dbFactory.ReferenceDb;
        long sourceJobCount = 0;
        var sourceJobs = new HashSet<ScheduledJobProjectionKey>();
        await foreach (var row in db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobs)}", ReferenceDbCql.GetScheduledJobs)
            .ExecuteStreamAsync(MapToScheduledJob!, cancellationToken))
        {
            sourceJobs.Add(new ScheduledJobProjectionKey(row.JobName, row.JobId));
            sourceJobCount++;
        }

        long projectedJobCount = 0;
        long tokenlessScheduledJobReservations = 0;
        var projectedJobs = new HashSet<ScheduledJobProjectionKey>();
        await foreach (var row in db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobsByNameV3All)}", ReferenceDbCql.GetScheduledJobsByNameV3All)
            .ExecuteStreamAsync(MapToScheduledJobProjectionRow, cancellationToken))
        {
            projectedJobs.Add(row.Key);
            projectedJobCount++;
            if (!row.ReservationToken.HasValue)
                tokenlessScheduledJobReservations++;
        }

        return new ReferenceProjectionReconciliationResult(
            sourceJobCount,
            projectedJobCount,
            sourceJobs.Except(projectedJobs).LongCount(),
            projectedJobs.Except(sourceJobs).LongCount(),
            tokenlessScheduledJobReservations);
    }

 }

internal readonly record struct ScheduledJobProjectionKey(string JobName, int JobId);
internal readonly record struct ScheduledJobProjectionRow(
    ScheduledJobProjectionKey Key,
    Guid? ReservationToken);
internal readonly record struct ScheduledJobReservation(int JobId, Guid? ReservationToken);
internal readonly record struct ScheduledJobWriteOwnership(
    string ScopeType,
    string ScopeKey,
    Guid OperationId,
    DateTime StartedOn);
internal sealed record ScheduledJobWriteOperation(
    Guid OperationId,
    DateTime StartedOn,
    List<ScheduledJobWriteOwnership> Ownerships);
internal readonly record struct MdiForwardLossRatioLogicalKey(
    string TrendDirection,
    string TradeType,
    int Mdi);
internal readonly record struct ReferenceProjectionState(Guid Generation, bool Completed);
internal readonly record struct ReferenceProjectionMutationJournalEntry(
    string ProjectionName,
    Guid MutationId,
    DateTime StartedOn);
internal readonly record struct ReferenceProjectionReadToken(
    Guid ProjectionGeneration,
    Guid? ScopeGeneration);
internal readonly record struct ReferenceProjectionMutation(
    Guid Generation,
    bool RestoreReady,
    bool OwnsWriteOwnership);
internal readonly record struct ReferenceProjectionScopedMutation(
    string ScopeName,
    ReferenceProjectionMutation Mutation);
internal sealed record ReferenceProjectionWriteState(
    string ProjectionName,
    ReferenceProjectionMutation GroupMutation,
    IReadOnlyList<ReferenceProjectionScopedMutation> ScopeMutations);
internal sealed record ScheduledJobIdRow(int Value);

