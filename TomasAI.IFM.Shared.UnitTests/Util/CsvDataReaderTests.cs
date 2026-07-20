using System;
using Xunit;
using FluentAssertions;

namespace TomasAI.IFM.Shared.Util.UnitTests
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
