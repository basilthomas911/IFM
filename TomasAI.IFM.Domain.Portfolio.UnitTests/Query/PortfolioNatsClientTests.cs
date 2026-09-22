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
        producer.Subject.Verb.Should().Be("GetPortfolio");
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
            object result = typeof(TResult) == typeof(PortfolioReadModel) ? new PortfolioReadModel { PortfolioId = 101, PortfolioVersion = 2 } : Activator.CreateInstance<TResult>();
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
