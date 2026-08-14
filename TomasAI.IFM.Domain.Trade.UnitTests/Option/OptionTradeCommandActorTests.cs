using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Option.Command;
using TomasAI.IFM.Domain.Trade.Option.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Option.Command.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Option;

public class OptionTradeCommandActorTests : IClassFixture<TradeFixture>
{
    readonly TradeFixture _fixture;

    public OptionTradeCommandActorTests(TradeFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableOptionTradeCommandActor(
        IEventSourceActorDbContext dbEventSource,
        IDbContextFactory dbFactory,
        ILogger<OptionTradeCommandActor> logger)
        : OptionTradeCommandActor(dbEventSource, dbFactory, Substitute.For<IEventProjector<OptionTradeCommandActor>>(), logger)
    {
        public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(ICommandActorContext context, IActorState state, ICommand cmd)
            => await ReceiveAsync(context, state, cmd);

        public async ValueTask InvokeOnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
            => await OnValidateAsync(context, threadId, cmd);

        public async ValueTask<IActorState> InvokeOnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
            => await OnLoadStateAsync(context, threadId, cmd);

        public async ValueTask InvokeOnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand cmd)
            => await OnSaveStateAsync(context, threadId, state, cmd);

        public async ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd, Exception ex)
            => await OnExceptionAsync(context, threadId, cmd, ex);

        public async ValueTask InvokeOnStartup(ICommandActorContext context)
            => await OnStartup(context);
    }

    [Fact]
    public async Task Parse_does_not_block_on_command_audit_and_validation_awaits_it()
    {
        var auditCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var database = Substitute.For<IEventSourceActorDbContext>();
        database.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(auditCompletion.Task);
        var actor = _fixture.CreateActor(database);
        var command = new DeleteOptionTradeCommand(1, 2) with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                OptionTradeCommandActor.ActorName,
                DeleteOptionTradeCommand.Verb,
                "1-2")
        };
        var message = new NatsMsg<byte[]>
        {
            Subject = command.Subject.ToString(),
            Data = _fixture.DataSerializer.Serialize(command)
        };

        var parseTask = Task.Run(() => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message));
        var completed = await Task.WhenAny(parseTask, Task.Delay(TimeSpan.FromSeconds(1)));

        completed.Should().BeSameAs(parseTask);
        var parsed = await parseTask;
        var validationTask = actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(),
            command.Subject.ThreadId,
            parsed).AsTask();
        await Task.Delay(25);
        validationTask.IsCompleted.Should().BeFalse();

        auditCompletion.SetResult();
        await validationTask;
        await database.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(value => value.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>());
    }

}
