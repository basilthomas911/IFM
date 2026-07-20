using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Command;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.Exceptions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Trade;


namespace TomasAI.IFM.Domain.Reference.UnitTests.EconomicCalendar;

public class EconomicCalendarCommandActorTests : IClassFixture<ReferenceTestFixture>
{
    readonly ReferenceTestFixture _fixture;

    public EconomicCalendarCommandActorTests(ReferenceTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableEconomicCalendarCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<EconomicCalendarCommandActor> logger)
        : EconomicCalendarCommandActor(dbEventSource, logger)
    {
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

