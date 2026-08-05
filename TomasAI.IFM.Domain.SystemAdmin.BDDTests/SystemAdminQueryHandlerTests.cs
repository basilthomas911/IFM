using FluentAssertions;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.Shared;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ViewModels;
using TomasAI.IFM.Domain.SystemAdmin.Command.State;

namespace TomasAI.IFM.Domain.SystemAdmin.BDDTests;

public class SystemAdminQueryHandlerTests 
{
    [Fact]
    public void DatabaseNames_AreReturnedAsAStableReadOnlySnapshot()
    {
        var first = SystemAdminQueryState.GetDatabaseNames();
        var second = SystemAdminQueryState.GetDatabaseNames();

        second.Should().BeSameAs(first);
        first.Names.Should().Contain(DatabaseBackupNames.EventDb);
        first.Names.Should().Contain(DatabaseBackupNames.TradeDb);
    }
}
