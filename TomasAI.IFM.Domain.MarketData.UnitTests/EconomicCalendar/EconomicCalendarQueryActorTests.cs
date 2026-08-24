using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query.Actor;


namespace TomasAI.IFM.Domain.MarketData.UnitTests.EconomicCalendar;

public class EconomicCalendarQueryActorTests : IClassFixture<EconomicCalendarTestFixture>
{
    readonly EconomicCalendarTestFixture _fixture;

    public EconomicCalendarQueryActorTests(EconomicCalendarTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableEconomicCalendarQueryActor : EconomicCalendarQueryActor
    {
        public TestableEconomicCalendarQueryActor(
            IDbContextFactory dbFactory,
            ILogger<EconomicCalendarQueryActor> logger)
            : this(CreateContext(dbFactory, logger))
        {
        }

        TestableEconomicCalendarQueryActor(IEconomicCalendarQueryContext context) : base(context)
            => Context = context;

        static IEconomicCalendarQueryContext CreateContext(IDbContextFactory dbFactory, ILogger<EconomicCalendarQueryActor> logger)
        {
            var context = Substitute.For<IEconomicCalendarQueryContext>();
            context.ActorId.Returns(new ActorMailboxId(ActorType.Query, EconomicCalendarQueryActor.ActorName));
            context.DbFactory.Returns(dbFactory);
            context.Logger.Returns(logger);
            return context;
        }

        public IEconomicCalendarQueryContext Context { get; }

        public IQuery InvokeParseMessage(IQueryActorContext<EconomicCalendarQueryActor> context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext<EconomicCalendarQueryActor> context, IQuery query)
            => await ReceiveAsync(context, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext<EconomicCalendarQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);


    }
}

