using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Events;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
using ActorCommandExceptionEvent = TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class CommandExceptionEventActorTests
{
    [Fact]
    public async Task Command_exception_mailbox_accepts_and_terminates_generic_failure_event()
    {
        var mailbox = new Mock<IActorMailbox>();
        var producer = new Mock<IJSActorProducer>();
        producer
            .Setup(value => value.StartAsync(
                It.IsAny<ActorMailboxId>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        producer
            .Setup(value => value.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var supervisor = new Mock<IActorSupervisor>();
        supervisor
            .Setup(value => value.CreateMailbox(It.IsAny<ActorMailboxId>()))
            .Returns(mailbox.Object);
        supervisor
            .Setup(value => value.GetJSProducer(It.IsAny<ActorMailboxId>()))
            .Returns(producer.Object);

        var logger = new Mock<ILogger<CommandExceptionEventActor>>();
        var actor = new CommandExceptionEventActor(supervisor.Object, logger.Object);
        var errorEvent = new ActorCommandExceptionEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                CommandExceptionEventActor.Actor,
                ActorCommandExceptionEvent.CommandFail,
                ActorEntityId.Default.Format()),
            CommandName = "StartMarketDataFeedCommand",
            ErrorCode = 9999,
            ErrorMessage = "FuturesContract.Currency is required"
        };
        var message = new CommandExceptionMessage(errorEvent);

        await actor.StartAsync(supervisor.Object);
        await actor.HandleMessageAsync(message);
        await actor.StopAsync();

        Assert.Equal(
            new ActorMailboxId(ActorType.Event, CommandExceptionEventActor.Actor),
            actor.Id);
        Assert.Equal(1, message.ReleaseCount);
        producer.Verify(value => value.StartAsync(
            actor.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        logger.Verify(
            value => value.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("FuturesContract.Currency is required")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private sealed class CommandExceptionMessage(ActorCommandExceptionEvent errorEvent) : IActorMessage
    {
        public int ReleaseCount { get; private set; }
        public ActorSubject Subject => errorEvent.Subject;
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => errorEvent as TEvent;
        public TQuery? AsQuery<TQuery, TResult>()
            where TQuery : class, IQuery<TResult>
            where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() => ReleaseCount++;
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() => ReleasePayload();
    }
}
