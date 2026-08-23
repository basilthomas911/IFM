using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Exceptions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;


namespace TomasAI.IFM.Domain.MarketData.UnitTests.EconomicCalendar;

public class EconomicCalendarCommandActorTests : IClassFixture<EconomicCalendarTestFixture>
{
    readonly EconomicCalendarTestFixture _fixture;

    public EconomicCalendarCommandActorTests(EconomicCalendarTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ParseMessage_IncompleteAudit_DoesNotBlockActorThread()
    {
        var pendingAudit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(pendingAudit.Task);
        var actor = _fixture.CreateActor(
            dbEventSource,
            Substitute.For<ILogger<EconomicCalendarCommandActor>>());
        var command = new AddEconomicCalendarCommand(SampleData.EconomicCalendar)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                EconomicCalendarCommandActor.Actor,
                AddEconomicCalendarCommand.Verb,
                SampleData.EconomicCalendar.Id.Format())
        };
        var message = new NatsMsg<byte[]>
        {
            Subject = command.Subject.ToString(),
            Data = ActorExtensions.DataSerializer!.Serialize(command)
        };

        var parsed = actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        parsed.CommandId.Should().Be(command.CommandId);
        pendingAudit.Task.IsCompleted.Should().BeFalse();
        pendingAudit.SetResult();
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableEconomicCalendarCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<EconomicCalendarCommandActor> logger)
        : EconomicCalendarCommandActor(CreateContext(dbEventSource, logger))
    {
        static ICommandActorContext<EconomicCalendarCommandActor> CreateContext(
            IEventSourceActorDbContext dbEventSource, ILogger<EconomicCalendarCommandActor> logger)
        {
            var context = Substitute.For<IEconomicCalendarCommandContext>();
            context.ActorId.Returns(new ActorMailboxId(ActorType.Command, EconomicCalendarCommandActor.Actor));
            context.DbEventSource.Returns(dbEventSource);
            context.Logger.Returns(logger);
            return context;
        }
        public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(ICommandActorContext context, IActorState state, ICommand cmd)
            => await ReceiveAsync(context, state, cmd);

        public async ValueTask InvokeOnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
            => await OnValidateAsync(context, threadId, cmd);

        public async ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd, Exception ex)
            => await OnExceptionAsync(context, threadId, cmd, ex);
    }

    
}

