using System;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class TickQuoteParameterOwnershipTests
{
    [Fact]
    public void Quote_parameters_cannot_be_rebound_after_udt_resolution()
    {
        var original = new object?[21];
        var binding = (IScyllaOwnedBindValues)new InsertTickQuoteData(original).Bind();

        Assert.Same(original, binding.TakeValues());
        Assert.Throws<InvalidOperationException>(() => binding.TakeValues());
    }
    [Fact]
    public void Raw_quote_parameters_transfer_the_array_once_and_retain_bind_lifetime()
    {
        var original = new object?[21];
        var encoded = new Storage.TradeTickQuoteDb.TickQuoteScyllaBindValue(
            new TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.FuturesTickQuoteDataSegment(
                [new(1, 2, 3, 4, 5, 5m, 6, 7, 8, 8m, 9, 10)], 1));
        var binding = (IScyllaOwnedBindValues)new Storage.TradeTickQuoteDb.InsertTickQuoteData(original, encoded).Bind();
        Assert.Same(encoded, binding.BindLifetime);
        Assert.Same(original, binding.TakeValues());
        Assert.Throws<InvalidOperationException>(() => binding.TakeValues());
        binding.BindLifetime!.Dispose();
        binding.BindLifetime.Dispose();
    }
}
