using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.SystemAdmin.BDDTests.ScheduledTask;

public sealed class ScheduledTaskClockOriginFeatureTests
{
    [Fact]
    public void Given_a_controlled_server_clock_when_catalog_command_is_executed_then_source_times_use_that_clock()
    {
        var given = new ScheduledTaskScenario();
        var command = given.Register();
        Assert.True(given.Send(command).Success);
        var source = Assert.IsType<ScheduledTaskCatalogProjectRegisteredEvent>(Assert.Single(given.CatalogState.Events));
        Assert.Equal(ScheduledTaskScenario.Now, MessageOrigins.Get(command)!.OriginatedAtUtc);
        Assert.Equal(ScheduledTaskScenario.Now, MessageOrigins.Get(source)!.OriginatedAtUtc);
        Assert.Equal(ScheduledTaskScenario.Now.UtcDateTime, source.ReceivedOn);
        var catalog = given.Catalog;
        given.RestartFromRecordedEvents();
        Assert.Equivalent(catalog, given.Catalog, strict: true);
        Assert.Empty(given.CatalogState.Events);
    }

    [Fact]
    public void Given_a_legacy_command_with_unknown_origin_when_forwarded_then_current_time_is_not_invented()
    {
        var command = new ScheduledTaskScenario().Register();
        MessageOrigins.Restore(command, null);
        Assert.Null(MessageOrigins.ForPublication(command, TimeProvider.System));
        Assert.Null(MessageOrigins.Get(command));
    }
}
