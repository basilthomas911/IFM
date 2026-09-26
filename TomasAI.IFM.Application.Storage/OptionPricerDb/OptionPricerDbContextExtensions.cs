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
        /// <summary>
        /// Gets the identities of all persisted spread-distribution jobs that are currently in progress.
        /// </summary>
        /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
        /// <returns>The identities of the in-progress jobs.</returns>
        internal async Task<IReadOnlyList<SpreadDistributionJobEntityId>> GetSpreadDistributionJobsInProgressAsync(
            CancellationToken cancellationToken = default)
        {
            var jobs = await context.Database
                .Use($"{nameof(OptionPricerDbCql)}.{nameof(OptionPricerDbCql.GetAllSpreadDistributionJobs)}", OptionPricerDbCql.GetAllSpreadDistributionJobs)
                .ExecuteQueryAsync<SpreadDistributionJobReadModel>(OptionPricerDbContext.MapToSpreadDistributionJob, cancellationToken);
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
