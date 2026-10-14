using TomasAI.IFM.Application.MarketData.Contracts;
using System.Text.Json;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.Serialization;
using Npgsql;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Microsoft.Extensions.Logging;

using TomasAI.IFM.Framework.Storage;

using TomasAI.IFM.Shared.JobScheduler;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

using TomasAI.IFM.Shared.Storage;


using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
/// <summary>Provides typed PostgreSQL Reference queries and mutations. Domain services own publication and catalog policy.</summary>
/// <param name="connectionSettings">The named PostgreSQL connection.</param>
/// <param name="dbFactory">Retained for constructor compatibility with repository registration.</param>
/// <param name="sequenceIdGenerator">The technical sequence allocator used by the existing seed capability.</param>
/// <param name="logger">The storage logger.</param>
public sealed class ReferenceDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ISequenceIdGenerator sequenceIdGenerator, ILogger<DbProvider> logger) : ObjectDataRepository<ReferenceDbContext>(connectionSettings["ReferenceDbConnection"], logger), IReferenceDbContext
{
    readonly ISequenceIdGenerator _sequenceIdGenerator = sequenceIdGenerator;
    /// <summary>The named Reference database connection.</summary>
    public const string ReferenceDbConnection = "ReferenceDbConnection";
    /// <inheritdoc />
    public override ReferenceDbContext Database => this;

    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
    internal static bool MapToBoolean(IObjectDataRecord e) => e.GetBool(0);
    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
    internal static Guid MapToGuid(IObjectDataRecord e) => e.GetGuid(0);
    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
    internal static string MapToString(IObjectDataRecord e) => e.GetString(0);
    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
    internal static LookupTypeReadModel MapToLookupType(IObjectDataRecord e) => new(lookupTypeName: e.GetString(0), shortCode: e.GetString(1), orderId: e.GetInt(2), description: e.GetString(3), createdOn: e.GetDateTime(4), createdBy: e.GetString(5));
    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
    internal static LookupTypeNameReadModel MapToLookupTypeName(IObjectDataRecord e) => new(lookupTypeName: e.GetString(0));
    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
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
    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
    internal static ScheduledJobReadModel MapToScheduledJob<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(jobId: e.GetInt(0), jobName: e.GetString(1), jobSchedule: e.GetEnum<JobScheduleType>(2), jobScheduleDate: e.GetDateTime(3), jobScheduleInterval: e.GetDouble(4), taskName: e.GetString(5), taskEnabled: e.GetBool(6), createdOn: e.GetDateTime(7), createdBy: e.GetString(8), updatedOn: e.GetDateTime(9), updatedBy: e.GetString(10));
    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
    internal static ScheduledJobDaysOfWeekReadModel MapToScheduledJobDaysOfWeek<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(jobId: e.GetInt(0), monday: e.GetBool(1), tuesday: e.GetBool(2), wednesday: e.GetBool(3), thursday: e.GetBool(4), friday: e.GetBool(5), saturday: e.GetBool(6), sunday: e.GetBool(7));
    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
    internal static MDIForwardLossRatioReadModel MapToMDIForwardLossRatio<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(trendDirection: e.GetEnum<IntrinsicTimeTrendType>(0), tradeType: e.GetEnum<TradeType>(1), mdi: e.GetInt(2), forwardLossRatio: e.GetDouble(3), createdBy: e.GetString(4), createdOn: e.IsNull(5) ? null : e.GetDateTime(5), updatedBy: e.GetString(6), updatedOn: e.IsNull(7) ? null : e.GetDateTime(7));
    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
    internal static LookupTypeShortCodeReadModel MapToLookupTypeShortCode<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(shortCode: e.GetString(0), orderId: e.GetInt(1));
    /// <inheritdoc/>
    public IReferenceDbReadContext DbReader => this;
    /// <inheritdoc/>
    public IReferenceDbWriteContext DbWriter => this;

    /// <inheritdoc/>
    public async Task DeleteLookupTypeAsync(LookupTypeId lookupTypeId)
    {
        this.ValidateRequiredArgument(lookupTypeId);
        await (this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
        {
            await (ReferenceDbContextExtensions.DeleteLookupAsync(transaction, lookupTypeId, token))
            .ConfigureAwait(false);
            return true;
        }))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task DeleteScheduledJobAsync(int scheduledJobId)
    {
        await this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.DeleteScheduledJobAsync(scheduledJobId, token)
            .ConfigureAwait(false);
            return true;
        })
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> GetNextSeedIdAsync(string seedType) => checked((int)await _sequenceIdGenerator.GetSequenceIdAsync(SequenceNameExtensions.ParseSequenceName(seedType))
            .ConfigureAwait(false));
    /// <inheritdoc/>
    public async Task<int> GetNextSeedIdAsync(string seedType, CancellationToken cancellationToken) => checked((int)await _sequenceIdGenerator.GetSequenceIdAsync(SequenceNameExtensions.ParseSequenceName(seedType), cancellationToken)
            .ConfigureAwait(false));
    /// <inheritdoc/>
    public async Task<int> GetCurrentSeedIdAsync(string seedType) => checked((int)await _sequenceIdGenerator.GetHighWatermarkAsync(SequenceNameExtensions.ParseSequenceName(seedType))
            .ConfigureAwait(false));
    /// <inheritdoc/>
    public async Task<int> GetCurrentSeedIdAsync(string seedType, CancellationToken cancellationToken) => checked((int)await _sequenceIdGenerator.GetHighWatermarkAsync(SequenceNameExtensions.ParseSequenceName(seedType), cancellationToken)
            .ConfigureAwait(false));
    /// <inheritdoc/>
    public async Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId)
    {
        this.ValidateRequiredArgument(lookupTypeId);
        return await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetLookupTypeById)}", ReferenceDbSql.GetLookupTypeById)
            .SetParameters(new GetLookupTypeById(lookupTypeId.LookupTypeName, lookupTypeId.OrderId))
            .ExecuteSingleAsync(MapToLookupType!))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(lookupTypeId);
        return await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetLookupTypeById)}", ReferenceDbSql.GetLookupTypeById)
            .SetParameters(new GetLookupTypeById(lookupTypeId.LookupTypeName, lookupTypeId.OrderId))
            .ExecuteSingleAsync(MapToLookupType!, cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName) => await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetLookupType)}", ReferenceDbSql.GetLookupType)
            .SetParameters(new GetLookupType(lookupTypeName))
            .ExecuteQueryAsync(MapToLookupType!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName, CancellationToken cancellationToken) => await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetLookupType)}", ReferenceDbSql.GetLookupType)
            .SetParameters(new GetLookupType(lookupTypeName))
            .ExecuteQueryAsync(MapToLookupType!, cancellationToken))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync() => await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetLookupTypes)}", ReferenceDbSql.GetLookupTypes)
            .ExecuteQueryAsync(MapToLookupType!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync(CancellationToken cancellationToken) => await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetLookupTypes)}", ReferenceDbSql.GetLookupTypes)
            .ExecuteQueryAsync(MapToLookupType!, cancellationToken))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<string>> GetLookupTypeNamesAsync() => [..(await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetLookupTypeNames)}", ReferenceDbSql.GetLookupTypeNames)
            .ExecuteQueryAsync(MapToLookupTypeName!))
            .ConfigureAwait(false)).Select(e => e.LookupTypeName)];
    /// <inheritdoc/>
    public async Task<ICollection<string>> GetLookupTypeNamesAsync(CancellationToken cancellationToken) => [..(await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetLookupTypeNames)}", ReferenceDbSql.GetLookupTypeNames)
            .ExecuteQueryAsync(MapToLookupTypeName!, cancellationToken))
            .ConfigureAwait(false)).Select(e => e.LookupTypeName)];
    /// <inheritdoc/>
    public async Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName) => await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetLookupTypeShortCodes)}", ReferenceDbSql.GetLookupTypeShortCodes)
            .SetParameters(new GetLookupType(lookupTypeName))
            .ExecuteQueryAsync(MapToLookupTypeShortCode!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName, CancellationToken cancellationToken) => await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetLookupTypeShortCodes)}", ReferenceDbSql.GetLookupTypeShortCodes)
            .SetParameters(new GetLookupType(lookupTypeName))
            .ExecuteQueryAsync(MapToLookupTypeShortCode!, cancellationToken))
            .ConfigureAwait(false);
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

    /// <inheritdoc/>
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

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<int> GetScheduledJobIdAsync(string scheduledJobName)
    {
        return await this.LookupTransaction().ExecuteOnceAsync((transaction, token) => transaction.ReadScheduledJobIdAsync(scheduledJobName, token), default)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<int> GetScheduledJobIdAsync(string scheduledJobName, CancellationToken cancellationToken)
    {
        return await this.LookupTransaction().ExecuteOnceAsync((transaction, token) => transaction.ReadScheduledJobIdAsync(scheduledJobName, token), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync()
    {
        return[..await this.LookupTransaction().ExecuteOnceAsync((transaction, token) => transaction.ReadScheduledJobsAsync(token), default)
            .ConfigureAwait(false)];
    }

    /// <inheritdoc/>
    public async Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync(CancellationToken cancellationToken)
    {
        return[..await this.LookupTransaction().ExecuteOnceAsync((transaction, token) => transaction.ReadScheduledJobsAsync(token), cancellationToken)
            .ConfigureAwait(false)];
    }

    /// <inheritdoc/>
    public async Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType) => await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetMDIForwardLossRatios)}", ReferenceDbSql.GetMDIForwardLossRatios)
            .SetParameters(new GetMDIForwardLossRatios(trendDirection.ToStringFast(), tradeType.ToStringFast()))
            .ExecuteQueryAsync(MapToMDIForwardLossRatio!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType, CancellationToken cancellationToken) => await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetMDIForwardLossRatios)}", ReferenceDbSql.GetMDIForwardLossRatios)
            .SetParameters(new GetMDIForwardLossRatios(trendDirection.ToStringFast(), tradeType.ToStringFast()))
            .ExecuteQueryAsync(MapToMDIForwardLossRatio!, cancellationToken))
            .ConfigureAwait(false);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertLookupTypeAsync(LookupTypeReadModel e)
    {
        this.ValidateRequiredArgument(e);
        await (this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
        {
            await (ReferenceDbContextExtensions.InsertLookupAsync(transaction, e, token))
            .ConfigureAwait(false);
            return true;
        }))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertScheduledJobAsync(ScheduledJobReadModel e)
    {
        this.ValidateRequiredArgument(e);
        var id = await GetNextSeedIdAsync("ScheduledJobId")
            .ConfigureAwait(false);
        await this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.InsertScheduledJobAsync(e with { JobId = id }, token)
            .ConfigureAwait(false);
            return true;
        })
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task UpdateScheduledJobAsync(ScheduledJobReadModel e)
    {
        await this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.UpdateScheduledJobAsync(e, token)
            .ConfigureAwait(false);
            return true;
        })
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task UpdateLookupTypeAsync(LookupTypeId id, LookupTypeReadModel e)
    {
        this.ValidateRequiredArgument(e);
        this.ValidateRequiredArgument(id);
        await (this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
        {
            await (ReferenceDbContextExtensions.UpdateLookupAsync(transaction, id, e, token))
            .ConfigureAwait(false);
            return true;
        }))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel e)
    {
        this.ValidateRequiredArgument(e);
        await this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.InsertMDIForwardLossRatio)}", ReferenceDbSql.InsertMDIForwardLossRatio)
            .SetParameters(new InsertMDIForwardLossRatio(e.MDI, e.TrendDirection.ToStringFast(), e.TradeType.ToStringFast(), e.ForwardLossRatio, e.CreatedBy, e.CreatedOn, e.UpdatedBy, e.UpdatedOn))
            .ExecuteCommandAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task InsertMDIForwardLossRatiosAsync(ICollection<MDIForwardLossRatioReadModel> mdiForwardLossRatios)
    {
        this.ValidateRequiredArgument(mdiForwardLossRatios);
        this.ValidateMdiForwardLossRatioLogicalKeys(mdiForwardLossRatios);
        await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.InsertMDIForwardLossRatio)}", ReferenceDbSql.InsertMDIForwardLossRatio)
            .SetParameters(mdiForwardLossRatios.Select(o => new InsertMDIForwardLossRatio(o.MDI, o.TrendDirection.ToStringFast(), o.TradeType.ToStringFast(), o.ForwardLossRatio, o.CreatedBy, o.CreatedOn, o.UpdatedBy, o.UpdatedOn)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteMDIForwardLossRatioAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType) => await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.DeleteMDIForwardLossRatio)}", ReferenceDbSql.DeleteMDIForwardLossRatio)
            .SetParameters(new DeleteMDIForwardLossRatio(trendDirection.ToStringFast(), tradeType.ToStringFast()))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task UpdateMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel mdiForwardLossRatio)
    {
        this.ValidateRequiredArgument(mdiForwardLossRatio);
        var queuedCommands = new List<object>();
        var db = this;
        // Delete the existing record
        queuedCommands.Add(db.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.DeleteMDIForwardLossRatio)}", ReferenceDbSql.DeleteMDIForwardLossRatio)
            .SetParameters(new DeleteMDIForwardLossRatio(mdiForwardLossRatio.TrendDirection.ToStringFast(), mdiForwardLossRatio.TradeType.ToStringFast()))
            .QueueCommand());
        // Insert the updated record
        queuedCommands.Add(db.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.InsertMDIForwardLossRatio)}", ReferenceDbSql.InsertMDIForwardLossRatio)
            .SetParameters(new InsertMDIForwardLossRatio(mdiForwardLossRatio.MDI, mdiForwardLossRatio.TrendDirection.ToStringFast(), mdiForwardLossRatio.TradeType.ToStringFast(), mdiForwardLossRatio.ForwardLossRatio, mdiForwardLossRatio.CreatedBy, mdiForwardLossRatio.CreatedOn, mdiForwardLossRatio.UpdatedBy, mdiForwardLossRatio.UpdatedOn))
            .QueueCommand());
        await (db.ExecuteQueuedCommandsAsync(queuedCommands))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public async Task<ReferenceProjectionBackfillResult> BackfillQueryProjectionsV2Async(int batchSize = 256, CancellationToken cancellationToken = default, DateTime? staleOperationCutoffUtc = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.ValidatePositiveBatchSize(batchSize);
        // Native exact-name reads use the canonical unique index; no shadow reservation table needs rebuilding.
        return new ReferenceProjectionBackfillResult(0);
    }

    /// <inheritdoc/>
    public async Task<ReferenceProjectionReconciliationResult> ReconcileQueryProjectionsV2Async(CancellationToken cancellationToken = default)
    {
        return await this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
        {
            var count = (long)(await transaction.ScalarAsync(ReferenceDbSql.CountScheduledJobs, [], token)
            .ConfigureAwait(false))!;
            var orphans = (long)(await transaction.ScalarAsync(ReferenceDbSql.CountOrphanScheduledJobDays, [], token)
            .ConfigureAwait(false))!;
            // The unique index is the PostgreSQL name projection, not scheduled_job_by_name.
            return new ReferenceProjectionReconciliationResult(count, count, 0, orphans, 0);
        }, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TradeStrategyFamilyReadModel>> GetTradeStrategyFamiliesAsync(CancellationToken cancellationToken = default) => [..(await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetTradeStrategyFamilies)}", ReferenceDbSql.GetTradeStrategyFamilies)
            .SetParameters(new GetTradeStrategyFamilies("V1"))
            .ExecuteQueryAsync(MapToTradeStrategyFamily, cancellationToken))
            .ConfigureAwait(false)).Concat(await (this.ReadCatalogDefinitionsAsync(cancellationToken))
            .ConfigureAwait(false)).OrderBy(x => x.TradeStrategyFamilyId).ThenBy(x => x.DefinitionVersion)];
    /// <inheritdoc/>
    public async Task<TradeStrategyFamilyReadModel?> GetTradeStrategyFamilyAsync(int tradeStrategyFamilyId, long definitionVersion, CancellationToken cancellationToken = default) => (await GetTradeStrategyFamiliesAsync(cancellationToken)
            .ConfigureAwait(false)).SingleOrDefault(x => x.TradeStrategyFamilyId == tradeStrategyFamilyId && x.DefinitionVersion == definitionVersion);
    /// <inheritdoc/>
    public async Task InsertTradeStrategyFamilyAsync(TradeStrategyFamilyReadModel family, CancellationToken cancellationToken = default)
    {
        this.ValidateRequiredArgument(family);
        await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.InsertTradeStrategyFamily)}", ReferenceDbSql.InsertTradeStrategyFamily)
            .SetParameters(new InsertTradeStrategyFamily("V1", family.TradeStrategyFamilyId, family.DefinitionVersion, family.SystemKey, family.Family.ToString(), family.Strategy.ToString(), family.TimeFrame.ToString(), family.Symbol, family.Currency, family.Description, family.State.ToString(), family.CreatedOnUtc, family.CreatedBy))
            .ExecuteCommandAsync(cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
    internal static ScheduledJobReadModel MapToScheduledJobJoined(NpgsqlDataReader reader)
    {
        var row = new ScheduledJobReadModel(reader.GetInt32(0), reader.GetString(1), Enum.Parse<JobScheduleType>(reader.GetString(2)), reader.GetDateTime(3), reader.GetDouble(4), reader.GetString(5), reader.GetBoolean(6), reader.GetDateTime(7), reader.GetString(8), reader.GetDateTime(9), reader.GetString(10));
        if (!reader.IsDBNull(11))
            row.DaysOfWeek = new ScheduledJobDaysOfWeekReadModel(row.JobId, reader.GetBoolean(12), reader.GetBoolean(13), reader.GetBoolean(14), reader.GetBoolean(15), reader.GetBoolean(16), reader.GetBoolean(17), reader.GetBoolean(18));
        return row;
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertInstrumentDefinitionAsync(Guid snapshot, long index, ExactInstrumentDefinition row, CancellationToken cancellationToken)
    {
        await this.Use("InstrumentDefinition.Insert", ReferenceDbSql.InsertInstrumentDefinition)
            .SetParameters(new ReferenceParameters([Uuid(snapshot), Text(row.Dataset), Integer((int)(index % 128)), Bigint(index), Integer((int)row.PublisherId), Bigint((long)row.InstrumentId), Text(row.RawSymbol), Text(row.Asset), Text(row.InstrumentClass), Text(row.Currency), Text(row.Exchange), Text(row.Json)]))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<InstrumentDefinitionSnapshot?> GetInstrumentDefinitionSnapshotAsync(CancellationToken cancellationToken) => (await this.Use("InstrumentDefinition.Snapshot", ReferenceDbSql.GetInstrumentDefinitionSnapshot)
            .ExecuteQueryAsync(MapToInstrumentDefinitionSnapshot, cancellationToken)
            .ConfigureAwait(false)).SingleOrDefault();
    /// <inheritdoc/>
    public async Task<TradeStrategySymbolReadModel[]> GetInstrumentDefinitionSymbolsAsync(Guid snapshot, TradeStrategyFamilyType family, CancellationToken cancellationToken)
    {
        var rows = await this.Use("InstrumentDefinition.Symbols", ReferenceDbSql.GetInstrumentDefinitionSymbols)
            .SetParameters(new ReferenceParameters([Uuid(snapshot), Integer((int)family)]))
            .ExecuteQueryAsync(row => new TradeStrategyProduct(family, row.GetString(1), row.GetString(2), row.GetString(3)).WithId(row.GetInt(0)), cancellationToken)
            .ConfigureAwait(false);
        return rows.ToArray();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<string> ReadInstrumentDefinitionJsonAsync(Guid snapshot, string dataset, int bucket, CancellationToken cancellationToken = default)
    {
        if (bucket is < 0 or >= 128)
            throw new ArgumentOutOfRangeException(nameof(bucket));
        return this.Use("InstrumentDefinition.ReadJson", ReferenceDbSql.ReadInstrumentDefinitionJson)
            .SetParameters(new ReferenceParameters([Uuid(snapshot), Text(dataset), Integer(bucket)]))
            .ExecuteStreamAsync(MapToString, cancellationToken);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertInstrumentDefinitionSelectionAsync(InstrumentDefinitionSelection value, CancellationToken cancellationToken)
    {
        var key = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{value.PublisherId:D5}:{value.InstrumentId:D10}");
        await this.Use("InstrumentDefinition.IndexSelection", ReferenceDbSql.InsertInstrumentDefinitionSelection)
            .SetParameters(new ReferenceParameters([Uuid(value.SnapshotId), Text(value.Dataset), Text(value.Root), Boolean(!string.Equals(value.InstrumentClass, "F", StringComparison.Ordinal)), Text(key), Bytea(MessagePackBinarySerializer.Shared.Serialize(value)!)]))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task InsertInstrumentDefinitionProductAsync(Guid snapshot, TradeStrategyFamilyType family, TradeStrategySymbolReadModel symbol, CancellationToken cancellationToken = default) => this.Use("InstrumentDefinition.Product", ReferenceDbSql.InsertInstrumentDefinitionProduct)
            .SetParameters(new ReferenceParameters(Values(Uuid(snapshot), Integer((int)family), Text(symbol.Symbol), Text(symbol.Exchange), Text(symbol.Currency), Integer(symbol.Id))))
            .ExecuteCommandAsync(cancellationToken);
    /// <inheritdoc/>
    public async Task PublishInstrumentDefinitionSnapshotAsync(InstrumentDefinitionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await this.Use("InstrumentDefinition.SelectionComplete", ReferenceDbSql.CompleteInstrumentDefinitionSelection)
            .SetParameters(new ReferenceParameters(Values(Uuid(snapshot.Id))))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        await this.Use("InstrumentDefinition.Publish", ReferenceDbSql.PublishInstrumentDefinitionSnapshot)
            .SetParameters(new ReferenceParameters(Values(Uuid(snapshot.Id), TimestampTz(snapshot.CompletedUtc), Bigint(snapshot.RecordCount), Text(JsonSerializer.Serialize(snapshot.Datasets)))))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> IsInstrumentDefinitionSelectionCompleteAsync(Guid snapshot, CancellationToken cancellationToken = default) => (await this.Use("InstrumentDefinition.SelectionStatus", ReferenceDbSql.GetInstrumentDefinitionSelectionStatus)
            .SetParameters(new ReferenceParameters(Values(Uuid(snapshot))))
            .ExecuteQueryAsync(MapToBoolean, cancellationToken)
            .ConfigureAwait(false)).SingleOrDefault();
    /// <inheritdoc/>
    public async Task<IReadOnlyList<(string Key, InstrumentDefinitionSelection Value)>> GetInstrumentDefinitionSelectionAsync(Guid snapshot, string dataset, string root, bool options, string after, int limit, CancellationToken cancellationToken = default) => (await this.Use("InstrumentDefinition.SelectionPage", ReferenceDbSql.GetInstrumentDefinitionSelectionPage)
            .SetParameters(new ReferenceParameters(Values(Uuid(snapshot), Text(dataset), Text(root), Boolean(options), Text(after), Integer(limit))))
            .ExecuteQueryAsync(MapToInstrumentSelection, cancellationToken)
            .ConfigureAwait(false)).ToArray();
    /// <inheritdoc/>
    public async Task<TradeStrategySymbolReadModel?> GetTradeStrategyProductAsync(TradeStrategyProduct product, CancellationToken cancellationToken = default) => (await this.Use("TradeStrategySymbol.ByKey", ReferenceDbSql.GetTradeStrategyProduct)
            .SetParameters(new ReferenceParameters(Values(Integer((int)product.Family), Text(product.Exchange), Text(product.Symbol), Text(product.Currency))))
            .ExecuteQueryAsync(row => MapToTradeStrategyProduct(product.Family, row), cancellationToken)
            .ConfigureAwait(false)).SingleOrDefault();
    /// <inheritdoc/>
    public async Task<TradeStrategySymbolReadModel?> GetTradeStrategySymbolAsync(TradeStrategyFamilyType family, int id, CancellationToken cancellationToken = default) => (await this.Use("TradeStrategySymbol.ById", ReferenceDbSql.GetTradeStrategySymbol)
            .SetParameters(new ReferenceParameters(Values(Integer((int)family), Integer(id))))
            .ExecuteQueryAsync(row => MapToTradeStrategyProduct(family, row), cancellationToken)
            .ConfigureAwait(false)).SingleOrDefault();
    /// <inheritdoc/>
    public Task InsertTradeStrategyProductAsync(TradeStrategyProduct product, int id, CancellationToken cancellationToken = default) => this.Use("TradeStrategySymbol.Insert", ReferenceDbSql.InsertTradeStrategyProduct)
            .SetParameters(new ReferenceParameters(Values(Integer((int)product.Family), Text(product.Exchange), Text(product.Symbol), Text(product.Currency), Integer(id))))
            .ExecuteCommandAsync(cancellationToken);
    /// <inheritdoc/>
    public async Task<(long Revision, string Payload)?> GetTradeStrategyFamilyCatalogAsync(CancellationToken cancellationToken = default) => (await this.Use("TradeStrategyFamilyCatalog.Read", ReferenceDbSql.GetTradeStrategyFamilyCatalog)
            .SetParameters(new ReferenceParameters(Values(Text("V1"))))
            .ExecuteQueryAsync(row => ((long Revision, string Payload)? )(row.GetLong(0), row.GetString(1)), cancellationToken)
            .ConfigureAwait(false)).SingleOrDefault();
    /// <inheritdoc/>
    public Task InitializeTradeStrategyFamilyCatalogAsync(long revision, string payload, CancellationToken cancellationToken = default) => this.Use("TradeStrategyFamilyCatalog.Initialize", ReferenceDbSql.InitializeTradeStrategyFamilyCatalog)
            .SetParameters(new ReferenceParameters(Values(Text("V1"), Bigint(revision), Text(payload))))
            .ExecuteCommandAsync(cancellationToken);
    /// <inheritdoc/>
    public Task UpdateTradeStrategyFamilyCatalogAsync(long revision, string payload, long expectedRevision, CancellationToken cancellationToken = default) => this.Use("TradeStrategyFamilyCatalog.CompareExchange", ReferenceDbSql.UpdateTradeStrategyFamilyCatalog)
            .SetParameters(new ReferenceParameters(Values(Bigint(revision), Text(payload), Text("V1"), Bigint(expectedRevision))))
            .ExecuteCommandAsync(cancellationToken);
    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
    internal static TradeStrategySymbolReadModel MapToTradeStrategyProduct(TradeStrategyFamilyType family, IObjectDataRecord row) => new TradeStrategyProduct(family, row.GetString(1), row.GetString(2), row.GetString(3)).WithId(row.GetInt(0));
    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
    internal static (string Key, InstrumentDefinitionSelection Value) MapToInstrumentSelection(IObjectDataRecord row) => (row.GetString(0), MessagePackBinarySerializer.Shared.Deserialize<InstrumentDefinitionSelection>(row.GetBytes(1))!);
    /// <summary>Maps a lookup from its native PostgreSQL ordinal columns.</summary>
    internal static LookupTypeReadModel MapToLookupReader(NpgsqlDataReader reader) => new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), reader.GetDateTime(4), reader.GetString(5));
    /// <summary>Maps the published instrument snapshot metadata.</summary>
    internal static InstrumentDefinitionSnapshot MapToInstrumentDefinitionSnapshot(IObjectDataRecord row) =>
        new(row.GetGuid(0),row.GetDateTime(1),row.GetLong(2),JsonSerializer.Deserialize<string[]>(row.GetString(3))!);
    /// <summary>Maps the catalog revision and preserved document payload.</summary>
    internal static (long Revision,string Payload)? MapToCatalogSnapshot(IObjectDataRecord row) => (row.GetLong(0),row.GetString(1));
}
