using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

public sealed class PortfolioFundCommandClientTests
{
    [Fact]
    public async Task Fund_producer_attaches_the_receipt_for_its_exact_mandate_and_access()
    {
        var producer = new AcknowledgingProducer();
        var client = new PortfolioFundCommandApi(producer, identity: Substitute.For<IPortfolioIdentityApi>());
        var mandate = new FundMandateReadModel { PortfolioId = 101, FundId = 202 };
        var key = Guid.NewGuid();

        var result = await client.CreateFundMandateAsync(mandate, key);

        result.Success.Should().BeTrue();
        var sent = producer.Command.Should().BeOfType<CreateFundMandateCommand>().Subject;
        var validation = producer.ReferenceValidation.Should().BeOfType<ValidatePortfolioReferenceCommand>().Subject;
        sent.ReferencePreparationId.Should().Be(validation.CommandId);
        validation.Should().Be(ValidatePortfolioReferenceCommand.ForOwnerCommand(sent.CommandId, validation.CommandId) with
            { Mandate = mandate, Access = sent.Access });
    }

    [Fact]
    public async Task Policy_activation_producer_attaches_exact_policy_receipt_and_parent_revision_evidence()
    {
        var producer = new AcknowledgingProducer();
        var queries = Substitute.For<IPortfolioQueryApi>();
        var parent = new PortfolioReadModel { PortfolioId = 101, PortfolioVersion = 2, OperatingState = PortfolioOperatingState.Active };
        var policy = new PortfolioFinancialPolicyReadModel { PortfolioId = 101, PolicyId = 303, PolicyVersion = 4 };
        var revision = new PortfolioAggregateRevision { PortfolioId = 101, Revision = 15, SourceEventId = 99 };
        queries.GetPolicyAsync(303, 4, Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioFinancialPolicyReadModel>(policy));
        queries.GetPortfolioRevisionAsync(101, Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioAggregateRevision>(revision));
        queries.GetPortfolioAsync(101, null, Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioReadModel>(parent));

        var result = await new PortfolioFinancialPolicyCommandApi(producer, queries).ActivateAndAssignAsync(new(101, 303), 4, 12, 15);

        result.Success.Should().BeTrue();
        var command = producer.Command.Should().BeOfType<ActivateAndAssignPortfolioFinancialPolicyCommand>().Subject;
        var validation = producer.ReferenceValidation.Should().BeOfType<ValidatePortfolioReferenceCommand>().Subject;
        command.ParentPortfolio.Should().Be(parent);
        command.ReferencePreparationId.Should().Be(validation.CommandId);
        validation.Should().Be(ValidatePortfolioReferenceCommand.ForOwnerCommand(command.CommandId, validation.CommandId) with
            { Access = command.Access, Policy = policy, ActivatesPolicy = true });
    }

    [Fact]
    public async Task Manual_order_producer_sends_allocated_id_and_pinned_parent_revision()
    {
        var now = new DateTime(2026, 8, 30, 14, 0, 0, DateTimeKind.Utc);
        var request = new CreateManualFundOrderRequest
        {
            PortfolioId = 101, PortfolioVersion = 2, FundId = 202, FundMandateVersion = 3,
            IdempotencyKey = Guid.NewGuid(), RequestedAtUtc = now, ExpiresAtUtc = now.AddDays(1)
        };
        var parent = new PortfolioReadModel { PortfolioId = 101, PortfolioVersion = 2, OperatingState = PortfolioOperatingState.Active };
        var revision = new PortfolioAggregateRevision { PortfolioId = 101, Revision = 15, SourceEventId = 99 };
        var order = new FundOrderProjectionReadModel
        {
            PortfolioId = 101, FundId = 202, OrderId = 7001, IdempotencyKey = request.IdempotencyKey,
            CreatedOnUtc = now, AggregateVersion = 1, CanonicalRequestHash = PortfolioCanonicalHash.Compute(request)
        };
        var queries = Substitute.For<IPortfolioQueryApi>();
        queries.GetPortfolioRevisionAsync(101, Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioAggregateRevision>(revision));
        queries.GetPortfolioAsync(101, null, Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioReadModel>(parent));
        queries.GetOrdersAsync(101, 202, new DateOnly(2026, 8, 1), 200, null, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundOrderProjectionReadModel>>(new() { Items = [] }),
                new ServiceOk<PortfolioPage<FundOrderProjectionReadModel>>(new() { Items = [order] }));
        var identity = Substitute.For<IPortfolioIdentityApi>();
        identity.AllocateForOperationAsync(PortfolioBusinessIdentityKind.Order, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioBusinessIdAllocation>(new() { Value = 7001 }));
        var producer = new AcknowledgingProducer();

        var result = await new PortfolioFundCommandApi(producer, queries, identity).CreateManualOrderAsync(request);

        result.Success.Should().BeTrue();
        var sent = producer.Command.Should().BeOfType<CreateManualFundOrderCommand>().Subject;
        sent.Preparation.Should().BeEquivalentTo(new FundOrderPreparation { OrderId = 7001, Parent = parent, ParentSourceRevision = 15 });
        var allocationOperation = FundOrderPreparation.AllocationOperationId(new(101, 202), sent.CommandId, PortfolioBusinessIdentityKind.Order, 0);
        await identity.Received(1).AllocateForOperationAsync(PortfolioBusinessIdentityKind.Order, allocationOperation, Arg.Any<CancellationToken>());
        result.Value!.Order.Should().Be(order);
    }

    [Fact]
    [Trait("Gate", "PF-28")]
    [Trait("Category", "Portfolio")]
    public async Task Manual_order_uses_typed_Portfolio_command_and_returns_sequence_allocated_projection()
    {
        var now = new DateTime(2026, 8, 30, 14, 0, 0, DateTimeKind.Utc);
        var key = Guid.NewGuid();
        var request = new CreateManualFundOrderRequest
        {
            PortfolioId = 101,
            PortfolioVersion = 2,
            FundId = 202,
            FundMandateVersion = 3,
            IdempotencyKey = key,
            RequestedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1),
        };
        var projected = new FundOrderProjectionReadModel
        {
            PortfolioId = 101,
            FundId = 202,
            OrderId = 7001,
            IdempotencyKey = key,
            CreatedOnUtc = now,
            AggregateVersion = 1,
            Origin = CompositionOrigin.ManualUi,
            CanonicalRequestHash = PortfolioCanonicalHash.Compute(request),
        };
        var producer = new AcknowledgingProducer();
        var queries = new ReservationQueryStub([], new Dictionary<int, FundOrderProjectionReadModel>(), [], [projected]);
        var client = new PortfolioFundCommandApi(producer, queries);

        var result = await client.CreateManualOrderAsync(request);

        result.Success.Should().BeTrue();
        result.Value!.Order.Should().Be(projected);
        result.Value.Trades.Should().BeEmpty();
        producer.Subject.Verb.Should().Be("CreateManualFundOrder");
    }

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
        producer.Subject.Name.Should().Be(CreateFundMandateCommand.Actor);
        producer.Subject.Verb.Should().Be("ReserveFundOrderComposition");
    }

    sealed class AcknowledgingProducer : IActorProducer
    {
        public Type? ResultType { get; private set; }
        public ICommand? Command { get; private set; }
        public ValidatePortfolioReferenceCommand? ReferenceValidation { get; private set; }
        public ActorSubject Subject { get; private set; }
        public bool IsRunning => true;
        public ValueTask<ServiceResult<TResult>> RequestAsync<TCommand, TEntityId, TResult>(ActorSubject subject, TCommand command, TEntityId entityId)
            where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class
        {
            Subject = subject;
            Command = command;
            ResultType = typeof(TResult);
            object value = typeof(TResult) == typeof(GuidResult)
                ? new GuidResult(command.CommandId)
                : throw new InvalidOperationException($"Unexpected command result {typeof(TResult).Name}.");
            return ValueTask.FromResult<ServiceResult<TResult>>(new ServiceOk<TResult>((TResult)value));
        }
        public ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(ActorSubject subject, TQuery query) where TQuery : class, IQuery<TResult> where TResult : class => throw new NotSupportedException();
        public ValueTask<ServiceResult<TResult>> RequestFunctionAsync<TCommand, TEntityId, TResult>(ActorSubject subject, TCommand command, TEntityId entityId, CancellationToken cancellationToken = default) where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class
        {
            ReferenceValidation = command as ValidatePortfolioReferenceCommand ?? throw new NotSupportedException();
            var result = FunctionResult<PortfolioReferenceValidationCompletedEvent, PortfolioReferenceValidationFailedEvent>.Complete(new()
            {
                CommandId = command.CommandId,
                InputHash = ReferenceValidation.ComputeInputHash()
            });
            return ValueTask.FromResult<ServiceResult<TResult>>(new ServiceOk<TResult>((TResult)(object)result));
        }
        public ValueTask SendAsync<TCommand, TEntityId>(ActorSubject subject, TCommand command, TEntityId entityId) where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId => throw new NotSupportedException();
        public ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event) where TEvent : class, IEvent<TEntityId> where TEntityId : IActorEntityId => throw new NotSupportedException();
        public ValueTask StartAsync(ActorMailboxId mailboxId) => ValueTask.CompletedTask;
        public ValueTask StopAsync() => ValueTask.CompletedTask;
    }

    sealed class ReservationQueryStub(
        FundCompositionWorkflowProjectionReadModel[] workflow,
        IReadOnlyDictionary<int, FundOrderProjectionReadModel> orders,
        FundOrderTradeProjectionReadModel[] trades,
        FundOrderProjectionReadModel[]? timeline = null) : IPortfolioQueryApi
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
        public Task<ServiceResult<PortfolioPage<FundOrderProjectionReadModel>>> GetOrdersAsync(int portfolioId, int fundId, DateOnly orderMonth, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<ServiceResult<PortfolioPage<FundOrderProjectionReadModel>>>(new ServiceOk<PortfolioPage<FundOrderProjectionReadModel>>(new()
            {
                Items = [.. (timeline ?? []).Where(x => x.PortfolioId == portfolioId && x.FundId == fundId)],
                PageSize = pageSize,
            }));
        public Task<ServiceResult<PortfolioFundStrategyReferenceCombination[]>> GetStrategyReferenceCombinationsAsync(int portfolioId, DateTime asOfUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
