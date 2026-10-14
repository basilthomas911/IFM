using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Order.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

[Collection("PostgreSQL plan atomicity")]
[Trait("Infrastructure","SelfContained")]
public sealed class TradeOrderAtomicRepositoryTests(PostgresTradePlanFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Repository_accepts_state_and_submits_handoff_only_after_required_commit(bool fail)
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaSql.Create,[],ct));
        var settings=new TomasAI.IFM.Shared.Storage.DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres");
        var factory=new Mock<IDbContextFactory>();
        var tradeDb=new TradeDbContext(settings,factory.Object,Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        factory.SetupGet(x=>x.TradeDb).Returns(tradeDb);
        var sourceDb=new Mock<IEventSourceActorDbContext>();
        sourceDb.SetupGet(x=>x.ProviderName).Returns("System.Data.Postgres");
        sourceDb.SetupGet(x=>x.ConnectionString).Returns(fixture.Connection);
        var context=new Mock<ITradeOrderCommandContext>();context.SetupGet(x=>x.DbFactory).Returns(factory.Object);context.SetupGet(x=>x.DbEventSource).Returns(sourceDb.Object);
        var id=new TradeOrderId(Random.Shared.Next(60_000_000,70_000_000),1,1);var order=new TradeOrderDefinition {Id=id,Revision=1,DefinitionHash="repository-atomic"};
        var command=new CreateTradeOrderCommand {CommandId=Guid.NewGuid(),EntityId=id,Order=order,Subject=new ActorSubject(ActorType.Command,CreateTradeOrderCommand.Actor,CreateTradeOrderCommand.Verb,id.Format())};
        var source=new TradeOrderChangedEvent {Id=Guid.NewGuid(),EntityId=id,CommandId=command.CommandId,TradeOrderDefinition=order,ReceivedOn=DateTime.UtcNow};
        var state=new TradeOrderCommandState();state.Apply(source).Should().BeTrue();
        var projector=new Mock<IEventProjector<TradeOrderCommandActor>>();
        projector.Setup(x=>x.DomainEventsProjectionAsync(It.IsAny<DomainEventCollection>())).Returns((DomainEventCollection events)=>new ValueTask(CheckCommitted()));
        async Task CheckCommitted()
        {
            state.Events.Should().BeEmpty();state.CommittedStreamVersion.Should().Be(1);
            (await fixture.EventCountAsync(command.StreamId)).Should().Be(1);
            (await tradeDb.GetTradeOrderAsync(id)).Should().BeEquivalentTo(order);
            var marker=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM event_projector_state WHERE eventId=$1 AND actorName=$2 AND projectorName=$3;",[source.EventId,"TradeOrderCommandActor","TradeOrderEventProjector"],ct));
            Convert.ToInt64(marker).Should().Be(1);
        }
        var owner=fail?(IPostgresEventTransaction)new FailAfterOperation(fixture.Transactions):fixture.Transactions;
        var repository=new TradeOrderStateRepository(Mock.Of<IEventSourceActorStateFactory>(),sourceDb.Object,Mock.Of<IActorService>(),projector.Object,NullLogger<TradeOrderStateRepository>.Instance,owner);
        if(fail)
        {
            await FluentActions.Awaiting(async()=>await repository.SaveStateAsync(context.Object,state,command)).Should().ThrowAsync<InvalidOperationException>();
            state.Events.Should().ContainSingle();state.CommittedStreamVersion.Should().Be(0);
            (await fixture.EventCountAsync(command.StreamId)).Should().Be(0);(await tradeDb.GetTradeOrderAsync(id)).Should().BeNull();
            projector.Verify(x=>x.DomainEventsProjectionAsync(It.IsAny<DomainEventCollection>()),Times.Never);
        }
        else
        {
            await repository.SaveStateAsync(context.Object,state,command);
            projector.Verify(x=>x.DomainEventsProjectionAsync(It.IsAny<DomainEventCollection>()),Times.Once);
        }
    }

    sealed class FailAfterOperation(IPostgresEventTransaction actual):IPostgresEventTransaction
    {
        public Task<T> ExecuteAsync<T>(Func<EnlistedEventTransaction,CancellationToken,Task<T>> operation,CancellationToken token=default)=>ExecuteOnceAsync(operation,token);
        public Task<T> ExecuteOnceAsync<T>(Func<EnlistedEventTransaction,CancellationToken,Task<T>> operation,CancellationToken token=default)
            =>actual.ExecuteOnceAsync<T>(async(db,ct)=>{await operation(db,ct);throw new InvalidOperationException("Injected failure before commit");},token);
    }
}
