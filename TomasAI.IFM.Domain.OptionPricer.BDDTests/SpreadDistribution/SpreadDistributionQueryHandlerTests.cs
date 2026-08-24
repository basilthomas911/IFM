using FluentAssertions;
using NATS.Client.Core;
using NSubstitute;
using NSubstitute.Extensions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Query.Actor;
using TomasAI.IFM.Domain.OptionPricer.Shared.Queries;

namespace TomasAI.IFM.Domain.OptionPricer.BDDTests.SpreadDistribution;

public class SpreadDistributionQueryHandlerTests
{
    [Fact]
    public async Task ReceiveAsync_GetSpreadDistributionQuery_RepliesWithTypedResultAndQueryVerb()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IOptionPricerDbContext>();
        dbFactory.OptionPricerDb.Returns(db);
        db.GetSpreadDistributionAsync(
                Arg.Any<int>(), Arg.Any<TradeType>(), Arg.Any<TradeStatus>(),
                Arg.Any<DateOnly>(), Arg.Any<int>())
            .Returns(Task.FromResult<SpreadDistributionReadModel?>(null));

        var actor = new TestableSpreadDistributionQueryActor(
            dbFactory, Substitute.For<ILogger<SpreadDistributionQueryActor>>());
        var query = new GetSpreadDistributionQuery(
            42, TradeType.ShortIronCondor, TradeStatus.Open,
            new DateOnly(2026, 8, 2), 30);
        var context = Substitute.For<IQueryActorContext<SpreadDistributionQueryActor>>();

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetSpreadDistributionQuery.Verb,
            Arg.Is<ServiceResult<SpreadDistributionReadModel?>>(result =>
                result.Success && result.Value == null));
    }

    sealed class TestableSpreadDistributionQueryActor(
        IDbContextFactory dbFactory,
        ILogger<SpreadDistributionQueryActor> logger)
        : SpreadDistributionQueryActor(new SpreadDistributionQueryContext(Substitute.For<IActorSupervisor>(), dbFactory, logger))
    {
        public ValueTask InvokeReceiveAsync(IQueryActorContext<SpreadDistributionQueryActor> context, IQuery query)
            => base.ReceiveAsync(context, query);
    }
}
