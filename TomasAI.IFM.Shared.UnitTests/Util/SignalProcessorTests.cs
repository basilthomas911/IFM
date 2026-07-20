using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.Util;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Service.MarketDataFeed;
using TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers;

namespace TomasAI.IFM.Shared.UnitTests.Util
{
    public class SignalProcessorTests
    {

        [Fact]
        public void FilterOk()
        {
            var sp = new SignalProcessor<TickPriceData>();

            for (var index = 0; index < 10; index++)
            {
                var tickPriceData = new TickPriceData(DateTime.Now, index + 1, 5.0, 1, string.Empty);
                var tickPriceDataOut = sp.Filter(tickPriceData, 10, sigIn => sigIn.Price);
                tickPriceDataOut.Price.Should().Be(tickPriceData.Price);
            }

            var spikedData = new TickPriceData(DateTime.Now, 11, 10.75, 1, string.Empty);
            var spikedDataOut = sp.Filter(spikedData, 10, sigIn => sigIn.Price);
            spikedDataOut.Price.Should().NotBe(spikedData.Price);

        }

    }
}
