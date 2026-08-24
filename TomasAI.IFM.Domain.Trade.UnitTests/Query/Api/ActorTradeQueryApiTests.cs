using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Query.Extensions;
using TomasAI.IFM.Domain.Trade.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Query.Api;

public class ActorTradeQueryApiTests
{
    [Fact]
    public async Task TradeQuantityUsesDirectStorageAndReturnsTypedSuccess()
    {
        var (api, db) = CreateApi();
        db.GetTradeQuantityAsync(7).Returns(4);

        var result = await api.GetTradeQuantityAsync(7);

        api.Should().BeAssignableTo<ITradeQueryContext>();
        result.Success.Should().BeTrue();
        result.Value!.Value.Should().Be(4);
        await db.Received(1).GetTradeQuantityAsync(7);
    }

    [Fact]
    public async Task StorageFailureReturnsTheQueryErrorId()
    {
        var (api, db) = CreateApi();
        var exception = new InvalidOperationException("trade unavailable");
        db.GetTradeQuantityAsync(Arg.Any<int>())
            .Returns(_ => Task.FromException<int>(exception));

        var result = await api.GetTradeQuantityAsync(7);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(GetTradeQuantityQuery.ErrorId);
        result.ErrorMessage.Should().Be(exception.Message);
    }

    [Fact]
    public async Task TradePlanSummaryIsExplicitlyNotImplementedPendingUiRemoval()
    {
        var (api, _) = CreateApi();

        var action = () => api.GetTradePlanSummaryAsync(1, 2, new DateOnly(2026, 1, 5));

        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task CancellationUsesTokenAwareStorageAndIsNotConvertedToFailure()
    {
        var (api, db) = CreateApi();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        db.GetTradeQuantityAsync(7, cancellation.Token)
            .Returns(async _ =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
                return 0;
            });

        var operation = api.GetTradeQuantityAsync(7, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        Func<Task> act = async () => await operation;

        await act.Should().ThrowAsync<OperationCanceledException>();
        await db.Received(1).GetTradeQuantityAsync(7, cancellation.Token);
    }

    static (ITradeQueryContext Api, ITradeDbContext Db) CreateApi()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<ITradeDbContext>();
        dbFactory.TradeDb.Returns(db);
        var context = Substitute.For<ITradeQueryContext>();
        context.DbFactory.Returns(dbFactory);
        context.BlackboardService.Returns(Substitute.For<IBlackboardService>());
        return (context, db);
    }
}
