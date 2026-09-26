using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Query;

public sealed class PortfolioNatsClientTests
{
    [Fact]
    public void Portfolio_retry_identity_excludes_only_correlation_and_timestamp()
    {
        var original = new CreatePortfolioCommand
        {
            CommandId = Guid.NewGuid(), EntityId = new(101),
            Subject = new(ActorType.Command, CreatePortfolioCommand.Actor, CreatePortfolioCommand.Verb, "101"),
            Portfolio = new PortfolioReadModel { PortfolioId = 101, Name = "Original" }, IdempotencyKey = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(), RequestedOnUtc = DateTime.UtcNow,
            Access = PortfolioAccessContext.Administrator("admin")
        };
        var retry = original with { CorrelationId = Guid.NewGuid(), RequestedOnUtc = original.RequestedOnUtc.AddSeconds(1) };
        ICommand Normalize(ICommand value) => ((ICommandRetryIdentity)value).ForRetryIdentity();
        Normalize(retry).Should().BeEquivalentTo(Normalize(original));
        var normalized = (CreatePortfolioCommand)Normalize(original);
        normalized.Should().BeEquivalentTo(original, options => options.Excluding(x => x.CorrelationId).Excluding(x => x.RequestedOnUtc));
        normalized.CorrelationId.Should().BeEmpty();
        normalized.RequestedOnUtc.Should().Be(default);
        normalized.Access.Should().BeEquivalentTo(original.Access);
        Normalize(retry with { Access = PortfolioAccessContext.Reader("admin") }).Should().NotBeEquivalentTo(normalized);
        Normalize(retry with { Access = PortfolioAccessContext.Administrator("other") }).Should().NotBeEquivalentTo(normalized);
        Normalize(retry with { Portfolio = original.Portfolio with { Name = "Changed" } }).Should().NotBeEquivalentTo(normalized);
    }

    [Fact]
    [Trait("Gate", "PF-10")]
    [Trait("Category", "Portfolio")]
    public void Command_envelope_preserves_base_keys_and_appends_correlation_and_access_metadata()
    {
        var id = new PortfolioId(101);
        var subject = new ActorSubject(ActorType.Command, CreatePortfolioCommand.Actor, "ChangePortfolioOperatingState", id.Format());
        var command = new ChangePortfolioOperatingStateCommand
        {
            CommandId = Guid.NewGuid(), Subject = subject, EntityId = id, ErrorCode = 34005,
            ExpectedVersion = 2, State = PortfolioOperatingState.Paused, Reason = "test",
            Access = PortfolioAccessContext.Administrator("unit-admin"),
        };

        var json = MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(command));

        using var document = System.Text.Json.JsonDocument.Parse(json);
        document.RootElement.GetArrayLength().Should().Be(12);
        var copy = MessagePackSerializer.Deserialize<ChangePortfolioOperatingStateCommand>(MessagePackSerializer.Serialize(command));
        copy.ExpectedVersion.Should().Be(2);
        copy.CorrelationId.Should().Be(Guid.Empty, "older producers deserialize appended metadata to compatible defaults");
        copy.Access.Principal.Should().Be("unit-admin");
    }

    [Fact]
    [Trait("Gate", "PF-10")]
    [Trait("Category", "Portfolio")]
    public async Task Client_constructs_typed_actor_subject_and_payload()
    {
        var producer = new CapturingProducer();
        var api = new PortfolioQueryApi(producer);

        var result = await api.GetPortfolioAsync(101, 2);

        result.Success.Should().BeTrue();
        producer.Subject.Name.Should().Be(GetPortfolioQuery.Actor);
        producer.Subject.Verb.Should().Be(GetPortfolioQuery.Verb);
        producer.Subject.EntityId.Should().Be("101");
        var query = producer.Query.Should().BeOfType<GetPortfolioQuery>().Subject;
        query.PortfolioId.Should().Be(101);
        query.Version.Should().Be(2);
        query.CorrelationId.Should().NotBeEmpty();
        query.RequestedOnUtc.Kind.Should().Be(DateTimeKind.Utc);
        query.Access.Roles.Should().ContainSingle(PortfolioOperationalPolicy.ReaderRole);
        var copy = MessagePackSerializer.Deserialize<GetPortfolioQuery>(MessagePackSerializer.Serialize(query));
        copy.PortfolioId.Should().Be(query.PortfolioId);
        copy.Version.Should().Be(query.Version);
    }

    [Fact]
    public async Task Fund_queries_use_the_split_Fund_actor_route()
    {
        var producer = new CapturingProducer();
        var api = new PortfolioQueryApi(producer);

        await api.GetFundAsync(101, 202);

        producer.Subject.Name.Should().Be(PortfolioQueryRoutes.Fund);
        producer.Subject.Verb.Should().Be(GetFundQuery.Verb);
        var query = producer.Query.Should().BeOfType<GetFundQuery>()
            .Subject;
        query.Subject.Name.Should().Be(PortfolioQueryRoutes.Fund);
        var copy = MessagePackSerializer.Deserialize<GetFundQuery>(MessagePackSerializer.Serialize(query));
        copy.PortfolioId.Should().Be(101);
        copy.FundId.Should().Be(202);
        copy.Subject.Name.Should().Be(PortfolioQueryRoutes.Fund);
    }

    [Fact]
    public async Task Every_Fund_query_api_method_sends_a_canonical_contract_to_the_Fund_actor()
    {
        var producer = new CapturingProducer();
        var api = new PortfolioQueryApi(producer);
        var now = DateTime.UtcNow;
        var workflowId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        async Task AssertRoute(Task request, Type contract)
        {
            await request;
            producer.Subject.Name.Should().Be(PortfolioQueryRoutes.Fund);
            producer.Query.Should().BeOfType(contract);
            producer.Subject.Verb.Should().NotEndWith("V2");
        }

        await AssertRoute(api.GetFundAsync(101, 202), typeof(GetFundQuery));
        await AssertRoute(api.GetFundRevisionAsync(101, 202), typeof(GetFundRevisionQuery));
        await AssertRoute(api.GetFundsAsync(101, null, 20), typeof(GetFundsQuery));
        await AssertRoute(api.GetFundAllocationAsync(101, 202), typeof(GetFundAllocationQuery));
        await AssertRoute(api.GetFundRiskEnvelopeAsync(101, 202, now), typeof(GetFundRiskEnvelopeQuery));
        await AssertRoute(api.GetAssignmentsAsync(101, 202, 1), typeof(GetFundTemplateAssignmentsQuery));
        await AssertRoute(api.ResolveForSelectionAsync(101, 202, 2026, "Daily", "ES", now, workflowId, 1, correlationId), typeof(ResolveForSelectionQuery));
        await AssertRoute(api.GetStrategySnapshotAsync(101, 2026, "Daily", "ES", "Futures", now, workflowId, 1, correlationId), typeof(GetPortfolioFundStrategySnapshotQuery));
        await AssertRoute(api.GetOrderAsync(303), typeof(GetFundOrderByOrderIdQuery));
        await AssertRoute(api.GetTradeAsync(404), typeof(GetFundOrderTradeByTradeIdQuery));
        await AssertRoute(api.GetCompositionByWorkflowAsync(workflowId), typeof(GetFundCompositionByWorkflowQuery));
        await AssertRoute(api.GetOrdersAsync(101, 202, new DateOnly(2026, 9, 1), 20), typeof(GetFundOrdersPageQuery));
        await AssertRoute(api.GetOrderTradesAsync(303, 20), typeof(GetFundOrderTradesPageQuery));
        await AssertRoute(api.GetStrategyReferenceCombinationsAsync(101, now), typeof(GetPortfolioFundStrategyReferenceCombinationsQuery));
    }

    [Fact]
    public void GetFund_uses_the_canonical_Fund_route_and_direct_schema()
    {
        GetFundQuery.Actor.Should().Be(PortfolioQueryRoutes.Fund);
        GetFundQuery.Verb.Should().Be("GetFund");
        var keys = typeof(GetFundQuery).GetProperties()
            .Select(property => property.GetCustomAttributes(typeof(KeyAttribute), false).Cast<KeyAttribute>().SingleOrDefault()?.IntKey)
            .Where(key => key is not null).Select(key => key!.Value).Order().ToArray();
        keys.Should().Equal(Enumerable.Range(0, 8));
        var query = new GetFundQuery(101, 202, 3)
        {
            Subject = new(ActorType.Query, GetFundQuery.Actor, GetFundQuery.Verb, "101.202"),
            EntityId = new("101.202")
        };
        var copy = MessagePackSerializer.Deserialize<GetFundQuery>(MessagePackSerializer.Serialize(query));
        copy.Subject.Name.Should().Be(PortfolioQueryRoutes.Fund);
        copy.Version.Should().Be(3);
    }

    [Fact]
    public async Task Financial_policy_queries_use_the_split_policy_actor_route()
    {
        var producer = new CapturingProducer();
        var api = new PortfolioQueryApi(producer);

        await api.GetPolicyAsync(303);

        producer.Subject.Name.Should().Be(PortfolioQueryRoutes.FinancialPolicy);
        producer.Subject.Verb.Should().Be(GetPortfolioFinancialPolicyQuery.Verb);
        producer.Query.Should().BeOfType<GetPortfolioFinancialPolicyQuery>()
            .Subject.Subject.Name.Should().Be(PortfolioQueryRoutes.FinancialPolicy);
        await api.GetPoliciesAsync(101, 20);
        producer.Query.Should().BeOfType<GetPortfolioFinancialPoliciesQuery>();
        producer.Subject.Name.Should().Be(PortfolioQueryRoutes.FinancialPolicy);
        await api.GetActivePolicyAsync(101);
        producer.Query.Should().BeOfType<GetActivePortfolioFinancialPolicyQuery>();
        producer.Subject.Name.Should().Be(PortfolioQueryRoutes.FinancialPolicy);
    }

    [Fact]
    public async Task Identity_allocation_uses_the_versioned_Portfolio_query_and_entity_key()
    {
        var producer = new CapturingProducer();
        var api = new PortfolioIdentityApi(producer);

        await api.AllocateFundIdAsync();

        producer.Subject.Name.Should().Be(PortfolioQueryRoutes.Portfolio);
        producer.Subject.Verb.Should().Be(AllocatePortfolioBusinessIdQuery.Verb);
        producer.Subject.EntityId.Should().Be("Fund");
        var query = producer.Query.Should().BeOfType<AllocatePortfolioBusinessIdQuery>().Subject;
        query.EntityId.Should().Be(new ActorEntityId("Fund"));
        query.Subject.Should().Be(producer.Subject);
    }

    [Fact]
    [Trait("Gate", "PF-10")]
    [Trait("Category", "Portfolio")]
    public async Task Client_propagates_pre_cancelled_request_without_transport_send()
    {
        var producer = new CapturingProducer();
        var api = new PortfolioQueryApi(producer);
        using var source = new CancellationTokenSource();
        source.Cancel();

        var action = () => api.GetPortfolioAsync(101, cancellationToken: source.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
        producer.Query.Should().BeNull();
    }

    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Gate", "PF-10")]
    [Trait("Category", "Portfolio")]
    public async Task Draft_deletion_client_uses_typed_NATS_verb_revision_and_reason()
    {
        var producer = new CapturingProducer();
        var api = new PortfolioCommandApi(producer);

        var result = await api.DeleteDraftPortfolioAsync(new PortfolioId(101), 7, "duplicate");

        result.Success.Should().BeTrue();
        producer.Subject.Name.Should().Be(CreatePortfolioCommand.Actor);
        producer.Subject.Verb.Should().Be("DeleteDraftPortfolio");
        var command = producer.Query.Should().BeOfType<DeleteDraftPortfolioCommand>().Subject;
        command.EntityId.Should().Be(new PortfolioId(101));
        command.ExpectedVersion.Should().Be(7);
        command.Reason.Should().Be("duplicate");
    }

    sealed class CapturingProducer : IActorProducer
    {
        public ActorSubject Subject { get; private set; }
        public object? Query { get; private set; }
        public bool IsRunning => true;
        public ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(ActorSubject subject, TQuery query) where TQuery : class, IQuery<TResult> where TResult : class
        {
            Subject = subject;
            Query = query;
            object result = typeof(TResult) == typeof(PortfolioReadModel)
                ? new PortfolioReadModel { PortfolioId = 101, PortfolioVersion = 2 }
                : typeof(TResult).IsArray
                    ? Array.CreateInstance(typeof(TResult).GetElementType()!, 0)
                    : Activator.CreateInstance<TResult>();
            return ValueTask.FromResult<ServiceResult<TResult>>(new ServiceOk<TResult>((TResult)result));
        }
        public ValueTask<ServiceResult<TResult>> RequestAsync<TCommand, TEntityId, TResult>(ActorSubject subject, TCommand command, TEntityId entityId) where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class => CaptureCommand<TCommand, TEntityId, TResult>(subject, command);
        public ValueTask<ServiceResult<TResult>> RequestFunctionAsync<TCommand, TEntityId, TResult>(ActorSubject subject, TCommand command, TEntityId entityId, CancellationToken cancellationToken = default) where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class => CaptureCommand<TCommand, TEntityId, TResult>(subject, command);
        public ValueTask SendAsync<TCommand, TEntityId>(ActorSubject subject, TCommand command, TEntityId entityId) where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId => throw new NotSupportedException();
        public ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event) where TEvent : class, IEvent<TEntityId> where TEntityId : IActorEntityId => throw new NotSupportedException();
        public ValueTask StartAsync(ActorMailboxId mailboxId) => ValueTask.CompletedTask;
        public ValueTask StopAsync() => ValueTask.CompletedTask;

        ValueTask<ServiceResult<TResult>> CaptureCommand<TCommand, TEntityId, TResult>(ActorSubject subject, TCommand command)
            where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class
        {
            Subject = subject; Query = command;
            object value = typeof(TResult) == typeof(GuidResult) ? new GuidResult(command.CommandId) : Activator.CreateInstance<TResult>();
            return ValueTask.FromResult<ServiceResult<TResult>>(new ServiceOk<TResult>((TResult)value));
        }
    }
}
