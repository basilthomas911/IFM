using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using System.Text.Json;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

public static class PortfolioDbContextExtensions
{
    extension(PortfolioDbContext context)
    {
        /// <summary>Gets the required PostgreSQL event transaction coordinator.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal PortfolioParameters ToParameters(params object?[] values)
            => values.ToPortfolioParameters();


    }

    extension<T>(PortfolioProjection<T> projection)
    {
        /// <summary>Validates the projection wrapper and returns its domain value.</summary>
        /// <returns>The accepted domain read model.</returns>
        internal T RequiredValue()
        {
            ArgumentNullException.ThrowIfNull(projection);
            return projection.Value;
        }

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

    extension(DraftPolicyProjectionDeletion deletion)
    {
        /// <summary>Validates a policy deletion and binds its accepted identifiers.</summary>
        /// <returns>The canonical policy deletion parameters.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "The C# 14 extension member reads and validates its declared receiver.")]
        internal object?[] ToDeletionValues()
        {
            ArgumentNullException.ThrowIfNull(deletion);
            return [deletion.PortfolioId, deletion.PolicyId, deletion.SourceEventId];
        }
    }

    extension(DraftPortfolioProjectionDeletion deletion)
    {
        /// <summary>Validates a portfolio deletion and binds its accepted identifiers.</summary>
        /// <returns>The canonical portfolio deletion parameters.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "The C# 14 extension member reads and validates its declared receiver.")]
        internal object?[] ToDeletionValues()
        {
            ArgumentNullException.ThrowIfNull(deletion);
            return [deletion.PortfolioId, deletion.SourceEventId];
        }
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
                    ? BindValues.Json(json.Value)
                    : value)
                .ToArray();
            return new(parameters);
        }
    }

    extension(string json)
    {
        /// <summary>Decodes the persisted financial value using the existing financial serialization contract.</summary>
        internal T DecodeFinancialValue<T>() => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json)
            ?? throw new InvalidDataException($"Invalid stored {typeof(T).Name}.");
    }

    extension(EnlistedEventTransaction transaction)
    {
        /// <summary>Reads the committed operation receipt without applying or replaying a business operation.</summary>
        internal async Task<T?> ReadFinancialOperationAsync<T>(int portfolioId, Guid operationId, string? inputHash, CancellationToken token)
            where T : class, IFinancialCompletedEvent
        {
            var rows = await transaction.QueryAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Select02,
                [portfolioId, operationId], reader => (Hash: reader.GetString(0), Event: new EventLogReadModel(
                    reader.GetInt64(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4),
                    reader.GetFieldValue<byte[]>(5), reader.GetGuid(6), reader.GetString(7), reader.GetInt64(8))), token)
                .ConfigureAwait(false);
            if (rows.Count == 0) return null;
            var row = rows.Single();
            if (inputHash is not null && !string.Equals(inputHash, row.Hash, StringComparison.Ordinal))
                throw new FinancialOperationException(FinancialReasons.RequestMismatch, "Operation identity was used for different input.");
            return row.Event.ToDomainEvent() as T ?? throw new FinancialOperationException(FinancialReasons.RequestMismatch,
                "Operation belongs to a different financial actor/result contract.", FinancialCommitDisposition.NoNewMutation, operationId);
        }
    }
    extension(IEnlistedPostgresTransaction transaction)
    {
        /// <summary>Stores the current mandate and disables superseded mandate projections atomically.</summary>
        public async Task WriteFundProjectionAsync(PortfolioProjection<FundMandateReadModel> row, CancellationToken token)
        {
            var fund = row.Value;
            await transaction.ScalarAsync("SELECT pg_advisory_xact_lock(1701,$1)", [fund.FundId], token).ConfigureAwait(false);
            var versions = await transaction.QueryAsync(
                "SELECT payload_json::text FROM portfolio.fund_by_portfolio WHERE portfolio_id=$1 AND fund_id=$2 FOR UPDATE",
                [fund.PortfolioId, fund.FundId], reader => JsonSerializer.Deserialize<FundMandateReadModel>(reader.GetString(0))!, token).ConfigureAwait(false);
            if (versions.Any(v => v.FundMandateVersion > fund.FundMandateVersion))
            {
                fund = fund with { OperatingState = FundOperatingState.Disabled };
                row = PortfolioProjection<FundMandateReadModel>.Create(fund, row.AggregateVersion, row.SourceEventId, row.UpdatedOnUtc);
            }
            foreach (var old in versions.Where(v => v.FundMandateVersion < fund.FundMandateVersion && v.OperatingState != FundOperatingState.Disabled))
            {
                var disabled = PortfolioProjection<FundMandateReadModel>.Create(old with { OperatingState = FundOperatingState.Disabled }, row.AggregateVersion, row.SourceEventId, row.UpdatedOnUtc);
                await transaction.WritePortfolioValuesAsync("""
                    UPDATE portfolio.fund_by_portfolio SET operating_state='Disabled',schema_version=$4,aggregate_version=$5,
                    source_event_id=$6,updated_on_utc=$7,payload_json=$8,payload_hash=$9
                    WHERE portfolio_id=$1 AND fund_id=$2 AND fund_mandate_version=$3 AND source_event_id<=$6
                    """, token, old.PortfolioId, old.FundId, old.FundMandateVersion, disabled.ToCommonValues()).ConfigureAwait(false);
            }
            await transaction.WritePortfolioValuesAsync(PortfolioDbSql.Fund.Upsert, token, fund.PortfolioId, fund.FundId,
                fund.FundMandateVersion, fund.OperatingState.ToString(), row.ToCommonValues(), fund.TradingYear, fund.DecisionHorizon, fund.EffectiveFromUtc).ConfigureAwait(false);
        }

        /// <summary>Persists accepted business rows, indexes and deletions without reloading aggregate state.</summary>
        public async Task PersistAcceptedPortfolioAsync(IEvent source,PortfolioProjectionFacts facts,CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(facts);
            if (facts.SchemaVersion != 1 || facts.Revision <= 0 || source.EventId <= 0 || facts.UpdatedAtUtc.Kind != DateTimeKind.Utc)
                throw new InvalidOperationException("Accepted Portfolio projection facts require schema 1, committed identity, revision and UTC timestamp.");
            await transaction.WritePortfolioAsync(source,facts,token)
                .ConfigureAwait(false);
            await transaction.WriteFundAsync(source,facts,token)
                .ConfigureAwait(false);
            await transaction.WriteCompositionAsync(source,facts,token)
                .ConfigureAwait(false);
            foreach (var value in facts.Policies)
            {
                var row = facts.ToAcceptedRow(value,source);
                await transaction.WritePortfolioValuesAsync(PortfolioDbSql.Policy.Upsert,token,value.PolicyId,value.PolicyVersion,value.PortfolioId,value.OperatingState.ToString(),row.ToCommonValues())
                .ConfigureAwait(false);
            }
            foreach (var tradeId in facts.DeletedTradeIds)
                await transaction.WritePortfolioValuesAsync(PortfolioDbSql.Orders.DeleteTrade,token,tradeId,source.EventId)
                .ConfigureAwait(false);
            if (facts.DeletedOrderId > 0)
                await transaction.WritePortfolioValuesAsync(PortfolioDbSql.Orders.DeleteOrder,token,facts.DeletedOrderId,source.EventId)
                .ConfigureAwait(false);
            if (facts.DeletedPolicyId > 0)
                await transaction.WritePortfolioValuesAsync(PortfolioDbSql.Policy.DeleteDraft,token,facts.DeletedPolicyPortfolioId,facts.DeletedPolicyId,source.EventId)
                .ConfigureAwait(false);
            if (facts.DeletedPortfolioId > 0)
                await transaction.WritePortfolioValuesAsync(PortfolioDbSql.Portfolio.DeleteDraft,token,facts.DeletedPortfolioId,source.EventId)
                .ConfigureAwait(false);
        }

        private async Task WritePortfolioAsync(IEvent source,PortfolioProjectionFacts facts,CancellationToken token)
        {
            if (facts.Portfolio is { } value)
                await transaction.WritePortfolioValuesAsync(PortfolioDbSql.Portfolio.Upsert,token,value.PortfolioId,value.PortfolioVersion,value.OperatingState.ToString(),facts.ToAcceptedRow(value,source).ToCommonValues(),checked((value.PortfolioId-1)/1000))
                .ConfigureAwait(false);
            if (facts.Allocation is { } allocation)
                await transaction.WritePortfolioValuesAsync(PortfolioDbSql.Fund.UpsertAllocation,token,allocation.PortfolioId,allocation.FundId,allocation.AllocationVersion,facts.ToAcceptedRow(allocation,source).ToCommonValues())
                .ConfigureAwait(false);
            if (facts.RiskEnvelope is { } risk)
                await transaction.WritePortfolioValuesAsync(PortfolioDbSql.Fund.UpsertEnvelope,token,risk.PortfolioId,risk.FundId,risk.EnvelopeVersion,facts.ToAcceptedRow(risk,source).ToCommonValues())
                .ConfigureAwait(false);
        }

        private async Task WriteFundAsync(IEvent source,PortfolioProjectionFacts facts,CancellationToken token)
        {
            if (facts.Fund is { } value)
                await transaction.WriteFundProjectionAsync(facts.ToAcceptedRow(value,source),token).ConfigureAwait(false);
            if (facts.Assignment is { } assigned)
                await transaction.WritePortfolioValuesAsync(PortfolioDbSql.Fund.UpsertAssignment,token,assigned.PortfolioId,assigned.FundId,assigned.FundMandateVersion,assigned.TradeTemplateId,assigned.TradeTemplateVersion,facts.ToAcceptedRow(assigned,source).ToCommonValues())
                .ConfigureAwait(false);
        }

        private async Task WriteCompositionAsync(IEvent source,PortfolioProjectionFacts facts,CancellationToken token)
        {
            if (facts.Order is not { } order) return;
            var row = PortfolioProjection<FundOrderProjectionReadModel>.Create(order,facts.OrderRevision,source.EventId,facts.UpdatedAtUtc);
            var month = new DateOnly(order.CreatedOnUtc.Year,order.CreatedOnUtc.Month,1);
            await transaction.WritePortfolioValuesAsync(PortfolioDbSql.Orders.UpsertOrder,token,order.PortfolioId,order.FundId,month,order.CreatedOnUtc,order.OrderId,order.Status,row.ToCommonValues())
                .ConfigureAwait(false);
            foreach (var trade in facts.Trades)
                await transaction.WritePortfolioValuesAsync(PortfolioDbSql.Orders.UpsertTrade,token,trade.OrderId,trade.TradeId,trade.PortfolioId,trade.FundId,
                    PortfolioProjection<FundOrderTradeProjectionReadModel>.Create(trade,facts.OrderRevision,source.EventId,facts.UpdatedAtUtc).ToCommonValues())
                .ConfigureAwait(false);
            if (facts.Composition is { } composition)
                await transaction.WritePortfolioValuesAsync(PortfolioDbSql.Orders.UpsertComposition,token,composition.WorkflowId,composition.OrderId,composition.PortfolioId,composition.FundId,composition.Status,
                    PortfolioProjection<FundCompositionWorkflowProjectionReadModel>.Create(composition,facts.OrderRevision,source.EventId,facts.UpdatedAtUtc).ToCommonValues())
                .ConfigureAwait(false);
        }

        private Task<int> WritePortfolioValuesAsync(string sql,CancellationToken token,params object?[] values)
            => transaction.ExecuteAsync(sql,(object?[])values.ToPortfolioParameters().Bind(),token);
    }
    extension(PortfolioProjectionFacts facts)
    {
        /// <summary>Maps accepted values to their persisted event identity and revision.</summary>
        private PortfolioProjection<T> ToAcceptedRow<T>(T value, IEvent source)
            => PortfolioProjection<T>.Create(value, facts.Revision, source.EventId, facts.UpdatedAtUtc);
    }
}
