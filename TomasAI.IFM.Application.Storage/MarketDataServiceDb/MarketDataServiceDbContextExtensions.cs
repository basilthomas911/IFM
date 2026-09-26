using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

internal static class MarketDataServiceDbContextExtensions
{
    extension(MarketDataServiceDbContext context)
    {
        /// <summary>Gets a watchdog observation by its durable identity.</summary>
        /// <param name="id">The observation identity.</param>
        /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
        /// <returns>The matching observation, or <see langword="null"/> when none exists.</returns>
        internal Task<DatabentoWatchdogObservation?> GetObservationByIdentityAsync(Guid id, CancellationToken cancellationToken)
            => context.Database
                .Use("MarketDataService.GetObservationByIdentity", MarketDataServiceDbSql.GetObservationByIdentity)
                .SetParameters(new IdentityParameter(id))
                .ExecuteSingleAsync<DatabentoWatchdogObservation?>(MarketDataServiceDbContext.MapToObservation, cancellationToken);

    }

    extension(FuturesRolloverContractAssignment assignment)
    {
        /// <summary>Builds the PostgreSQL bind values for a contract assignment mutation.</summary>
        /// <param name="expectedRowVersion">The optimistic-concurrency row version.</param>
        /// <returns>The ordered PostgreSQL parameters.</returns>
        internal Npgsql.NpgsqlParameter[] BindAssignment(long expectedRowVersion) => Values(
            Text(assignment.ContractRole.ToString()),
            Text(assignment.RootSymbol),
            Text(assignment.ContractId),
            Text(assignment.Description),
            Text(assignment.LocalSymbol),
            Text(assignment.SecurityType),
            Text(assignment.Currency),
            Text(assignment.Exchange),
            Text(assignment.Multiplier),
            Date(assignment.LastTradeDate),
            Date(assignment.NextRolloverDate),
            Text(assignment.SourceContractHash),
            TimestampTz(assignment.CreatedOnUtc),
            Text(assignment.CreatedBy),
            TimestampTz(assignment.UpdatedOnUtc),
            Text(assignment.UpdatedBy),
            Bigint(expectedRowVersion));
    }

    extension(DateTime value)
    {
        /// <summary>Marks a persisted PostgreSQL timestamp as UTC.</summary>
        /// <returns>The timestamp with <see cref="DateTimeKind.Utc"/>.</returns>
        internal DateTime AsUtc() => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
