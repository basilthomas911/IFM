using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Application.Shared;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

/// <summary>Exercises the actual API initializer with controlled actor responses and projected readiness.</summary>
public sealed class SessionAnalyticsInitializationTests
{
    [Fact]
    public async Task Each_market_session_activates_its_own_consumers_and_duplicate_starts_reuse_warm_values()
    {
        var commands = Substitute.For<IMarketDataAnalyticsCommandApi>();
        var market = Substitute.For<TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi>();
        var cache = Substitute.For<IMarketOutlookHotCache>();
        market.PrepareSessionAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new[] {
            new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel("ES-SESSION-TEST", "ES", "ES", "ES", "FUT", "USD", "CME", "50", new(2026,12,18), true),
            new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel("VX-FRONT", "VX", "VX", "VX", "FUT", "USD", "CFE", "1000", new(2026,12,18), true),
            new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel("VX-SECOND", "VX", "VX", "VX", "FUT", "USD", "CFE", "1000", new(2027,1,18), false) }));
        var ready = true;
        cache.TryGetInputs(Arg.Any<MarketOutlookEntityId>(), out Arg.Any<MarketOutlookInputState>()).Returns(call => {
            var id = call.Arg<MarketOutlookEntityId>(); call[1] = Warm(id); return ready;
        });
        commands.StartFuturesRsiSignalAsync(Arg.Any<FuturesRsiSignalEntityId>()).Returns(call => {
            FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Attach(call.Arg<FuturesRsiSignalEntityId>()); return Accepted();
        });
        commands.StartFuturesAtrSignalAsync(Arg.Any<FuturesAtrSignalEntityId>()).Returns(call => {
            FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Attach(call.Arg<FuturesAtrSignalEntityId>()); return Accepted();
        });
        commands.StartFuturesAdxSignalAsync(Arg.Any<FuturesAdxSignalEntityId>()).Returns(call => {
            FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Attach(call.Arg<FuturesAdxSignalEntityId>()); return Accepted();
        });
        commands.StartFuturesMacdSignalAsync(Arg.Any<FuturesMacdSignalEntityId>()).Returns(call => {
            FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Attach(call.Arg<FuturesMacdSignalEntityId>()); return Accepted();
        });
        var sessions = Substitute.For<IFuturesMarketSessionAuthority>();
        var initializer = new ApiApplicationStartupActivities(sessions, null!, null!, null!, commands,
            null!, null!, market, null!, null!, cache, new(), new(), new() { ParticipantTimeout = TimeSpan.FromMilliseconds(30) },
            TimeProvider.System, NullLogger<ApiApplicationStartupActivities>.Instance);
        var first = new DateOnly(2026, 10, 19); var next = first.AddDays(1);
        try
        {
            await initializer.InitializeAsync(first, default);
            await initializer.InitializeAsync(first, default);
            await commands.Received(6).StartFuturesRsiSignalAsync(Arg.Is<FuturesRsiSignalEntityId>(id => id.ValueDate == first));
            ready = false;
            // The provider/feed-start callback must complete even when no indicator has a value yet.
            await initializer.InitializeAsync(next, default).WaitAsync(TimeSpan.FromSeconds(2));
            sessions.Current.Returns(new MarketSessionReadModel { ActiveValueDate = next, OperationalValueDate = next });
            var startup = new ApplicationStartupContext(next, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            // Full operational startup still rejects cold indicators; it never declares trading ready.
            await Assert.ThrowsAsync<TimeoutException>(() => initializer.StartRealtimeAnalyticsAsync(startup, default).AsTask());
            ready = true;
            Assert.Equal(ApplicationStartupActivityOutcome.Started,
                await initializer.StartRealtimeAnalyticsAsync(startup, default));
            await commands.Received(12).StartFuturesRsiSignalAsync(Arg.Is<FuturesRsiSignalEntityId>(id => id.ValueDate == next));
            await commands.Received(12).StartFuturesAdxSignalAsync(Arg.Is<FuturesAdxSignalEntityId>(id => id.ValueDate == next));
        }
        finally
        {
            foreach (var date in new[] { first, next })
                foreach (var activation in FuturesIntradaySignalActivationProfile.Create("ES-SESSION-TEST", date))
                {
                    FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Detach(activation.Rsi);
                    FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Detach(activation.Atr);
                    FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Detach(activation.Adx);
                    FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Detach(activation.Macd);
                }
        }
    }

    static Task<ServiceResult<Guid>> Accepted() => Task.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(Guid.NewGuid()));

    static FuturesRolloverContractAssignment Assignment(DatabentoContractRole role, string root) => new() {
        ContractRole = role, RootSymbol = root, ContractId = root == "ES" ? "ES-SESSION-TEST" : role.ToString(),
        Description = root, LocalSymbol = root, SecurityType = "FUT", Currency = "USD", Exchange = "CME", Multiplier = "50",
        LastTradeDate = new(2026, 12, 18), NextRolloverDate = new(2026, 12, 18), SourceContractHash = new('a',64),
        CreatedOnUtc = DateTime.UtcNow, UpdatedOnUtc = DateTime.UtcNow, CreatedBy = "test", UpdatedBy = "test"
    };

    static MarketOutlookInputState Warm(MarketOutlookEntityId id) => new() {
        EntityId = id,
        FuturesAdxSignal = new() { ContractId = id.ContractId, ValueDate = id.ValueDate, TimePeriod = TimeFrameType.FiveMinutes, IsWarm = true },
        FuturesAtrSignal = new() { ContractId = id.ContractId, ValueDate = id.ValueDate, TimePeriod = TimeFrameType.FiveMinutes, IsWarm = true },
        FuturesMacdSignal = new() { ContractId = id.ContractId, ValueDate = id.ValueDate, TimePeriod = TimeFrameType.FiveMinutes, IsWarm = true },
        FuturesRsiSignal = new() { ContractId = id.ContractId, ValueDate = id.ValueDate, TimePeriod = TimeFrameType.FiveMinutes, IsWarm = true },
        FuturesTdiSignal = new() { ContractId = id.ContractId, ValueDate = id.ValueDate, TimePeriod = TimeFrameType.FiveMinutes, SchemaVersion = 2 }
    };
}
