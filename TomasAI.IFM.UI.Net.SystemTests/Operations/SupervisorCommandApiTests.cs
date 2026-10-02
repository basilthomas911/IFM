using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.SystemTests.Operations;

public sealed class SupervisorCommandApiTests
{
    [Fact]
    public async Task ExecuteActorOperation_UsesNatsCommandRouteAndPreservesSafetyFields()
    {
        var producer = Substitute.For<IActorProducer>();
        RestartSupervisorActorCommand? sent = null;
        producer.RequestAsync<RestartSupervisorActorCommand, ActorEntityId, GuidResult>(
                Arg.Any<ActorSubject>(),
                Arg.Do<RestartSupervisorActorCommand>(value => sent = value),
                Arg.Any<ActorEntityId>(),
                Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult<ServiceResult<GuidResult>>(
                new ServiceOk<GuidResult>(new GuidResult(
                    call.Arg<RestartSupervisorActorCommand>().CommandId))));
        var target = new ActorThreadId(ActorType.Command, "ExampleCommand", "entity-42");
        var api = new SupervisorCommandApi(producer);

        var result = await api.ExecuteActorOperationAsync(target, 7, SupervisorActorOperationKind.Restart,
            "IFM.UI.Development", "Operator qualification", TimeSpan.FromMinutes(2));

        Assert.True(result.Success);
        Assert.NotNull(sent);
        Assert.Equal(RestartSupervisorActorCommand.Actor, sent.Subject.Name);
        Assert.Equal(RestartSupervisorActorCommand.Verb, sent.Subject.Verb);
        Assert.Equal(target, sent.Target);
        Assert.Equal(7, sent.ExpectedGeneration);
        Assert.Equal(SupervisorActorOperationKind.Restart, sent.OperationKind);
        Assert.Equal("IFM.UI.Development", sent.Requester);
        Assert.Equal("Operator qualification", sent.Reason);
        Assert.Equal(TimeSpan.FromMinutes(2), sent.Timeout);
    }
}
