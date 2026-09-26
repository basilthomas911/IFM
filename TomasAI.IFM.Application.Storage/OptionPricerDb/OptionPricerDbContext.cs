using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

/// <summary>
/// Provides read and write persistence operations for option-pricer data.
/// </summary>
/// <param name="connectionSettings">The named database connection settings.</param>
/// <param name="dbFactory">The factory used to resolve database contexts.</param>
/// <param name="sequenceIdGenerator">The generator used to allocate spread-distribution identifiers.</param>
/// <param name="logger">The database-provider logger.</param>
public class OptionPricerDbContext(
    IDbConnectionSettings connectionSettings,
    IDbContextFactory dbFactory,
    ISequenceIdGenerator sequenceIdGenerator,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<OptionPricerDbContext>(connectionSettings[ConnectionName], logger),
      IOptionPricerDbContext
{
    private readonly IDbContextFactory _dbFactory = dbFactory;
    private readonly ISequenceIdGenerator _sequenceIdGenerator = sequenceIdGenerator;

    /// <summary>Gets the option-pricer database connection-setting name.</summary>
    public const string ConnectionName = "OptionPricerDbConnection";

    /// <summary>Gets the concrete option-pricer database context.</summary>
    public override OptionPricerDbContext Database => this;

    /// <summary>Gets the option-pricer read capability.</summary>
    public IOptionPricerDbReadContext DbReader => this;

    /// <summary>Gets the option-pricer write capability.</summary>
    public IOptionPricerDbWriteContext DbWriter => this;

    internal static OptionPricerDeviceReadModel MapToOptionPricerDevice<TDataRecord>(TDataRecord record)
        where TDataRecord : IObjectDataRecord
        => new(
            deviceId: record.GetInt(0),
            deviceName: record.GetString(1),
            spreadPaths: record.GetInt(2),
            volatilityPaths: record.GetInt(3),
            maxBatchSize: record.GetInt(4),
            optionType: record.GetEnum<OptionType>(5),
            enabled: record.GetBool(6));

    internal static SpreadDistributionReadModel MapToSpreadDistribution<TDataRecord>(TDataRecord record)
        where TDataRecord : IObjectDataRecord
        => new(
            id: record.GetLong(0),
            tradeId: record.GetInt(1),
            valueDate: record.GetDateOnly(2),
            tradeType: record.GetEnum<TradeType>(3),
            tradeStatus: record.GetEnum<TradeStatus>(4),
            daysToExpiry: record.GetInt(5),
            forwardPrice: record.GetDouble(6),
            lossProbability: record.GetDouble(7),
            lossThreshold: record.GetDecimal(8),
            lossThresholdCount: record.GetInt(9),
            shortVolatility: record.GetDouble(10),
            longVolatility: record.GetDouble(11),
            forwardLossRatio: record.GetDouble(12),
            createdOn: record.GetDateTime(13));

    internal static SpreadDistributionJobReadModel MapToSpreadDistributionJob<TDataRecord>(TDataRecord record)
        where TDataRecord : IObjectDataRecord
        => new(
            orderId: record.GetInt(0),
            tradeId: record.GetInt(1),
            tradeType: record.GetEnum<TradeType>(2),
            tradeStatus: record.GetEnum<TradeStatus>(3),
            valueDate: record.GetDateOnly(4),
            daysToExpiry: record.GetInt(5),
            jobSubmitted: record.GetDateTime(6),
            jobStatus: record.GetEnum<SpreadDistributionJobStatus>(7),
            jobCompleted: record.GetDateTime(8),
            jobFailed: record.GetDateTime(9),
            inProgress: record.GetBool(10),
            lossProbabilityFactor: record.GetDouble(11));

    /// <summary>Deletes an option-pricer device.</summary>
    /// <param name="deviceId">The device identity to delete.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    public async Task DeleteOptionPricerDeviceAsync(OptionPricerDeviceEntityId deviceId)
    {
        var parameters = new DeleteOptionPricerDevice(deviceId.DeviceId, deviceId.DeviceName);
        await _dbFactory.OptionPricerDb
            .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.DeleteOptionPricerDevice)}", OptionPricerDbCql.DeleteOptionPricerDevice)
            .SetParameters(parameters)
            .ExecuteCommandAsync();
    }

    /// <summary>Deletes spread distributions for a trade and value date.</summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="valueDate">The distribution value date.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    public async Task DeleteSpreadDistributionAsync(int tradeId, DateOnly valueDate)
    {
        var parameters = new DeleteSpreadDistribution(tradeId, valueDate);
        await _dbFactory.OptionPricerDb
            .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.DeleteSpreadDistribution)}", OptionPricerDbCql.DeleteSpreadDistribution)
            .SetParameters(parameters)
            .ExecuteCommandAsync();
    }

    /// <summary>Deletes every spread-distribution job currently marked as in progress.</summary>
    /// <returns>A task representing the asynchronous cleanup operation.</returns>
    public async Task DeleteSpreadDistributionJobsInProgressAsync()
    {
        var db = _dbFactory.OptionPricerDb;
        var jobIds = await this.GetSpreadDistributionJobsInProgressAsync();
        foreach (var jobId in jobIds)
        {
            var parameters = new DeleteSpreadDistributionJob(jobId.OrderId, jobId.TradeId, jobId.ValueDate);
            await db
                .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.DeleteSpreadDistributionJob)}", OptionPricerDbCql.DeleteSpreadDistributionJob)
                .SetParameters(parameters)
                .ExecuteCommandAsync();
        }
    }

    /// <summary>Deletes all spread-distribution jobs for an order and trade.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    public async Task DeleteSpreadDistributionJobsAsync(int orderId, int tradeId)
    {
        var parameters = new DeleteSpreadDistributionJobs(orderId, tradeId);
        await _dbFactory.OptionPricerDb
            .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.DeleteSpreadDistributionJobs)}", OptionPricerDbCql.DeleteSpreadDistributionJobs)
            .SetParameters(parameters)
            .ExecuteCommandAsync();
    }

    /// <summary>Gets all configured option-pricer devices.</summary>
    /// <returns>The configured option-pricer devices.</returns>
    public Task<ICollection<OptionPricerDeviceReadModel>> GetOptionPricerDevicesAsync()
        => _dbFactory.OptionPricerDb
            .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.GetOptionPricerDevices)}", OptionPricerDbCql.GetOptionPricerDevices)
            .ExecuteQueryAsync<OptionPricerDeviceReadModel>(MapToOptionPricerDevice);

    /// <summary>Gets all configured option-pricer devices.</summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The configured option-pricer devices.</returns>
    public Task<ICollection<OptionPricerDeviceReadModel>> GetOptionPricerDevicesAsync(CancellationToken cancellationToken)
        => _dbFactory.OptionPricerDb
            .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.GetOptionPricerDevices)}", OptionPricerDbCql.GetOptionPricerDevices)
            .ExecuteQueryAsync<OptionPricerDeviceReadModel>(MapToOptionPricerDevice, cancellationToken);

    /// <summary>Gets the number of in-progress spread-distribution jobs for an order and trade.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>The number of matching in-progress jobs.</returns>
    public Task<int> GetSpreadDistributionJobInProgressCountAsync(int orderId, int tradeId)
        => GetSpreadDistributionJobInProgressCountAsync(orderId, tradeId, CancellationToken.None);

    /// <summary>Gets the number of in-progress spread-distribution jobs for an order and trade.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The number of matching in-progress jobs.</returns>
    public async Task<int> GetSpreadDistributionJobInProgressCountAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
    {
        var parameters = new GetSpreadDistributionJobs(orderId, tradeId);
        var jobs = await _dbFactory.OptionPricerDb
            .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.GetSpreadDistributionJobs)}", OptionPricerDbCql.GetSpreadDistributionJobs)
            .SetParameters(parameters)
            .ExecuteQueryAsync<SpreadDistributionJobReadModel>(MapToSpreadDistributionJob, cancellationToken);
        return jobs.Count(job => job.JobStatus == SpreadDistributionJobStatus.InProgress || job.InProgress);
    }

    /// <summary>Gets a spread distribution matching the supplied trade dimensions.</summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="tradeType">The trade type.</param>
    /// <param name="tradeStatus">The trade status.</param>
    /// <param name="valueDate">The distribution value date.</param>
    /// <param name="daysToExpiry">The number of days to expiry.</param>
    /// <returns>The matching spread distribution, or <see langword="null"/> when none exists.</returns>
    public Task<SpreadDistributionReadModel?> GetSpreadDistributionAsync(
        int tradeId,
        TradeType tradeType,
        TradeStatus tradeStatus,
        DateOnly valueDate,
        int daysToExpiry)
        => GetSpreadDistributionAsync(tradeId, tradeType, tradeStatus, valueDate, daysToExpiry, CancellationToken.None);

    /// <summary>Gets a spread distribution matching the supplied trade dimensions.</summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="tradeType">The trade type.</param>
    /// <param name="tradeStatus">The trade status.</param>
    /// <param name="valueDate">The distribution value date.</param>
    /// <param name="daysToExpiry">The number of days to expiry.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The matching spread distribution, or <see langword="null"/> when none exists.</returns>
    public Task<SpreadDistributionReadModel?> GetSpreadDistributionAsync(
        int tradeId,
        TradeType tradeType,
        TradeStatus tradeStatus,
        DateOnly valueDate,
        int daysToExpiry,
        CancellationToken cancellationToken)
    {
        var parameters = new GetSpreadDistribution(
            tradeId,
            tradeType.ToStringFast(),
            tradeStatus.ToStringFast(),
            valueDate,
            daysToExpiry);
        return _dbFactory.OptionPricerDb
            .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.GetSpreadDistribution)}", OptionPricerDbCql.GetSpreadDistribution)
            .SetParameters(parameters)
            .ExecuteSingleAsync<SpreadDistributionReadModel>(MapToSpreadDistribution, cancellationToken);
    }

    /// <summary>Inserts an option-pricer device configuration.</summary>
    /// <param name="device">The device configuration to insert.</param>
    /// <returns>A task representing the asynchronous insert operation.</returns>
    public async Task InsertOptionPricerDeviceAsync(OptionPricerDeviceReadModel device)
    {
        var parameters = new InsertOptionPricerDevice(
            device.DeviceId,
            device.DeviceName,
            device.SpreadPaths,
            device.VolatilityPaths,
            device.MaxBatchSize,
            device.OptionType.ToStringFast(),
            device.Enabled);
        await _dbFactory.OptionPricerDb
            .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.InsertOptionPricerDevice)}", OptionPricerDbCql.InsertOptionPricerDevice)
            .SetParameters(parameters)
            .ExecuteCommandAsync();
    }

    /// <summary>Inserts the put-side and call-side spread distributions as one queued operation.</summary>
    /// <param name="putSpreadDistribution">The put-side spread distribution.</param>
    /// <param name="callSpreadDistribution">The call-side spread distribution.</param>
    /// <returns>A task representing the asynchronous insert operation.</returns>
    public async Task InsertSpreadDistributionsAsync(
        SpreadDistributionReadModel putSpreadDistribution,
        SpreadDistributionReadModel callSpreadDistribution)
    {
        var putId = putSpreadDistribution.Id != 0
            ? putSpreadDistribution.Id
            : await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.SpreadDistribution_Id);
        var callId = callSpreadDistribution.Id != 0
            ? callSpreadDistribution.Id
            : await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.SpreadDistribution_Id);
        var putParameters = putSpreadDistribution.ToInsertSpreadDistribution(putId);
        var callParameters = callSpreadDistribution.ToInsertSpreadDistribution(callId);
        var db = _dbFactory.OptionPricerDb;
        var putCommand = db
            .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.InsertSpreadDistribution)}", OptionPricerDbCql.InsertSpreadDistribution)
            .SetParameters(putParameters)
            .QueueCommand();
        var callCommand = db
            .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.InsertSpreadDistribution)}", OptionPricerDbCql.InsertSpreadDistribution)
            .SetParameters(callParameters)
            .QueueCommand();
        await db.ExecuteQueuedCommandsAsync([putCommand, callCommand]);
    }

    /// <summary>Inserts a spread-distribution job.</summary>
    /// <param name="job">The spread-distribution job to insert.</param>
    /// <returns>A task representing the asynchronous insert operation.</returns>
    public async Task InsertSpreadDistributionJobAsync(SpreadDistributionJobReadModel job)
    {
        var parameters = job.ToInsertSpreadDistributionJob();
        await _dbFactory.OptionPricerDb
            .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.InsertSpreadDistributionJob)}", OptionPricerDbCql.InsertSpreadDistributionJob)
            .SetParameters(parameters)
            .ExecuteCommandAsync();
    }

    /// <summary>Updates the persisted status of a spread-distribution job.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="valueDate">The job value date.</param>
    /// <param name="jobStatus">The new job status.</param>
    /// <param name="jobCompleted">The job completion timestamp.</param>
    /// <param name="jobFailed">The optional job failure timestamp.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    public async Task UpdateSpreadDistributionJobStatusAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        SpreadDistributionJobStatus jobStatus,
        DateTime jobCompleted,
        DateTime? jobFailed)
    {
        var parameters = new UpdateSpreadDistributionJobStatus(
            orderId,
            tradeId,
            valueDate,
            jobStatus.ToStringFast(),
            jobCompleted,
            jobFailed,
            jobStatus == SpreadDistributionJobStatus.InProgress);
        await _dbFactory.OptionPricerDb
            .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.UpdateSpreadDistributionJobStatus)}", OptionPricerDbCql.UpdateSpreadDistributionJobStatus)
            .SetParameters(parameters)
            .ExecuteCommandAsync();
    }
}
