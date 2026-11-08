using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class StreamingIndicatorSeedFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Partial_stream_failure_or_cancellation_disposes_the_reader_and_retry_has_no_partial_bar(bool cancel)
    {
        var now = new DateTimeOffset(2026, 11, 2, 0, 5, 0, TimeSpan.Zero);
        var calendar = new CmeFuturesMarketSessionCalendar();
        var window = RsiHistoricalSeedWindowModel.Create(TimeFrameType.OneMinute, 1, now, calendar)[0];
        var ticks = Substitute.For<ITradeTickQuoteDbContext, IStreamingRawFuturesTradeReader>();
        var streaming = (IStreamingRawFuturesTradeReader)ticks;
        var databases = Substitute.For<IDbContextFactory>();
        databases.TradeTickQuoteDb.Returns(ticks);
        var fail = true;
        var disposed = 0;
        using var cancellation = new CancellationTokenSource();
        streaming.StreamRawFuturesTradeWindowAsync("ES20261218", window.ValueDate, Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => Rows(call.Arg<CancellationToken>()));
        var market = Substitute.For<IMarketDataApi>();
        var reader = new StoredSessionIndicatorSeedReader(databases, calendar, new Clock(now),
            NullLogger<StoredSessionIndicatorSeedReader>.Instance, market);
        if (cancel)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync("ES20261218", TimeFrameType.OneMinute, 1, cancellation.Token));
        else
            await Assert.ThrowsAsync<IOException>(() => reader.ReadAsync("ES20261218", TimeFrameType.OneMinute, 1, default));
        Assert.Equal(1, disposed);
        fail = false;
        var bar = Assert.Single(await reader.ReadAsync("ES20261218", TimeFrameType.OneMinute, 1, default));
        Assert.Equal(2, bar.TradeCount);
        Assert.Equal(101, bar.Close);
        Assert.Equal(2, disposed);
        await ticks.DidNotReceive().GetRawFuturesTradeWindowAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>());
        await market.DidNotReceive().ReadIndicatorSeedBarsAsync(Arg.Any<string>(), Arg.Any<TimeFrameType>(), Arg.Any<CancellationToken>());

        async IAsyncEnumerable<BootstrapTradeRow> Rows([EnumeratorCancellation] CancellationToken token)
        {
            try
            {
                for (var index = 0; index < 2; index++)
                {
                    var time = window.StartUtc.AddSeconds(index + 1);
                    yield return new(window.ValueDate, 100 + index, 1, index + 1,
                        time.ToUnixTimeMilliseconds() * 1000000, 0, (short)'T', 0, 1, 1)
                        { AggregationTimeUtc = TimeOnly.FromDateTime(time.UtcDateTime) };
                    await Task.Yield();
                    if (fail)
                    {
                        if (!cancel) throw new IOException("Injected raw-page failure.");
                        cancellation.Cancel();
                        token.ThrowIfCancellationRequested();
                    }
                }
            }
            finally { disposed++; }
        }
    }

    sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
