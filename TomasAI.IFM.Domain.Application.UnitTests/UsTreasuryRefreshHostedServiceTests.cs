using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.ReferenceData;

namespace TomasAI.IFM.Domain.Application.UnitTests;

public sealed class UsTreasuryRefreshHostedServiceTests
{
    sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
    }
    sealed class Readiness(bool healthy) : IApplicationBootstrapReadiness
    {
        public TaskCompletionSource Checked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken) { Checked.TrySetResult(); return Task.FromResult(healthy); }
    }
    sealed class Imports : IFmpMarketDataImportCoordinator
    {
        public TaskCompletionSource<FmpMarketDataImportRequest> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<FmpMarketDataImportResult> ImportAsync(FmpMarketDataImportRequest request, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult(request);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException();
        }
    }
    [Fact]
    public async Task Startup_does_not_wait_for_import_and_shutdown_cancels_pending_submission()
    {
        var clock = new Clock(); var imports = new Imports();
        using var client = new HttpClient(); using var source = new UsTreasuryCurve(client, clock);
        using var service = new UsTreasuryRefreshHostedService(new Readiness(true), imports,
            new TreasuryPricingProvider(source, clock), UsTreasuryPublicationCalendar.Default2026,
            clock, NullLogger<UsTreasuryRefreshHostedService>.Instance);
        await service.StartAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        var request = await imports.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new DateOnly(2026, 9, 4), request.FromInclusive);
        Assert.Equal(request.FromInclusive, request.ToInclusive);
        Assert.True(request.IncludeTreasury); Assert.False(request.IncludeEconomicCalendar);
        await service.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
    }
    [Fact]
    public async Task No_import_is_submitted_before_actor_bootstrap_is_healthy()
    {
        var clock = new Clock(); var imports = new Imports(); var readiness = new Readiness(false);
        using var client = new HttpClient(); using var source = new UsTreasuryCurve(client, clock);
        using var service = new UsTreasuryRefreshHostedService(readiness, imports,
            new TreasuryPricingProvider(source, clock), UsTreasuryPublicationCalendar.Default2026,
            clock, NullLogger<UsTreasuryRefreshHostedService>.Instance);
        await service.StartAsync(default);
        await readiness.Checked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(imports.Entered.Task.IsCompleted);
        await service.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
    }
}
