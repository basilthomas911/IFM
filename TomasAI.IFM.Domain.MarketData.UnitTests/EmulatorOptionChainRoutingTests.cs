using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class EmulatorOptionChainRoutingTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Frozen_preview_uses_stored_backend_inputs_without_calling_live_host(bool frozenOnly, bool running)
    {
        var api = Substitute.For<IMarketDataApi>();
        var date = new DateOnly(2026, 10, 30);
        api.GetRuntimeStatus().Returns(new MarketDataFeedRuntimeStatusReadModel { IsRunning = running, ActiveValueDate = running ? date : null });
        var factory = Substitute.For<IDbContextFactory>();
        var securities = Substitute.For<ISecuritiesDbContext>();
        var market = Substitute.For<IMarketDataDbContext>();
        factory.SecuritiesDb.Returns(securities);
        factory.MarketDataDb.Returns(market);
        var query = new GetEvaluatedOptionChainQuery
        {
            UnderlyingContractId = "ES20261218", UnderlyingSymbol = "ES",
            ExpiryDate = new DateOnly(2099, 12, 18), ProviderRoots = ["ES"],
            AllowFrozenEmulatorPreview = true, FrozenEmulatorPreviewOnly = frozenOnly,
            StandardDeviationMultiplier = 2.5
        };
        securities.GetCachedOptionContractDefinitionsAsync(new OptionDefinitionPartition(query.UnderlyingSymbol, query.UnderlyingContractId, query.ExpiryDate), query.ProviderRoots, Arg.Any<CancellationToken>()).Returns([
                new CachedOptionContractDefinitionReadModel { Definition = new FuturesOptionContractReadModel {
                    ContractId = "ES-C-5000", StrikePrice = 5000, OptionType = "Call",
                    ExpirationUtc = new DateTimeOffset(2099, 12, 18, 0, 0, 0, TimeSpan.Zero), TickSize = 0.25m
                }}
            ]);
        market.GetFuturesEodDataAsync(query.UnderlyingContractId, Arg.Any<DateOnly>()).Returns(
            new FuturesEodDataV2ReadModel { ClosePrice = 5000, DailyStdDevAmount = 50, ValueDate = date });
        var context = Substitute.For<IMarketDataQueryContext>();
        context.DbFactory.Returns(factory);
        context.MarketDataApi.Returns(api);
        context.Logger.Returns(Substitute.For<ILogger<MarketDataQueryActor>>());
        context.MarketSessionAuthority.Returns(new FuturesMarketSessionAuthority(TimeProvider.System));

        var result = await QualifiedEvaluatedOptionChain.ExecuteAsync(query, context, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.StartsWith("Frozen emulator preview", result.Value!.WindowMethod);
        var quote = Assert.Single(result.Value.Contracts);
        Assert.True(quote.IsStale);
        Assert.True(quote.Bid > 0);
        Assert.True(quote.GreeksValid);
        await api.DidNotReceive().GetFuturesPriceAsync(Arg.Any<string>());
        await api.DidNotReceive().EvaluateOptionChainAsync(Arg.Any<GetEvaluatedOptionChainQuery>(), Arg.Any<CancellationToken>());
        if (frozenOnly) api.DidNotReceive().GetRuntimeStatus();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task Live_requests_and_release_requests_keep_the_native_host_route(bool allowFrozen, bool release)
    {
        var api = Substitute.For<IMarketDataApi>();
        api.GetRuntimeStatus().Returns(new MarketDataFeedRuntimeStatusReadModel {
            IsRunning = true, ActiveValueDate = new DateOnly(2026, 10, 30)
        });
        var context = Substitute.For<IMarketDataQueryContext>();
        context.MarketDataApi.Returns(api);
        var query = new GetEvaluatedOptionChainQuery { AllowFrozenEmulatorPreview = allowFrozen,
            FrozenEmulatorPreviewOnly = release, ReleaseOnly = release };
        api.EvaluateOptionChainAsync(query, Arg.Any<CancellationToken>()).Returns(
            new ServiceFailed<EvaluatedOptionChainReadModel>(503, "Host test response"));

        var result = await QualifiedEvaluatedOptionChain.ExecuteAsync(query, context, CancellationToken.None);

        Assert.Equal("Host test response", result.ErrorMessage);
        await api.Received(1).EvaluateOptionChainAsync(query, Arg.Any<CancellationToken>());
    }
}
