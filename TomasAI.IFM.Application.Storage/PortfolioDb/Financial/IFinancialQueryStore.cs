using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public interface IFinancialQueryStore
{
    Task<FinancialRead<FinancialLedgerConfiguration>> ReadAsync(FinancialReadScope scope, GetFinancialLedgerConfigurationRequest request, CancellationToken token = default)
        => throw new NotSupportedException();
    Task<FinancialRead<FinancialPostingConfiguration>> ReadAsync(FinancialReadScope scope, GetFinancialPostingConfigurationRequest request, CancellationToken token = default)
        => throw new NotSupportedException();
    Task<FinancialRead<FundRiskAuthorizationEvidence>> ReadAsync(FinancialReadScope scope, GetFundRiskAuthorizationRequest request, CancellationToken token = default)
        => throw new NotSupportedException();
    Task<FinancialRead<FinancialAdmissionSnapshot>> ReadAsync(FinancialReadScope scope, GetFinancialAdmissionSnapshotRequest request, CancellationToken token = default);
    Task<FinancialRead<FinancialOperationOutcome>> ReadAsync(FinancialReadScope scope, GetPostingReceiptRequest request, CancellationToken token = default);
    Task<FinancialRead<FinancialJournal>> ReadAsync(FinancialReadScope scope, GetJournalRequest request, CancellationToken token = default);
    Task<FinancialRead<FinancialBalanceSnapshot>> ReadAsync(FinancialReadScope scope, GetAccountBalancesRequest request, CancellationToken token = default);
    Task<FinancialRead<FinancialTrialBalance>> ReadAsync(FinancialReadScope scope, GetTrialBalanceRequest request, CancellationToken token = default);
    Task<FinancialRead<FinancialReservationView>> ReadAsync(FinancialReadScope scope, GetCapacityReservationRequest request, CancellationToken token = default);
    Task<FinancialRead<FinancialCapacityUsage>> ReadAsync(FinancialReadScope scope, GetCapacityUsageRequest request, CancellationToken token = default);
    Task<FinancialRead<FinancialPage<FinancialTransactionRow>>> ReadAsync(FinancialReadScope scope, GetFundTransactionsPageRequest request, CancellationToken token = default);
    Task<FinancialRead<FinancialPage<FinancialReservationView>>> ReadAsync(FinancialReadScope scope, GetFundReservationsPageRequest request, CancellationToken token = default);
    Task<FinancialRead<FinancialReconciliationView>> ReadAsync(FinancialReadScope scope, GetReconciliationRequest request, CancellationToken token = default);
}
