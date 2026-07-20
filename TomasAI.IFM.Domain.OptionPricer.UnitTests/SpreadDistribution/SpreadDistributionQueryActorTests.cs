using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.State;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.Queries;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests.SpreadDistribution;

public class SpreadDistributionQueryActorTests : IClassFixture<OptionPricerTestFixture>
{
    readonly OptionPricerTestFixture _fixture;

    public SpreadDistributionQueryActorTests(OptionPricerTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected methods for unit testing.
    public class TestableSpreadDistributionQueryActor : SpreadDistributionQueryActor
    {
        public TestableSpreadDistributionQueryActor(IDbContextFactory dbFactory, ILogger<SpreadDistributionQueryActor> logger)
            : base(dbFactory, logger)
        {
        }

        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IActorState state, IQuery query)
            => await ReceiveAsync(context, state, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);

        public async ValueTask<IActorState> InvokeOnLoadStateAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query)
            => await OnLoadStateAsync(context, threadId, query);
    }

}
