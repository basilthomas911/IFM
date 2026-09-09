using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Uses standard typed NATS transport without nested serialization or replacement operation identities.</summary>
public sealed class PortfolioFinancialApi(IActorProducer producer):NatsClientApi(producer),IPortfolioFinancialApi
{
    public Task<ServiceResult<FinancialRead<FinancialAuthorityDraft>>> PrepareFinancialAuthorityAsync(FinancialReadScope scope,PrepareFinancialAuthorityRequest request,CancellationToken token=default)
        =>Read<PrepareFinancialAuthorityRequest,FinancialAuthorityDraft>(scope,request,"GeneralLedgerQuery","PrepareFinancialAuthority",token);
    public Task<ServiceResult<FinancialRead<FinancialBookSetup>>> PrepareFinancialBookAsync(FinancialReadScope scope,PrepareFinancialBookRequest request,CancellationToken token=default)
        =>Read<PrepareFinancialBookRequest,FinancialBookSetup>(scope,request,"GeneralLedgerQuery","PrepareFinancialBook",token);
    public Task<ServiceResult<FinancialRead<FinancialLedgerConfiguration>>> GetFinancialLedgerConfigurationAsync(FinancialReadScope scope,GetFinancialLedgerConfigurationRequest request,CancellationToken token=default)
        =>Read<GetFinancialLedgerConfigurationRequest,FinancialLedgerConfiguration>(scope,request,"GeneralLedgerQuery","GetFinancialLedgerConfiguration",token);
    public Task<ServiceResult<FinancialRead<FinancialPostingConfiguration>>> GetFinancialPostingConfigurationAsync(FinancialReadScope scope,GetFinancialPostingConfigurationRequest request,CancellationToken token=default)
        =>Read<GetFinancialPostingConfigurationRequest,FinancialPostingConfiguration>(scope,request,"GeneralLedgerQuery","GetFinancialPostingConfiguration",token);
    public Task<ServiceResult<FinancialRead<FundRiskAuthorizationEvidence>>> GetFundRiskAuthorizationAsync(FinancialReadScope scope, GetFundRiskAuthorizationRequest request, CancellationToken token=default)
        => Read<GetFundRiskAuthorizationRequest,FundRiskAuthorizationEvidence>(scope,request,"GeneralLedgerQuery","GetFundRiskAuthorization",token);
    public Task<ServiceResult<FinancialRead<FinancialAdmissionSnapshot>>> GetFinancialAdmissionSnapshotAsync(FinancialReadScope scope,GetFinancialAdmissionSnapshotRequest request,CancellationToken token=default)
        =>Read<GetFinancialAdmissionSnapshotRequest,FinancialAdmissionSnapshot>(scope,request,"CapacityReservationQuery","GetFinancialAdmissionSnapshot",token);
    public Task<ServiceResult<FinancialRead<FinancialOperationOutcome>>> GetPostingReceiptAsync(FinancialReadScope scope,GetPostingReceiptRequest request,CancellationToken token=default)
        =>Read<GetPostingReceiptRequest,FinancialOperationOutcome>(scope,request,"GeneralLedgerQuery","GetPostingReceipt",token);
    public Task<ServiceResult<FinancialRead<FinancialJournal>>> GetJournalAsync(FinancialReadScope scope,GetJournalRequest request,CancellationToken token=default)
        =>Read<GetJournalRequest,FinancialJournal>(scope,request,"GeneralLedgerQuery","GetJournal",token);
    public Task<ServiceResult<FinancialRead<FinancialBalanceSnapshot>>> GetAccountBalancesAsync(FinancialReadScope scope,GetAccountBalancesRequest request,CancellationToken token=default)
        =>Read<GetAccountBalancesRequest,FinancialBalanceSnapshot>(scope,request,"GeneralLedgerQuery","GetAccountBalances",token);
    public Task<ServiceResult<FinancialRead<FinancialTrialBalance>>> GetTrialBalanceAsync(FinancialReadScope scope,GetTrialBalanceRequest request,CancellationToken token=default)
        =>Read<GetTrialBalanceRequest,FinancialTrialBalance>(scope,request,"GeneralLedgerQuery","GetTrialBalance",token);
    public Task<ServiceResult<FinancialRead<FinancialPage<FinancialTransactionRow>>>> GetFundTransactionsPageAsync(FinancialReadScope scope,GetFundTransactionsPageRequest request,CancellationToken token=default)
        =>Read<GetFundTransactionsPageRequest,FinancialPage<FinancialTransactionRow>>(scope,request,"GeneralLedgerQuery","GetFundTransactionsPage",token);
    public Task<ServiceResult<FinancialRead<FinancialReconciliationView>>> GetReconciliationAsync(FinancialReadScope scope,GetReconciliationRequest request,CancellationToken token=default)
        =>Read<GetReconciliationRequest,FinancialReconciliationView>(scope,request,"GeneralLedgerQuery","GetReconciliation",token);
    public Task<ServiceResult<FinancialRead<FinancialReservationView>>> GetCapacityReservationAsync(FinancialReadScope scope,GetCapacityReservationRequest request,CancellationToken token=default)
        =>Read<GetCapacityReservationRequest,FinancialReservationView>(scope,request,"CapacityReservationQuery","GetCapacityReservation",token);
    public Task<ServiceResult<FinancialRead<FinancialCapacityUsage>>> GetCapacityUsageAsync(FinancialReadScope scope,GetCapacityUsageRequest request,CancellationToken token=default)
        =>Read<GetCapacityUsageRequest,FinancialCapacityUsage>(scope,request,"CapacityReservationQuery","GetCapacityUsage",token);
    public Task<ServiceResult<FinancialRead<FinancialPage<FinancialReservationView>>>> GetFundReservationsPageAsync(FinancialReadScope scope,GetFundReservationsPageRequest request,CancellationToken token=default)
        =>Read<GetFundReservationsPageRequest,FinancialPage<FinancialReservationView>>(scope,request,"CapacityReservationQuery","GetFundReservationsPage",token);
    public ValueTask<ServiceResult<GuidResult>> SubmitEmulatorOrderAsync(SubmitEmulatorOrderCommand request,CancellationToken token=default)
        =>RequestCommandResultAsync<SubmitEmulatorOrderCommand,LedgerPortfolioId,GuidResult>(request,request.EntityId,token);
    public ValueTask<ServiceResult<GuidResult>> PostAsync(PostFundTransactionCommand request,CancellationToken token=default)
        =>RequestCommandResultAsync<PostFundTransactionCommand,LedgerPortfolioId,GuidResult>(request,request.EntityId,token);
    public ValueTask<ServiceResult<GuidResult>> ConfigureAsync(ConfigureLedgerCommand request,CancellationToken token=default)
        =>RequestCommandResultAsync<ConfigureLedgerCommand,LedgerPortfolioId,GuidResult>(request,request.EntityId,token);
    public ValueTask<ServiceResult<GuidResult>> PostBatchAsync(PostFundTransactionsCommand request,CancellationToken token=default)
        =>RequestCommandResultAsync<PostFundTransactionsCommand,LedgerPortfolioId,GuidResult>(request,request.EntityId,token);
    public ValueTask<ServiceResult<GuidResult>> ChangeAsync(ChangeCapacityReservationCommand request,CancellationToken token=default)
        =>RequestCommandResultAsync<ChangeCapacityReservationCommand,CapacityReservationEntityId,GuidResult>(request,request.EntityId,token);
    public ValueTask<ServiceResult<FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>>> ReserveAsync(ReservePortfolioTradeRiskCommand request,CancellationToken token=default)
        =>RequestFunctionAsync<ReservePortfolioTradeRiskCommand,FinancialExecutionId,FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>>(request,request.EntityId,token);
    public ValueTask<ServiceResult<FunctionResult<CapacityConsumptionCompletedEvent,CapacityConsumptionFailedEvent>>> ConsumeAsync(ConsumeCapacityReservationCommand request,CancellationToken token=default)
        =>RequestFunctionAsync<ConsumeCapacityReservationCommand,FinancialExecutionId,FunctionResult<CapacityConsumptionCompletedEvent,CapacityConsumptionFailedEvent>>(request,request.EntityId,token);
    async Task<ServiceResult<FinancialRead<TResult>>> Read<TRequest,TResult>(FinancialReadScope scope,TRequest parameters,string actor,string verb,CancellationToken token)
        where TResult:class
    {
        var entity=new LedgerPortfolioId(scope.PortfolioId);
        var query=new FinancialQuery<TRequest,TResult> { Subject=new(ActorType.Query,actor,verb,entity.Format()),QueryEntityId=entity,
            Scope=scope,Parameters=parameters,CorrelationId=PortfolioRequestCorrelation.CurrentOrNew(),RequestedAtUtc=DateTime.UtcNow };
        return await RequestAsync<FinancialQuery<TRequest,TResult>,FinancialRead<TResult>>(query.Subject,query,token);
    }
}
