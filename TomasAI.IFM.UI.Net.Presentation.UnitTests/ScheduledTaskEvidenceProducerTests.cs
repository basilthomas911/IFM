using NSubstitute;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;

public sealed class ScheduledTaskEvidenceProducerTests
{
    [Fact]
    public async Task Enable_queries_the_selected_scope_and_sends_the_returned_snapshot()
    {
        var commands = Substitute.For<IScheduledTaskCommandApi>();
        var queries = Substitute.For<IScheduledTaskQueryApi>();
        var evidence = new ScheduledTaskCatalog { Revision = 17, Environment = "Production", HostId = "host-two" };
        queries.GetScheduledTaskCatalogAsync(Arg.Any<GetScheduledTaskCatalogQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<ScheduledTaskCatalog>(evidence));
        commands.EnableScheduledTaskAsync(Arg.Any<EnableScheduledTaskCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<GuidResult>(new(Guid.NewGuid())));
        var service = new ScheduledTaskService(commands, queries, Substitute.For<IScheduledTaskUIEventConsumer>());
        var id = Guid.NewGuid(); using var deadline = new CancellationTokenSource();
        var result = await service.SetEnabledAsync(id, 4, true, "Production", "host-two", deadline.Token);
        Assert.True(result.IsSuccess);
        await queries.Received(1).GetScheduledTaskCatalogAsync(Arg.Is<GetScheduledTaskCatalogQuery>(q => q.Environment == "Production" && q.HostId == "host-two"), deadline.Token);
        await commands.Received(1).EnableScheduledTaskAsync(Arg.Is<EnableScheduledTaskCommand>(c => c.EntityId.Value == id && c.ExpectedRevision == 4 && ReferenceEquals(c.CatalogEvidence, evidence)), deadline.Token);
    }

    [Fact]
    public async Task Failed_catalog_query_does_not_send_an_enable_command()
    {
        var commands = Substitute.For<IScheduledTaskCommandApi>();
        var queries = Substitute.For<IScheduledTaskQueryApi>();
        queries.GetScheduledTaskCatalogAsync(Arg.Any<GetScheduledTaskCatalogQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceFailed<ScheduledTaskCatalog>(9300, "catalog unavailable"));
        var service = new ScheduledTaskService(commands, queries, Substitute.For<IScheduledTaskUIEventConsumer>());
        var result = await service.SetEnabledAsync(Guid.NewGuid(), 4, true, "Production", "host-two");
        Assert.False(result.IsSuccess);
        await commands.DidNotReceiveWithAnyArgs().EnableScheduledTaskAsync(default!, default);
    }

    [Fact]
    public async Task Disable_does_not_need_catalog_evidence()
    {
        var commands = Substitute.For<IScheduledTaskCommandApi>();
        var queries = Substitute.For<IScheduledTaskQueryApi>();
        commands.DisableScheduledTaskAsync(Arg.Any<DisableScheduledTaskCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<GuidResult>(new(Guid.NewGuid())));
        var service = new ScheduledTaskService(commands, queries, Substitute.For<IScheduledTaskUIEventConsumer>());
        await service.SetEnabledAsync(Guid.NewGuid(), 4, false, "Production", "host-two");
        await queries.DidNotReceiveWithAnyArgs().GetScheduledTaskCatalogAsync(default!, default);
        await commands.ReceivedWithAnyArgs(1).DisableScheduledTaskAsync(default!, default);
    }
}
