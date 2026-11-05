using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using static TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialRepositorySupport;

namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Identifies prepared numeric IDs and the Fund command permitted to use them.</summary>
public sealed record PortfolioPreparedIdentifiers(
    PortfolioFundId FundId, Guid CommandId, PortfolioAccessContext Access, int OrderId, IReadOnlyList<int> TradeIds);

internal static class PortfolioPreparedIdentifierFence
{
    internal static async Task VerifyAsync(EnlistedEventTransaction db, PortfolioPreparedIdentifiers evidence, CancellationToken token)
    {
        var expected = new[] { ReceiptFor(evidence, PortfolioBusinessIdentityKind.Order, 0, evidence.OrderId) }
            .Concat(evidence.TradeIds.Select((id, index) => ReceiptFor(evidence, PortfolioBusinessIdentityKind.Trade, index, id)))
            .ToDictionary(receipt => receipt.Stream, StringComparer.Ordinal);
        var committed = await db.QueryAsync(PortfolioDbSql.Financial.ReadPreparedIdentifierReceipts, [expected.Keys.ToArray()],
            row => (Stream: row.GetString(0), Event: new EventLogReadModel(row.GetInt64(1), row.GetString(2), row.GetString(3),
                row.GetInt64(4), row.GetFieldValue<byte[]>(5), row.GetGuid(6), row.GetString(7), row.GetInt64(8))), token).ConfigureAwait(false);
        Require(committed.Count == expected.Count, FinancialReasons.AuthorityDenied,
            "The Fund command requires a committed receipt for each prepared identifier.");
        foreach (var (stream, storedEvent) in committed)
        {
            var expectedReceipt = expected[stream];
            Require(storedEvent.ToDomainEvent() is PortfolioBusinessIdAllocationCompletedEvent receipt
                    && receipt.CommandId == expectedReceipt.OperationId
                    && receipt.Value is { } allocation && allocation.Kind == expectedReceipt.Kind && allocation.Value == expectedReceipt.Value
                    && string.Equals(receipt.InputHash, expectedReceipt.InputHash, StringComparison.Ordinal),
                FinancialReasons.AuthorityDenied, "A prepared Fund identifier does not match its committed allocation receipt.");
        }
    }

    static (string Stream, Guid OperationId, PortfolioBusinessIdentityKind Kind, int Value, string InputHash) ReceiptFor(
        PortfolioPreparedIdentifiers evidence, PortfolioBusinessIdentityKind kind, int index, int id)
    {
        var operationId = FundOrderPreparation.AllocationOperationId(evidence.FundId, evidence.CommandId, kind, index);
        var inputHash = FinancialCanonicalHash.Compute(new { Kind = kind, evidence.Access });
        return (AllocatePortfolioBusinessIdCommand.ForOperation(operationId).StreamId, operationId, kind, id, inputHash);
    }
}
