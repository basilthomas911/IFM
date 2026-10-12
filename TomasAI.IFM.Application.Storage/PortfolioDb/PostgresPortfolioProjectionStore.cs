using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.Storage.PortfolioDb;

/// <summary>Materializes exact accepted Portfolio values on the caller's source transaction; performs no admission or external effects.</summary>
public static class PostgresPortfolioProjectionStore
{
    /// <summary>Persists accepted business rows, indexes and deletions without reloading aggregate state.</summary>
    public static async Task ApplyAsync(IEnlistedPostgresTransaction transaction,IEvent source,PortfolioProjectionFacts facts,CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.SchemaVersion != 1 || facts.Revision <= 0 || source.EventId <= 0 || facts.UpdatedAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Accepted Portfolio projection facts require schema 1, committed identity, revision and UTC timestamp.");
        await WritePortfolioAsync(transaction,source,facts,token).ConfigureAwait(false);
        await WriteFundAsync(transaction,source,facts,token).ConfigureAwait(false);
        await WriteCompositionAsync(transaction,source,facts,token).ConfigureAwait(false);
        foreach (var value in facts.Policies)
        {
            var row = Row(value,source,facts);
            await WriteAsync(transaction,PortfolioDbSql.Policy.Upsert,token,value.PolicyId,value.PolicyVersion,value.PortfolioId,value.OperatingState.ToString(),row.ToCommonValues()).ConfigureAwait(false);
        }
        foreach (var tradeId in facts.DeletedTradeIds)
            await WriteAsync(transaction,PortfolioDbSql.Orders.DeleteTrade,token,tradeId,source.EventId).ConfigureAwait(false);
        if (facts.DeletedOrderId > 0)
            await WriteAsync(transaction,PortfolioDbSql.Orders.DeleteOrder,token,facts.DeletedOrderId,source.EventId).ConfigureAwait(false);
        if (facts.DeletedPolicyId > 0)
            await WriteAsync(transaction,PortfolioDbSql.Policy.DeleteDraft,token,facts.DeletedPolicyPortfolioId,facts.DeletedPolicyId,source.EventId).ConfigureAwait(false);
        if (facts.DeletedPortfolioId > 0)
            await WriteAsync(transaction,PortfolioDbSql.Portfolio.DeleteDraft,token,facts.DeletedPortfolioId,source.EventId).ConfigureAwait(false);
    }

    static async Task WritePortfolioAsync(IEnlistedPostgresTransaction transaction,IEvent source,PortfolioProjectionFacts facts,CancellationToken token)
    {
        if (facts.Portfolio is { } value)
            await WriteAsync(transaction,PortfolioDbSql.Portfolio.Upsert,token,value.PortfolioId,value.PortfolioVersion,value.OperatingState.ToString(),Row(value,source,facts).ToCommonValues(),checked((value.PortfolioId-1)/1000)).ConfigureAwait(false);
        if (facts.Allocation is { } allocation)
            await WriteAsync(transaction,PortfolioDbSql.Fund.UpsertAllocation,token,allocation.PortfolioId,allocation.FundId,allocation.AllocationVersion,Row(allocation,source,facts).ToCommonValues()).ConfigureAwait(false);
        if (facts.RiskEnvelope is { } risk)
            await WriteAsync(transaction,PortfolioDbSql.Fund.UpsertEnvelope,token,risk.PortfolioId,risk.FundId,risk.EnvelopeVersion,Row(risk,source,facts).ToCommonValues()).ConfigureAwait(false);
    }

    static async Task WriteFundAsync(IEnlistedPostgresTransaction transaction,IEvent source,PortfolioProjectionFacts facts,CancellationToken token)
    {
        if (facts.Fund is { } value)
            await WriteAsync(transaction,PortfolioDbSql.Fund.Upsert,token,value.PortfolioId,value.FundId,value.FundMandateVersion,value.OperatingState.ToString(),Row(value,source,facts).ToCommonValues(),value.TradingYear,value.DecisionHorizon,value.EffectiveFromUtc).ConfigureAwait(false);
        if (facts.Assignment is { } assigned)
            await WriteAsync(transaction,PortfolioDbSql.Fund.UpsertAssignment,token,assigned.PortfolioId,assigned.FundId,assigned.FundMandateVersion,assigned.TradeTemplateId,assigned.TradeTemplateVersion,Row(assigned,source,facts).ToCommonValues()).ConfigureAwait(false);
    }

    static async Task WriteCompositionAsync(IEnlistedPostgresTransaction transaction,IEvent source,PortfolioProjectionFacts facts,CancellationToken token)
    {
        if (facts.Order is not { } order) return;
        var row = PortfolioProjection<FundOrderProjectionReadModel>.Create(order,facts.OrderRevision,source.EventId,facts.UpdatedAtUtc);
        var month = new DateOnly(order.CreatedOnUtc.Year,order.CreatedOnUtc.Month,1);
        await WriteAsync(transaction,PortfolioDbSql.Orders.UpsertOrder,token,order.PortfolioId,order.FundId,month,order.CreatedOnUtc,order.OrderId,order.Status,row.ToCommonValues()).ConfigureAwait(false);
        foreach (var trade in facts.Trades)
            await WriteAsync(transaction,PortfolioDbSql.Orders.UpsertTrade,token,trade.OrderId,trade.TradeId,trade.PortfolioId,trade.FundId,
                PortfolioProjection<FundOrderTradeProjectionReadModel>.Create(trade,facts.OrderRevision,source.EventId,facts.UpdatedAtUtc).ToCommonValues()).ConfigureAwait(false);
        if (facts.Composition is { } composition)
            await WriteAsync(transaction,PortfolioDbSql.Orders.UpsertComposition,token,composition.WorkflowId,composition.OrderId,composition.PortfolioId,composition.FundId,composition.Status,
                PortfolioProjection<FundCompositionWorkflowProjectionReadModel>.Create(composition,facts.OrderRevision,source.EventId,facts.UpdatedAtUtc).ToCommonValues()).ConfigureAwait(false);
    }

    static PortfolioProjection<T> Row<T>(T value,IEvent source,PortfolioProjectionFacts facts)
        => PortfolioProjection<T>.Create(value,facts.Revision,source.EventId,facts.UpdatedAtUtc);

    static Task<int> WriteAsync(IEnlistedPostgresTransaction transaction,string sql,CancellationToken token,params object?[] values)
        => transaction.ExecuteAsync(sql,(Npgsql.NpgsqlParameter[])values.ToPortfolioParameters().Bind(),token);
}
