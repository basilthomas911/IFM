using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Typed financial operations; callers retain the exact request and OperationId until the receipt is reconciled.</summary>
public interface IPortfolioFinancialApi
{
    Task<ServiceResult<FinancialRead<FinancialAuthorityDraft>>> PrepareFinancialAuthorityAsync(FinancialReadScope scope,PrepareFinancialAuthorityRequest request,CancellationToken token=default)
        => throw new NotSupportedException("Financial authority preparation is unavailable.");
    Task<ServiceResult<FinancialRead<FinancialBookSetup>>> PrepareFinancialBookAsync(FinancialReadScope scope,PrepareFinancialBookRequest request,CancellationToken token=default)
        => throw new NotSupportedException("Ledger book preparation is unavailable.");
    Task<ServiceResult<FinancialRead<FinancialLedgerConfiguration>>> GetFinancialLedgerConfigurationAsync(FinancialReadScope scope,GetFinancialLedgerConfigurationRequest request,CancellationToken token=default)
        => throw new NotSupportedException("Ledger configuration is unavailable.");
    Task<ServiceResult<FinancialRead<FinancialPostingConfiguration>>> GetFinancialPostingConfigurationAsync(FinancialReadScope scope,GetFinancialPostingConfigurationRequest request,CancellationToken token=default)
        => throw new NotSupportedException("Posting configuration is unavailable.");
    Task<ServiceResult<FinancialRead<FundRiskAuthorizationEvidence>>> GetFundRiskAuthorizationAsync(FinancialReadScope scope, GetFundRiskAuthorizationRequest request, CancellationToken token=default)
        => throw new NotSupportedException("Fund financial authorization queries are unavailable.");
    Task<ServiceResult<FinancialRead<FinancialAdmissionSnapshot>>> GetFinancialAdmissionSnapshotAsync(FinancialReadScope scope,GetFinancialAdmissionSnapshotRequest request,CancellationToken token=default);
    Task<ServiceResult<FinancialRead<FinancialOperationOutcome>>> GetPostingReceiptAsync(FinancialReadScope scope,GetPostingReceiptRequest request,CancellationToken token=default);
    Task<ServiceResult<FinancialRead<FinancialJournal>>> GetJournalAsync(FinancialReadScope scope,GetJournalRequest request,CancellationToken token=default);
    Task<ServiceResult<FinancialRead<FinancialBalanceSnapshot>>> GetAccountBalancesAsync(FinancialReadScope scope,GetAccountBalancesRequest request,CancellationToken token=default);
    Task<ServiceResult<FinancialRead<FinancialTrialBalance>>> GetTrialBalanceAsync(FinancialReadScope scope,GetTrialBalanceRequest request,CancellationToken token=default);
    Task<ServiceResult<FinancialRead<FinancialPage<FinancialTransactionRow>>>> GetFundTransactionsPageAsync(FinancialReadScope scope,GetFundTransactionsPageRequest request,CancellationToken token=default);
    Task<ServiceResult<FinancialRead<FinancialReconciliationView>>> GetReconciliationAsync(FinancialReadScope scope,GetReconciliationRequest request,CancellationToken token=default);
    Task<ServiceResult<FinancialRead<FinancialReservationView>>> GetCapacityReservationAsync(FinancialReadScope scope,GetCapacityReservationRequest request,CancellationToken token=default);
    Task<ServiceResult<FinancialRead<FinancialCapacityUsage>>> GetCapacityUsageAsync(FinancialReadScope scope,GetCapacityUsageRequest request,CancellationToken token=default);
    Task<ServiceResult<FinancialRead<FinancialPage<FinancialReservationView>>>> GetFundReservationsPageAsync(FinancialReadScope scope,GetFundReservationsPageRequest request,CancellationToken token=default);
    ValueTask<ServiceResult<GuidResult>> SubmitEmulatorOrderAsync(SubmitEmulatorOrderCommand request,CancellationToken token=default)
        => throw new NotSupportedException("Execution emulator is unavailable.");
    ValueTask<ServiceResult<GuidResult>> PostAsync(PostFundTransactionCommand request,CancellationToken token=default);
    ValueTask<ServiceResult<GuidResult>> ConfigureAsync(ConfigureLedgerCommand request,CancellationToken token=default);
    ValueTask<ServiceResult<GuidResult>> PostBatchAsync(PostFundTransactionsCommand request,CancellationToken token=default);
    ValueTask<ServiceResult<GuidResult>> ChangeAsync(ChangeCapacityReservationCommand request,CancellationToken token=default);
    ValueTask<ServiceResult<FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>>> ReserveAsync(ReservePortfolioTradeRiskCommand request,CancellationToken token=default);
    ValueTask<ServiceResult<FunctionResult<CapacityConsumptionCompletedEvent,CapacityConsumptionFailedEvent>>> ConsumeAsync(ConsumeCapacityReservationCommand request,CancellationToken token=default);
}
