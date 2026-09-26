using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage;
using Npgsql;
using NpgsqlTypes;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public interface IFinancialHistoryProjection
{
    Task ApplyAsync(IFinancialCompletedEvent completed,CancellationToken token=default);
}

/// <summary>Rebuildable monthly history. It never supplies financial spending authority.</summary>
public sealed class FinancialHistoryProjection(IDbContextFactory factory) : IFinancialHistoryProjection
{
    public const string CreateTable=PortfolioDbSql.Financial.FinancialHistoryProjection.Create01;
    const string Insert=PortfolioDbSql.Financial.FinancialHistoryProjection.Insert01;

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
        return factory.PortfolioDb.Use("PortfolioFinancial.ProjectHistory",Insert).SetParameters(new PortfolioParameters(
            [new() { Value = completed.PortfolioId },
             new() { Value = completed.CommittedAtUtc.Year * 100 + completed.CommittedAtUtc.Month },
             new() { Value = revision },
             new() { Value = completed.OperationId },
             new() { Value = completed.EventId },
             new() { Value = completed.GetType().FullName ?? completed.GetType().Name },
             new() { Value = completed.CommittedAtUtc },
             new() { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = completed.ToEventData() }])).ExecuteCommandAsync(token);
    }
}
