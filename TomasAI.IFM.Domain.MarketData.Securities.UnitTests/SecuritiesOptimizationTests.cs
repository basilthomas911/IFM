using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Actor;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using FeedOptionContractQuery = TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetFuturesOptionContractQuery;

namespace TomasAI.IFM.Domain.MarketData.Securities.UnitTests;

public class SecuritiesOptimizationTests
{
    [Fact]
    public void AddOptionContract_ExistingContractWithOverwrite_Succeeds()
    {
        var state = CreateStateWith(SampleData.FuturesOptionContract1);
        var command = CreateAddCommand(SampleData.FuturesOptionContract1, overwrite: true);

        var changed = command.Execute(state);

        changed.Should().BeTrue();
        state.Events.Should().HaveCount(2);
    }

    [Fact]
    public void AddOptionContract_ExistingContractWithoutOverwrite_Fails()
    {
        var state = CreateStateWith(SampleData.FuturesOptionContract1);
        var command = CreateAddCommand(SampleData.FuturesOptionContract1, overwrite: false);

        var act = () => command.Execute(state);

        act.Should().Throw<Exception>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public void ChangeOptionContract_MissingContractWithOverwrite_Succeeds()
    {
        var state = new FuturesOptionContractCommandState();
        var command = new ChangeFuturesOptionContractCommand(
            SampleData.FuturesOptionContract1.ContractId,
            SampleData.FuturesOptionContract2,
            overwrite: true)
        {
            CommandId = Guid.NewGuid(),
            Subject = CreateSubject(ChangeFuturesOptionContractCommand.Verb, SampleData.FuturesOptionContract1.ContractId)
        };

        var changed = command.Execute(state);

        changed.Should().BeTrue();
        state.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task GetOptionContractIds_UsesOneBulkReadAndPreservesInputOrderAndDuplicates()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<ISecuritiesDbContext>();
        dbFactory.SecuritiesDb.Returns(db);
        var input = new[]
        {
            SampleData.FuturesOptionContract2.ContractId,
            "missing",
            SampleData.FuturesOptionContract1.ContractId,
            SampleData.FuturesOptionContract2.ContractId
        };
        db.GetFuturesOptionContractsByIdsAsync(Arg.Any<ICollection<string>>())
            .Returns(new[] { SampleData.FuturesOptionContract1, SampleData.FuturesOptionContract2 });
        var query = new GetFuturesOptionContractIdsQuery(input);

        var result = await query.GetFuturesOptionContractIdsAsync(dbFactory);

        result.Should().Equal(
            SampleData.FuturesOptionContract2.ContractId,
            SampleData.FuturesOptionContract1.ContractId,
            SampleData.FuturesOptionContract2.ContractId);
        await db.Received(1).GetFuturesOptionContractsByIdsAsync(
            Arg.Is<ICollection<string>>(ids => ids.SequenceEqual(input)));
        await db.DidNotReceive().GetFuturesOptionContractAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetOptionContractIds_EmptyInput_DoesNotReadStorage()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<ISecuritiesDbContext>();
        dbFactory.SecuritiesDb.Returns(db);
        var query = new GetFuturesOptionContractIdsQuery([]);

        var result = await query.GetFuturesOptionContractIdsAsync(dbFactory);

        result.Should().BeEmpty();
        await db.DidNotReceive().GetFuturesOptionContractsByIdsAsync(Arg.Any<ICollection<string>>());
    }

    [Fact]
    public async Task InsertOptionContracts_UsesBoundedEnrichmentAndOneBatchWrite()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<ISecuritiesDbContext>();
        dbFactory.SecuritiesDb.Returns(db);
        var contracts = Enumerable.Range(0, 20)
            .Select(index => SampleData.FuturesOptionContract1 with
            {
                ContractId = $"ES20251215C{5000 + index}",
                StrikePrice = 5000 + index
            })
            .ToArray();
        var actorService = new TrackingActorService();

        await dbFactory.InsertFuturesOptionContractsAsync(contracts, actorService);

        actorService.RequestCount.Should().Be(contracts.Length);
        actorService.MaximumConcurrency.Should().BeGreaterThan(1).And.BeLessThanOrEqualTo(8);
        await db.Received(1).InsertFuturesOptionContractsAsync(
            Arg.Is<ICollection<FuturesOptionContractReadModel>>(values => values.Count == contracts.Length));
        await db.DidNotReceive().InsertFuturesOptionContractAsync(Arg.Any<FuturesOptionContractReadModel>());
    }

    [Fact]
    public async Task LoadBulkOptionState_UsesBulkAddedEventAsSnapshot()
    {
        var stateFactory = Substitute.For<IEventSourceActorStateFactory>();
        stateFactory.CreateState<FuturesOptionContractCommandState>()
            .Returns(new FuturesOptionContractCommandState());
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        eventSource.GetEventStreamIdAsync(Arg.Any<string>()).Returns(42L);
        var repository = new FuturesOptionContractStateRepository(
            stateFactory,
            eventSource,
            Substitute.For<IDbContextFactory>(),
            Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<FuturesOptionContractCommandActor>>(),
            Substitute.For<ILogger<FuturesOptionContractStateRepository>>());
        var command = new AddFuturesOptionContractsCommand(
            [SampleData.FuturesOptionContract1, SampleData.FuturesOptionContract2])
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                AddFuturesOptionContractsCommand.Actor,
                AddFuturesOptionContractsCommand.Verb,
                "2025")
        };

        _ = await repository.LoadStateAsync(command);

        await eventSource.Received(1)
            .MapReduceActorEventStreamAsync<
                FuturesOptionContractCommandState,
                FuturesOptionContractsAddedEvent>(
                42L,
                Arg.Any<Action<IEnumerable<EventStreamReadModel>>>());
        await eventSource.DidNotReceive()
            .MapReduceActorEventStreamAsync<
                FuturesOptionContractCommandState,
                FuturesOptionContractAddedEvent>(
                Arg.Any<long>(),
                Arg.Any<Action<IEnumerable<EventStreamReadModel>>>());
    }

    static FuturesOptionContractCommandState CreateStateWith(FuturesOptionContractReadModel contract)
    {
        var state = new FuturesOptionContractCommandState();
        CreateAddCommand(contract, overwrite: false).Execute(state);
        return state;
    }

    static AddFuturesOptionContractCommand CreateAddCommand(
        FuturesOptionContractReadModel contract,
        bool overwrite)
        => new(contract, overwrite)
        {
            CommandId = Guid.NewGuid(),
            Subject = CreateSubject(AddFuturesOptionContractCommand.Verb, contract.ContractId)
        };

    static ActorSubject CreateSubject(string verb, string entityId)
        => new(
            ActorType.Command,
            AddFuturesOptionContractCommand.Actor,
            verb,
            entityId);

    sealed class TrackingActorService : IActorService
    {
        int _concurrency;
        int _maximumConcurrency;
        int _requestCount;

        public int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);
        public int RequestCount => Volatile.Read(ref _requestCount);

        public async ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(TQuery query)
            where TResult : class
            where TQuery : class, IQuery<TResult>
        {
            Interlocked.Increment(ref _requestCount);
            var concurrency = Interlocked.Increment(ref _concurrency);
            UpdateMaximum(concurrency);
            try
            {
                await Task.Delay(10);
                var optionQuery = (FeedOptionContractQuery)(object)query;
                return (ServiceResult<TResult>)(object)new ServiceOk<FuturesOptionContractReadModel>(
                    optionQuery.QueryForContract!);
            }
            finally
            {
                Interlocked.Decrement(ref _concurrency);
            }
        }

        public ValueTask<ServiceResult<Guid>> SendAsync<TCommand, TEntityId>(TCommand command, TEntityId entityId)
            where TCommand : class, ICommand<TEntityId>
            where TEntityId : IActorEntityId
            => throw new NotSupportedException();

        public ValueTask<ServiceResult<Guid>> RequestAsync<TCommand, TEntityId>(TCommand command)
            where TCommand : class, ICommand<TEntityId>
            where TEntityId : IActorEntityId
            => throw new NotSupportedException();

        void UpdateMaximum(int value)
        {
            var current = Volatile.Read(ref _maximumConcurrency);
            while (value > current)
            {
                var observed = Interlocked.CompareExchange(ref _maximumConcurrency, value, current);
                if (observed == current)
                    return;
                current = observed;
            }
        }
    }
}
