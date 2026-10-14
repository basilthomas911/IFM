using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;

using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

using TomasAI.IFM.Shared.Extensions;





namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Rebuildable monthly history. It never supplies financial spending authority.</summary>
public sealed class FinancialHistoryProjection(IDbContextFactory factory) : IFinancialHistoryProjection
{
    public const string CreateTable = PortfolioSchemaSql.FinancialHistory;

    public Task ApplyAsync(IFinancialCompletedEvent completed, CancellationToken token = default)
    {
        var revision = completed switch
        {
            PortfolioOrderCompositionCompletedEvent x => x.Receipt.FinancialRevision,
            PortfolioCloseOrderCompositionCompletedEvent x => x.Receipt.FinancialRevision,
            EmulatorOrderSubmittedEvent x => x.Receipt.FinancialRevision,
            LedgerPostingCompletedEvent x => x.Receipt.FinancialRevision,
            LedgerPostingBatchCompletedEvent x => x.Receipt.FinancialRevision,
            CapacityReservationCompletedEvent x => x.Receipt.FinancialRevision,
            CapacityConsumptionCompletedEvent x => x.Receipt.FinancialRevision,
            CapacityLifecycleCompletedEvent x => x.Receipt.FinancialRevision,
            LedgerConfigurationCompletedEvent x => x.Receipt.FinancialRevision,
            _ => throw new InvalidOperationException("Unsupported financial history event.")
        };
        if (completed.EventId <= 0 || revision <= 0 || completed.CommittedAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Only committed financial events can enter history.");
        return factory.PortfolioDb.DbWriter.SaveFinancialHistoryAsync(completed.PortfolioId,
            completed.CommittedAtUtc.Year * 100 + completed.CommittedAtUtc.Month, revision,
            completed.OperationId, completed.EventId, completed.GetType().FullName ?? completed.GetType().Name,
            completed.CommittedAtUtc, completed.ToEventData(), token);
    }
}

