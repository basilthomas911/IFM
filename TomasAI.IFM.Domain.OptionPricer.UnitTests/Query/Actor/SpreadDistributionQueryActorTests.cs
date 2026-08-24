using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Domain.OptionPricer.Shared.Queries;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests.Query.Actor;

public class SpreadDistributionQueryActorTests
{
    sealed class TestableSpreadDistributionQueryActor(
        IDbContextFactory dbFactory,
        ILogger<SpreadDistributionQueryActor> logger)
        : SpreadDistributionQueryActor(new SpreadDistributionQueryContext(Substitute.For<IActorSupervisor>(), dbFactory, logger))
    {
        public ValueTask InvokeReceiveAsync(
            IQueryActorContext context,
            IQuery query,
            CancellationToken cancellationToken)
            => ReceiveAsync(context, query, cancellationToken);
    }

    [Fact]
    public async Task ReceiveAsync_WithCancellation_PropagatesTokenAndDoesNotReply()
    {
        var optionPricerDb = Substitute.For<IOptionPricerDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.OptionPricerDb.Returns(optionPricerDb);
        var actor = new TestableSpreadDistributionQueryActor(
            dbFactory,
            Substitute.For<ILogger<SpreadDistributionQueryActor>>());
        var query = new GetSpreadDistributionQuery(
            1,
            TradeType.ShortIronCondor,
            TradeStatus.Open,
            new DateOnly(2026, 8, 6),
            30);
        var context = Substitute.For<IQueryActorContext>();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        optionPricerDb.GetSpreadDistributionAsync(
                query.TradeId,
                query.TradeType,
                query.TradeStatus,
                query.ValueDate,
                query.DaysToExpiry,
                cancellation.Token)
            .Returns(async _ =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
                return null;
            });

        var operation = actor.InvokeReceiveAsync(context, query, cancellation.Token).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        Func<Task> act = async () => await operation;

        await act.Should().ThrowAsync<OperationCanceledException>();
        await optionPricerDb.Received(1).GetSpreadDistributionAsync(
            query.TradeId,
            query.TradeType,
            query.TradeStatus,
            query.ValueDate,
            query.DaysToExpiry,
            cancellation.Token);
        context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default,
            default!,
            default(ServiceResult<SpreadDistributionReadModel?>)!);
    }
}
