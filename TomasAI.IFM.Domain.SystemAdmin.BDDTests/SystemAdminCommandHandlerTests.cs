using FluentAssertions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.SystemAdmin.Shared;
using TomasAI.IFM.Domain.SystemAdmin.Shared.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.Events;
using TomasAI.IFM.Domain.SystemAdmin.Command;
using TomasAI.IFM.Domain.SystemAdmin.Command.State;

namespace TomasAI.IFM.Domain.SystemAdmin.BDDTests;

public class SystemAdminCommandHandlerTests
{
    [Fact]
    public void BackupCommand_ProducesOneDurableBackupEvent()
    {
        var command = new BackupDatabaseCommand(
            SampleData.DatabaseName,
            SampleData.BackupType,
            SampleData.CommandTimeout)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                BackupDatabaseCommand.Actor,
                BackupDatabaseCommand.Verb,
                SampleData.DatabaseName)
        };
        var state = new SystemAdminCommandState();

        var changed = command.Execute(state);

        changed.Should().BeTrue();
        state.Events.Should().ContainSingle().Which.Should().BeOfType<DatabaseBackupEvent>();
    }
}
