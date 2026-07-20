using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.UnitTests.MarketData
{
    public class FuturesOptionContractIdTests
    {

        [Fact]
        public void FuturesOptionContractIdOk()
        {
            var futuresOptionContractId = new FuturesOptionContractId("ES20181221P2790");
            futuresOptionContractId.Symbol.Should().Be("ES");
            futuresOptionContractId.MaturityDate.Should().Be(new DateTime(2018, 12, 21));
            futuresOptionContractId.OptionType.Should().Be(OptionType.Put);
            futuresOptionContractId.StrikePrice.Should().Be(2790);
        }

    }
}
