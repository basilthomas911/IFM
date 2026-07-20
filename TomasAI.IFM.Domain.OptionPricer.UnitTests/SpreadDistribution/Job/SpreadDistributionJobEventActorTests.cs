using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests.SpreadDistribution.Job;

public class SpreadDistributionJobEventActorTests : IClassFixture<OptionPricerTestFixture>
{
    readonly OptionPricerTestFixture _fixture;

    public SpreadDistributionJobEventActorTests(OptionPricerTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableSpreadDistributionJobEventActor : SpreadDistributionJobEventActor
    {
        public TestableSpreadDistributionJobEventActor(IActorSupervisor supervisor, IStatusConsoleWriter statusConsoleWriter, ILogger<SpreadDistributionJobEventActor> logger)
            : base(supervisor, statusConsoleWriter, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IActorState state, IEvent @event)
            => await ReceiveAsync(context, state, @event);

        public async ValueTask<IActorState> InvokeOnLoadStateAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event)
            => await OnLoadStateAsync(context, threadId, @event);

        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);

        public async ValueTask InvokeOnStartAsync(IEventActorContext context)
            => await OnStartup(context);

        public async ValueTask InvokeOnStopAsync(IEventActorContext context)
            => await OnShutdown(context);
    }

}
