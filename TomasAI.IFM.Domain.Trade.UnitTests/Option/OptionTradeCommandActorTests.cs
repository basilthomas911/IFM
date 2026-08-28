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
        : OptionTradeCommandActor(new OptionTradeCommandContext(Substitute.For<IActorSupervisor>(), dbEventSource, dbFactory, Substitute.For<IEventProjector<OptionTradeCommandActor>>(), logger))
    {
        public ICommand InvokeParseMessage(ICommandActorContext<OptionTradeCommandActor> context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(ICommandActorContext<OptionTradeCommandActor> context, IActorState state, ICommand cmd)
            => await ReceiveAsync(context, state, cmd);

        public async ValueTask InvokeOnValidateAsync(ICommandActorContext<OptionTradeCommandActor> context, ActorThreadId threadId, ICommand cmd)
            => await OnValidateAsync(context, threadId, cmd);

        public async ValueTask<IActorState> InvokeOnLoadStateAsync(ICommandActorContext<OptionTradeCommandActor> context, ActorThreadId threadId, ICommand cmd)
            => await OnLoadStateAsync(context, threadId, cmd);

        public async ValueTask InvokeOnSaveStateAsync(ICommandActorContext<OptionTradeCommandActor> context, ActorThreadId threadId, IActorState state, ICommand cmd)
            => await OnSaveStateAsync(context, threadId, state, cmd);

        public async ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(ICommandActorContext<OptionTradeCommandActor> context, ActorThreadId threadId, ICommand cmd, Exception ex)
            => await OnExceptionAsync(context, threadId, cmd, ex);

        public async ValueTask InvokeOnStartup(ICommandActorContext<OptionTradeCommandActor> context)
            => await OnStartup(context);
    }

    [Fact]
    public async Task Parse_and_validation_do_not_write_a_domain_local_command_audit()
    {
        var database = Substitute.For<IEventSourceActorDbContext>();
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

        var parsed = actor.InvokeParseMessage(
            Substitute.For<ICommandActorContext<OptionTradeCommandActor>>(),
            message);
        await actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext<OptionTradeCommandActor>>(),
            command.Subject.ThreadId,
            parsed);

        await database.DidNotReceive().InsertCommandLogAsync(
            Arg.Any<ICommand>(),
            Arg.Any<DateTime>(),
            Arg.Any<string>());
    }

}
