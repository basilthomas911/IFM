
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialRepositorySupport;

namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Verifies financial Fund approval inside the Portfolio configuration/event transaction.</summary>
internal static class FundRiskAuthorizationStore
{
    internal static async Task ValidateAsync(EnlistedEventTransaction db, int portfolioId, int? fundId,
        FundRiskAuthorizationReference value, FinancialBookConfiguration book, string operatingState, CancellationToken token)
    {
        value.Validate();
        Require(value.PortfolioId == portfolioId && value.FundId == fundId && string.Equals(operatingState,"Active",StringComparison.Ordinal) &&
            value.AuthorityEpoch == book.AuthorityEpoch && DateTime.UtcNow < value.ValidUntilUtc,
            FinancialReasons.AuthorityRevoked, "Fund financial authorization is no longer current.");
        await ValidateFundSourcesAsync(db, book, value.FundId, true, token).ConfigureAwait(false);
        var reservations = await (db.QueryAsync(PortfolioDbSql.Financial.FundRiskAuthorizationStore.Select01, [portfolioId, value.ReservationId], r => (Status: r.GetInt32(0), Version: r.GetInt64(1),
                Request: Decode<CapacityReservationRequest>(r.GetString(2)), Receipt: Decode<CapacityReservationReceipt>(r.GetString(3))), token)).ConfigureAwait(false);
        Require(reservations.Count == 1, FinancialReasons.AuthorityDenied, "Committed capacity reservation is required.");
        var row = reservations[0]; var receipt = row.Receipt; var request = row.Request;
        Require(row.Status == (int)ReservationStatus.Reserved && row.Version == 1 &&
            receipt.FundId == value.FundId && receipt.OrderId == value.OrderId && request.WorkflowId == value.WorkflowId &&
            request.RiskInvocationId == value.RiskInvocationId && receipt.RiskResultId == value.RiskResultId &&
            string.Equals(receipt.CompositionResultHash,value.CompositionResultHash,StringComparison.Ordinal) && string.Equals(receipt.UnitCandidateHash,value.UnitCandidateHash,StringComparison.Ordinal) &&
            string.Equals(receipt.RiskAssessmentHash,value.RiskAssessmentHash,StringComparison.Ordinal) && string.Equals(receipt.SizedOrderHash,value.SizedOrderHash,StringComparison.Ordinal) &&
            receipt.StrategyUnits == value.StrategyUnits && string.Equals(receipt.Requirements.ContentHash,value.RequirementsHash,StringComparison.Ordinal) &&
            receipt.CompletedEventId == value.ReservationCompletedEventId && receipt.OperationId == value.ReservationOperationId &&
            receipt.FinancialRevision == value.FinancialRevision && receipt.AuthorityEpoch == value.AuthorityEpoch &&
            receipt.ValidUntilUtc == value.ValidUntilUtc && string.Equals(receipt.ExecutionEnvironment,value.ExecutionEnvironment,StringComparison.Ordinal),
            FinancialReasons.RequestMismatch, "Fund authorization differs from its current committed capacity grant.");
        var committed = await ReadOperationAsync<CapacityReservationCompletedEvent>(db, portfolioId,
            receipt.OperationId, receipt.InputHash, token).ConfigureAwait(false);
        Require(committed?.Id == receipt.CompletedEventId, FinancialReasons.AuthorityDenied,
            "Reservation receipt and committed event do not agree.");
    }
}
