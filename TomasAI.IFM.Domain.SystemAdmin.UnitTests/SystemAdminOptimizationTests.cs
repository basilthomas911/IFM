using System.Collections;
using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Query.Api;
using TomasAI.IFM.Domain.SystemAdmin.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public class SystemAdminOptimizationTests
{
    [Fact]
    public async Task BackupStateLoad_CreatesFreshStateWithoutReadingEventStorage()
    {
        var expected = new SystemAdminCommandState();
        var stateFactory = Substitute.For<IEventSourceActorStateFactory>();
        stateFactory.CreateState<SystemAdminCommandState>().Returns(expected);
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var repository = new SystemAdminStateRepository(
            stateFactory,
            eventSource,
            Substitute.For<IActorService>(),
            Substitute.For<ILogger<SystemAdminStateRepository>>());
        var command = new BackupDatabaseCommand("eventdb", default, 300);

        var result = await repository.LoadStateAsync(command);

        result.Should().BeSameAs(expected);
        stateFactory.Received(1).CreateState<SystemAdminCommandState>();
        eventSource.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public void DatabaseNames_AreCachedAndReadOnly()
    {
        var first = SystemAdminQueryState.GetDatabaseNames();
        var second = SystemAdminQueryState.GetDatabaseNames();

        second.Should().BeSameAs(first);
        first.Names.Should().HaveCount(7);
        var collection = (IList)first.Names;
        var mutate = () => collection.Add("otherdb");
        mutate.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void DatabaseNames_RemainSerializableAcrossActorBoundaries()
    {
        var source = SystemAdminQueryState.GetDatabaseNames();

        var bytes = MessagePackSerializer.Serialize(source);
        var restored = MessagePackSerializer.Deserialize<Shared.ViewModels.DatabaseNamesReadModel>(bytes);

        restored.Names.Should().Equal(source.Names);
        ((IList)restored.Names).IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public async Task DirectQueryApi_ReusesCompletedImmutableResult()
    {
        var api = new ActorSystemAdminQueryApi();

        var firstTask = api.GetDatabaseNamesAsync();
        var secondTask = api.GetDatabaseNamesAsync();
        var first = await firstTask;
        var second = await secondTask;

        secondTask.Should().BeSameAs(firstTask);
        second.Should().BeSameAs(first);
        second.Value.Should().BeSameAs(first.Value);
    }
}
