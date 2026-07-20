using Microsoft.Extensions.Logging;
using System.Data;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;

public class OptionPricerDbContext(
    IDbConnectionSettings connectionSettings,
    IDbContextFactory dbFactory,
    ISequenceIdGenerator sequenceIdGenerator,
    ILogger<DbProvider> logger) 
    : ObjectDataRepository<OptionPricerDbContext>(connectionSettings[ConnectionName], logger    ), IOptionPricerDbContext
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);
    readonly ISequenceIdGenerator _sequenceIdGenerator = IsArgumentNull.Set(sequenceIdGenerator);
    public const string ConnectionName = "OptionPricerDbConnection";

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override OptionPricerDbContext Database => this;

    /// <summary>
    /// initialize option pricer view model mappings
    /// </summary>
    /// <param name="model"></param>
    public override void OnCreateModel(DbModel<OptionPricerDbContext> model)
    {
    }

    static OptionPricerDeviceReadModel MapToOptionPricerDevice<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new (
            deviceId: e.GetInt(0),
            deviceName: e.GetString(1),
            spreadPaths: e.GetInt(2),
            volatilityPaths: e.GetInt(3),
            maxBatchSize: e.GetInt(4),
            optionType: e.GetEnum<OptionType>(5),
            enabled: e.GetBool(6)
        );

    static SpreadDistributionReadModel MapToSpreadDistribution<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            id: e.GetLong(0),
            tradeId: e.GetInt(1),
            valueDate: e.GetDateOnly(2),
            tradeType: e.GetEnum<TradeType>(3),
            tradeStatus: e.GetEnum<TradeStatus>(4),
            daysToExpiry: e.GetInt(5),
            forwardPrice: e.GetDouble(6),
            lossProbability: e.GetDouble(7),
            lossThreshold: e.GetDecimal(8),
            lossThresholdCount: e.GetInt(9),
            shortVolatility: e.GetDouble(10),
            longVolatility: e.GetDouble(11),
            forwardLossRatio: e.GetDouble(12),
            createdOn: e.GetDateTime(13)
        );

    static SpreadDistributionJobReadModel MapToSpreadDistributionJob<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            tradeType: e.GetEnum<TradeType>(2),
            tradeStatus: e.GetEnum<TradeStatus>(3),
            valueDate: e.GetDateOnly(4),
            daysToExpiry: e.GetInt(5),
            jobSubmitted: e.GetDateTime(6),
            jobStatus: e.GetEnum<SpreadDistributionJobStatus>(7),
            jobCompleted: e.GetDateTime(8),
            jobFailed: e.GetDateTime(9),
            inProgress: e.GetBool(10),
            lossProbabilityFactor: e.GetDouble(11)
        );

    static SpreadDistributionJobEntityId MapToSpreadDistributionJobId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            OrderId: e.GetInt(0),
            TradeId: e.GetInt(1),
            ValueDate: e.GetDateOnly(2)
        );

    /// <summary>
    /// return db reader/writer properties
    /// </summary>
    public IOptionPricerDbReadContext DbReader => this;
    public IOptionPricerDbWriteContext DbWriter => this;

    /// <summary>
    /// delete option pricer device
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task DeleteOptionPricerDeviceAsync(OptionPricerDeviceEntityId e)
        => await _dbFactory.OptionPricerDb
            .Use(OptionPricerDbCql.DeleteOptionPricerDevice)
            .SetParameters(new DeleteOptionPricerDevice(e.DeviceId, e.DeviceName))
            .ExecuteCommandAsync();

    /// <summary>
    /// delete spread distribution
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task DeleteSpreadDistributionAsync(int tradeId, DateOnly valueDate)
        => await _dbFactory.OptionPricerDb
            .Use(OptionPricerDbCql.DeleteSpreadDistribution)
            .SetParameters(new DeleteSpreadDistribution(tradeId, valueDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// delete all spread distribution jobs in progress
    /// </summary>
    /// <returns></returns>
    public async Task DeleteSpreadDistributionJobsInProgressAsync()
    {
        var db = _dbFactory.OptionPricerDb;
        var spreadDistributionJobIds = (await db
            .Use(OptionPricerDbCql.GetSpreadDistributionIJobIds)
            .ExecuteQueryAsync<SpreadDistributionJobEntityId>(MapToSpreadDistributionJobId!)).ToList();
        var spreadDistributionJobsToDelete = new List<SpreadDistributionJobEntityId>();
        foreach (var e in spreadDistributionJobIds)
        {
            spreadDistributionJobIds.AddRange([.. (await db.Use(OptionPricerDbCql.GetSpreadDistributionJobs)
                .SetParameters(new GetSpreadDistributionJobs(e.OrderId, e.TradeId))
                 .ExecuteQueryAsync<SpreadDistributionJobReadModel>(MapToSpreadDistributionJob!)).Where(e =>
                    e.JobStatus == SpreadDistributionJobStatus.InProgress
                    || e.InProgress).Select(e => new SpreadDistributionJobEntityId(e.OrderId, e.TradeId, e.ValueDate) )]);
        }
        foreach(var e in spreadDistributionJobsToDelete)
        {
            await db.Use(OptionPricerDbCql.DeleteSpreadDistributionJobs)
                .SetParameters(new DeleteSpreadDistributionJobs(e.OrderId, e.TradeId))
                .ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// delete all spread distribution jobs for selected trade
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task DeleteSpreadDistributionJobsAsync(int orderId, int tradeId)
         => await _dbFactory.OptionPricerDb
                .Use(OptionPricerDbCql.DeleteSpreadDistributionJobs)
                .SetParameters(new DeleteSpreadDistributionJobs(orderId, tradeId))
                .ExecuteCommandAsync();

    /// <summary>
    /// return all enabled option pricer devices
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<OptionPricerDeviceReadModel>> GetOptionPricerDevicesAsync()
        => await _dbFactory.OptionPricerDb
            .Use(OptionPricerDbCql.GetOptionPricerDevices)
            .ExecuteQueryAsync<OptionPricerDeviceReadModel>(MapToOptionPricerDevice!);

    /// <summary>
    /// return count of spread distribution jobs in progress
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<int> GetSpreadDistributionJobInProgressCountAsync(int orderId, int tradeId)
    {
        var db = _dbFactory.OptionPricerDb;
        var spreadDistributionJobs = await db.Use(OptionPricerDbCql.GetSpreadDistributionJobs)
            .SetParameters(new GetSpreadDistributionJobs(orderId, tradeId))
             .ExecuteQueryAsync<SpreadDistributionJobReadModel>(MapToSpreadDistributionJob!);
        return spreadDistributionJobs.Count(e => 
            e.JobStatus == SpreadDistributionJobStatus.InProgress
            || e.InProgress);
    }

    /// <summary>
    /// return spread distribution
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"
    /// <param name="tradeStatus"></param>
    /// <param name="valueDate"></param>
    /// <param name="daysToExpiry"></param>
    /// <returns></returns>
    public async Task<SpreadDistributionReadModel?> GetSpreadDistributionAsync(
        int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
        => await _dbFactory.OptionPricerDb
                .Use(OptionPricerDbCql.GetSpreadDistribution)
                .SetParameters(new GetSpreadDistribution(tradeId, tradeType.ToStringFast(), tradeStatus.ToStringFast(), valueDate, daysToExpiry))
                .ExecuteSingleAsync<SpreadDistributionReadModel>(MapToSpreadDistribution!);

    /// <summary>
    /// insert option pricer device configuration
    /// </summary>
    /// <returns></returns>
    public async Task InsertOptionPricerDeviceAsync(OptionPricerDeviceReadModel e)
        => await _dbFactory.OptionPricerDb
            .Use(OptionPricerDbCql.InsertIOptionPricerDevice)
            .SetParameters(new InsertOptionPricerDevice(e.DeviceId, e.DeviceName, e.SpreadPaths, e.VolatilityPaths, e.MaxBatchSize, e.OptionType.ToStringFast(), e.Enabled))
            .ExecuteCommandAsync();

    /// <summary>
    /// insert spread distribution
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertSpreadDistributionsAsync(SpreadDistributionReadModel ePut, SpreadDistributionReadModel eCall)
    {
        var putId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.SpreadDistribution_Id);
        var callId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.SpreadDistribution_Id);
        var queuedCommands = new List<object>();
        var db = _dbFactory.OptionPricerDb;
            queuedCommands.Add(db.Use(OptionPricerDbCql.InsertSpreadDistribution)
                .SetParameters(new InsertSpreadDistribution(putId, ePut.TradeId, ePut.TradeType.ToStringFast(), ePut.TradeStatus.ToStringFast(), ePut.ValueDate, ePut.DaysToExpiry, ePut.ForwardPrice, ePut.LossProbability, ePut.ShortVolatility, ePut.LongVolatility, ePut.LossThreshold, ePut.LossThresholdCount, ePut.ForwardLossRatio, ePut.CreatedOn))
                .QueueCommand());

        // insert call spread distribution...
        queuedCommands.Add(
            db.Use(OptionPricerDbCql.InsertSpreadDistribution)
               .SetParameters(new InsertSpreadDistribution(callId, eCall.TradeId, eCall.TradeType.ToStringFast(), eCall.TradeStatus.ToStringFast(), eCall.ValueDate, eCall.DaysToExpiry, eCall.ForwardPrice, eCall.LossProbability, eCall.ShortVolatility, eCall.LongVolatility, eCall.LossThreshold, eCall.LossThresholdCount, eCall.ForwardLossRatio, eCall.CreatedOn))
               .QueueCommand());

        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// insert spread distribution job
    /// </summary>
    /// <param name="e">spread distribution job</param>
    /// <returns></returns>
    public async Task InsertSpreadDistributionJobAsync(SpreadDistributionJobReadModel e)
        => await _dbFactory.OptionPricerDb
                    .Use(OptionPricerDbCql.InsertSpreadDistributionJob)
                    .SetParameters(new InsertSpreadDistributionJob(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.TradeStatus.ToStringFast(), e.ValueDate, e.DaysToExpiry, e.JobSubmitted, e.JobStatus.ToStringFast(), e.JobCompleted, e.JobFailed, e.JobStatus == SpreadDistributionJobStatus.InProgress, e.LossProbabilityFactor))
                 .ExecuteCommandAsync();

    /// <summary>
    /// update spread distribution job when completed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="jobId"></param>
    /// <param name="jobStatus">job status</param>
    /// <param name="jobCompleted">job completion date</param>
    /// <param name="jobFailed"></param>
    /// <returns></returns>
    public async Task UpdateSpreadDistributionJobStatusAsync(int orderId, int tradeId, SpreadDistributionJobStatus jobStatus, DateTime jobCompleted, DateTime? jobFailed)
        => await _dbFactory.OptionPricerDb
            .Use(OptionPricerDbCql.UpdateSreadDistributionJobStatus)
            .SetParameters(new UpdateSpreadDistributionJobStatus(orderId, tradeId, jobStatus.ToStringFast(), jobCompleted, jobFailed, jobStatus == SpreadDistributionJobStatus.InProgress))
            .ExecuteCommandAsync();

}
