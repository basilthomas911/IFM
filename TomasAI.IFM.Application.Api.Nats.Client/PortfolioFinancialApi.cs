using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Uses standard typed NATS transport without nested serialization or replacement operation identities.</summary>
public sealed class PortfolioFinancialApi(IActorProducer producer):NatsClientApi(producer),IPortfolioFinancialApi,IPortfolioOrderCompositionApi
{
    public ValueTask<ServiceResult<FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent>>> EvaluateAsync(
        EvaluatePortfolioOrderCompositionCommand request,CancellationToken cancellationToken=default)
        =>RequestFunctionAsync<EvaluatePortfolioOrderCompositionCommand,FinancialExecutionId,
            FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent>>(
                request,request.EntityId,cancellationToken);
    public ValueTask<ServiceResult<FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,PortfolioCloseOrderCompositionFailedEvent>>> EvaluateCloseAsync(
        EvaluatePortfolioCloseOrderCompositionCommand request,CancellationToken cancellationToken=default)
        =>RequestFunctionAsync<EvaluatePortfolioCloseOrderCompositionCommand,FinancialExecutionId,
            FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,PortfolioCloseOrderCompositionFailedEvent>>(
                request,request.EntityId,cancellationToken);
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
        var entity = new LedgerPortfolioId(scope.PortfolioId);
        var subject = new ActorSubject(ActorType.Query, actor, verb, entity.Format());
        var correlationId = PortfolioRequestCorrelation.CurrentOrNew();
        var requestedAtUtc = DateTime.UtcNow;
        object query = parameters switch
        {
            PrepareFinancialAuthorityRequest value => new PrepareFinancialAuthorityQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            PrepareFinancialBookRequest value => new PrepareFinancialBookQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            GetFinancialLedgerConfigurationRequest value => new GetFinancialLedgerConfigurationQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            GetFinancialPostingConfigurationRequest value => new GetFinancialPostingConfigurationQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            GetFundRiskAuthorizationRequest value => new GetFundRiskAuthorizationQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            GetFinancialAdmissionSnapshotRequest value => new GetFinancialAdmissionSnapshotQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            GetPostingReceiptRequest value => new GetPostingReceiptQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            GetJournalRequest value => new GetJournalQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            GetAccountBalancesRequest value => new GetAccountBalancesQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            GetTrialBalanceRequest value => new GetTrialBalanceQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            GetFundTransactionsPageRequest value => new GetFundTransactionsPageQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            GetReconciliationRequest value => new GetReconciliationQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            GetCapacityReservationRequest value => new GetCapacityReservationQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            GetCapacityUsageRequest value => new GetCapacityUsageQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            GetFundReservationsPageRequest value => new GetFundReservationsPageQuery(value) { Subject = subject, EntityId = entity, Scope = scope, CorrelationId = correlationId, RequestedAtUtc = requestedAtUtc },
            _ => throw new InvalidOperationException($"Unsupported Portfolio financial query parameters {typeof(TRequest).FullName}."),
        };
        var result = query switch
        {
            PrepareFinancialAuthorityQuery value => (object)await RequestAsync<PrepareFinancialAuthorityQuery, FinancialRead<FinancialAuthorityDraft>>(subject, value, token).ConfigureAwait(false),
            PrepareFinancialBookQuery value => (object)await RequestAsync<PrepareFinancialBookQuery, FinancialRead<FinancialBookSetup>>(subject, value, token).ConfigureAwait(false),
            GetFinancialLedgerConfigurationQuery value => (object)await RequestAsync<GetFinancialLedgerConfigurationQuery, FinancialRead<FinancialLedgerConfiguration>>(subject, value, token).ConfigureAwait(false),
            GetFinancialPostingConfigurationQuery value => (object)await RequestAsync<GetFinancialPostingConfigurationQuery, FinancialRead<FinancialPostingConfiguration>>(subject, value, token).ConfigureAwait(false),
            GetFundRiskAuthorizationQuery value => (object)await RequestAsync<GetFundRiskAuthorizationQuery, FinancialRead<FundRiskAuthorizationEvidence>>(subject, value, token).ConfigureAwait(false),
            GetFinancialAdmissionSnapshotQuery value => (object)await RequestAsync<GetFinancialAdmissionSnapshotQuery, FinancialRead<FinancialAdmissionSnapshot>>(subject, value, token).ConfigureAwait(false),
            GetPostingReceiptQuery value => (object)await RequestAsync<GetPostingReceiptQuery, FinancialRead<FinancialOperationOutcome>>(subject, value, token).ConfigureAwait(false),
            GetJournalQuery value => (object)await RequestAsync<GetJournalQuery, FinancialRead<FinancialJournal>>(subject, value, token).ConfigureAwait(false),
            GetAccountBalancesQuery value => (object)await RequestAsync<GetAccountBalancesQuery, FinancialRead<FinancialBalanceSnapshot>>(subject, value, token).ConfigureAwait(false),
            GetTrialBalanceQuery value => (object)await RequestAsync<GetTrialBalanceQuery, FinancialRead<FinancialTrialBalance>>(subject, value, token).ConfigureAwait(false),
            GetFundTransactionsPageQuery value => (object)await RequestAsync<GetFundTransactionsPageQuery, FinancialRead<FinancialPage<FinancialTransactionRow>>>(subject, value, token).ConfigureAwait(false),
            GetReconciliationQuery value => (object)await RequestAsync<GetReconciliationQuery, FinancialRead<FinancialReconciliationView>>(subject, value, token).ConfigureAwait(false),
            GetCapacityReservationQuery value => (object)await RequestAsync<GetCapacityReservationQuery, FinancialRead<FinancialReservationView>>(subject, value, token).ConfigureAwait(false),
            GetCapacityUsageQuery value => (object)await RequestAsync<GetCapacityUsageQuery, FinancialRead<FinancialCapacityUsage>>(subject, value, token).ConfigureAwait(false),
            GetFundReservationsPageQuery value => (object)await RequestAsync<GetFundReservationsPageQuery, FinancialRead<FinancialPage<FinancialReservationView>>>(subject, value, token).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported Portfolio financial query {query.GetType().FullName}."),
        };
        return (ServiceResult<FinancialRead<TResult>>)result;
    }
}
