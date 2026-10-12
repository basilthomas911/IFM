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

/// <summary>Provides Reference data persistence operations.</summary>
/// <param name="connectionSettings">The named database connection settings.</param>
/// <param name="dbFactory">The database-context factory.</param>
/// <param name="sequenceIdGenerator">The sequence identifier generator.</param>
/// <param name="logger">The database-provider logger.</param>
public sealed class ReferenceDbContext(
    IDbConnectionSettings connectionSettings,
    IDbContextFactory dbFactory,
    ISequenceIdGenerator sequenceIdGenerator,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<ReferenceDbContext>(connectionSettings["ReferenceDbConnection"], logger), IReferenceDbContext
{
    readonly PostgresLookupStore postgresLookups = new();
readonly IDbContextFactory _dbFactory = dbFactory;
    readonly ISequenceIdGenerator _sequenceIdGenerator = sequenceIdGenerator;
    /// <summary>Gets the Reference database connection-setting key.</summary>
    public const string ReferenceDbConnection = "ReferenceDbConnection";
    internal const string ScheduledJobProjectionName = "scheduled_job_by_name";
    internal const string ScheduledJobIdOwnershipScope = "job-id";
    internal const string ScheduledJobNameOwnershipScope = "job-name";
    internal const char ProjectionScopeSeparator = '\u001f';
    internal const int MaxReservationRotationAttempts = 8;
    internal Func<string, int, Task>? ScheduledJobBackfillReservationInsertedForTestingAsync { get; set; }
    internal Func<Task>? ScheduledJobCanonicalMutationSubmittingForTestingAsync { get; set; }

    /// <summary>
    /// Gets the database context.
    /// </summary>
    /// <inheritdoc />
    public override ReferenceDbContext Database => this;

    /// <inheritdoc />
    public InstrumentDefinitionStore InstrumentDefinitions => new(this, new TradeStrategySymbolStore(_dbFactory, _sequenceIdGenerator));

    internal static bool MapToBoolean(IObjectDataRecord e)
        => e.GetBool(0);

    internal static Guid MapToGuid(IObjectDataRecord e)
        => e.GetGuid(0);

    internal static string MapToString(IObjectDataRecord e)
        => e.GetString(0);

    internal static ReferenceProjectionState MapToReferenceProjectionState(IObjectDataRecord e)
        => new(e.GetGuid(0), e.GetBool(1));

    internal static ReferenceProjectionMutationJournalEntry MapToReferenceProjectionMutationJournalEntry(
        IObjectDataRecord e)
        => new(e.GetString(0), e.GetGuid(1), e.GetDateTime(2));

    internal static LookupTypeReadModel MapToLookupType(IObjectDataRecord e)
        => new(
            lookupTypeName: e.GetString(0),
            shortCode: e.GetString(1),
            orderId: e.GetInt(2),
            description: e.GetString(3),
            createdOn: e.GetDateTime(4),
            createdBy: e.GetString(5)
        );

    internal static LookupTypeNameReadModel MapToLookupTypeName(IObjectDataRecord e)
        => new(
            lookupTypeName: e.GetString(0)
        );

    internal static TradeStrategyFamilyReadModel MapToTradeStrategyFamily(IObjectDataRecord e) => new()
    {
        TradeStrategyFamilyId = e.GetInt(0),
        DefinitionVersion = e.GetLong(1),
        SystemKey = e.GetString(2),
        Family = e.GetEnum<TradeStrategyFamilyType>(3),
        Strategy = e.GetEnum<TradeStrategyType>(4),
        TimeFrame = e.GetEnum<TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType>(5),
        Symbol = e.GetString(6),
        Currency = e.GetString(7),
        Description = e.GetString(8),
        State = e.GetEnum<TradeStrategyFamilyState>(9),
        CreatedOnUtc = e.GetDateTime(10),
        CreatedBy = e.GetString(11),
    };

    internal static ScheduledJobReadModel MapToScheduledJob<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
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

    internal static ScheduledJobDaysOfWeekReadModel MapToScheduledJobDaysOfWeek<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
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

    internal static MDIForwardLossRatioReadModel MapToMDIForwardLossRatio<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            trendDirection: e.GetEnum<IntrinsicTimeTrendType>(0),
            tradeType: e.GetEnum<TradeType>(1),
            mdi: e.GetInt(2),
            forwardLossRatio: e.GetDouble(3),
            createdBy: e.GetString(4),
            createdOn: e.IsNull(5) ? null : e.GetDateTime(5),
            updatedBy: e.GetString(6),
            updatedOn: e.IsNull(7) ? null : e.GetDateTime(7)
        );

    internal static LookupTypeShortCodeReadModel MapToLookupTypeShortCode<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            shortCode: e.GetString(0),
            orderId: e.GetInt(1)
        );

    internal static ScheduledJobIdRow MapToJobId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(e.GetInt(0));

    internal static ScheduledJobProjectionRow MapToScheduledJobProjectionRow(IObjectDataRecord e)
        => new(
            new ScheduledJobProjectionKey(e.GetString(0), e.GetInt(1)),
            e.IsNull(2) ? null : e.GetGuid(2));

    internal static ScheduledJobReservation MapToScheduledJobReservation(IObjectDataRecord e)
        => new(
            e.GetInt(0),
            e.IsNull(1) ? null : e.GetGuid(1));

    internal static ScheduledJobWriteOwnership MapToScheduledJobWriteOwnership(IObjectDataRecord e)
        => new(
            e.GetString(0),
            e.GetString(1),
            e.GetGuid(2),
            e.GetDateTime(3));


    /// <summary>
    /// return db reader/writer properties
    /// </summary>
    /// <inheritdoc />
    public IReferenceDbReadContext DbReader => this;
    /// <inheritdoc />
    public IReferenceDbWriteContext DbWriter => this;

    /// <summary>
    /// delete lookup type by name
    /// </summary>
    /// <param name="lookupTypeId"></param>
    /// <returns></returns>
    /// <inheritdoc />
    public async Task DeleteLookupTypeAsync(LookupTypeId lookupTypeId)
    {
        this.ValidateRequiredArgument(lookupTypeId);
        if (string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
        {
            await (this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
            {
                await (postgresLookups.DeleteAsync(transaction, lookupTypeId, token))
                .ConfigureAwait(false);
                return true;
            }))
                .ConfigureAwait(false);
            return;
        }
        await (_dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteLookupType)}", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ReferenceDbSql.DeleteLookupType : ReferenceDbCql.DeleteLookupType)
               .SetParameters(new DeleteLookupType(lookupTypeId.LookupTypeName, lookupTypeId.OrderId))
               .ExecuteCommandAsync())
                .ConfigureAwait(false);

        var lookupTypes = await (GetLookupTypeAsync(lookupTypeId.LookupTypeName))
                .ConfigureAwait(false);
        if (lookupTypes?.Count > 0)
        {
            var orderId = 0;
            foreach (var e in lookupTypes.OrderBy(e => e.OrderId).ThenBy(e => e.ShortCode, StringComparer.Ordinal))
                await (UpdateLookupTypeAsync(e.Id, e with { OrderId = orderId++ }))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// delete scheduled job
    /// </summary>
    /// <param name="scheduledJobId"></param>
    /// <returns></returns>
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task DeleteScheduledJobAsync(int scheduledJobId)
    {
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            await this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
            {
                await PostgresScheduledJobStore.DeleteAsync(transaction,scheduledJobId,token)
                .ConfigureAwait(false);
                return true;
            })
                .ConfigureAwait(false);
            return;
        }
        var db = _dbFactory.ReferenceDb;
        var writeOperation = this.CreateScheduledJobWriteOperation();
        var targetMutationSubmissionStarted = false;
        try
        {
            await this.ClaimScheduledJobWriteScopesAsync(
                db,
                writeOperation,
                [this.GetScheduledJobIdOwnershipScope(scheduledJobId)])
                    .ConfigureAwait(false);

            // The job-ID ownership is acquired before observing the canonical name.
            var existing = await (db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJob)}", ReferenceDbCql.GetScheduledJob)
                .SetParameters(new GetScheduledJob(scheduledJobId))
                .ExecuteSingleAsync(MapToScheduledJob!))
                .ConfigureAwait(false);
            if (existing is null)
            {
                await this.ReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships)
                    .ConfigureAwait(false);
                return;
            }

            await this.ClaimScheduledJobWriteScopesAsync(
                db,
                writeOperation,
                [this.GetScheduledJobNameOwnershipScope(existing.JobName)])
                    .ConfigureAwait(false);

            var confirmed = await (db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJob)}", ReferenceDbCql.GetScheduledJob)
                .SetParameters(new GetScheduledJob(scheduledJobId))
                .ExecuteSingleAsync(MapToScheduledJob!))
                .ConfigureAwait(false);
            if (confirmed is null ||
                !string.Equals(confirmed.JobName, existing.JobName, StringComparison.Ordinal))
            {
                throw new StorageException(
                    $"Scheduled job {scheduledJobId} changed while its name ownership was being acquired.");
            }

            var projectionState = await (this.SuspendScheduledJobProjectionAsync(db, [existing.JobName]))
                .ConfigureAwait(false);
            var succeeded = false;
            try
            {
                var reservationToken = await this.ReserveScheduledJobNameAsync(
                    db,
                    existing.JobName,
                    scheduledJobId)
                        .ConfigureAwait(false);

                if (ScheduledJobCanonicalMutationSubmittingForTestingAsync is { } mutationSubmitting)
                    await mutationSubmitting()
                        .ConfigureAwait(false);

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
                await this.ExecuteCanonicalMutationThenReleaseReservationAsync(
                    () => db.ExecuteQueuedCommandsAsync(queuedCommands),
                    () => this.ReleaseScheduledJobNameReservationAsync(
                        db,
                        existing.JobName,
                        scheduledJobId,
                        reservationToken))
                            .ConfigureAwait(false);
                succeeded = true;
            }
            finally
            {
                await this.FinishProjectionScopesAfterMutationAttemptAsync(
                    db,
                    projectionState,
                    succeeded,
                    targetMutationSubmissionStarted)
                        .ConfigureAwait(false);
            }

            await this.ReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships)
                .ConfigureAwait(false);
        }
        catch
        {
            if (!targetMutationSubmissionStarted)
                await this.TryReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships)
                    .ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// return next seed id by seed type
    /// </summary>
    /// <param name="seedType"></param>
    /// <returns></returns>
    /// <inheritdoc />
    public async Task<int> GetNextSeedIdAsync(string seedType)
        => checked((int)await _sequenceIdGenerator
            .GetSequenceIdAsync(SequenceNameExtensions.ParseSequenceName(seedType))
            .ConfigureAwait(false));

    /// <inheritdoc />
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
    /// <inheritdoc />
    public async Task<int> GetCurrentSeedIdAsync(string seedType)
        => checked((int)await _sequenceIdGenerator
            .GetHighWatermarkAsync(SequenceNameExtensions.ParseSequenceName(seedType))
            .ConfigureAwait(false));

    /// <inheritdoc />
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
    /// <inheritdoc />
    public async Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId)
       
    {
        this.ValidateRequiredArgument(lookupTypeId);
        return await (_dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypeById)}", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ReferenceDbSql.GetLookupTypeById : ReferenceDbCql.GetLookupTypeById)
                .SetParameters(new GetLookupTypeById(lookupTypeId.LookupTypeName, lookupTypeId.OrderId) { PostgreSql = string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) })
                .ExecuteSingleAsync(MapToLookupType!))
                .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId, CancellationToken cancellationToken)
       
    {
        this.ValidateRequiredArgument(lookupTypeId);
        return await (_dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypeById)}", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ReferenceDbSql.GetLookupTypeById : ReferenceDbCql.GetLookupTypeById)
                .SetParameters(new GetLookupTypeById(lookupTypeId.LookupTypeName, lookupTypeId.OrderId) { PostgreSql = string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) })
                .ExecuteSingleAsync(MapToLookupType!, cancellationToken))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// return lookup types from lookup type name
    /// </summary>
    /// <param name="lookupTypeName"></param>
    /// <returns></returns>
    /// <inheritdoc />
    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName)
       => await (_dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupType)}", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ReferenceDbSql.GetLookupType : ReferenceDbCql.GetLookupType)
                .SetParameters(new GetLookupType(lookupTypeName) { PostgreSql = string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) })
                .ExecuteQueryAsync(MapToLookupType!))
                .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName, CancellationToken cancellationToken)
       => await (_dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupType)}", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ReferenceDbSql.GetLookupType : ReferenceDbCql.GetLookupType)
                .SetParameters(new GetLookupType(lookupTypeName) { PostgreSql = string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) })
                .ExecuteQueryAsync(MapToLookupType!, cancellationToken))
                .ConfigureAwait(false);

    /// <summary>
    /// return all lookup types 
    /// </summary>
    /// <returns></returns>
    /// <inheritdoc />
    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync()
       => await (_dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypes)}", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ReferenceDbSql.GetLookupTypes : ReferenceDbCql.GetLookupTypes)
               .ExecuteQueryAsync(MapToLookupType!))
                .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync(CancellationToken cancellationToken)
       => await (_dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypes)}", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ReferenceDbSql.GetLookupTypes : ReferenceDbCql.GetLookupTypes)
               .ExecuteQueryAsync(MapToLookupType!, cancellationToken))
                .ConfigureAwait(false);

    /// <summary>
    /// return all lookup type names
    /// </summary>
    /// <returns></returns>
    /// <inheritdoc />
    public async Task<ICollection<string>> GetLookupTypeNamesAsync()
       => [.. (await (_dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypeNames)}", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ReferenceDbSql.GetLookupTypeNames : ReferenceDbCql.GetLookupTypeNames)
               .ExecuteQueryAsync(MapToLookupTypeName!))
                .ConfigureAwait(false)).Select(e => e.LookupTypeName)];

    /// <inheritdoc />
    public async Task<ICollection<string>> GetLookupTypeNamesAsync(CancellationToken cancellationToken)
       => [.. (await (_dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypeNames)}", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ReferenceDbSql.GetLookupTypeNames : ReferenceDbCql.GetLookupTypeNames)
               .ExecuteQueryAsync(MapToLookupTypeName!, cancellationToken))
                .ConfigureAwait(false)).Select(e => e.LookupTypeName)];

    /// <summary>
    /// return all lookup type short codes by lookup type
    /// </summary>
    /// <returns></returns>
    /// <inheritdoc />
    public async Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName)
       => await (_dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypeShortCodes)}", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ReferenceDbSql.GetLookupTypeShortCodes : ReferenceDbCql.GetLookupTypeShortCodes)
                .SetParameters(new GetLookupType(lookupTypeName) { PostgreSql = string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) })
                .ExecuteQueryAsync(MapToLookupTypeShortCode!))
                .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName, CancellationToken cancellationToken)
       => await (_dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetLookupTypeShortCodes)}", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ReferenceDbSql.GetLookupTypeShortCodes : ReferenceDbCql.GetLookupTypeShortCodes)
                .SetParameters(new GetLookupType(lookupTypeName) { PostgreSql = string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) })
                .ExecuteQueryAsync(MapToLookupTypeShortCode!, cancellationToken))
                .ConfigureAwait(false);

    /// <summary>
    /// Checks a lookup-type partition without allocating a LINQ iterator or closure.
    /// </summary>
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Retain sequential awaited database operations and explicit loops without allocating LINQ iterators on persistence paths.")]
    public async Task<bool> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode)
    {
        var values = await (GetLookupTypeShortCodesAsync(lookupTypeName))
                .ConfigureAwait(false);
        foreach (var value in values)
            if (string.Equals(value.ShortCode, shortCode, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <inheritdoc />
    public async Task<bool> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode, CancellationToken cancellationToken)
    {
        var values = await (GetLookupTypeShortCodesAsync(lookupTypeName, cancellationToken))
                .ConfigureAwait(false);
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
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<int> GetScheduledJobIdAsync(string scheduledJobName)
    {
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            return await this.LookupTransaction().ExecuteOnceAsync((transaction, token) => PostgresScheduledJobStore.GetIdAsync(transaction,scheduledJobName,token), default)
                .ConfigureAwait(false);

        this.ValidateRequiredArgument(scheduledJobName);
        var db = _dbFactory.ReferenceDb;
        var scopeName = this.GetScheduledJobProjectionScope(scheduledJobName);
        var readToken = await (this.GetScopedProjectionReadTokenAsync(
            db,
            ScheduledJobProjectionName,
            scopeName))
                .ConfigureAwait(false);
        if (readToken is not null)
        {
            var jobId = await (this.GetScheduledJobProjectionIdAsync(db, scheduledJobName))
                .ConfigureAwait(false);
            if (await (this.IsScopedProjectionReadTokenValidAsync(
                db,
                ScheduledJobProjectionName,
                scopeName,
                readToken.Value))
                .ConfigureAwait(false))
                return jobId ?? 0;
        }

        var jobs = await (db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobs)}", ReferenceDbCql.GetScheduledJobs)
            .ExecuteQueryAsync(MapToScheduledJob!))
                .ConfigureAwait(false);
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

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<int> GetScheduledJobIdAsync(string scheduledJobName, CancellationToken cancellationToken)
    {
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            return await this.LookupTransaction().ExecuteOnceAsync((transaction, token) => PostgresScheduledJobStore.GetIdAsync(transaction,scheduledJobName,token), cancellationToken)
                .ConfigureAwait(false);

        this.ValidateRequiredArgument(scheduledJobName);
        var db = _dbFactory.ReferenceDb;
        var scopeName = this.GetScheduledJobProjectionScope(scheduledJobName);
        var readToken = await (this.GetScopedProjectionReadTokenAsync(
            db, ScheduledJobProjectionName, scopeName, cancellationToken))
                .ConfigureAwait(false);
        if (readToken is not null)
        {
            var jobId = await (this.GetScheduledJobProjectionIdAsync(db, scheduledJobName, cancellationToken))
                .ConfigureAwait(false);
            if (await (this.IsScopedProjectionReadTokenValidAsync(
                db, ScheduledJobProjectionName, scopeName, readToken.Value, cancellationToken))
                .ConfigureAwait(false))
                return jobId ?? 0;
        }

        var jobs = await (db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobs)}", ReferenceDbCql.GetScheduledJobs)
            .ExecuteQueryAsync(MapToScheduledJob!, cancellationToken))
                .ConfigureAwait(false);
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
    /// <inheritdoc />
    public async Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync()
    {
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            return [.. await this.LookupTransaction().ExecuteOnceAsync((transaction, token) => PostgresScheduledJobStore.ReadAsync(transaction,token), default)
                .ConfigureAwait(false)];

        var db = _dbFactory.ReferenceDb;
        var scheduledJobs = await (db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobs)}", ReferenceDbCql.GetScheduledJobs)
               .ExecuteQueryAsync(MapToScheduledJob!))
                .ConfigureAwait(false);
        foreach (var e in scheduledJobs)
        {
            var jobDaysOfWeek = await (GetScheduledJobDaysAsync(e.JobId))
                .ConfigureAwait(false);
            if (jobDaysOfWeek is not null)
                e.DaysOfWeek = jobDaysOfWeek;
        }
        return scheduledJobs;

        Task<ScheduledJobDaysOfWeekReadModel?> GetScheduledJobDaysAsync(int jobId)
           => db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobDays)}", ReferenceDbCql.GetScheduledJobDays)
               .SetParameters(new GetScheduledJobDays(jobId))
               .ExecuteSingleAsync(MapToScheduledJobDaysOfWeek!);
    }

    /// <inheritdoc />
    public async Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync(CancellationToken cancellationToken)
    {
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            return [.. await this.LookupTransaction().ExecuteOnceAsync((transaction, token) => PostgresScheduledJobStore.ReadAsync(transaction,token), cancellationToken)
                .ConfigureAwait(false)];

        var db = _dbFactory.ReferenceDb;
        var scheduledJobs = await (db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobs)}", ReferenceDbCql.GetScheduledJobs)
            .ExecuteQueryAsync(MapToScheduledJob!, cancellationToken))
                .ConfigureAwait(false);
        foreach (var e in scheduledJobs)
        {
            var jobDaysOfWeek = await (db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobDays)}", ReferenceDbCql.GetScheduledJobDays)
                .SetParameters(new GetScheduledJobDays(e.JobId))
                .ExecuteSingleAsync(MapToScheduledJobDaysOfWeek!, cancellationToken))
                .ConfigureAwait(false);
            if (jobDaysOfWeek is not null)
                e.DaysOfWeek = jobDaysOfWeek;
        }
        return scheduledJobs;
    }

    /// <summary>
    /// return mdi forward loss ratio data
    /// </summary>
    /// <param name="trendDirection"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    /// <inheritdoc />
    public async Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
        => await (_dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetMDIForwardLossRatios)}", string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? ReferenceDbSql.GetMDIForwardLossRatios : ReferenceDbCql.GetMDIForwardLossRatios)
               .SetParameters(new GetMDIForwardLossRatios(trendDirection.ToStringFast(), tradeType.ToStringFast()) { PostgreSql = string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) })
               .ExecuteQueryAsync(MapToMDIForwardLossRatio!))
                .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(
        IntrinsicTimeTrendType trendDirection,
        TradeType tradeType,
        CancellationToken cancellationToken)
        => await (_dbFactory.ReferenceDb
               .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetMDIForwardLossRatios)}", string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? ReferenceDbSql.GetMDIForwardLossRatios : ReferenceDbCql.GetMDIForwardLossRatios)
               .SetParameters(new GetMDIForwardLossRatios(trendDirection.ToStringFast(), tradeType.ToStringFast()) { PostgreSql = string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) })
               .ExecuteQueryAsync(MapToMDIForwardLossRatio!, cancellationToken))
                .ConfigureAwait(false);

    /// <summary>
    /// insert lookup types
    /// </summary>
    /// <param name="lookupTypes"></param>
    /// <returns></returns>
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertLookupTypeAsync(LookupTypeReadModel e)
    {
        this.ValidateRequiredArgument(e);
        if (string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
        {
            await (this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
            {
                await (postgresLookups.InsertAsync(transaction, e, token))
                .ConfigureAwait(false);
                return true;
            }))
                .ConfigureAwait(false);
            return;
        }
        await (_dbFactory.ReferenceDb
            .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertLookupType)}", ReferenceDbCql.InsertLookupType)
            .SetParameters(new InsertLookupType(e.LookupTypeName, e.ShortCode, e.OrderId, e.Description, e.CreatedOn, e.CreatedBy))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }

    /// <summary>
    /// insert scheduled job
    /// </summary>
    /// <param name="e">scheduled job</param>
    /// <returns></returns>
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertScheduledJobAsync(ScheduledJobReadModel e)
    {
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            this.ValidateRequiredArgument(e);
            var id = await GetNextSeedIdAsync("ScheduledJobId")
                .ConfigureAwait(false);
            await this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
            {
                await PostgresScheduledJobStore.InsertAsync(transaction,e with { JobId = id },token)
                .ConfigureAwait(false);
                return true;
            })
                .ConfigureAwait(false);
            return;
        }
        this.ValidateRequiredArgument(e);
        var db = _dbFactory.ReferenceDb;
        var writeOperation = this.CreateScheduledJobWriteOperation();
        var targetMutationSubmissionStarted = false;
        var destinationReservationSubmissionStarted = false;
        var jobId = 0;
        try
        {
            // The name scope is the only stable identity available before a new ID
            // is allocated, so it must be claimed before reading the reservation.
            await this.ClaimScheduledJobWriteScopesAsync(
                db,
                writeOperation,
                [this.GetScheduledJobNameOwnershipScope(e.JobName)])
                    .ConfigureAwait(false);

            var reservation = await this.ReadScheduledJobReservationAsync(db, e.JobName)
                .ConfigureAwait(false);
            if (reservation is { ReservationToken: null })
            {
                throw new StorageException(
                    $"Scheduled job name '{e.JobName}' has a legacy tokenless reservation. " +
                    "Repair it only while scheduled-job writers are drained.");
            }

            jobId = reservation?.JobId ?? await (GetNextSeedIdAsync("ScheduledJobId"))
                .ConfigureAwait(false);
            if (jobId <= 0)
            {
                throw new StorageException(
                    $"Scheduled job name '{e.JobName}' resolves to invalid job ID {jobId}.");
            }

            await this.ClaimScheduledJobWriteScopesAsync(
                db,
                writeOperation,
                [this.GetScheduledJobIdOwnershipScope(jobId)])
                    .ConfigureAwait(false);

            var duplicate = await (db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJob)}", ReferenceDbCql.GetScheduledJob)
                .SetParameters(new GetScheduledJob(jobId))
                .ExecuteSingleAsync(MapToScheduledJob!))
                .ConfigureAwait(false);
            if (duplicate is not null)
            {
                throw new StorageException(
                    string.Equals(duplicate.JobName, e.JobName, StringComparison.Ordinal)
                        ? $"Scheduled job name '{e.JobName}' is already assigned to job {jobId}."
                        : $"Scheduled job name '{e.JobName}' ambiguously resolves to job {jobId}, " +
                          $"whose canonical name is '{duplicate.JobName}'.");
            }

            var projectionState = await (this.SuspendScheduledJobProjectionAsync(db, [e.JobName]))
                .ConfigureAwait(false);
            var succeeded = false;
            try
            {
                // A reused same-owner reservation gets a fresh epoch while the
                // distributed name and ID scopes remain exclusively owned.
                destinationReservationSubmissionStarted = true;
                _ = await this.ReserveScheduledJobNameAsync(
                    db,
                    e.JobName,
                    jobId)
                        .ConfigureAwait(false);

                if (ScheduledJobCanonicalMutationSubmittingForTestingAsync is { } mutationSubmitting)
                    await mutationSubmitting()
                        .ConfigureAwait(false);

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
                await db.ExecuteQueuedCommandsAsync(queuedCommands)
                    .ConfigureAwait(false);
                succeeded = true;
            }
            finally
            {
                await this.FinishProjectionScopesAfterMutationAttemptAsync(
                    db,
                    projectionState,
                    succeeded,
                    targetMutationSubmissionStarted)
                        .ConfigureAwait(false);
            }

            await this.ReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships)
                .ConfigureAwait(false);
        }
        catch
        {
            if (!targetMutationSubmissionStarted)
            {
                var ownershipCanBeReleased = await this.ResolvePreCanonicalScheduledJobDestinationAsync(
                    db,
                    e.JobName,
                    jobId,
                    destinationReservationSubmissionStarted)
                        .ConfigureAwait(false);

                if (ownershipCanBeReleased)
                {
                    await this.TryReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships)
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
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task UpdateScheduledJobAsync(ScheduledJobReadModel e)
    {
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            await this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
            {
                await PostgresScheduledJobStore.UpdateAsync(transaction,e,token)
                .ConfigureAwait(false);
                return true;
            })
                .ConfigureAwait(false);
            return;
        }
        this.ValidateRequiredArgument(e);
        var db = _dbFactory.ReferenceDb;
        var writeOperation = this.CreateScheduledJobWriteOperation();
        var targetMutationSubmissionStarted = false;
        var destinationReservationSubmissionStarted = false;
        try
        {
            await this.ClaimScheduledJobWriteScopesAsync(
                db,
                writeOperation,
                [this.GetScheduledJobIdOwnershipScope(e.JobId)])
                    .ConfigureAwait(false);

            var existing = await (db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJob)}", ReferenceDbCql.GetScheduledJob)
                .SetParameters(new GetScheduledJob(e.JobId))
                .ExecuteSingleAsync(MapToScheduledJob!))
                .ConfigureAwait(false);
            if (existing is null)
            {
                throw new StorageException(
                    $"Scheduled job {e.JobId} does not exist; update was not submitted.");
            }

            await this.ClaimScheduledJobWriteScopesAsync(
                db,
                writeOperation,
                new[] { existing.JobName, e.JobName }
                    .Distinct(StringComparer.Ordinal)
                    .Select(this.GetScheduledJobNameOwnershipScope))
                        .ConfigureAwait(false);

            var confirmed = await (db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJob)}", ReferenceDbCql.GetScheduledJob)
                .SetParameters(new GetScheduledJob(e.JobId))
                .ExecuteSingleAsync(MapToScheduledJob!))
                .ConfigureAwait(false);
            if (confirmed is null ||
                !string.Equals(confirmed.JobName, existing.JobName, StringComparison.Ordinal))
            {
                throw new StorageException(
                    $"Scheduled job {e.JobId} changed while its name ownership was being acquired.");
            }

            var projectionState = await (this.SuspendScheduledJobProjectionAsync(
                db,
                string.Equals(existing.JobName, e.JobName, StringComparison.Ordinal)
                    ? [e.JobName]
                    : [existing.JobName, e.JobName]))
                .ConfigureAwait(false);
            var succeeded = false;
            try
            {
                Guid? oldNameReservationToken = null;
                if (!string.Equals(existing.JobName, e.JobName, StringComparison.Ordinal))
                {
                    oldNameReservationToken = await this.ReserveScheduledJobNameAsync(
                        db,
                        existing.JobName,
                        e.JobId)
                            .ConfigureAwait(false);
                }

                if (!string.Equals(existing.JobName, e.JobName, StringComparison.Ordinal))
                    destinationReservationSubmissionStarted = true;
                _ = await this.ReserveScheduledJobNameAsync(
                    db,
                    e.JobName,
                    e.JobId)
                        .ConfigureAwait(false);

                if (ScheduledJobCanonicalMutationSubmittingForTestingAsync is { } mutationSubmitting)
                    await mutationSubmitting()
                        .ConfigureAwait(false);

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
                await this.ExecuteCanonicalMutationThenReleaseReservationAsync(
                    () => db.ExecuteQueuedCommandsAsync(queuedCommands),
                    oldNameReservationToken.HasValue
                        ? () => this.ReleaseScheduledJobNameReservationAsync(
                            db,
                            existing.JobName,
                            e.JobId,
                            oldNameReservationToken.Value)
                        : static () => Task.CompletedTask)
                            .ConfigureAwait(false);
                succeeded = true;
            }
            finally
            {
                await this.FinishProjectionScopesAfterMutationAttemptAsync(
                    db,
                    projectionState,
                    succeeded,
                    targetMutationSubmissionStarted)
                        .ConfigureAwait(false);
            }

            await this.ReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships)
                .ConfigureAwait(false);
        }
        catch
        {
            if (!targetMutationSubmissionStarted)
            {
                var ownershipCanBeReleased = await this.ResolvePreCanonicalScheduledJobDestinationAsync(
                    db,
                    e.JobName,
                    e.JobId,
                    destinationReservationSubmissionStarted)
                        .ConfigureAwait(false);

                if (ownershipCanBeReleased)
                {
                    await this.TryReleaseScheduledJobWritesAsync(db, writeOperation.Ownerships)
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
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task UpdateLookupTypeAsync(LookupTypeId id, LookupTypeReadModel e)
    {
        this.ValidateRequiredArgument(e);
        this.ValidateRequiredArgument(id);
        if (string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
        {
            await (this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
            {
                await (postgresLookups.UpdateAsync(transaction, id, e, token))
                .ConfigureAwait(false);
                return true;
            }))
                .ConfigureAwait(false);
            return;
        }
        var queuedCommands = new List<object>();
        var db = _dbFactory.ReferenceDb;

        queuedCommands.Add(
        db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteLookupType)}", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ReferenceDbSql.DeleteLookupType : ReferenceDbCql.DeleteLookupType)
               .SetParameters(new DeleteLookupType(id.LookupTypeName, id.OrderId))
               .QueueCommand());

        queuedCommands.Add(
        db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertLookupType)}", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ReferenceDbSql.InsertLookupType : ReferenceDbCql.InsertLookupType)
               .SetParameters(new InsertLookupType(e.LookupTypeName, e.ShortCode, e.OrderId, e.Description, e.CreatedOn, e.CreatedBy))
               .QueueCommand());
        await (db.ExecuteQueuedCommandsAsync(queuedCommands))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// insert mdi forward loss ratio
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel e)
        
    {
        this.ValidateRequiredArgument(e);
        await _dbFactory.ReferenceDb
              .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertMDIForwardLossRatio)}", string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? ReferenceDbSql.InsertMDIForwardLossRatio : ReferenceDbCql.InsertMDIForwardLossRatio)
              .SetParameters(new InsertMDIForwardLossRatio(e.MDI, e.TrendDirection.ToStringFast(), e.TradeType.ToStringFast(), e.ForwardLossRatio, e.CreatedBy, e.CreatedOn, e.UpdatedBy, e.UpdatedOn) { PostgreSql = string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) })
              .ExecuteCommandAsync()
              .ConfigureAwait(false);
    }

    /// <summary>
    /// insert mdi forward loss ratios
    /// </summary>
    /// <param name="mdiForwardLossRatios"></param>
    /// <returns></returns>
    /// <inheritdoc />
    public async Task InsertMDIForwardLossRatiosAsync(ICollection<MDIForwardLossRatioReadModel> mdiForwardLossRatios)
    {
        this.ValidateRequiredArgument(mdiForwardLossRatios);
        this.ValidateMdiForwardLossRatioLogicalKeys(mdiForwardLossRatios);
        await (_dbFactory.ReferenceDb
            .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertMDIForwardLossRatio)}", string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? ReferenceDbSql.InsertMDIForwardLossRatio : ReferenceDbCql.InsertMDIForwardLossRatio)
            .SetParameters(mdiForwardLossRatios.Select(o => new InsertMDIForwardLossRatio(o.MDI, o.TrendDirection.ToStringFast(), o.TradeType.ToStringFast(), o.ForwardLossRatio, o.CreatedBy, o.CreatedOn, o.UpdatedBy, o.UpdatedOn) { PostgreSql = string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) }))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }

    /// <summary>
    /// delete mdi forward loss ratio
    /// </summary>
    /// <param name="trendDirection"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    /// <inheritdoc />
    public async Task DeleteMDIForwardLossRatioAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
        => await (_dbFactory.ReferenceDb
                .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteMDIForwardLossRatio)}", string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? ReferenceDbSql.DeleteMDIForwardLossRatio : ReferenceDbCql.DeleteMDIForwardLossRatio)
                .SetParameters(new DeleteMDIForwardLossRatio(trendDirection.ToStringFast(), tradeType.ToStringFast()) { PostgreSql = string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) })
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// update mdi forward loss ratio
    /// </summary>
    /// <param name="mdiForwardLossRatio"></param>
    /// <returns></returns>
    /// <inheritdoc />
    public async Task UpdateMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel mdiForwardLossRatio)
    {
        this.ValidateRequiredArgument(mdiForwardLossRatio);
        var queuedCommands = new List<object>();
        var db = _dbFactory.ReferenceDb;

        // Delete the existing record
        queuedCommands.Add(
            db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.DeleteMDIForwardLossRatio)}", string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? ReferenceDbSql.DeleteMDIForwardLossRatio : ReferenceDbCql.DeleteMDIForwardLossRatio)
                .SetParameters(new DeleteMDIForwardLossRatio(mdiForwardLossRatio.TrendDirection.ToStringFast(), mdiForwardLossRatio.TradeType.ToStringFast()) { PostgreSql = string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) })
                .QueueCommand());

        // Insert the updated record
        queuedCommands.Add(
            db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertMDIForwardLossRatio)}", string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? ReferenceDbSql.InsertMDIForwardLossRatio : ReferenceDbCql.InsertMDIForwardLossRatio)
                .SetParameters(new InsertMDIForwardLossRatio(mdiForwardLossRatio.MDI, mdiForwardLossRatio.TrendDirection.ToStringFast(), mdiForwardLossRatio.TradeType.ToStringFast(), mdiForwardLossRatio.ForwardLossRatio, mdiForwardLossRatio.CreatedBy, mdiForwardLossRatio.CreatedOn, mdiForwardLossRatio.UpdatedBy, mdiForwardLossRatio.UpdatedOn) { PostgreSql = string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) })
                .QueueCommand());
        await (db.ExecuteQueuedCommandsAsync(queuedCommands))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Idempotently rebuilds the ReferenceDb V2 query projections from their canonical tables.
    /// This method is intended for an operator-controlled migration before V2-only reads are deployed.
    /// </summary>
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public async Task<ReferenceProjectionBackfillResult> BackfillQueryProjectionsV2Async(
        int batchSize = 256,
        CancellationToken cancellationToken = default,
        DateTime? staleOperationCutoffUtc = null)
    {
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
            // Native exact-name reads use the canonical unique index; no shadow reservation table needs rebuilding.
            return new ReferenceProjectionBackfillResult(0);
        }

        staleOperationCutoffUtc.ValidateStaleOperationCutoffUtc(nameof(staleOperationCutoffUtc));

        var db = _dbFactory.ReferenceDb;
        if (staleOperationCutoffUtc is { } verifiedInactiveCutoffUtc)
        {
            await this.RecoverVerifiedInactiveScheduledJobWritesAsync(
                db,
                verifiedInactiveCutoffUtc,
                cancellationToken)
                    .ConfigureAwait(false);
            await (this.RecoverVerifiedInactiveProjectionMutationsAsync(
                db,
                [ScheduledJobProjectionName],
                verifiedInactiveCutoffUtc,
                cancellationToken))
                .ConfigureAwait(false);
        }
        long scheduledJobCount = 0;
        ReferenceProjectionMutation? scheduledJobMutation = null;
        var published = false;
        var targetMutationSubmissionStarted = false;
        try
        {
            scheduledJobMutation = await (this.SuspendProjectionAsync(
                db,
                ScheduledJobProjectionName))
                .ConfigureAwait(false);

            var scheduledJobs = new Dictionary<string, int>(StringComparer.Ordinal);
            var scheduledJobNamesById = new Dictionary<int, string>();
            await foreach (var row in (db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobs)}", ReferenceDbCql.GetScheduledJobs)
                .ExecuteStreamAsync(MapToScheduledJob!, cancellationToken))
                .ConfigureAwait(false))
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
                await (this.EnsureScheduledJobNameProjectionAsync(
                    db,
                    scheduledJob.Key,
                    scheduledJob.Value,
                    cancellationToken))
                .ConfigureAwait(false);
            }

            var reconciliation = await (ReconcileQueryProjectionsV2Async(cancellationToken))
                .ConfigureAwait(false);
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
            await (this.ClearScopedProjectionStatesAsync(
                db,
                [ScheduledJobProjectionName],
                cancellationToken))
                .ConfigureAwait(false);

            var scheduledJobCompleted = await (this.TryCompleteProjectionAsync(
                db,
                ScheduledJobProjectionName,
                scheduledJobMutation.Value))
                .ConfigureAwait(false);
            if (!scheduledJobCompleted)
            {
                throw new StorageException(
                    "ReferenceDb V2 projection cutover was superseded by a concurrent mutation. " +
                    "The affected projection remains on canonical fallback; replay the backfill.");
            }

            await (this.DeleteProjectionMutationAsync(
                db,
                ScheduledJobProjectionName,
                scheduledJobMutation.Value.Generation))
                .ConfigureAwait(false);
            published = true;
            return new ReferenceProjectionBackfillResult(scheduledJobCount);
        }
        finally
        {
            if (!published && targetMutationSubmissionStarted.CanRemoveProjectionMutationJournalAfterFailure())
            {
                var cleanupTasks = new List<Task>(1);
                if (scheduledJobMutation.HasValue)
                {
                    cleanupTasks.Add(this.AbandonProjectionAsync(
                        db,
                        ScheduledJobProjectionName,
                        scheduledJobMutation.Value));
                }
                if (cleanupTasks.Count != 0)
                    await (Task.WhenAll(cleanupTasks))
                .ConfigureAwait(false);
            }
        }

    }

    /// <summary>
    /// Compares canonical ReferenceDb keys with the V2 projection keys after a backfill.
    /// </summary>
    /// <inheritdoc />
    public async Task<ReferenceProjectionReconciliationResult> ReconcileQueryProjectionsV2Async(
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            return await this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
            {
                var count = (long)(await transaction.ScalarAsync("SELECT count(*) FROM reference.scheduled_job;", [], token)
                .ConfigureAwait(false))!;
                var orphans = (long)(await transaction.ScalarAsync("SELECT count(*) FROM reference.scheduled_job_days d WHERE NOT EXISTS(SELECT 1 FROM reference.scheduled_job j WHERE j.jobId=d.jobId);", [], token)
                .ConfigureAwait(false))!;
                // The unique index is the PostgreSQL name projection, not scheduled_job_by_name.
                return new ReferenceProjectionReconciliationResult(count,count,0,orphans,0);
            }, cancellationToken)
                .ConfigureAwait(false);

        var db = _dbFactory.ReferenceDb;
        long sourceJobCount = 0;
        var sourceJobs = new HashSet<ScheduledJobProjectionKey>();
        await foreach (var row in (db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobs)}", ReferenceDbCql.GetScheduledJobs)
            .ExecuteStreamAsync(MapToScheduledJob!, cancellationToken))
                .ConfigureAwait(false))
        {
            sourceJobs.Add(new ScheduledJobProjectionKey(row.JobName, row.JobId));
            sourceJobCount++;
        }

        long projectedJobCount = 0;
        long tokenlessScheduledJobReservations = 0;
        var projectedJobs = new HashSet<ScheduledJobProjectionKey>();
        await foreach (var row in (db.Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetScheduledJobsByNameV3All)}", ReferenceDbCql.GetScheduledJobsByNameV3All)
            .ExecuteStreamAsync(MapToScheduledJobProjectionRow, cancellationToken))
                .ConfigureAwait(false))
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<TradeStrategyFamilyReadModel>> GetTradeStrategyFamiliesAsync(CancellationToken cancellationToken = default) =>
        [.. (await (_dbFactory.ReferenceDb
            .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.GetTradeStrategyFamilies)}", string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? ReferenceDbSql.GetTradeStrategyFamilies : ReferenceDbCql.GetTradeStrategyFamilies)
            .SetParameters(new GetTradeStrategyFamilies("V1") { PostgreSql = string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) })
            .ExecuteQueryAsync(MapToTradeStrategyFamily, cancellationToken))
                .ConfigureAwait(false)).Concat(await (TradeStrategyFamilyCatalogStore.ReadDefinitionsAsync(_dbFactory, cancellationToken))
                .ConfigureAwait(false))
            .OrderBy(x => x.TradeStrategyFamilyId).ThenBy(x => x.DefinitionVersion)];

    /// <inheritdoc />
    public async Task<TradeStrategyFamilyReadModel?> GetTradeStrategyFamilyAsync(int tradeStrategyFamilyId, long definitionVersion, CancellationToken cancellationToken = default) =>
        (await GetTradeStrategyFamiliesAsync(cancellationToken)
            .ConfigureAwait(false))
            .SingleOrDefault(x => x.TradeStrategyFamilyId == tradeStrategyFamilyId && x.DefinitionVersion == definitionVersion);

    /// <inheritdoc />
    public async Task InsertTradeStrategyFamilyAsync(TradeStrategyFamilyReadModel family, CancellationToken cancellationToken = default)
    {
        this.ValidateRequiredArgument(family);
        await (_dbFactory.ReferenceDb
            .Use($"{nameof(ReferenceDbCql)}.{nameof(ReferenceDbCql.InsertTradeStrategyFamily)}", string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? ReferenceDbSql.InsertTradeStrategyFamily : ReferenceDbCql.InsertTradeStrategyFamily)
            .SetParameters(new InsertTradeStrategyFamily("V1", family.TradeStrategyFamilyId, family.DefinitionVersion, family.SystemKey, family.Family.ToString(), family.Strategy.ToString(), family.TimeFrame.ToString(), family.Symbol, family.Currency, family.Description, family.State.ToString(), family.CreatedOnUtc, family.CreatedBy) { PostgreSql = string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal) })
            .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
    }

}

