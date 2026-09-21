using System.Net;
using System.Text;
using Xunit;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class InstrumentDefinitionClientTests
{
    const string Definition = """
        {"hd":{"rtype":19,"publisher_id":1,"instrument_id":42002230,"ts_event":"1788089407203111341"},"ts_recv":"1788480000000000000","raw_symbol":"OZRU7 P2020","asset":"OZR","instrument_class":"P","underlying_id":42000681,"currency":"USD","exchange":"XCBT","expiration":"1819390800000000000","activation":"1784928600000000000","strike_price":"2020000000000","high_limit_price":"9223372036854775807","extra_field":"keep"}
        """;
    [Fact]
    public async Task Requests_the_definition_schema_range_and_keeps_raw_json_without_numeric_conversion()
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            if (request.Method == HttpMethod.Get)
            {
                Assert.Contains("metadata.get_dataset_range?dataset=GLBX.MDP3", request.RequestUri!.ToString());
                return new(HttpStatusCode.OK) { Content = new StringContent("""
                    {"end":"2026-09-05T21:00:00.000000000Z","schema":{"definition":{"end":"2026-09-04T23:30:00.000000000Z"}}}
                    """) };
            }
            var body = await request.Content!.ReadAsStringAsync();
            Assert.Contains("symbols=ALL_SYMBOLS", body); Assert.Contains("schema=definition", body);
            Assert.Contains("start=2026-09-03", body); Assert.Contains("end=2026-09-04T23%3A30%3A00.000000000Z", body);
            Assert.Contains("encoding=json", body); Assert.Contains("pretty_px=false", body); Assert.Contains("pretty_ts=false", body);
            Assert.DoesNotContain("limit=", body);
            return new(HttpStatusCode.OK) { Content = new StringContent(Definition + "\n", Encoding.UTF8, "application/json") };
        }));
        var client = new DatabentoInstrumentDefinitionClient(http, () => "test-only-credential");
        var rows = new List<ExactInstrumentDefinition>();
        await foreach (var row in client.ReadLatestAsync("GLBX.MDP3")) rows.Add(row);
        Assert.Equal(Definition, Assert.Single(rows).Json);
    }
    [Fact]
    public async Task Provider_failure_is_not_treated_as_an_empty_success()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden))));
        var client = new DatabentoInstrumentDefinitionClient(http, () => "test-only-credential");
        await Assert.ThrowsAsync<HttpRequestException>(async () => { await foreach (var row in client.ReadLatestAsync("GLBX.MDP3")) { } });
    }
    [Fact]
    public void Definition_summary_preserves_raw_fractional_prices_and_contract_parameters()
    {
        var json = Definition.Replace("2020000000000", "6500500000000")
            .Replace("\"extra_field\":\"keep\"", "\"extra_field\":\"keep\",\"contract_multiplier\":50,\"min_price_increment\":\"250000000\",\"min_price_increment_amount\":\"12500000000\",\"maturity_week\":3");
        var value = ExactInstrumentDefinition.Parse("GLBX.MDP3", json);
        Assert.Equal(json, value.Json);
        Assert.Equal(6500500000000L, value.Summary.StrikePrice);
        Assert.Equal(250000000L, value.Summary.MinimumPriceIncrement);
        Assert.Equal(12500000000L, value.Summary.MinimumPriceIncrementAmount);
        Assert.Equal(50, value.Summary.ContractMultiplier);
        Assert.Equal((byte)3, value.Summary.MaturityWeek);
    }

    [Fact]
    public void Definition_summary_does_not_turn_provider_sentinels_into_reference_facts()
    {
        var json = Definition.Replace("2020000000000", "9223372036854775807")
            .Replace("\"extra_field\":\"keep\"", "\"contract_multiplier\":2147483647,\"min_price_increment\":\"9223372036854775807\",\"min_price_increment_amount\":\"9223372036854775807\",\"maturity_week\":255");
        var summary = ExactInstrumentDefinition.Parse("GLBX.MDP3", json).Summary;
        Assert.Null(summary.StrikePrice);
        Assert.Null(summary.MinimumPriceIncrement);
        Assert.Null(summary.MinimumPriceIncrementAmount);
        Assert.Null(summary.ContractMultiplier);
        Assert.Null(summary.MaturityWeek);
    }

    sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
