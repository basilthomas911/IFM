using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.Query.Api;

public sealed class DatabentoNatsQueryClientTests
{
    [Fact]
    public async Task Nats_client_constructs_typed_readiness_contract_and_history_queries()
    {
        var producer = new CapturingProducer();
        var api = new MarketDataFeedQueryApi(producer);

        await api.GetDatabentoReadinessAsync();
        producer.AssertLast<GetDatabentoReadinessQuery>(GetDatabentoReadinessQuery.Verb);
        await api.GetDatabentoCurrentContractsAsync();
        producer.AssertLast<GetDatabentoCurrentContractsQuery>(GetDatabentoCurrentContractsQuery.Verb);
        await api.GetDatabentoWatchdogHistoryAsync(new DateOnly(2026, 9, 2), "Down", 25);
        var history = producer.AssertLast<GetDatabentoWatchdogHistoryQuery>(GetDatabentoWatchdogHistoryQuery.Verb);
        var parameter = history.EntityId.Should().BeOfType<GetDatabentoWatchdogHistoryParameter>().Subject;
        parameter.ValueDate.Should().Be(new DateOnly(2026, 9, 2));
        parameter.MajorStatus.Should().Be("Down");
        parameter.PageSize.Should().Be(25);
        MessagePackSerializer.Deserialize<GetDatabentoWatchdogHistoryQuery>(
            MessagePackSerializer.Serialize(history)).EntityId.Should().BeOfType<GetDatabentoWatchdogHistoryParameter>();
    }

    sealed class CapturingProducer : IActorProducer
    {
        public ActorSubject Subject { get; private set; }
        public object? Query { get; private set; }
        public bool IsRunning => true;

        public T AssertLast<T>(string verb) where T : class
        {
            Subject.Name.Should().Be(GetDatabentoReadinessQuery.Actor);
            Subject.Verb.Should().Be(verb);
            return Query.Should().BeOfType<T>().Subject;
        }

        public ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(ActorSubject subject, TQuery query)
            where TQuery : class, IQuery<TResult> where TResult : class
        {
            Subject = subject;
            Query = query;
            object result = typeof(TResult) == typeof(DatabentoReadinessReadModel)
                ? new DatabentoReadinessReadModel()
                : Array.CreateInstance(typeof(TResult).GetElementType()!, 0);
            return ValueTask.FromResult<ServiceResult<TResult>>(new ServiceOk<TResult>((TResult)result));
        }

        public ValueTask<ServiceResult<TResult>> RequestAsync<TCommand, TEntityId, TResult>(ActorSubject subject,
            TCommand command, TEntityId entityId)
            where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class
            => throw new NotSupportedException();
        public ValueTask<ServiceResult<TResult>> RequestFunctionAsync<TCommand, TEntityId, TResult>(ActorSubject subject,
            TCommand command, TEntityId entityId, CancellationToken cancellationToken = default)
            where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class
            => throw new NotSupportedException();
        public ValueTask SendAsync<TCommand, TEntityId>(ActorSubject subject, TCommand command, TEntityId entityId)
            where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId => throw new NotSupportedException();
        public ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event)
            where TEvent : class, IEvent<TEntityId> where TEntityId : IActorEntityId => throw new NotSupportedException();
        public ValueTask StartAsync(ActorMailboxId mailboxId) => ValueTask.CompletedTask;
        public ValueTask StopAsync() => ValueTask.CompletedTask;
    }
}
