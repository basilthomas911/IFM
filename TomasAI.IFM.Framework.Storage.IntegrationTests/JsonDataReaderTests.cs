using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using System;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage.Json;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests
{
    public class JsonDataReaderTests
    {
        [Fact(Skip = "Requires a licensed HTTP data feed connection.")]
        public async Task CreateJsonDataReaderOk()
        {
            var dataUri = new Uri(@"https://financialmodelingprep.com/api/v3/economic_calendar?from=2022-03-28&to=2022-04-01&apikey=52abc9eb0d78d3e53c3f1a2c5f6d0383");
            var sr = new HttpStringReader(dataUri);
            var dr = await JsonDataReader<EconomicCalendarJsonModel>.CreateAsync(sr);
            (dr.FieldCount > 0).Should().BeTrue();
        }

        [Fact(Skip = "Requires a licensed HTTP data feed connection.")]
        public async Task GetThisObjectByIndexOk()
        {
            var dataUri = new Uri(@"https://financialmodelingprep.com/api/v3/economic_calendar?from=2022-03-28&to=2022-04-01&apikey=52abc9eb0d78d3e53c3f1a2c5f6d0383");
            var sr = new HttpStringReader(dataUri);
            var dr = await JsonDataReader<EconomicCalendarJsonModel>.CreateAsync(sr);
            dr.Read().Should().BeTrue();
            var value = dr[0];
            value.Should().NotBeNull();
        }

        [Fact(Skip = "Requires a licensed HTTP data feed connection.")]
        public async Task GetThisObjectByPropertyNameOk()
        {
            var dataUri = new Uri(@"https://financialmodelingprep.com/api/v3/economic_calendar?from=2022-03-28&to=2022-04-01&apikey=52abc9eb0d78d3e53c3f1a2c5f6d0383");
            var sr = new HttpStringReader(dataUri);
            var dr = await JsonDataReader<EconomicCalendarJsonModel>.CreateAsync(sr);
            dr.Read().Should().BeTrue();
            var value = dr["Date"];
            value.Should().NotBeNull();
        }

    }

}
