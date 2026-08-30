using FluentAssertions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

public sealed class PortfolioFundCommandClientTests
{
    [Fact]
    [Trait("Gate", "PF-10")]
    [Trait("Gate", "PF-12")]
    [Trait("Category", "Portfolio")]
    public async Task Reservation_uses_command_ack_then_returns_the_matching_typed_projection_with_original_ids()
    {
        var workflowId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        var request = new ReserveFundOrderCompositionRequest
        {
            WorkflowId = workflowId,
            PortfolioId = 101,
            FundId = 202,
            IdempotencyKey = idempotencyKey,
        };
        var producer = new AcknowledgingProducer();
        var queries = new ReservationQueryStub(
            [
                new() { WorkflowId = workflowId, PortfolioId = 101, FundId = 202, OrderId = 300 },
                new() { WorkflowId = workflowId, PortfolioId = 101, FundId = 202, OrderId = 301 },
            ],
            new Dictionary<int, FundOrderProjectionReadModel>
            {
                [300] = new() { PortfolioId = 101, FundId = 202, OrderId = 300, IdempotencyKey = Guid.NewGuid() },
                [301] = new() { PortfolioId = 101, FundId = 202, OrderId = 301, IdempotencyKey = idempotencyKey, AggregateVersion = 4, CanonicalRequestHash = "hash", CreatedOnUtc = DateTime.UtcNow },
            },
            [new() { PortfolioId = 101, FundId = 202, OrderId = 301, TradeId = 401, LegOrdinal = 1 }]);
        var client = new PortfolioFundCommandApi(producer, queries);

        var result = await client.ReserveCompositionAsync(request, new PortfolioFundStrategySnapshot());

        result.Success.Should().BeTrue();
        result.Value!.Order.OrderId.Should().Be(301);
        result.Value.Trades.Should().ContainSingle().Which.TradeId.Should().Be(401);
        result.Value.Disposition.Should().Be(ReservationDisposition.IdempotentReplay);
        producer.ResultType.Should().Be<GuidResult>();
        producer.Subject.Name.Should().Be(PortfolioCommandSubjects.FundActor);
        producer.Subject.Verb.Should().Be("ReserveFundOrderComposition");
    }

    sealed class AcknowledgingProducer : IActorProducer
    {
        public Type? ResultType { get; private set; }
        public ActorSubject Subject { get; private set; }
        public bool IsRunning => true;
        public ValueTask<ServiceResult<TResult>> RequestAsync<TCommand, TEntityId, TResult>(ActorSubject subject, TCommand command, TEntityId entityId)
            where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class
        {
            Subject = subject;
            ResultType = typeof(TResult);
            object value = typeof(TResult) == typeof(GuidResult)
                ? new GuidResult(command.CommandId)
                : throw new InvalidOperationException($"Unexpected command result {typeof(TResult).Name}.");
            return ValueTask.FromResult<ServiceResult<TResult>>(new ServiceOk<TResult>((TResult)value));
        }
        public ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(ActorSubject subject, TQuery query) where TQuery : class, IQuery<TResult> where TResult : class => throw new NotSupportedException();
        public ValueTask<ServiceResult<TResult>> RequestFunctionAsync<TCommand, TEntityId, TResult>(ActorSubject subject, TCommand command, TEntityId entityId, CancellationToken cancellationToken = default) where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class => throw new NotSupportedException();
        public ValueTask SendAsync<TCommand, TEntityId>(ActorSubject subject, TCommand command, TEntityId entityId) where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId => throw new NotSupportedException();
        public ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event) where TEvent : class, IEvent<TEntityId> where TEntityId : IActorEntityId => throw new NotSupportedException();
        public ValueTask StartAsync(ActorMailboxId mailboxId) => ValueTask.CompletedTask;
        public ValueTask StopAsync() => ValueTask.CompletedTask;
    }

    sealed class ReservationQueryStub(
        FundCompositionWorkflowProjectionReadModel[] workflow,
        IReadOnlyDictionary<int, FundOrderProjectionReadModel> orders,
        FundOrderTradeProjectionReadModel[] trades) : IPortfolioQueryApi
    {
        public Task<ServiceResult<FundCompositionWorkflowProjectionReadModel[]>> GetCompositionByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default) => Task.FromResult<ServiceResult<FundCompositionWorkflowProjectionReadModel[]>>(new ServiceOk<FundCompositionWorkflowProjectionReadModel[]>(workflow));
        public Task<ServiceResult<FundOrderProjectionReadModel>> GetOrderAsync(int orderId, CancellationToken cancellationToken = default) => Task.FromResult<ServiceResult<FundOrderProjectionReadModel>>(new ServiceOk<FundOrderProjectionReadModel>(orders[orderId]));
        public Task<ServiceResult<PortfolioPage<FundOrderTradeProjectionReadModel>>> GetOrderTradesAsync(int orderId, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default) => Task.FromResult<ServiceResult<PortfolioPage<FundOrderTradeProjectionReadModel>>>(new ServiceOk<PortfolioPage<FundOrderTradeProjectionReadModel>>(new() { Items = [.. trades.Where(x => x.OrderId == orderId)], PageSize = pageSize }));
        public Task<ServiceResult<PortfolioReadModel>> GetPortfolioAsync(int portfolioId, long? version = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<PortfolioAggregateRevision>> GetPortfolioRevisionAsync(int portfolioId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<PortfolioPage<PortfolioReadModel>>> GetPortfoliosAsync(PortfolioOperatingState? state, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<FundMandateReadModel>> GetFundAsync(int portfolioId, int fundId, long? version = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<PortfolioAggregateRevision>> GetFundRevisionAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<PortfolioPage<FundMandateReadModel>>> GetFundsAsync(int portfolioId, FundOperatingState? state, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<FundRiskEnvelopeReadModel>> GetFundRiskEnvelopeAsync(int portfolioId, int fundId, DateTime asOfUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<FundAllocationReadModel>> GetFundAllocationAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<FundTradeTemplateAssignmentReadModel[]>> GetAssignmentsAsync(int portfolioId, int fundId, long mandateVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<PortfolioFundStrategySnapshot>> GetStrategySnapshotAsync(int portfolioId, int tradingYear, string decisionHorizon, string underlyingRoot, string assetType, DateTime asOfUtc, Guid workflowId, long workflowRevision, Guid correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<FundOrderTradeProjectionReadModel>> GetTradeAsync(int tradeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<PortfolioPage<FundOrderProjectionReadModel>>> GetOrdersAsync(int portfolioId, int fundId, DateOnly orderMonth, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<PortfolioFundStrategyReferenceCombination[]>> GetStrategyReferenceCombinationsAsync(int portfolioId, DateTime asOfUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
