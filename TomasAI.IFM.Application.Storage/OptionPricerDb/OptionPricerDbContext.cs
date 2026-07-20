using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Data;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.SystemAdmin;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb
{
    /// <summary>
    /// option pricer database
    /// </summary>
    public class OptionPricerDbContext : ObjectDataRepository<OptionPricerDbContext>, IOptionPricerDbContext, IOptionPricerDbReadContext, IOptionPricerDbWriteContext
    {
        public const string ConnectionName = "OptionPricerDbConnection";
        readonly IDbContextFactory _dbFactory;

        /// <summary>
        /// option pricer database constructor
        /// </summary>
        /// <param name="connectionSettings"></param>
        /// <param name="dbFactory"></param>
        public OptionPricerDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<OptionPricerDbContext> logger) 
            :base(connectionSettings[ConnectionName], logger    )
        {
            _dbFactory = dbFactory;
        }

        /// <summary>
        /// initialize option pricer view model mappings
        /// </summary>
        /// <param name="model"></param>
        public override void OnCreateModel(DbModel<OptionPricerDbContext> model)
        {
            OptionPricerDevice = model.Map(e => e.OptionPricerDevice)
                .Parameters(e =>
                    e.Set(o => o.DeviceId)
                        .Set(o => o.DeviceName)
                        .Set(o => o.SpreadPaths)
                        .Set(o => o.VolatilityPaths)
                        .Set(o => o.MaxBatchSize)
                        .Set(o => o.OptionType, o => o.AsEnum<OptionType>())
                        .Set(o => o.Enabled)
                );

            SpreadDistribution = model.Map(e => e.SpreadDistribution)
                .Parameters(e =>
                    e.Set(o => o.Id)
                     .Set(o => o.TradeId)
                     .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                     .Set(o => o.TradeStatus, o => o.AsEnum<TradeStatus>())
                     //.Set(o => o.ValueDate)
                     .Set(o => o.DaysToExpiry)
                     .Set(o => o.ShortVolatility)
                     .Set(o => o.LongVolatility)
                     .Set(o => o.ForwardPrice)
                     .Set(o => o.LossProbability)
                     .Set(o => o.LossThreshold)
                     .Set(o => o.LossThresholdCount)
                     .Set(o => o.CreatedOn)
                );

            SpreadDistributionPaths = model.Map(e => e.SpreadDistributionPaths)
                .Parameters(e =>
                    e.Set(o => o.Id)
                     .Set(o => o.SpreadDistributionId)
                     .Set(o => o.DaysToMaturity)
                     .Set(o => o.AveragePrice)
                );

            SpreadDistributionPathValues = model.Map(e => e.SpreadDistributionPathValues)
                .Parameters(e =>
                    e.Set(o => o.Id)
                     .Set(o => o.SpreadDistributionId)
                     .Set(o => o.DaysToMaturity)
                     .Set(o => o.SpreadValue)
                );

            SpreadDistributionJob = model.Map(e => e.SpreadDistributionJob)
                .Parameters(e =>
                    e.Set(o => o.JobId)
                     .Set(o => o.OrderId)
                     .Set(o => o.TradeId)
                     .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                     .Set(o => o.TradeStatus, o => o.AsEnum<TradeStatus>())
                     //.Set(o => o.ValueDate)
                     .Set(o => o.DaysToExpiry)
                     .Set(o => o.OptionStyle, o => o.AsEnum<OptionStyle>())
                     .Set(o => o.OptionType, o => o.AsEnum<OptionType>())
                     .Set(o => o.JobSubmitted)
                     .Set(o => o.JobStatus)
                     .Set(o => o.JobCompleted)
                     .Set(o => o.JobFailed)
                     .Set(o => o.InProgress)
                     .Set(o => o.LossProbabilityFactor)
                );
        }

        /// <summary>
        /// return db reader/writer properties
        /// </summary>
        public IOptionPricerDbReadContext DbReader => this;
        public IOptionPricerDbWriteContext DbWriter => this;

        /// <summary>
        /// db resultset to table mappings
        /// </summary>
        public DbMap<OptionPricerDeviceReadModel> OptionPricerDevice { get; private set; }
        public DbMap<SpreadDistributionReadModel> SpreadDistribution { get; private set; }
        public DbMap<SpreadDistributionPathReadModel> SpreadDistributionPaths { get; private set; }
        public DbMap<SpreadDistributionPathValueReadModel> SpreadDistributionPathValues { get; private set; }
        public DbMap<SpreadDistributionJobReadModel> SpreadDistributionJob { get; private set; }

        public enum StoredProcedure
        {
            spBackupDatabase,
             spDeleteSpreadDistributionJobs,
            spDeleteSpreadDistributionJobsInProgress,
            spGetOptionPricerDevices,
            spGetSpreadDistributionId,
            spGetSpreadDistribution,
            spGetSpreadDistributionPaths,
            spGetSpreadDistributionPathValues,
            spGetSpreadDistributionJobInProgressCount,
            spInsertOptionPricerDevice,
            spInsertSpreadDistribution,
            spInsertSpreadDistributionPath,
            spInsertSpreadDistributionPathValue,
            spInsertSpreadDistributionJob,
            spUpdateSpreadDistributionJobStatus,
            spUpdateSpreadDistributionJobFailed
        }

        /// <summary>
        /// delete all spread distribution jobs in progress
        /// </summary>
        /// <returns></returns>
        public async Task DeleteSpreadDistributionJobsInProgressAsync()
        {
            var db = _dbFactory.OptionPricerDb;
             await db.Use(StoredProcedure.spDeleteSpreadDistributionJobsInProgress)
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// delete all spread distribution jobs for selected trade
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task DeleteSpreadDistributionJobsAsync(int orderId, int tradeId)
        {
            var db = _dbFactory.OptionPricerDb;
            await db.Use(StoredProcedure.spDeleteSpreadDistributionJobs)
                .SetParameters(new {
                    orderId,
                    tradeId })
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// return all enabled option pricer devices
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyList<OptionPricerDeviceReadModel>> GetOptionPricerDevicesAsync()
        {
            var db = _dbFactory.OptionPricerDb;
            return await db.Use(StoredProcedure.spGetOptionPricerDevices)
                .ExecuteQueryAsync<OptionPricerDeviceReadModel>();
        }

        /// <summary>
        /// return count of spread distribution jobs in progress
        /// </summary>
        /// <returns></returns>
        public async Task<int> GetSpreadDistributionJobInProgressCountAsync(int orderId, int tradeId)
        {
            var db = _dbFactory.OptionPricerDb;
            return await db.Use(StoredProcedure.spGetSpreadDistributionJobInProgressCount)
                .SetParameters(new {
                    orderId,
                    tradeId })
                 .ExecuteScalarAsync<int>();
        }

        /// <summary>
        /// return spread distribution
        /// </summary>
        /// <param name="tradeId"></param>
        /// <param name="tradeStatus"></param>
        /// <param name="valueDate"></param>
        /// <param name="daysToExpiry"></param>
        /// <returns></returns>
        public async Task<SpreadDistributionReadModel> GetSpreadDistributionAsync(
            int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateTime valueDate, int daysToExpiry)
        {
            var db = _dbFactory.OptionPricerDb;
            return await db.Use(StoredProcedure.spGetSpreadDistribution)
                .SetParameters(new {
                    tradeId,
                    tradeType = $"{tradeType}",
                    tradeStatus = $"{tradeStatus}",
                    valueDate,
                    daysToExpiry })
                .ExecuteSingleAsync<SpreadDistributionReadModel>();
        }


        /// <summary>
        /// insert option pricer device configuration
        /// </summary>
        /// <returns></returns>
        public async Task InsertOptionPricerDeviceAsync(OptionPricerDeviceReadModel e)
        {
            var db = _dbFactory.OptionPricerDb;
            await db.Use(StoredProcedure.spInsertOptionPricerDevice)
                .SetParameters(new {
                    deviceId = e.DeviceId,
                    deviceName = e.DeviceName,
                    spreadPaths = e.SpreadPaths,
                    volatilityPaths = e.VolatilityPaths,
                    maxBatchSize = e.MaxBatchSize,
                    enabled = e.Enabled })
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert spread distribution
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task InsertSpreadDistributionsAsync(SpreadDistributionReadModel ePut, SpreadDistributionReadModel eCall)
        {
            var queuedCommands = new List<object>();
            var db = _dbFactory.OptionPricerDb;
                queuedCommands.Add(db.Use(StoredProcedure.spInsertSpreadDistribution)
                    .SetParameters(new {
                        tradeId = ePut.TradeId,
                        tradeType = $"{ePut.TradeType}",
                        tradeStatus = $"{ePut.TradeStatus}",
                        valueDate = ePut.ValueDate,
                        daysToExpiry = ePut.DaysToExpiry,
                        forwardPrice = ePut.ForwardPrice,
                        lossProbability = ePut.LossProbability,
                        shortVolatility = ePut.ShortVolatility,
                        longVolatility = ePut.LongVolatility,
                        lossThreshold = ePut.LossThreshold,
                        lossThresholdCount = ePut.LossThresholdCount })
                    .QueueCommand());

            // insert call spread distribution...
            queuedCommands.Add(
                db.Use(StoredProcedure.spInsertSpreadDistribution)
                   .SetParameters(new {
                       tradeId = eCall.TradeId,
                       tradeType = $"{eCall.TradeType}",
                       tradeStatus = $"{eCall.TradeStatus}",
                       valueDate = eCall.ValueDate,
                       daysToExpiry = eCall.DaysToExpiry,
                       forwardPrice = eCall.ForwardPrice,
                       lossProbability = eCall.LossProbability,
                       shortVolatility = eCall.ShortVolatility,
                       longVolatility = eCall.LongVolatility,
                       lossThreshold = eCall.LossThreshold,
                       lossThresholdCount = eCall.LossThresholdCount })
                   .QueueCommand());

            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }


        /// <summary>
        /// insert spread distribution job
        /// </summary>
        /// <param name="e">spread distribution job</param>
        /// <returns></returns>
        public async Task InsertSpreadDistributionJobAsync(SpreadDistributionJobReadModel e)
        {
            var db = _dbFactory.OptionPricerDb;
             await db.Use(StoredProcedure.spInsertSpreadDistributionJob)
                    .SetParameters(new {
                        jobId = e.JobId,
                        orderId = e.OrderId,
                        tradeId = e.TradeId,
                        tradeType = $"{e.TradeType}",
                        tradeStatus = $"{e.TradeStatus}",
                        valueDate = e.ValueDate,
                        daysToExpiry = e.DaysToExpiry,
                        optionStyle = $"{e.OptionStyle}",
                        optionType = $"{e.OptionType}",
                        jobSubmitted = e.JobSubmitted,
                        jobStatus = $"{SpreadDistributionJobStatus.InProgress}",
                        lossProbabilityFactor = e.LossProbabilityFactor })
                     .ExecuteCommandAsync();
        }

        /// <summary>
        /// update spread distribution job when completed
        /// </summary>
        /// <param name="jobId">job id</param>
        /// <param name="jobCompleted">job completion date</param>
        /// <param name="jobStatus">job status</param>
        /// <returns></returns>
        public async Task UpdateSpreadDistributionJobStatusAsync(int jobId, DateTime jobCompleted, SpreadDistributionJobStatus jobStatus)
        {
            var db = _dbFactory.OptionPricerDb;
             await db.Use(StoredProcedure.spUpdateSpreadDistributionJobStatus)
                .SetParameters(new {
                    jobId,
                    jobCompleted,
                    jobStatus = $"{jobStatus}" })
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// backup option pricer database
        /// </summary>
        /// <param name="backupType"></param>
        /// <returns></returns>
        public async Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage)
        {
            var db = _dbFactory.OptionPricerDb;
            await db.Use(StoredProcedure.spBackupDatabase)
                .SetParameters(new { backupType = $"{backupType}" })
                .WithNoTransaction()
                .SetCommandTimeout(commandTimeout)
                .ExecuteCommandAsync(onInfoMessage);
        }

    }
}
