using System;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.Util;

namespace TomasAI.IFM.Shared.UnitTests.Util
{
    public class CsvDataReaderTests
    {
        [Fact]
        public void CsvDataReaderOk()
        {
            var testFile = @"C:\TomasAI\Projects\IFM\TomasAI.InvestmentFundManager\TomasAI.IFM.Shared.UnitTests\TestData\FuturesEodData-TestData.csv";

            var dataReader = new CsvDataReader(testFile);
            dataReader.IsEmpty.Should().BeFalse();
            dataReader.GetName(0).Should().Be("ContractId");
        }
    }
}
