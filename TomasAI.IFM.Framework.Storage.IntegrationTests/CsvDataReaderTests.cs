using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage.Csv;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests
{
    public class CsvDataReaderTests
    {
        [Fact(Skip = "Requires a licensed HTTP data feed connection.")]
        public async Task CreateCsvDataReaderOk()
        {
            var dataUri = new Uri("https://example.invalid/sanitized-yield-fixture.csv");
            var sr = new HttpStringReader(dataUri);
            var dr = await CsvDataReader<YieldCurveRateReadModel>.CreateAsync(sr);
            (dr.FieldCount > 0).Should().BeTrue();
        }

        [Fact(Skip = "Requires a licensed HTTP data feed connection.")]
        public async Task GetThisObjectByIndexOk()
        {
            var dataUri = new Uri("https://example.invalid/sanitized-yield-fixture.csv");
            var sr = new HttpStringReader(dataUri);
            var dr = await CsvDataReader<YieldCurveRateReadModel>.CreateAsync(sr);
            dr.Read().Should().BeTrue();
            var value = dr[0];
            value.Should().NotBeNull();
        }

        [Fact(Skip = "Requires a licensed HTTP data feed connection.")]
        public async Task GetThisObjectByPropertyNameOk()
        {
            var dataUri = new Uri("https://example.invalid/sanitized-yield-fixture.csv");
            var sr = new HttpStringReader(dataUri);
            var dr = await CsvDataReader<YieldCurveRateReadModel>.CreateAsync(sr);
            dr.Read().Should().BeTrue();
            var value = dr["ValueDate"];
            value.Should().NotBeNull();
        }

    }

}
