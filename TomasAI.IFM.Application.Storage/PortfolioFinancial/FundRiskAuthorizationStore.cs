using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialDbContext;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Verifies financial Fund approval inside the Portfolio configuration/event transaction.</summary>
internal static class FundRiskAuthorizationStore
{
    internal static async Task ValidateAsync(EnlistedEventTransaction db, int portfolioId, int? fundId,
        FundRiskAuthorizationReference value, FinancialBookConfiguration book, string operatingState, CancellationToken token)
    {
        value.Validate();
        Require(value.PortfolioId == portfolioId && value.FundId == fundId && operatingState == "Active" &&
            value.AuthorityEpoch == book.AuthorityEpoch && DateTime.UtcNow < value.ValidUntilUtc,
            FinancialReasons.AuthorityRevoked, "Fund financial authorization is no longer current.");
        await ValidateFundSourcesAsync(db, book, value.FundId, true, token).ConfigureAwait(false);
        var reservations = await db.QueryAsync("""
            SELECT status,version,request::text,receipt::text FROM portfolio_financial.capacity_reservation
            WHERE portfolio_id=$1 AND reservation_id=$2;
            """, [portfolioId, value.ReservationId], r => (Status:r.GetInt32(0), Version:r.GetInt64(1),
                Request:Decode<CapacityReservationRequest>(r.GetString(2)), Receipt:Decode<CapacityReservationReceipt>(r.GetString(3))), token);
        Require(reservations.Count == 1, FinancialReasons.AuthorityDenied, "Committed capacity reservation is required.");
        var row = reservations[0]; var receipt = row.Receipt; var request = row.Request;
        Require(row.Status == (int)ReservationStatus.Reserved && row.Version == 1 &&
            receipt.FundId == value.FundId && receipt.OrderId == value.OrderId && request.WorkflowId == value.WorkflowId &&
            request.RiskInvocationId == value.RiskInvocationId && receipt.RiskResultId == value.RiskResultId &&
            receipt.CompositionResultHash == value.CompositionResultHash && receipt.UnitCandidateHash == value.UnitCandidateHash &&
            receipt.RiskAssessmentHash == value.RiskAssessmentHash && receipt.SizedOrderHash == value.SizedOrderHash &&
            receipt.StrategyUnits == value.StrategyUnits && receipt.Requirements.ContentHash == value.RequirementsHash &&
            receipt.CompletedEventId == value.ReservationCompletedEventId && receipt.OperationId == value.ReservationOperationId &&
            receipt.FinancialRevision == value.FinancialRevision && receipt.AuthorityEpoch == value.AuthorityEpoch &&
            receipt.ValidUntilUtc == value.ValidUntilUtc && receipt.ExecutionEnvironment == value.ExecutionEnvironment,
            FinancialReasons.RequestMismatch, "Fund authorization differs from its current committed capacity grant.");
        var committed = await ReadOperationAsync<CapacityReservationCompletedEvent>(db, portfolioId,
            receipt.OperationId, receipt.InputHash, token).ConfigureAwait(false);
        Require(committed?.Id == receipt.CompletedEventId, FinancialReasons.AuthorityDenied,
            "Reservation receipt and committed event do not agree.");
    }
}
