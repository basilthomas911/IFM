using TomasAI.IFM.Shared.Identity;
using TomasAI.IFM.Shared.OptionPricing;
using Xunit;
using Calculator = TomasAI.IFM.Application.OptionPricing.OptionCalculator;
using ProviderCalculator = TomasAI.IFM.Framework.OptionPricer.Pricing.OptionCalculator;
namespace TomasAI.IFM.Shared.UnitTests.ApplicationBoundaries;
public sealed class ApplicationBoundaryCompatibilityTests
{
    [Fact]
    public async Task Pricing_boundary_preserves_results_provenance_failures_and_concurrent_use()
    {
        var app = new Calculator();
        var provider = new ProviderCalculator();
        var requests = new[] {
            new OptionPricingRequest(UnderlyingKind.Futures, ExerciseKind.European, PremiumKind.FuturesStyle, OptionSide.Put, 5000, 4975, 0.1, 0.04),
            new OptionPricingRequest(UnderlyingKind.Futures, ExerciseKind.European, PremiumKind.PaidUpfront, OptionSide.Call, 5000, 5025, 0.1, 0.04),
            new OptionPricingRequest(UnderlyingKind.Futures, ExerciseKind.European, PremiumKind.FuturesStyle, OptionSide.Call, -1, 5025, 0.1, 0.04) };
        var expected = requests.Select(request => provider.Price(request, 0.2)).ToArray();
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => {
            var actual = new PricingResult[requests.Length];
            app.PriceBatch(requests, new[] { 0.2, 0.2, 0.2 }, actual);
            Assert.Equal(expected, actual);
            for (var i = 0; i < requests.Length; i++)
            {
                Assert.Equal(provider.PriceAndDelta(requests[i], 0.2), app.PriceAndDelta(requests[i], 0.2));
                Assert.Equal(ProviderCalculator.EngineVersionFor(requests[i]), Calculator.EngineVersionFor(requests[i]));
            }
        })));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => app.Price(requests[0], 0.2, canceled.Token));
    }
    [Fact]
    public async Task Sequence_boundary_preserves_high_watermark_and_cancellation_without_allocating_on_read()
    {
        var source = new Sequence();
        var api = new TomasAI.IFM.Application.Identity.SequenceIdApi(source);
        Assert.Equal(0, await api.GetHighWatermarkAsync(SequenceName.Trade_TradeId));
        Assert.Equal(1, await api.GetSequenceIdAsync(SequenceName.Trade_TradeId));
        Assert.Equal(1, await api.GetHighWatermarkAsync(SequenceName.Trade_TradeId));
        Assert.Equal(SequenceName.Trade_TradeId, source.Last);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await api.GetSequenceIdAsync(SequenceName.Trade_TradeId, canceled.Token));
        Assert.Equal(1, await api.GetHighWatermarkAsync(SequenceName.Trade_TradeId));
    }
    [Fact]
    public void Serialization_boundary_preserves_encoded_bytes_historical_hash_input_and_size_limits()
    {
        var value = new[] { "frozen", "wire", "payload" };
        var app = TomasAI.IFM.Application.Serialization.MessagePackBinarySerializer.Shared;
        var provider = TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared;
        Assert.Equal(provider.Serialize(value), app.Serialize(value));
        Assert.Equal(value, app.Deserialize<string[]>(provider.Serialize(value)!));
        Assert.Equal(TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.SerializeHistoricalContent(value),
            TomasAI.IFM.Application.Serialization.MessagePackBinarySerializer.SerializeHistoricalContent(value));
        Assert.Equal(TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.MeasureEncoded(value),
            TomasAI.IFM.Application.Serialization.MessagePackBinarySerializer.MeasureEncoded(value));
    }
    sealed class Sequence : TomasAI.IFM.Framework.SequenceId.ISequenceIdGenerator
    {
        long next; public SequenceName Last;
        public ValueTask<long> GetSequenceIdAsync(SequenceName name, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Last = name; return ValueTask.FromResult(++next); }
        public ValueTask<long> GetHighWatermarkAsync(SequenceName name, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Last = name; return ValueTask.FromResult(next); }
    }
}
