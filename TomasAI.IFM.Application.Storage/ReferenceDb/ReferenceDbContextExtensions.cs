using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

internal static class ReferenceDbContextExtensions
{
    extension(ReferenceDbContext context)
    {
        /// <summary>
        /// Performs the ValidateMdiForwardLossRatioLogicalKeys Reference persistence helper operation.
        /// </summary>
        internal void ValidateMdiForwardLossRatioLogicalKeys(
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

        /// <summary>
        /// Performs the GetScheduledJobProjectionScope Reference persistence helper operation.
        /// </summary>
        internal string GetScheduledJobProjectionScope(string scheduledJobName)
            => FormattableString.Invariant(
                $"{ReferenceDbContext.ScheduledJobProjectionName}{ReferenceDbContext.ProjectionScopeSeparator}{scheduledJobName.Length}:{scheduledJobName}");

        /// <summary>
        /// Performs the GetScheduledJobProjectionIdAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<int?> GetScheduledJobProjectionIdAsync(
            IObjectRepository db,
            string scheduledJobName,
            CancellationToken cancellationToken = default)
            => (await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobId)}", ReferenceDbCql.GetScheduledJobId)
                .SetParameters(new GetScheduledJobId(scheduledJobName))
                .ExecuteSingleAsync(ReferenceDbContext.MapToJobId!, cancellationToken))?.Value;

        /// <summary>
        /// Performs the ReadScheduledJobReservationAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<ScheduledJobReservation?> ReadScheduledJobReservationAsync(
            IObjectRepository db,
            string scheduledJobName)
            => await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobReservationV3)}", ReferenceDbCql.GetScheduledJobReservationV3)
                .SetParameters(new GetScheduledJobReservationV3(scheduledJobName))
                .ExecuteSingleAsync<ScheduledJobReservation?>(
                    static row => ReferenceDbContext.MapToScheduledJobReservation(row));

        /// <summary>
        /// Performs the CreateScheduledJobWriteOperation Reference persistence helper operation.
        /// </summary>
        internal ScheduledJobWriteOperation CreateScheduledJobWriteOperation()
            => new(Guid.NewGuid(), DateTime.UtcNow, []);

        /// <summary>
        /// Performs the GetScheduledJobIdOwnershipScope Reference persistence helper operation.
        /// </summary>
        internal (string ScopeType, string ScopeKey) GetScheduledJobIdOwnershipScope(int scheduledJobId)
            => (ReferenceDbContext.ScheduledJobIdOwnershipScope,
                scheduledJobId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>
        /// Performs the GetScheduledJobNameOwnershipScope Reference persistence helper operation.
        /// </summary>
        internal (string ScopeType, string ScopeKey) GetScheduledJobNameOwnershipScope(string scheduledJobName)
            => (ReferenceDbContext.ScheduledJobNameOwnershipScope, scheduledJobName);

        /// <summary>
        /// Performs the ReadScheduledJobWriteOwnershipAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<ScheduledJobWriteOwnership?> ReadScheduledJobWriteOwnershipAsync(
            IObjectRepository db,
            string scopeType,
            string scopeKey)
            => await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobWriteOwnershipV3)}", ReferenceDbCql.GetScheduledJobWriteOwnershipV3)
                .SetParameters(new GetScheduledJobWriteOwnershipV3(scopeType, scopeKey))
                .ExecuteSingleAsync<ScheduledJobWriteOwnership?>(
                    static row => ReferenceDbContext.MapToScheduledJobWriteOwnership(row));

        /// <summary>
        /// Performs the ClaimScheduledJobWriteScopesAsync Reference persistence helper operation.
        /// </summary>
        internal async Task ClaimScheduledJobWriteScopesAsync(
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
                        .ExecuteSingleAsync(ReferenceDbContext.MapToBoolean!);
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
                    await context.TryReleaseScheduledJobWritesAsync(
                        db,
                        [.. operation.Ownerships, ownership])
                            .ConfigureAwait(false);
                    throw;
                }
            }
        }

        /// <summary>
        /// Performs the ReleaseScheduledJobWriteOwnershipAsync Reference persistence helper operation.
        /// </summary>
        internal async Task ReleaseScheduledJobWriteOwnershipAsync(
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
                    .ExecuteSingleAsync(ReferenceDbContext.MapToBoolean!);
                if (applied == true)
                    return;
            }
            catch
            {
                var currentAfterFailure = await context.ReadScheduledJobWriteOwnershipAsync(
                    db,
                    ownership.ScopeType,
                    ownership.ScopeKey)
                        .ConfigureAwait(false);
                if (currentAfterFailure is null ||
                    currentAfterFailure.Value.OperationId != ownership.OperationId)
                {
                    return;
                }
                throw;
            }

            var current = await context.ReadScheduledJobWriteOwnershipAsync(
                db,
                ownership.ScopeType,
                ownership.ScopeKey)
                    .ConfigureAwait(false);
            if (current is null || current.Value.OperationId != ownership.OperationId)
                return;

            throw new StorageException(
                $"Scheduled-job {ownership.ScopeType} scope '{ownership.ScopeKey}' ownership could not be released.");
        }

        /// <summary>
        /// Performs the ReleaseScheduledJobWritesAsync Reference persistence helper operation.
        /// </summary>
        internal async Task ReleaseScheduledJobWritesAsync(
            IObjectRepository db,
            IEnumerable<ScheduledJobWriteOwnership> ownerships)
        {
            foreach (var ownership in ownerships.Reverse())
                await context.ReleaseScheduledJobWriteOwnershipAsync(db, ownership)
                    .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the TryReleaseScheduledJobWritesAsync Reference persistence helper operation.
        /// </summary>
        internal async Task TryReleaseScheduledJobWritesAsync(
            IObjectRepository db,
            IEnumerable<ScheduledJobWriteOwnership> ownerships)
        {
            foreach (var ownership in ownerships.Reverse())
            {
                try
                {
                    await context.ReleaseScheduledJobWriteOwnershipAsync(db, ownership)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // An unresolved exact row remains fail-closed until explicit
                    // writers-drained stale recovery.
                }
            }
        }

        /// <summary>
        /// Performs the GetProjectionStateAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<ReferenceProjectionState?> GetProjectionStateAsync(
            IObjectRepository db,
            string projectionName,
            CancellationToken cancellationToken = default)
        {
            var states = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetReferenceProjectionStateV3)}", ReferenceDbCql.GetReferenceProjectionStateV3)
                .SetParameters(new GetReferenceProjectionStateV3(projectionName))
                .ExecuteQueryAsync(ReferenceDbContext.MapToReferenceProjectionState!, cancellationToken);
            return states.Count == 0 ? null : states.First();
        }

        /// <summary>
        /// Performs the GetScopedProjectionReadTokenAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<ReferenceProjectionReadToken?> GetScopedProjectionReadTokenAsync(
            IObjectRepository db,
            string projectionName,
            string scopeName,
            CancellationToken cancellationToken = default)
        {
            var projectionState = await context.GetProjectionStateAsync(db, projectionName, cancellationToken);
            if (projectionState is not { Completed: true })
                return null;

            var scopeState = await context.GetProjectionStateAsync(db, scopeName, cancellationToken);
            var activeScopeMutations = await context.GetProjectionMutationsAsync(db, scopeName, cancellationToken);
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

        /// <summary>
        /// Performs the IsScopedProjectionReadTokenValidAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<bool> IsScopedProjectionReadTokenValidAsync(
            IObjectRepository db,
            string projectionName,
            string scopeName,
            ReferenceProjectionReadToken token,
            CancellationToken cancellationToken = default)
        {
            var projectionState = await context.GetProjectionStateAsync(db, projectionName, cancellationToken);
            if (projectionState is not { Completed: true } ||
                projectionState.Value.Generation != token.ProjectionGeneration ||
                (await context.GetProjectionMutationsAsync(db, scopeName, cancellationToken)).Count != 0)
            {
                return false;
            }

            var scopeState = await context.GetProjectionStateAsync(db, scopeName, cancellationToken);
            return token.ScopeGeneration.HasValue
                ? scopeState is { Completed: true } &&
                    scopeState.Value.Generation == token.ScopeGeneration.Value
                : scopeState is null;
        }

        /// <summary>
        /// Performs the GetProjectionMutationsAsync Reference persistence helper operation.
        /// </summary>
        internal Task<ICollection<Guid>> GetProjectionMutationsAsync(
            IObjectRepository db,
            string projectionName,
            CancellationToken cancellationToken = default)
            => db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetReferenceProjectionMutationsV3)}", ReferenceDbCql.GetReferenceProjectionMutationsV3)
                .SetParameters(new GetReferenceProjectionMutationsV3(projectionName))
                .ExecuteQueryAsync(ReferenceDbContext.MapToGuid, cancellationToken);

        /// <summary>
        /// Performs the ClearScopedProjectionStatesAsync Reference persistence helper operation.
        /// </summary>
        internal async Task ClearScopedProjectionStatesAsync(
            IObjectRepository db,
            IReadOnlyCollection<string> projectionNames,
            CancellationToken cancellationToken)
        {
            var prefixes = projectionNames
                .Select(static projectionName => $"{projectionName}{ReferenceDbContext.ProjectionScopeSeparator}")
                .ToArray();
            var scopedStateNames = new List<string>();
            await foreach (var stateName in db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetReferenceProjectionStateNamesV3All)}", ReferenceDbCql.GetReferenceProjectionStateNamesV3All)
                .ExecuteStreamAsync(ReferenceDbContext.MapToString, cancellationToken))
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

        /// <summary>
        /// Performs the RecoverVerifiedInactiveProjectionMutationsAsync Reference persistence helper operation.
        /// </summary>
        internal async Task RecoverVerifiedInactiveProjectionMutationsAsync(
            IObjectRepository db,
            IReadOnlyCollection<string> projectionNames,
            DateTime staleOperationCutoffUtc,
            CancellationToken cancellationToken)
        {
            var prefixes = projectionNames
                .Select(static projectionName => $"{projectionName}{ReferenceDbContext.ProjectionScopeSeparator}")
                .ToArray();
            var staleMutations = new List<ReferenceProjectionMutationJournalEntry>();
            await foreach (var mutation in db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetReferenceProjectionMutationsV3All)}", ReferenceDbCql.GetReferenceProjectionMutationsV3All)
                .ExecuteStreamAsync(ReferenceDbContext.MapToReferenceProjectionMutationJournalEntry, cancellationToken))
            {
                var isRelevant = projectionNames.Contains(mutation.ProjectionName, StringComparer.Ordinal) ||
                    prefixes.Any(prefix => mutation.ProjectionName.StartsWith(prefix, StringComparison.Ordinal));
                if (isRelevant &&
                    mutation.StartedOn.AsProjectionUtc() <= staleOperationCutoffUtc)
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
                    .ExecuteScalarAsync(ReferenceDbContext.MapToBoolean!);
            }

            await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteReferenceProjectionMutationV3)}", ReferenceDbCql.DeleteReferenceProjectionMutationV3)
                .SetParameters(staleMutations.Select(static mutation =>
                    new DeleteReferenceProjectionMutationV3(
                        mutation.ProjectionName,
                        mutation.MutationId)))
                .ExecuteCommandAsync(cancellationToken);
        }

        /// <summary>
        /// Performs the RecoverVerifiedInactiveScheduledJobWritesAsync Reference persistence helper operation.
        /// </summary>
        internal async Task RecoverVerifiedInactiveScheduledJobWritesAsync(
            IObjectRepository db,
            DateTime staleOperationCutoffUtc,
            CancellationToken cancellationToken)
        {
            await foreach (var ownership in db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobWriteOwnershipsV3All)}", ReferenceDbCql.GetScheduledJobWriteOwnershipsV3All)
                .ExecuteStreamAsync(ReferenceDbContext.MapToScheduledJobWriteOwnership, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ownership.StartedOn.AsProjectionUtc() > staleOperationCutoffUtc)
                    continue;

                await context.ReleaseScheduledJobWriteOwnershipAsync(db, ownership)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Performs the SuspendProjectionAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<ReferenceProjectionMutation> SuspendProjectionAsync(
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
                    .ExecuteScalarAsync(ReferenceDbContext.MapToBoolean!);
                var state = await context.GetProjectionStateAsync(db, projectionName);
                var activeMutations = await context.GetProjectionMutationsAsync(db, projectionName);
                var markerIsExclusive = activeMutations.HasExclusiveProjectionMutation(generation);
                if (!ownsWriteOwnership || !markerIsExclusive)
                {
                    // Poison whichever owner is current. This also poisons a newly claimed epoch when
                    // an older contender's marker survives an ownership handoff.
                    _ = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.FlagReferenceProjectionOwnershipConflictV3)}", ReferenceDbCql.FlagReferenceProjectionOwnershipConflictV3)
                        .SetParameters(new FlagReferenceProjectionOwnershipConflictV3(projectionName))
                        .ExecuteScalarAsync(ReferenceDbContext.MapToBoolean!);
                }

                var inheritedState = state is null && inheritedReadyProjectionName is not null
                    ? await context.GetProjectionStateAsync(db, inheritedReadyProjectionName)
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
                    await context.TryConfirmProjectionOwnershipReleasedOrAbsentAsync(
                        db,
                        projectionName,
                        generation)
                            .ConfigureAwait(false);
                if (false.CanRemoveProjectionMutationJournalAfterFailure(
                    ownershipReleaseOrAbsenceConfirmed: ownershipResolved,
                    activationResponseConfirmed: stateActivationConfirmed))
                {
                    try
                    {
                        await context.DeleteProjectionMutationAsync(db, projectionName, generation);
                    }
                    catch
                    {
                        // Retaining a marker is the safe failure mode.
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Performs the CompleteProjectionAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<bool> CompleteProjectionAsync(
            IObjectRepository db,
            string projectionName,
            Guid generation)
            => await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.CompleteReferenceProjectionStateV3)}", ReferenceDbCql.CompleteReferenceProjectionStateV3)
                .SetParameters(new CompleteReferenceProjectionStateV3(
                    DateTime.UtcNow,
                    projectionName,
                    generation))
                .ExecuteScalarAsync(ReferenceDbContext.MapToBoolean!);

        /// <summary>
        /// Performs the TryCompleteProjectionAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<bool> TryCompleteProjectionAsync(
            IObjectRepository db,
            string projectionName,
            ReferenceProjectionMutation mutation)
        {
            var activeMutations = await context.GetProjectionMutationsAsync(db, projectionName);
            if (!mutation.OwnsWriteOwnership ||
                !activeMutations.HasExclusiveProjectionMutation(mutation.Generation) ||
                !await context.CompleteProjectionAsync(db, projectionName, mutation.Generation))
            {
                return false;
            }

            var releasedWithoutConflict = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.ReleaseReferenceProjectionOwnershipIfSafeV3)}", ReferenceDbCql.ReleaseReferenceProjectionOwnershipIfSafeV3)
                .SetParameters(new ReleaseReferenceProjectionOwnershipV3(projectionName, mutation.Generation))
                .ExecuteScalarAsync(ReferenceDbContext.MapToBoolean!);
            if (true.CanPublishProjectionReady(
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
            await context.ReleaseProjectionOwnershipAsync(db, projectionName, mutation);
            return false;
        }

        /// <summary>
        /// Performs the DeleteProjectionMutationAsync Reference persistence helper operation.
        /// </summary>
        internal Task DeleteProjectionMutationAsync(
            IObjectRepository db,
            string projectionName,
            Guid generation)
            => db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteReferenceProjectionMutationV3)}", ReferenceDbCql.DeleteReferenceProjectionMutationV3)
                .SetParameters(new DeleteReferenceProjectionMutationV3(projectionName, generation))
                .ExecuteCommandAsync();

        /// <summary>
        /// Performs the RestoreProjectionAsync Reference persistence helper operation.
        /// </summary>
        internal async Task RestoreProjectionAsync(
            IObjectRepository db,
            string projectionName,
            ReferenceProjectionMutation mutation)
        {
            var restored = mutation.RestoreReady &&
                await context.TryCompleteProjectionAsync(db, projectionName, mutation);
            if (!restored)
            {
                await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InvalidateReferenceProjectionStateV3)}", ReferenceDbCql.InvalidateReferenceProjectionStateV3)
                    .SetParameters(new InvalidateReferenceProjectionStateV3(mutation.Generation, projectionName))
                    .ExecuteCommandAsync(CancellationToken.None);
                await context.ReleaseProjectionOwnershipAsync(db, projectionName, mutation);
            }
            await context.DeleteProjectionMutationAsync(db, projectionName, mutation.Generation);
        }

        /// <summary>
        /// Performs the AbandonProjectionAsync Reference persistence helper operation.
        /// </summary>
        internal async Task AbandonProjectionAsync(
            IObjectRepository db,
            string projectionName,
            ReferenceProjectionMutation mutation)
        {
            await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InvalidateReferenceProjectionStateV3)}", ReferenceDbCql.InvalidateReferenceProjectionStateV3)
                .SetParameters(new InvalidateReferenceProjectionStateV3(mutation.Generation, projectionName))
                .ExecuteCommandAsync(CancellationToken.None);
            await context.ReleaseProjectionOwnershipAsync(db, projectionName, mutation);
            await context.DeleteProjectionMutationAsync(db, projectionName, mutation.Generation);
        }

        /// <summary>
        /// Performs the ReleaseProjectionOwnershipAsync Reference persistence helper operation.
        /// </summary>
        internal async Task ReleaseProjectionOwnershipAsync(
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
                .ExecuteScalarAsync(ReferenceDbContext.MapToBoolean!);
        }

        /// <summary>
        /// Performs the TryConfirmProjectionOwnershipReleasedOrAbsentAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<bool> TryConfirmProjectionOwnershipReleasedOrAbsentAsync(
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
                    .ExecuteScalarAsync(ReferenceDbContext.MapToBoolean!);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Performs the FinishProjectionMutationAsync Reference persistence helper operation.
        /// </summary>
        internal Task FinishProjectionMutationAsync(
            IObjectRepository db,
            string projectionName,
            ReferenceProjectionMutation mutation,
            bool succeeded)
            => succeeded
                ? context.RestoreProjectionAsync(db, projectionName, mutation)
                : context.AbandonProjectionAsync(db, projectionName, mutation);

        /// <summary>
        /// Performs the JoinProjectionGroupAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<ReferenceProjectionMutation> JoinProjectionGroupAsync(
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
                    .ExecuteScalarAsync(ReferenceDbContext.MapToBoolean!);
                var activeMutations = await context.GetProjectionMutationsAsync(db, projectionName);
                if (!ownsWriteOwnership ||
                    !activeMutations.HasExclusiveProjectionMutation(generation))
                {
                    _ = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.FlagReferenceProjectionOwnershipConflictV3)}", ReferenceDbCql.FlagReferenceProjectionOwnershipConflictV3)
                        .SetParameters(new FlagReferenceProjectionOwnershipConflictV3(projectionName))
                        .ExecuteScalarAsync(ReferenceDbContext.MapToBoolean!);
                }

                // The group journal coordinates normal scoped writes with a whole-projection backfill.
                // It deliberately does not invalidate global readiness for unrelated normal writes.
                return new(generation, RestoreReady: false, ownsWriteOwnership);
            }
            catch
            {
                var ownershipResolved = !ownershipClaimSubmissionStarted ||
                    await context.TryConfirmProjectionOwnershipReleasedOrAbsentAsync(
                        db,
                        projectionName,
                        generation)
                            .ConfigureAwait(false);
                if (false.CanRemoveProjectionMutationJournalAfterFailure(
                    ownershipReleaseOrAbsenceConfirmed: ownershipResolved))
                {
                    try
                    {
                        await context.DeleteProjectionMutationAsync(db, projectionName, generation);
                    }
                    catch
                    {
                        // Retain the journal if its delete is itself ambiguous.
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Performs the FinishProjectionGroupMutationAsync Reference persistence helper operation.
        /// </summary>
        internal async Task FinishProjectionGroupMutationAsync(
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
                    .ExecuteScalarAsync(ReferenceDbContext.MapToBoolean!);
                if (!releasedWithoutConflict)
                {
                    // Await the exact LWT response before deleting the group journal.
                    // If this request is ambiguous, the journal must survive.
                    _ = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.ReleaseReferenceProjectionOwnershipV3)}", ReferenceDbCql.ReleaseReferenceProjectionOwnershipV3)
                        .SetParameters(new ReleaseReferenceProjectionOwnershipV3(
                            projectionName,
                            mutation.Generation))
                        .ExecuteScalarAsync(ReferenceDbContext.MapToBoolean!);
                }
            }
            await context.DeleteProjectionMutationAsync(db, projectionName, mutation.Generation);
        }

        /// <summary>
        /// Performs the SuspendProjectionScopesAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<ReferenceProjectionWriteState> SuspendProjectionScopesAsync(
            IObjectRepository db,
            string projectionName,
            IEnumerable<string> scopeNames)
        {
            var distinctScopeNames = scopeNames
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var groupMutation = await context.JoinProjectionGroupAsync(db, projectionName);
            var scopeMutations = new List<ReferenceProjectionScopedMutation>(distinctScopeNames.Length);
            try
            {
                foreach (var scopeName in distinctScopeNames)
                {
                    scopeMutations.Add(new ReferenceProjectionScopedMutation(
                        scopeName,
                        await context.SuspendProjectionAsync(db, scopeName, projectionName)));
                }
                return new ReferenceProjectionWriteState(projectionName, groupMutation, scopeMutations);
            }
            catch
            {
                foreach (var scopeMutation in scopeMutations)
                    await context.AbandonProjectionAsync(db, scopeMutation.ScopeName, scopeMutation.Mutation);
                await context.FinishProjectionGroupMutationAsync(db, projectionName, groupMutation);
                throw;
            }
        }

        /// <summary>
        /// Performs the FinishProjectionScopesAsync Reference persistence helper operation.
        /// </summary>
        internal async Task FinishProjectionScopesAsync(
            IObjectRepository db,
            ReferenceProjectionWriteState state,
            bool succeeded)
        {
            Exception? firstError = null;
            foreach (var scopeMutation in state.ScopeMutations)
            {
                try
                {
                    await context.FinishProjectionMutationAsync(
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
                    await context.FinishProjectionGroupMutationAsync(db, state.ProjectionName, state.GroupMutation);
                }
                catch (Exception exception)
                {
                    firstError = exception;
                }
            }

            if (firstError is not null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstError).Throw();
        }

        /// <summary>
        /// Performs the FinishProjectionScopesAfterMutationAttemptAsync Reference persistence helper operation.
        /// </summary>
        internal Task FinishProjectionScopesAfterMutationAttemptAsync(
            IObjectRepository db,
            ReferenceProjectionWriteState state,
            bool succeeded,
            bool targetMutationSubmissionStarted)
            => succeeded || targetMutationSubmissionStarted.CanRemoveProjectionMutationJournalAfterFailure()
                ? context.FinishProjectionScopesAsync(db, state, succeeded)
                : Task.CompletedTask;

        /// <summary>
        /// Performs the SuspendScheduledJobProjectionAsync Reference persistence helper operation.
        /// </summary>
        internal Task<ReferenceProjectionWriteState> SuspendScheduledJobProjectionAsync(
            IObjectRepository db,
            IEnumerable<string> scheduledJobNames)
            => context.SuspendProjectionScopesAsync(
                db,
                ReferenceDbContext.ScheduledJobProjectionName,
                scheduledJobNames.Select(context.GetScheduledJobProjectionScope));

        /// <summary>
        /// Performs the ReserveScheduledJobNameAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<Guid> ReserveScheduledJobNameAsync(
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

            for (var attempt = 0; attempt < ReferenceDbContext.MaxReservationRotationAttempts; attempt++)
            {
                var reservation = await context.ReadScheduledJobReservationAsync(db, scheduledJobName)
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
                    .ExecuteSingleAsync(ReferenceDbContext.MapToBoolean!);
                if (rotated == true)
                    return replacementReservationToken;
            }

            throw new StorageException(
                $"Scheduled job name '{scheduledJobName}' reservation changed too frequently; retry the write.");
        }

        /// <summary>
        /// Performs the ReleaseScheduledJobNameReservationAsync Reference persistence helper operation.
        /// </summary>
        internal async Task ReleaseScheduledJobNameReservationAsync(
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
                    .ExecuteSingleAsync(ReferenceDbContext.MapToBoolean!);
                if (applied == true)
                    return;
            }
            catch
            {
                var currentAfterFailure = await context.ReadScheduledJobReservationAsync(db, scheduledJobName)
                    .ConfigureAwait(false);
                if (currentAfterFailure is null)
                    return;
                throw;
            }

            var current = await context.ReadScheduledJobReservationAsync(db, scheduledJobName)
                .ConfigureAwait(false);
            if (current is null)
                return;

            throw new StorageException(
                $"Scheduled job name '{scheduledJobName}' reservation changed or could not be released; " +
                "the mutation remains fail-closed for stale recovery.");
        }

        /// <summary>
        /// Performs the ResolvePreCanonicalScheduledJobDestinationAsync Reference persistence helper operation.
        /// </summary>
        internal async Task<bool> ResolvePreCanonicalScheduledJobDestinationAsync(
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
                current = await context.ReadScheduledJobReservationAsync(db, scheduledJobName)
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
                await context.ReleaseScheduledJobNameReservationAsync(
                    db,
                    scheduledJobName,
                    scheduledJobId,
                    currentToken)
                        .ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Performs the EnsureScheduledJobNameProjectionAsync Reference persistence helper operation.
        /// </summary>
        internal async Task EnsureScheduledJobNameProjectionAsync(
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
                .ExecuteSingleAsync(ReferenceDbContext.MapToBoolean!);
            if (inserted != true)
                return;

            if (context.ScheduledJobBackfillReservationInsertedForTestingAsync is { } insertedHook)
                await insertedHook(scheduledJobName, scheduledJobId)
                    .ConfigureAwait(false);

            // Do not let cancellation strand a candidate after its LWT was acknowledged.
            // A same-owner writer rotates to a new token, so this exact compensation can
            // never delete a newer reservation incarnation.
            var canonical = await db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJob)}", ReferenceDbCql.GetScheduledJob)
                .SetParameters(new GetScheduledJob(scheduledJobId))
                .ExecuteSingleAsync(ReferenceDbContext.MapToScheduledJob!);
            if (canonical is not null &&
                string.Equals(canonical.JobName, scheduledJobName, StringComparison.Ordinal))
            {
                return;
            }

            await context.ReleaseScheduledJobNameReservationAsync(
                db,
                scheduledJobName,
                scheduledJobId,
                reservationToken)
                    .ConfigureAwait(false);
        }

        /// <summary>Executes an acknowledged canonical mutation before releasing its uniqueness reservation.</summary>
        /// <param name="mutateCanonicalAsync">The canonical mutation that must receive a positive acknowledgement.</param>
        /// <param name="releaseReservationAsync">The reservation release performed only after canonical acknowledgement.</param>
        internal async Task ExecuteCanonicalMutationThenReleaseReservationAsync(
            Func<Task> mutateCanonicalAsync,
            Func<Task> releaseReservationAsync)
        {
            ArgumentNullException.ThrowIfNull(mutateCanonicalAsync);
            ArgumentNullException.ThrowIfNull(releaseReservationAsync);

            await mutateCanonicalAsync()
                .ConfigureAwait(false);
            await releaseReservationAsync()
                .ConfigureAwait(false);
        }
    }

    extension(DateTime? cutoffUtc)
    {
        /// <summary>Validates that an optional stale-operation cutoff is UTC and is not in the future.</summary>
        /// <param name="parameterName">The public API parameter name to report when validation fails.</param>
        internal void ValidateStaleOperationCutoffUtc(string parameterName)
        {
            if (cutoffUtc is not { } cutoff)
                return;
            if (cutoff.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "The stale-operation cutoff must have DateTimeKind.Utc.",
                    parameterName);
            }
            if (cutoff > DateTime.UtcNow)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    cutoff,
                    "The stale-operation cutoff cannot be in the future.");
            }
        }
    }

    extension(DateTime value)
    {
        /// <summary>Normalizes a persisted projection timestamp to UTC.</summary>
        /// <returns>The equivalent UTC timestamp.</returns>
        internal DateTime AsProjectionUtc()
            => value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
    }

    extension(ICollection<Guid> activeMutations)
    {
        /// <summary>Determines whether a mutation is the projection's only active mutation marker.</summary>
        /// <param name="mutationId">The mutation identifier that must own the exclusive marker.</param>
        /// <returns><see langword="true"/> when the requested mutation is the sole active marker.</returns>
        internal bool HasExclusiveProjectionMutation(Guid mutationId)
            => activeMutations.Count == 1 && activeMutations.Contains(mutationId);
    }

    extension(bool targetMutationSubmissionStarted)
    {
        /// <summary>Determines whether a failed projection mutation journal can be removed without hiding an ambiguous write.</summary>
        /// <param name="ownershipReleaseOrAbsenceConfirmed">Whether ownership is confirmed released or absent.</param>
        /// <param name="activationResponseConfirmed">Whether any activation response is known rather than ambiguous.</param>
        /// <returns><see langword="true"/> when cleanup cannot conceal a submitted or ambiguously applied mutation.</returns>
        internal bool CanRemoveProjectionMutationJournalAfterFailure(
            bool ownershipReleaseOrAbsenceConfirmed = true,
            bool activationResponseConfirmed = true)
            => !targetMutationSubmissionStarted &&
                ownershipReleaseOrAbsenceConfirmed &&
                activationResponseConfirmed;
    }

    extension(bool operationSucceeded)
    {
        /// <summary>Determines whether a projection generation may be published as ready.</summary>
        /// <param name="ownsWriteEpoch">Whether the operation owns the projection write epoch.</param>
        /// <param name="wasReadyOrExactlyReconciled">Whether prior readiness was retained or reconciliation was exact.</param>
        /// <param name="markerIsExclusive">Whether the operation owns the only active mutation marker.</param>
        /// <param name="generationStillMatches">Whether the projection generation still matches the operation.</param>
        /// <param name="ownershipReleasedWithoutConflict">Whether ownership was released without an overlapping writer conflict.</param>
        /// <returns><see langword="true"/> when every readiness publication condition is satisfied.</returns>
        internal bool CanPublishProjectionReady(
            bool ownsWriteEpoch,
            bool wasReadyOrExactlyReconciled,
            bool markerIsExclusive,
            bool generationStillMatches,
            bool ownershipReleasedWithoutConflict)
            => operationSucceeded &&
                ownsWriteEpoch &&
                wasReadyOrExactlyReconciled &&
                markerIsExclusive &&
                generationStillMatches &&
                ownershipReleasedWithoutConflict;
    }
}
