using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.MarketData.Definitions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using System.Text.Json;
using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Microsoft.Extensions.Logging;

using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

using TomasAI.IFM.Shared.JobScheduler;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

using TomasAI.IFM.Shared.Storage;


using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
/// <summary>Provides typed PostgreSQL Reference queries and mutations. Domain services own publication and catalog policy.</summary>
/// <param name="connectionSettings">The named PostgreSQL connection.</param>
/// <param name="dbFactory">Retained for constructor compatibility with repository registration.</param>
/// <param name="sequenceIdGenerator">The source of the existing seed high-water read model.</param>
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
    public async Task<int> GetCurrentSeedIdAsync(string seedType) => checked((int)await _sequenceIdGenerator.GetHighWatermarkAsync(SequenceNameExtensions.ParseSequenceName(seedType))
            .ConfigureAwait(false));
    /// <inheritdoc/>
    public async Task<int> GetCurrentSeedIdAsync(string seedType, CancellationToken cancellationToken) => checked((int)await _sequenceIdGenerator.GetHighWatermarkAsync(SequenceNameExtensions.ParseSequenceName(seedType), cancellationToken)
            .ConfigureAwait(false));
    /// <inheritdoc/>
    public async Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId)
    {
        var preparedParameters1 = new GetLookupTypeById(lookupTypeId.LookupTypeName, lookupTypeId.OrderId);
        return await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetLookupTypeById)}", ReferenceDbSql.GetLookupTypeById)
            .SetParameters(preparedParameters1)
            .ExecuteSingleAsync(MapToLookupType!))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId, CancellationToken cancellationToken)
    {
        var preparedParameters2 = new GetLookupTypeById(lookupTypeId.LookupTypeName, lookupTypeId.OrderId);
        return await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.GetLookupTypeById)}", ReferenceDbSql.GetLookupTypeById)
            .SetParameters(preparedParameters2)
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
        await this.LookupTransaction().ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.InsertScheduledJobAsync(e, token)
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
        var preparedParameters3 = new InsertMDIForwardLossRatio(e.MDI, e.TrendDirection.ToStringFast(), e.TradeType.ToStringFast(), e.ForwardLossRatio, e.CreatedBy, e.CreatedOn, e.UpdatedBy, e.UpdatedOn);
        await this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.InsertMDIForwardLossRatio)}", ReferenceDbSql.InsertMDIForwardLossRatio)
            .SetParameters(preparedParameters3)
            .ExecuteCommandAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task InsertMDIForwardLossRatiosAsync(ICollection<MDIForwardLossRatioReadModel> mdiForwardLossRatios)
    {
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
        var queuedCommands = new List<object>();
        var db = this;
        var preparedParameters4 = new DeleteMDIForwardLossRatio(mdiForwardLossRatio.TrendDirection.ToStringFast(), mdiForwardLossRatio.TradeType.ToStringFast());
        // Delete the existing record
        queuedCommands.Add(db.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.DeleteMDIForwardLossRatio)}", ReferenceDbSql.DeleteMDIForwardLossRatio)
            .SetParameters(preparedParameters4)
            .QueueCommand());
        var preparedParameters5 = new InsertMDIForwardLossRatio(mdiForwardLossRatio.MDI, mdiForwardLossRatio.TrendDirection.ToStringFast(), mdiForwardLossRatio.TradeType.ToStringFast(), mdiForwardLossRatio.ForwardLossRatio, mdiForwardLossRatio.CreatedBy, mdiForwardLossRatio.CreatedOn, mdiForwardLossRatio.UpdatedBy, mdiForwardLossRatio.UpdatedOn);
        // Insert the updated record
        queuedCommands.Add(db.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.InsertMDIForwardLossRatio)}", ReferenceDbSql.InsertMDIForwardLossRatio)
            .SetParameters(preparedParameters5)
            .QueueCommand());
        await (db.ExecuteQueuedCommandsAsync(queuedCommands))
            .ConfigureAwait(false);
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
        var preparedParameters6 = new InsertTradeStrategyFamily("V1", family.TradeStrategyFamilyId, family.DefinitionVersion, family.SystemKey, family.Family.ToString(), family.Strategy.ToString(), family.TimeFrame.ToString(), family.Symbol, family.Currency, family.Description, family.State.ToString(), family.CreatedOnUtc, family.CreatedBy);
        await (this.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.InsertTradeStrategyFamily)}", ReferenceDbSql.InsertTradeStrategyFamily)
            .SetParameters(preparedParameters6)
            .ExecuteCommandAsync(cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>Maps persisted Reference columns in query ordinal order.</summary>
    internal static ScheduledJobReadModel MapToScheduledJobJoined(System.Data.Common.DbDataReader reader)
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
        var preparedParameters7 = new ReferenceParameters([Uuid(snapshot), Text(row.Dataset), Integer((int)(index % 128)), Bigint(index), Integer((int)row.PublisherId), Bigint((long)row.InstrumentId), Text(row.RawSymbol), Text(row.Asset), Text(row.InstrumentClass), Text(row.Currency), Text(row.Exchange), Text(row.Json)]);
        await this.Use("InstrumentDefinition.Insert", ReferenceDbSql.InsertInstrumentDefinition)
            .SetParameters(preparedParameters7)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    static readonly System.Collections.Concurrent.ConcurrentDictionary<int,string> DefinitionBatchSql = new();
    /// <inheritdoc/>
    public async Task InsertInstrumentDefinitionsAsync(Guid snapshot, long firstIndex, IReadOnlyList<ExactInstrumentDefinition> rows, CancellationToken cancellationToken)
    {
        var sql = DefinitionBatchSql.GetOrAdd(rows.Count, static count => {
            var template = ReferenceDbSql.InsertInstrumentDefinition;
            var start = template.IndexOf("VALUES", StringComparison.Ordinal);
            var end = template.IndexOf("ON CONFLICT", StringComparison.Ordinal);
            return template[..start] + "VALUES " + string.Join(",", Enumerable.Range(0,count).Select(row => "(" + string.Join(",", Enumerable.Range(row*12+1,12).Select(index => "$"+index.ToString(System.Globalization.CultureInfo.InvariantCulture))) + ")")) + " " + template[end..];
        });
        var parameters = new object?[rows.Count*12];
        for (var offset = 0; offset < rows.Count; offset++) {
            var row = rows[offset];
            var index = checked(firstIndex + offset);
            object?[] values = [Uuid(snapshot), Text(row.Dataset), Integer((int)(index%128)), Bigint(index), Integer(row.PublisherId), Bigint(row.InstrumentId), Text(row.RawSymbol), Text(row.Asset), Text(row.InstrumentClass), Text(row.Currency), Text(row.Exchange), Text(row.Json)];
            values.CopyTo(parameters, offset*12);
        }
        var preparedParameters8 = new ReferenceParameters(parameters);
        await this.Use("InstrumentDefinition.InsertBatch", sql)
            .SetParameters(preparedParameters8)
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
        var preparedParameters9 = new ReferenceParameters([Uuid(snapshot), Integer((int)family)]);
        var rows = await this.Use("InstrumentDefinition.Symbols", ReferenceDbSql.GetInstrumentDefinitionSymbols)
            .SetParameters(preparedParameters9)
            .ExecuteQueryAsync((row) => MapToGetInstrumentDefinitionSymbolsRow1(row, family), cancellationToken)
            .ConfigureAwait(false);
        return rows.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string,string>> GetContractDescriptionsAsync(string exchange, string symbol, CancellationToken cancellationToken = default)
    {
        var preparedParameters10 = new ReferenceParameters([Text(exchange), Text(symbol)]);
        var rows = await this.Use("InstrumentDefinition.ContractDescriptions", ReferenceDbSql.GetContractDescriptions)
            .SetParameters(preparedParameters10)
            .ExecuteQueryAsync(MapToGetContractDescriptionsRow2, cancellationToken)
            .ConfigureAwait(false);
        return rows.ToDictionary(row => row.Contract, row => row.Description, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<string> ReadInstrumentDefinitionJsonAsync(Guid snapshot, string dataset, int bucket, CancellationToken cancellationToken = default)
    {
        var preparedParameters11 = new ReferenceParameters([Uuid(snapshot), Text(dataset), Integer(bucket)]);
        return this.Use("InstrumentDefinition.ReadJson", ReferenceDbSql.ReadInstrumentDefinitionJson)
            .SetParameters(preparedParameters11)
            .ExecuteStreamAsync(MapToString, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string?> GetInstrumentDefinitionJsonAsync(Guid snapshot, string dataset, int bucket, long index, CancellationToken cancellationToken = default)
    {
        var preparedParameters12 = new ReferenceParameters([Uuid(snapshot), Text(dataset), Integer(bucket), Bigint(index)]);
        var rows = await this.Use("InstrumentDefinition.GetJson", ReferenceDbSql.GetInstrumentDefinitionJson)
            .SetParameters(preparedParameters12)
            .ExecuteQueryAsync(MapToString, cancellationToken)
            .ConfigureAwait(false);
        return rows.SingleOrDefault();
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertInstrumentDefinitionSelectionAsync(InstrumentDefinitionSelection value, CancellationToken cancellationToken)
    {
        var key = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{value.PublisherId:D5}:{value.InstrumentId:D10}");
        var preparedParameters13 = new ReferenceParameters([Uuid(value.SnapshotId), Text(value.Dataset), Text(value.Root), Boolean(!string.Equals(value.InstrumentClass, "F", StringComparison.Ordinal)), Text(key), Bytea(MessagePackBinarySerializer.Shared.Serialize(value)!)]);
        await this.Use("InstrumentDefinition.IndexSelection", ReferenceDbSql.InsertInstrumentDefinitionSelection)
            .SetParameters(preparedParameters13)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task InsertInstrumentDefinitionProductAsync(Guid snapshot, TradeStrategyFamilyType family, TradeStrategySymbolReadModel symbol, CancellationToken cancellationToken = default) => this.Use("InstrumentDefinition.Product", ReferenceDbSql.InsertInstrumentDefinitionProduct)
            .SetParameters(new ReferenceParameters(Values(Uuid(snapshot), Integer((int)family), Text(symbol.Symbol), Text(symbol.Exchange), Text(symbol.Currency), Integer(symbol.Id))))
            .ExecuteCommandAsync(cancellationToken);
    /// <inheritdoc/>
    public Task LockInstrumentCatalogAsync(IEnlistedPostgresTransaction transaction, string stream, CancellationToken token)
        => transaction.ExecuteAsync(ReferenceDbSql.LockInstrumentCatalog, (object?[])new InstrumentCatalogStream(stream).Bind(), token);

    /// <inheritdoc/>
    public async Task<long> ReadInstrumentCatalogRevisionAsync(IEnlistedPostgresTransaction transaction, string stream, CancellationToken token)
        => (await transaction.ScalarAsync(ReferenceDbSql.ReadInstrumentCatalogRevision, (object? [])new InstrumentCatalogStream(stream).Bind(), token)
            .ConfigureAwait(false)) is long version ? version : 0;

    /// <inheritdoc/>
    public async Task<EventStreamReadModel?> ReadInstrumentCatalogEventAsync(IEnlistedPostgresTransaction transaction, string stream, long revision, CancellationToken token)
        => (await transaction.QueryAsync(ReferenceDbSql.ReadInstrumentCatalogEvent, (object? [])new InstrumentCatalogEventSelection(stream, revision).Bind(), MapToInstrumentCatalogEvent, token)
            .ConfigureAwait(false)).SingleOrDefault();

    internal static EventStreamReadModel MapToInstrumentCatalogEvent(System.Data.Common.DbDataReader row)
        => new() { EventTypeName = row.GetString(0), EventVersion = row.GetInt64(1), EventData = row.GetFieldValue<byte[]>(2) };

    /// <inheritdoc/>
    public async Task<DateTime?> ReadPreviousInstrumentCompletionAsync(IEnlistedPostgresTransaction transaction, CancellationToken token)
        => await transaction.ScalarAsync(ReferenceDbSql.ReadPreviousInstrumentCompletion, [], token)
            .ConfigureAwait(false) as DateTime?;

    /// <inheritdoc/>
    public async Task<bool> HasInstrumentCatalogReceiptAsync(IEnlistedPostgresTransaction transaction, string stream, Guid commandId, CancellationToken token)
        => await transaction.ScalarAsync(ReferenceDbSql.HasInstrumentCatalogReceipt, (object? [])new InstrumentCatalogReceiptSelection(stream, commandId).Bind(), token)
            .ConfigureAwait(false) is true;

    /// <inheritdoc/>
    public async Task<bool> HasInstrumentDefinitionSelectionAsync(IEnlistedPostgresTransaction transaction, Guid snapshot, CancellationToken token)
        => await transaction.ScalarAsync(ReferenceDbSql.HasInstrumentDefinitionSelection, (object? [])new InstrumentCatalogSnapshotSelection(snapshot).Bind(), token)
            .ConfigureAwait(false) is true;

    /// <inheritdoc/>
    public async Task WriteInstrumentCatalogPointerAsync(IEnlistedPostgresTransaction transaction, InstrumentDefinitionSnapshot snapshot, CancellationToken token)
    {
        await transaction.ExecuteAsync(ReferenceDbSql.CompleteInstrumentDefinitionSelection,
            (object?[])new InstrumentCatalogSnapshotSelection(snapshot.Id).Bind(), token)
            .ConfigureAwait(false);
        var values = new InstrumentCatalogPointer(snapshot.Id, snapshot.CompletedUtc, snapshot.RecordCount, JsonSerializer.Serialize(snapshot.Datasets));
        await transaction.ExecuteAsync(ReferenceDbSql.PublishInstrumentDefinitionSnapshot, (object?[])values.Bind(), token)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> IsInstrumentDefinitionSelectionCompleteAsync(Guid snapshot, CancellationToken cancellationToken = default) => (await this.Use("InstrumentDefinition.SelectionStatus", ReferenceDbSql.GetInstrumentDefinitionSelectionStatus)
            .SetParameters(new ReferenceParameters(Values(Uuid(snapshot))))
            .ExecuteQueryAsync(MapToBoolean, cancellationToken)
            .ConfigureAwait(false)).SingleOrDefault();
    /// <inheritdoc/>
    public async Task<IReadOnlyList<InstrumentDefinitionSelectionRow>> GetInstrumentDefinitionSelectionAsync(Guid snapshot, string dataset, string root, bool options, string after, int limit, CancellationToken cancellationToken = default) => (await this.Use("InstrumentDefinition.SelectionPage", ReferenceDbSql.GetInstrumentDefinitionSelectionPage)
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
    public async Task<TradeStrategyFamilyCatalogReadModel?> GetTradeStrategyFamilyCatalogAsync(CancellationToken cancellationToken = default) => (await this.Use("TradeStrategyFamilyCatalog.Read", ReferenceDbSql.GetTradeStrategyFamilyCatalog)
            .SetParameters(new ReferenceParameters(Values(Text("V1"))))
            .ExecuteQueryAsync(MapToStrategyFamilyCatalog, cancellationToken)
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
    internal static InstrumentDefinitionSelectionRow MapToInstrumentSelection(IObjectDataRecord row) => new(row.GetString(0), MessagePackBinarySerializer.Shared.Deserialize<InstrumentDefinitionSelection>(row.GetBytes(1))!);
    /// <summary>Maps a lookup from its native PostgreSQL ordinal columns.</summary>
    internal static LookupTypeReadModel MapToLookupReader(System.Data.Common.DbDataReader reader) => new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), reader.GetDateTime(4), reader.GetString(5));
    /// <summary>Maps the published instrument snapshot metadata.</summary>
    internal static InstrumentDefinitionSnapshot MapToInstrumentDefinitionSnapshot(IObjectDataRecord row) =>
        new(row.GetGuid(0),row.GetDateTime(1),row.GetLong(2),JsonSerializer.Deserialize<string[]>(row.GetString(3))!);
    /// <summary>Maps the catalog revision and preserved document payload.</summary>
    internal static (long Revision,string Payload)? MapToCatalogSnapshot(IObjectDataRecord row) => (row.GetLong(0),row.GetString(1));
    private static TradeStrategyFamilyCatalogReadModel? MapToStrategyFamilyCatalog(IObjectDataRecord row) =>
        new(row.GetLong(0), row.GetString(1));

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static global::TomasAI.IFM.Domain.MarketData.Shared.ViewModels.TradeStrategySymbolReadModel MapToGetInstrumentDefinitionSymbolsRow1(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row, global::TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyType family) {
                var product = new TradeStrategyProduct(family, row.GetString(1), row.GetString(2), row.GetString(3)).WithId(row.GetInt(0));
                return string.IsNullOrWhiteSpace(row.GetString(4)) ? product : product with { Description = row.GetString(4) };
            }

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static (string Contract, string Description) MapToGetContractDescriptionsRow2(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row) => (Contract: row.GetString(0), Description: row.GetString(1));
}
