using TomasAI.IFM.Application.Api.Server.Core.MarketData.Imports;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Service.TradePosition.HostedService;
using TomasAI.IFM.TradePlan.HostedService;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class ApiHostedServiceExceptionBoundaryTests
{
    [Fact]
    public async Task Fmp_worker_contains_import_failure_and_stops_cleanly()
    {
        var coordinator = new ThrowingFmpCoordinator();
        var service = new FmpMarketDataImportHostedService(
            coordinator,
            new FmpImportScheduleOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromMilliseconds(10)
            },
            TimeProvider.System,
            new FixedValueDateProvider(new DateOnly(2026, 9, 30)),
            NullLogger<FmpMarketDataImportHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await coordinator.Invoked.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
        Assert.Equal(new DateOnly(2026, 9, 23), coordinator.Request!.FromInclusive);
        Assert.Equal(new DateOnly(2026, 10, 7), coordinator.Request.ToInclusive);
    }

    [Fact]
    public async Task Trade_position_consumer_start_and_stop_failures_do_not_escape()
    {
        var service = new TradePositionHostedService(
            new ThrowingTradePositionConsumer(),
            NullLogger<TradePositionHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Trade_plan_consumer_start_and_stop_failures_do_not_escape()
    {
        var service = new TradePlanHostedService(
            new ThrowingTradePlanConsumer(),
            NullLogger<TradePlanHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    sealed class ThrowingFmpCoordinator : IFmpMarketDataImportCoordinator
    {
        public FmpMarketDataImportRequest? Request { get; private set; }
        public TaskCompletionSource Invoked { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<FmpMarketDataImportResult> ImportAsync(
            FmpMarketDataImportRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            Invoked.TrySetResult();
            throw new InvalidOperationException("Injected FMP failure.");
        }
    }

    sealed class FixedValueDateProvider(DateOnly valueDate) : IValueDateProvider
    {
        public DateOnly ValueDate => valueDate;
        public DateOnly GetValueDate(DateTimeOffset instant) => valueDate;
    }

    sealed class ThrowingTradePositionConsumer : ITradePositionEventConsumer
    {
        public ValueTask StartAsync() => ValueTask.FromException(
            new InvalidOperationException("Injected trade-position startup failure."));

        public ValueTask StopAsync() => ValueTask.FromException(
            new InvalidOperationException("Injected trade-position shutdown failure."));
    }

    sealed class ThrowingTradePlanConsumer : ITradePlanEventConsumer
    {
        public ValueTask StartAsync() => ValueTask.FromException(
            new InvalidOperationException("Injected trade-plan startup failure."));

        public ValueTask StopAsync() => ValueTask.FromException(
            new InvalidOperationException("Injected trade-plan shutdown failure."));
    }
}
