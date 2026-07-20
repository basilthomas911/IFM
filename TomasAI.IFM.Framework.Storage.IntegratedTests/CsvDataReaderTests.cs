using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Framework.Storage.Csv;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests
{
    public class CsvDataReaderTests
    {
        [Fact]
        public void CreateCsvDataReaderOk()
        {
            var dataUri = new Uri(@"https://www.quandl.com/api/v3/datasets/USTREASURY/YIELD.csv?api_key=Vpxxmo8BPMwZP-xH8XZZ");
            var sr = new HttpStringReader(dataUri);
            var dr = new CsvDataReader<YieldCurveRateReadModel>(sr);
            (dr.FieldCount > 0).Should().BeTrue();
        }

        [Fact]
        public void GetThisObjectByIndexOk()
        {
            var dataUri = new Uri(@"https://www.quandl.com/api/v3/datasets/USTREASURY/YIELD.csv?api_key=Vpxxmo8BPMwZP-xH8XZZ");
            var sr = new HttpStringReader(dataUri);
            var dr = new CsvDataReader<YieldCurveRateReadModel>(sr);
            dr.Read().Should().BeTrue();
            var value = dr[0];
            value.Should().NotBeNull();
        }

        [Fact]
        public void GetThisObjectByPropertyNameOk()
        {
            var dataUri = new Uri(@"https://www.quandl.com/api/v3/datasets/USTREASURY/YIELD.csv?api_key=Vpxxmo8BPMwZP-xH8XZZ");
            var sr = new HttpStringReader(dataUri);
            var dr = new CsvDataReader<YieldCurveRateReadModel>(sr);
            dr.Read().Should().BeTrue();
            var value = dr["ValueDate"];
            value.Should().NotBeNull();
        }

    }

}
