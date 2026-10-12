using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

internal static class OptionPricerDbContextExtensions
{
    extension(OptionPricerDbContext context)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the ProjectAcceptedEventAsync operation.</summary>
        internal async Task ProjectAcceptedEventAsyncCoreBinding(EventSourceDb.IEnlistedPostgresTransaction transaction, TomasAI.IFM.Shared.EventSourcing.IEvent source, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            if (!string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
                throw new InvalidOperationException("Enlisted pricing materialization requires the PostgreSQL provider.");
            ArgumentNullException.ThrowIfNull(source);
            switch (source)
            {
                case TomasAI.IFM.Domain.OptionPricer.Shared.Events.SpreadDistributionInsertedEvent inserted:
                    var put = inserted.PutSpreadDistribution;
                    var call = inserted.CallSpreadDistribution;
                    var putId = put.Id != 0 ? put.Id : context.StableSourceDistributionId(source.EventId, false);
                    var callId = call.Id != 0 ? call.Id : context.StableSourceDistributionId(source.EventId, true);
                    var putParameters = put.ToInsertSpreadDistribution(putId)with
                    {
                        PostgreSql = true
                    };
                    var callParameters = call.ToInsertSpreadDistribution(callId)with
                    {
                        PostgreSql = true
                    };
                    await transaction.ExecuteAsync(OptionPricerDbSql.InsertAcceptedSpreadDistribution, [..(Npgsql.NpgsqlParameter[])putParameters.Bind(), TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter.Bigint(source.EventId)], cancellationToken)
                .ConfigureAwait(false);
                    await transaction.ExecuteAsync(OptionPricerDbSql.InsertAcceptedSpreadDistribution, [..(Npgsql.NpgsqlParameter[])callParameters.Bind(), TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter.Bigint(source.EventId)], cancellationToken)
                .ConfigureAwait(false);
                    break;
                case TomasAI.IFM.Domain.OptionPricer.Shared.Events.SpreadDistributionDeletedEvent deleted:
                    var deletion = new DeleteSpreadDistribution(deleted.EntityId.TradeId, deleted.EntityId.ValueDate)
                    {
                        PostgreSql = true
                    };
                    await transaction.ExecuteAsync(OptionPricerDbSql.DeleteSpreadDistribution, (Npgsql.NpgsqlParameter[])deletion.Bind(), cancellationToken)
                .ConfigureAwait(false);
                    break;
                case TomasAI.IFM.Domain.OptionPricer.Shared.Events.SpreadDistributionJobSubmittedEvent submitted:
                    var submission = submitted.SpreadDistributionJob.ToInsertSpreadDistributionJob()with
                    {
                        PostgreSql = true
                    };
                    await transaction.ExecuteAsync(OptionPricerDbSql.InsertSpreadDistributionJob, (Npgsql.NpgsqlParameter[])submission.Bind(), cancellationToken)
                .ConfigureAwait(false);
                    break;
                case TomasAI.IFM.Domain.OptionPricer.Shared.Events.SpreadDistributionJobsInProgressDeletedEvent deleted:
                    var jobs = new DeleteSpreadDistributionJobs(deleted.EntityId.OrderId, deleted.EntityId.TradeId)
                    {
                        PostgreSql = true
                    };
                    await transaction.ExecuteAsync(OptionPricerDbSql.DeleteSpreadDistributionJobs, (Npgsql.NpgsqlParameter[])jobs.Bind(), cancellationToken)
                .ConfigureAwait(false);
                    break;
                case TomasAI.IFM.Domain.OptionPricer.Shared.Events.SpreadDistributionJobStatusUpdatedEvent changed:
                    var update = new UpdateSpreadDistributionJobStatus(changed.EntityId.OrderId, changed.EntityId.TradeId, changed.EntityId.ValueDate, changed.JobStatus.ToStringFast(), changed.ReceivedOn, null, changed.JobStatus == SpreadDistributionJobStatus.InProgress)
                    {
                        PostgreSql = true
                    };
                    await transaction.ExecuteAsync(OptionPricerDbSql.UpdateSpreadDistributionJobStatus, (Npgsql.NpgsqlParameter[])update.Bind(), cancellationToken)
                .ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException("Unexpected option-pricer source event.");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the DeleteOptionPricerDeviceAsync operation.</summary>
        internal async Task DeleteOptionPricerDeviceAsyncCoreBinding(OptionPricerDeviceEntityId deviceId)
        {
            ArgumentNullException.ThrowIfNull(deviceId);
            var parameters = new DeleteOptionPricerDevice(deviceId.DeviceId, deviceId.DeviceName);
            await (context._dbFactory.OptionPricerDb.Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.DeleteOptionPricerDevice)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? OptionPricerDbSql.DeleteOptionPricerDevice : OptionPricerDbCql.DeleteOptionPricerDevice)
                .SetParameters(parameters with { PostgreSql = string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) })
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertOptionPricerDeviceAsync operation.</summary>
        internal async Task InsertOptionPricerDeviceAsyncCoreBinding(OptionPricerDeviceReadModel device)
        {
            ArgumentNullException.ThrowIfNull(device);
            var parameters = new InsertOptionPricerDevice(device.DeviceId, device.DeviceName, device.SpreadPaths, device.VolatilityPaths, device.MaxBatchSize, device.OptionType.ToStringFast(), device.Enabled);
            await (context._dbFactory.OptionPricerDb.Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.InsertOptionPricerDevice)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? OptionPricerDbSql.InsertOptionPricerDevice : OptionPricerDbCql.InsertOptionPricerDevice)
                .SetParameters(parameters with { PostgreSql = string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) })
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertSpreadDistributionsAsync operation.</summary>
        internal async Task InsertSpreadDistributionsAsyncCoreBinding(SpreadDistributionReadModel putSpreadDistribution, SpreadDistributionReadModel callSpreadDistribution)
        {
            ArgumentNullException.ThrowIfNull(callSpreadDistribution);
            ArgumentNullException.ThrowIfNull(putSpreadDistribution);
            var putId = putSpreadDistribution.Id != 0 ? putSpreadDistribution.Id : await (context._sequenceIdGenerator.GetSequenceIdAsync(SequenceName.SpreadDistribution_Id))
                .ConfigureAwait(false);
            var callId = callSpreadDistribution.Id != 0 ? callSpreadDistribution.Id : await (context._sequenceIdGenerator.GetSequenceIdAsync(SequenceName.SpreadDistribution_Id))
                .ConfigureAwait(false);
            var putParameters = putSpreadDistribution.ToInsertSpreadDistribution(putId);
            var callParameters = callSpreadDistribution.ToInsertSpreadDistribution(callId);
            var db = context._dbFactory.OptionPricerDb;
            var putCommand = db.Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.InsertSpreadDistribution)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? OptionPricerDbSql.InsertSpreadDistribution : OptionPricerDbCql.InsertSpreadDistribution)
                .SetParameters(putParameters with { PostgreSql = string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) })
                .QueueCommand();
            var callCommand = db.Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.InsertSpreadDistribution)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? OptionPricerDbSql.InsertSpreadDistribution : OptionPricerDbCql.InsertSpreadDistribution)
                .SetParameters(callParameters with { PostgreSql = string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) })
                .QueueCommand();
            await (db.ExecuteQueuedCommandsAsync([putCommand, callCommand], useTransaction: true))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertSpreadDistributionJobAsync operation.</summary>
        internal async Task InsertSpreadDistributionJobAsyncCoreBinding(SpreadDistributionJobReadModel job)
        {
            ArgumentNullException.ThrowIfNull(job);
            var parameters = job.ToInsertSpreadDistributionJob();
            await (context._dbFactory.OptionPricerDb.Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.InsertSpreadDistributionJob)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? OptionPricerDbSql.InsertSpreadDistributionJob : OptionPricerDbCql.InsertSpreadDistributionJob)
                .SetParameters(parameters with { PostgreSql = string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) })
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Executes the StableSourceDistributionId persistence helper.</summary>
        internal long StableSourceDistributionId(long eventId, bool isCall)
        {
            if (eventId <= 0 || eventId > (long.MaxValue - 1) / 2)
                throw new ArgumentOutOfRangeException(nameof(eventId), eventId, "A positive persisted source event id is required.");
            return isCall ? checked(-(eventId * 2 + 1)) : checked(-(eventId * 2));
        }

        /// <summary>
        /// Gets the identities of all persisted spread-distribution jobs that are currently in progress.
        /// </summary>
        /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
        /// <returns>The identities of the in-progress jobs.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<IReadOnlyList<SpreadDistributionJobEntityId>> GetSpreadDistributionJobsInProgressAsync(
            CancellationToken cancellationToken = default)
        {
            var jobs = await (context.Database
                .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.GetAllSpreadDistributionJobs)}", string.Equals(context.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? OptionPricerDbSql.GetAllSpreadDistributionJobs : OptionPricerDbCql.GetAllSpreadDistributionJobs)
                .ExecuteQueryAsync<SpreadDistributionJobReadModel>(OptionPricerDbContext.MapToSpreadDistributionJob, cancellationToken))
                .ConfigureAwait(false);
            return jobs
                .Where(job => job.JobStatus == SpreadDistributionJobStatus.InProgress || job.InProgress)
                .Select(job => job.EntityId)
                .ToArray();
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
}
