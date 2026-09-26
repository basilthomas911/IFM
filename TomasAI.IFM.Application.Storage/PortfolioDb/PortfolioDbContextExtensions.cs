using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

internal static class PortfolioDbContextExtensions
{
    extension(PortfolioDbContext context)
    {
        /// <summary>Gets the required PostgreSQL event transaction coordinator.</summary>
        internal IPostgresEventTransaction RequiredTransactions
            => context._transactions
                ?? throw new InvalidOperationException(
                    "The EventSource PostgreSQL transaction coordinator is required for financial operations.");

        /// <summary>Reads one JSON-backed domain read model.</summary>
        /// <typeparam name="T">The shared-domain read-model type.</typeparam>
        /// <param name="name">The persistence operation name.</param>
        /// <param name="sql">The SQL statement.</param>
        /// <param name="parameters">The SQL parameters.</param>
        /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
        /// <returns>The matching model, or <see langword="null"/> when none exists.</returns>
        internal async Task<T?> ReadOneAsync<T>(
            string name,
            string sql,
            PortfolioParameters parameters,
            CancellationToken cancellationToken)
            where T : class
            => await context
                .Use($"{nameof(PortfolioDbSql)}.{name}", sql)
                .SetParameters(parameters)
                .ExecuteSingleAsync(PortfolioDbContext.MapToModel<T>, cancellationToken)
                .ConfigureAwait(false);

        /// <summary>Reads one mapped domain value.</summary>
        /// <typeparam name="T">The shared-domain result type.</typeparam>
        /// <param name="name">The persistence operation name.</param>
        /// <param name="sql">The SQL statement.</param>
        /// <param name="parameters">The SQL parameters.</param>
        /// <param name="map">The result-set mapper.</param>
        /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
        /// <returns>The matching value, or <see langword="null"/> when none exists.</returns>
        internal async Task<T?> ReadOneValueAsync<T>(
            string name,
            string sql,
            PortfolioParameters parameters,
            Func<IObjectDataRecord, T> map,
            CancellationToken cancellationToken)
            where T : class
            => await context
                .Use($"{nameof(PortfolioDbSql)}.{name}", sql)
                .SetParameters(parameters)
                .ExecuteSingleAsync(map, cancellationToken)
                .ConfigureAwait(false);

        /// <summary>Reads a collection of JSON-backed domain read models.</summary>
        /// <typeparam name="T">The shared-domain read-model type.</typeparam>
        /// <param name="name">The persistence operation name.</param>
        /// <param name="sql">The SQL statement.</param>
        /// <param name="parameters">The SQL parameters.</param>
        /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
        /// <returns>The matching models.</returns>
        internal async Task<IReadOnlyList<T>> ReadManyAsync<T>(
            string name,
            string sql,
            PortfolioParameters parameters,
            CancellationToken cancellationToken)
            where T : class
            => [.. await context
                .Use($"{nameof(PortfolioDbSql)}.{name}", sql)
                .SetParameters(parameters)
                .ExecuteQueryAsync(PortfolioDbContext.MapToModel<T>, cancellationToken)
                .ConfigureAwait(false)];

        /// <summary>Executes one Portfolio persistence command.</summary>
        /// <param name="name">The persistence operation name.</param>
        /// <param name="sql">The SQL statement.</param>
        /// <param name="parameters">The SQL parameters.</param>
        /// <param name="cancellationToken">The token used to cancel the asynchronous command.</param>
        /// <returns>A task representing the asynchronous command.</returns>
        internal Task WriteAsync(
            string name,
            string sql,
            PortfolioParameters parameters,
            CancellationToken cancellationToken)
            => context
                .Use($"{nameof(PortfolioDbSql)}.{name}", sql)
                .SetParameters(parameters)
                .ExecuteCommandAsync(cancellationToken);

        /// <summary>Creates a Portfolio PostgreSQL parameter binding from positional values.</summary>
        /// <param name="values">The positional persistence values.</param>
        /// <returns>The Portfolio parameter binding.</returns>
        internal PortfolioParameters ToParameters(params object?[] values)
            => values.ToPortfolioParameters();

        /// <summary>Creates a financial book and its initial ledger configuration.</summary>
        /// <param name="book">The financial book configuration.</param>
        /// <param name="accounts">The ledger accounts.</param>
        /// <param name="rules">The ledger posting rules.</param>
        /// <param name="periodStart">The accounting-period start date.</param>
        /// <param name="periodEnd">The accounting-period end date.</param>
        /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal Task CreateBookAsync(
            FinancialBookConfiguration book,
            IReadOnlyList<LedgerAccountDefinition> accounts,
            IReadOnlyList<LedgerPostingRule> rules,
            DateOnly periodStart,
            DateOnly periodEnd,
            CancellationToken cancellationToken = default)
            => context.RequiredTransactions.ExecuteAsync(
                async (db, cancellation) =>
                {
                    await PortfolioDbFinancialSupport.CreateBookAsync(
                        db,
                        book,
                        accounts,
                        rules,
                        periodStart,
                        periodEnd,
                        Guid.NewGuid(),
                        cancellation)
                        .ConfigureAwait(false);
                    return true;
                },
                cancellationToken);
    }

    extension<T>(PortfolioProjection<T> projection)
    {
        /// <summary>Creates the common persisted metadata values for a domain projection.</summary>
        /// <returns>The common persistence values.</returns>
        internal object?[] ToCommonValues()
            =>
            [
                projection.SchemaVersion,
                projection.AggregateVersion,
                projection.SourceEventId,
                projection.UpdatedOnUtc,
                new PortfolioJson(JsonSerializer.Serialize(projection.Value)),
                projection.PayloadHash
            ];
    }

    extension(object?[] values)
    {
        /// <summary>Converts positional values into PostgreSQL parameters.</summary>
        /// <returns>The Portfolio parameter binding.</returns>
        internal PortfolioParameters ToPortfolioParameters()
        {
            var flattened = values.SelectMany(value => value is object?[] nested ? nested : [value]);
            var parameters = flattened
                .Select(value => value is PortfolioJson json
                    ? new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json.Value }
                    : new NpgsqlParameter { Value = value ?? DBNull.Value })
                .ToArray();
            return new(parameters);
        }
    }
}
