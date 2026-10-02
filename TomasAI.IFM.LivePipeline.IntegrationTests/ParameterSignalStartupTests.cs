using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class ParameterSignalStartupTests
{
    [Fact]
    public async Task Reference_reconciliation_imports_configured_calendar_window()
    {
        var valueDate = new DateOnly(2026, 9, 29);
        FmpMarketDataImportRequest? captured = null;
        var imports = Substitute.For<IFmpMarketDataImportCoordinator>();
        imports.ImportAsync(Arg.Any<FmpMarketDataImportRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.Arg<FmpMarketDataImportRequest>();
                return new FmpMarketDataImportResult(
                    captured.FromInclusive,
                    captured.ToInclusive,
                    15,
                    15,
                    0,
                    0,
                    false,
                    []);
            });
        var activities = new ApiApplicationStartupActivities(
            marketSessionAuthority: null!,
            contractAuthority: null!,
            rolloverCheck: null!,
            referenceImportCoordinator: imports,
            marketDataFeedCommandApi: null!,
            marketDataFeedQueryApi: null!,
            analyticsCommandApi: null!,
            historicalDataLoaderStore: null!,
            dbContextFactory: null!,
            marketDataApi: null!,
            marketOutlookWriter: null!,
            marketOutlookOperations: null!,
            marketOutlookCache: null!,
            historicalWarmupOptions: new(),
            fmpImportScheduleOptions: new()
            {
                LookbackDays = 7,
                ForwardDays = 7,
                CountryCodes = ["US"]
            },
            options: new(),
            timeProvider: TimeProvider.System,
            logger: NullLogger<ApiApplicationStartupActivities>.Instance);

        var outcome = await activities.ReconcileReferenceDataAsync(
            new ApplicationStartupContext(valueDate, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(ApplicationStartupActivityOutcome.Started, outcome);
        Assert.NotNull(captured);
        Assert.Equal(new DateOnly(2026, 9, 22), captured.FromInclusive);
        Assert.Equal(new DateOnly(2026, 10, 6), captured.ToInclusive);
        Assert.False(captured.IncludeTreasury);
        Assert.True(captured.IncludeEconomicCalendar);
        Assert.Equal(["US"], captured.CountryCodes);
    }
}
