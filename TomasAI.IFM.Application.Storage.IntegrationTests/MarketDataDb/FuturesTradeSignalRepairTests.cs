using System;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class FuturesTradeSignalRepairTests
{
    [Fact]
    public void ParseRepairRow_AcceptsCanonicalSignalIdentity()
    {
        const string payload = """
            {"contractid":"ES20260918","valuedate":"2026-08-21","timeperiod":"FifteenSeconds","timestamp":"14:30:01.250000000","sequenceid":42}
            """;

        var result = MarketDataDbContext.ParseFuturesTradeSignalRepairRow(payload);

        Assert.Null(result.Error);
        Assert.NotNull(result.Row);
        Assert.Equal("ES20260918", result.Row.ContractId);
        Assert.Equal(new DateOnly(2026, 8, 21), result.Row.ValueDate);
        Assert.Equal("FifteenSeconds", result.Row.TimePeriod);
        Assert.Equal(42, result.Row.SequenceId);
    }

    [Fact]
    public void ParseRepairRow_RejectsLegacyCsvIdentityWithoutThrowing()
    {
        const string payload = """
            {"contractid":"ES20250919,2025-08-21,FifteenSeconds,0,00:00:00","valuedate":"0001-01-01","timeperiod":null,"timestamp":"00:00:00","sequenceid":0}
            """;

        var result = MarketDataDbContext.ParseFuturesTradeSignalRepairRow(payload);

        Assert.Null(result.Row);
        Assert.Contains("invalid contractId", result.Error);
        Assert.Contains("invalid valueDate", result.Error);
        Assert.Contains("invalid timePeriod", result.Error);
    }

    [Fact]
    public void ParseRepairRow_ReportsInvalidJsonWithoutThrowing()
    {
        var result = MarketDataDbContext.ParseFuturesTradeSignalRepairRow("not-json");

        Assert.Null(result.Row);
        Assert.StartsWith("invalid JSON:", result.Error);
    }
}
