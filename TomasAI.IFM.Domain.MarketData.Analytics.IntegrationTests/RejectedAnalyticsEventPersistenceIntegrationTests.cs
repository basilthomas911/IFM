using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests;

/// <summary>Checks the state-to-repository boundary for rejected Analytics events.</summary>
[Trait("Category", "Integration")]
public sealed class RejectedAnalyticsEventPersistenceIntegrationTests
{
    [Fact]
    public async Task RejectedStateEventCannotCauseAnEventLogWrite()
    {
        var eventDb = Substitute.For<IEventSourceActorDbContext>();
        var repository = new MarketOutlookSnapshotStateRepository(
            Substitute.For<IEventSourceActorStateFactory>(), eventDb,
            Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<MarketOutlookSnapshotCommandActor>>(),
            NullLogger<MarketOutlookSnapshotStateRepository>.Instance);
        var command = new InsertMarketOutlookSnapshotCommand(new MarketOutlookReadModel
        {
            ContractId = "ESU6", ValueDate = new DateOnly(2026, 9, 15)
        });
        var state = new MarketOutlookSnapshotCommandState { Id = command.Subject.ThreadId };
        Assert.False(state.Update(Substitute.For<IEvent>()));

        await repository.SaveStateAsync(Substitute.For<ICommandActorContext>(), state, command);

        Assert.Empty(state.Events);
        eventDb.DidNotReceiveWithAnyArgs().SaveEventsAsync(default!, default, default!);
        eventDb.DidNotReceiveWithAnyArgs().SaveEventsAsync(
            default!, default, default!, default(CancellationToken));
    }
}
