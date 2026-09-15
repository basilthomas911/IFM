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
}
