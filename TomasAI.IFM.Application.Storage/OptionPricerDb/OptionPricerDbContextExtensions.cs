using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.SequenceId;

using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;


namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

/// <summary>Typed PostgreSQL persistence helpers for the context and caller-owned transactions.</summary>
public static class OptionPricerDbContextExtensions
{
    extension(OptionPricerDbContext context)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the DeleteOptionPricerDeviceAsync operation.</summary>
        internal async Task DeleteDeviceAsync(OptionPricerDeviceEntityId deviceId)
        {
            ArgumentNullException.ThrowIfNull(deviceId);
            var parameters = new DeleteOptionPricerDevice(deviceId.DeviceId, deviceId.DeviceName);
            await (context.Database.Use("OptionPricer.DeleteOptionPricerDevice", OptionPricerDbSql.DeleteOptionPricerDevice)
                .SetParameters(parameters)
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertOptionPricerDeviceAsync operation.</summary>
        internal async Task PersistDeviceAsync(OptionPricerDeviceReadModel device)
        {
            ArgumentNullException.ThrowIfNull(device);
            var parameters = new InsertOptionPricerDevice(device.DeviceId, device.DeviceName, device.SpreadPaths, device.VolatilityPaths, device.MaxBatchSize, device.OptionType.ToStringFast(), device.Enabled);
            await (context.Database.Use("OptionPricer.InsertOptionPricerDevice", OptionPricerDbSql.InsertOptionPricerDevice)
                .SetParameters(parameters)
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertSpreadDistributionsAsync operation.</summary>
        internal async Task PersistDistributionPairAsync(SpreadDistributionReadModel putSpreadDistribution, SpreadDistributionReadModel callSpreadDistribution)
        {
            ArgumentNullException.ThrowIfNull(callSpreadDistribution);
            ArgumentNullException.ThrowIfNull(putSpreadDistribution);
            var putId = putSpreadDistribution.Id != 0 ? putSpreadDistribution.Id : await (context._sequenceIdGenerator.GetSequenceIdAsync(SequenceName.SpreadDistribution_Id))
                .ConfigureAwait(false);
            var callId = callSpreadDistribution.Id != 0 ? callSpreadDistribution.Id : await (context._sequenceIdGenerator.GetSequenceIdAsync(SequenceName.SpreadDistribution_Id))
                .ConfigureAwait(false);
            var putParameters = putSpreadDistribution.ToInsertSpreadDistribution(putId);
            var callParameters = callSpreadDistribution.ToInsertSpreadDistribution(callId);
            var db = context.Database;
            var putCommand = db.Use("OptionPricer.InsertSpreadDistribution", OptionPricerDbSql.InsertSpreadDistribution)
                .SetParameters(putParameters)
                .QueueCommand();
            var callCommand = db.Use("OptionPricer.InsertSpreadDistribution", OptionPricerDbSql.InsertSpreadDistribution)
                .SetParameters(callParameters)
                .QueueCommand();
            await (db.ExecuteQueuedCommandsAsync([putCommand, callCommand], useTransaction: true))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertSpreadDistributionJobAsync operation.</summary>
        internal async Task PersistDistributionJobAsync(SpreadDistributionJobReadModel job)
        {
            ArgumentNullException.ThrowIfNull(job);
            var parameters = job.ToInsertSpreadDistributionJob();
            await (context.Database.Use("OptionPricer.InsertSpreadDistributionJob", OptionPricerDbSql.InsertSpreadDistributionJob)
                .SetParameters(parameters)
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }


    }

    extension(SpreadDistributionReadModel distribution)
    {
        /// <summary>
        /// Converts a shared spread-distribution read model into its storage insert binding.
        /// </summary>
        /// <param name="id">The persisted distribution identifier.</param>
        /// <returns>The storage insert binding.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal InsertSpreadDistribution ToInsertSpreadDistribution(long id)
            => new(
                id,
                distribution.TradeId,
                distribution.TradeType.ToStringFast(),
                distribution.TradeStatus.ToStringFast(),
                distribution.ValueDate,
                distribution.DaysToExpiry,
                distribution.ForwardPrice,
                distribution.LossProbability,
                distribution.ShortVolatility,
                distribution.LongVolatility,
                distribution.LossThreshold,
                distribution.LossThresholdCount,
                distribution.ForwardLossRatio,
                distribution.CreatedOn);
    }

    extension(SpreadDistributionJobReadModel job)
    {
        /// <summary>
        /// Converts a shared spread-distribution job read model into its storage insert binding.
        /// </summary>
        /// <returns>The storage insert binding.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal InsertSpreadDistributionJob ToInsertSpreadDistributionJob()
            => new(
                job.OrderId,
                job.TradeId,
                job.TradeType.ToStringFast(),
                job.TradeStatus.ToStringFast(),
                job.ValueDate,
                job.DaysToExpiry,
                job.JobSubmitted,
                job.JobStatus.ToStringFast(),
                job.JobCompleted,
                job.JobFailed,
                job.JobStatus == SpreadDistributionJobStatus.InProgress,
                job.LossProbabilityFactor);
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        /// <summary>Persists the accepted put/call pair on the caller-owned event transaction.</summary>
        public async Task PersistAcceptedDistributionPairAsync(SpreadDistributionReadModel put, SpreadDistributionReadModel call, long eventId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(put);
            ArgumentNullException.ThrowIfNull(call);
            var putId = put.Id != 0 ? put.Id : eventId.AsDistributionId(false);
            var callId = call.Id != 0 ? call.Id : eventId.AsDistributionId(true);
            var putParameters = (NpgsqlParameter[])put.ToInsertSpreadDistribution(putId).Bind();
            var callParameters = (NpgsqlParameter[])call.ToInsertSpreadDistribution(callId).Bind();
            await transaction.ExecuteAsync(OptionPricerDbSql.InsertAcceptedSpreadDistribution, [.. putParameters, Bigint(eventId)], cancellationToken)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync(OptionPricerDbSql.InsertAcceptedSpreadDistribution, [.. callParameters, Bigint(eventId)], cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>Deletes persisted distributions on the caller-owned event transaction.</summary>
        public Task DeleteAcceptedDistributionsAsync(int tradeId, DateOnly valueDate, CancellationToken cancellationToken = default)
            => transaction.ExecuteAsync(OptionPricerDbSql.DeleteSpreadDistribution, (NpgsqlParameter[])new DeleteSpreadDistribution(tradeId, valueDate).Bind(), cancellationToken);

        /// <summary>Persists the accepted job on the caller-owned event transaction.</summary>
        public Task PersistAcceptedDistributionJobAsync(SpreadDistributionJobReadModel job, CancellationToken cancellationToken = default)
            => transaction.ExecuteAsync(OptionPricerDbSql.InsertSpreadDistributionJob, (NpgsqlParameter[])job.ToInsertSpreadDistributionJob().Bind(), cancellationToken);

        /// <summary>Deletes an order/trade's jobs on the caller-owned event transaction.</summary>
        public Task DeleteAcceptedDistributionJobsAsync(int orderId, int tradeId, CancellationToken cancellationToken = default)
            => transaction.ExecuteAsync(OptionPricerDbSql.DeleteSpreadDistributionJobs, (NpgsqlParameter[])new DeleteSpreadDistributionJobs(orderId, tradeId).Bind(), cancellationToken);

        /// <summary>Updates a job's persisted status on the caller-owned event transaction.</summary>
        public Task UpdateAcceptedDistributionJobStatusAsync(int orderId, int tradeId, DateOnly valueDate, SpreadDistributionJobStatus status, DateTime completed, DateTime? failed, CancellationToken cancellationToken = default)
            => transaction.ExecuteAsync(OptionPricerDbSql.UpdateSpreadDistributionJobStatus, (NpgsqlParameter[])new UpdateSpreadDistributionJobStatus(orderId, tradeId, valueDate, status.ToStringFast(), completed, failed, status == SpreadDistributionJobStatus.InProgress).Bind(), cancellationToken);
    }

    extension(long eventId)
    {
        /// <summary>Derives distinct deterministic negative put/call IDs for source-event replay.</summary>
        internal long AsDistributionId(bool isCall)
        {
            if (eventId <= 0 || eventId > (long.MaxValue - 1) / 2)
                throw new ArgumentOutOfRangeException(nameof(eventId), eventId, "A positive persisted source event id is required.");
            return isCall ? checked(-(eventId * 2 + 1)) : checked(-(eventId * 2));
        }
    }

    extension(DateTime value)
    {
        /// <summary>Preserves the established UTC interpretation of unspecified timestamps.</summary>
        internal DateTime AsOptionPricerUtc() => value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    extension(DateTime? value)
    {
        /// <summary>Converts an optional persisted timestamp to UTC while preserving null.</summary>
        internal DateTime? AsOptionPricerUtc() => value?.AsOptionPricerUtc();
    }
}
