using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Framework.Storage.Json;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests
{
    public class JsonDataReaderTests
    {
        [Fact]
        public void CreateJsonDataReaderOk()
        {
            var dataUri = new Uri(@"https://financialmodelingprep.com/api/v3/economic_calendar?from=2022-03-28&to=2022-04-01&apikey=52abc9eb0d78d3e53c3f1a2c5f6d0383");
            var sr = new HttpStringReader(dataUri);
            var dr = new JsonDataReader<EconomicCalendarJsonModel>(sr);
            (dr.FieldCount > 0).Should().BeTrue();
        }

        [Fact]
        public void GetThisObjectByIndexOk()
        {
            var dataUri = new Uri(@"https://financialmodelingprep.com/api/v3/economic_calendar?from=2022-03-28&to=2022-04-01&apikey=52abc9eb0d78d3e53c3f1a2c5f6d0383");
            var sr = new HttpStringReader(dataUri);
            var dr = new JsonDataReader<EconomicCalendarJsonModel>(sr);
            dr.Read().Should().BeTrue();
            var value = dr[0];
            value.Should().NotBeNull();
        }

        [Fact]
        public void GetThisObjectByPropertyNameOk()
        {
            var dataUri = new Uri(@"https://financialmodelingprep.com/api/v3/economic_calendar?from=2022-03-28&to=2022-04-01&apikey=52abc9eb0d78d3e53c3f1a2c5f6d0383");
            var sr = new HttpStringReader(dataUri);
            var dr = new JsonDataReader<EconomicCalendarJsonModel>(sr);
            dr.Read().Should().BeTrue();
            var value = dr["Date"];
            value.Should().NotBeNull();
        }

    }

}
