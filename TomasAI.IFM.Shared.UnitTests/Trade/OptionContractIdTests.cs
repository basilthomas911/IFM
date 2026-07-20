using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.UnitTests.Trade
{
    public class OptionContractIdTests
    {
        [Fact]
        public void OptionContractIdOk()
        {
            var optionContractId = OptionContractId.Create("ES20181221C2550");
            optionContractId.Symbol.Should().Be("ES");
            optionContractId.MaturityDate.Should().Be(new DateTime(2018, 12, 21));
            optionContractId.OptionType.Should().Be(OptionType.Call);
            optionContractId.StrikePrice.Should().Be(2550);
        }
    }
}
