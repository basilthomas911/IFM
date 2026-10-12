using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;
using TomasAI.IFM.Domain.EventSourcing.Command;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.CommandDeduplication;

public sealed class CommandAuditCoordinatorTests
{
    [Fact]
    public async Task Binary_reservation_persists_once_and_rejects_changed_command_facts()
    {
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.TryInsertCommandAuditAsync(Arg.Any<CommandAuditEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(true));
        var coordinator = new CommandAuditCoordinator(db);
        var command = CommandAuditPersistenceTests.TestCommand.Create(Guid.NewGuid(), 42);

        Assert.True((await coordinator.TryReserveAsync(command)).Accepted);
        Assert.False((await coordinator.TryReserveAsync(command)).Accepted);
        await Assert.ThrowsAsync<CommandAuditPayloadConflictException>(
            () => coordinator.TryReserveAsync(command with { Value = 43 }).AsTask());
        await db.Received(1).TryInsertCommandAuditAsync(Arg.Any<CommandAuditEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Legacy_reservation_uses_the_configured_json_persistence_path()
    {
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.TryInsertCommandLogAsync(Arg.Any<TomasAI.IFM.Shared.EventSourcing.ICommand>(),
            Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        var coordinator = new CommandAuditCoordinator(db, options: new()
        { WriteMode = CommandAuditWriteMode.SequentialJsonLegacy });
        var command = CommandAuditPersistenceTests.TestCommand.Create(Guid.NewGuid(), 42);

        Assert.True((await coordinator.TryReserveAsync(command)).Accepted);
        Assert.False((await coordinator.TryReserveAsync(command)).Accepted);
        await db.Received(1).TryInsertCommandLogAsync(command, Arg.Any<DateTime>(),
            Arg.Is<string>(json => json.Contains("42")), Arg.Any<CancellationToken>());
        await db.DidNotReceive().TryInsertCommandAuditAsync(Arg.Any<CommandAuditEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preparse_acceptance_is_consumed_by_only_one_actor_execution()
    {
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.TryInsertCommandLogAsync(Arg.Any<TomasAI.IFM.Shared.EventSourcing.ICommand>(),
            Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        var coordinator = new CommandAuditCoordinator(db);
        var command = CommandAuditPersistenceTests.TestCommand.Create(Guid.NewGuid(), 42);
        await coordinator.InsertCommandLogAsync(command, DateTime.UtcNow, "payload");

        Assert.True((await coordinator.TryReserveAsync(command)).Accepted);
        Assert.False((await coordinator.TryReserveAsync(command)).Accepted);
        await db.DidNotReceive().TryInsertCommandAuditAsync(Arg.Any<CommandAuditEnvelope>(), Arg.Any<CancellationToken>());
    }
}
