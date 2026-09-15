using System;
using System.IO;
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
            var testFile = Path.Combine(AppContext.BaseDirectory, "TestData", "FuturesEodData-TestData.csv");

            var dataReader = new CsvDataReader(testFile);
            dataReader.IsEmpty.Should().BeFalse();
            dataReader.GetName(0).Should().Be("ContractId");
        }
    }
}
