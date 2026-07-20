using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.MarketData;

namespace TomasAI.IFM.Shared.UnitTests.MarketData
{
    public class FuturesContractIdTests
    {

        [Fact]
        public void FuturesContractIdOk()
        {
            var futuresContractId = new FuturesContractIdParser("ES20181221").Id;
            futuresContractId.Symbol.Should().Be("ES");
            futuresContractId.MaturityDate.Should().Be(new DateOnly(2018, 12, 21));
        }

    }
}
