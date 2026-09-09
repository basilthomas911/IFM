using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public interface IFinancialHistoryProjection
{
    Task ApplyAsync(IFinancialCompletedEvent completed,CancellationToken token=default);
}

/// <summary>Rebuildable monthly history. It never supplies financial spending authority.</summary>
public sealed class FinancialHistoryProjection(IDbContextFactory factory) : IFinancialHistoryProjection
{
    public const string CreateTable="""
        CREATE TABLE IF NOT EXISTS financial_operation_by_portfolio_month(
          portfolioId int, month int, financialRevision bigint, operationId uuid, sourceEventId bigint,
          eventType text, committedAtUtc timestamp, payloadJson text,
          PRIMARY KEY((portfolioId,month),financialRevision,operationId))
          WITH CLUSTERING ORDER BY(financialRevision DESC,operationId ASC);
        """;
    const string Insert="""
        INSERT INTO financial_operation_by_portfolio_month(portfolioId,month,financialRevision,operationId,sourceEventId,eventType,committedAtUtc,payloadJson)
        VALUES(?,?,?,?,?,?,?,?) USING TIMESTAMP ?;
        """;

    public Task ApplyAsync(IFinancialCompletedEvent completed,CancellationToken token=default)
    {
        var revision=completed switch
        {
            EmulatorOrderSubmittedEvent x=>x.Receipt.FinancialRevision,
            LedgerPostingCompletedEvent x=>x.Receipt.FinancialRevision,
            LedgerPostingBatchCompletedEvent x=>x.Receipt.FinancialRevision,
            CapacityReservationCompletedEvent x=>x.Receipt.FinancialRevision,
            CapacityConsumptionCompletedEvent x=>x.Receipt.FinancialRevision,
            CapacityLifecycleCompletedEvent x=>x.Receipt.FinancialRevision,
            LedgerConfigurationCompletedEvent x=>x.Receipt.FinancialRevision,
            _=>throw new InvalidOperationException("Unsupported financial history event.")
        };
        if(completed.EventId<=0 || revision<=0 || completed.CommittedAtUtc.Kind!=DateTimeKind.Utc)
            throw new InvalidOperationException("Only committed financial events can enter history.");
        return factory.PortfolioDb.Use("PortfolioFinancial.ProjectHistory",Insert).SetParameters(new HistoryValues(
            [completed.PortfolioId,completed.CommittedAtUtc.Year*100+completed.CommittedAtUtc.Month,revision,completed.OperationId,
             completed.EventId,completed.GetType().FullName,completed.CommittedAtUtc,completed.ToEventData(),completed.EventId])).ExecuteCommandAsync(token);
    }
    readonly record struct HistoryValues(object?[] Values) : IBindValue { public object Bind()=>Values; }
}
