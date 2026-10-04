using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using Xunit;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

/// <summary>Opt-in maintenance integration. Downloads real BBOs and fills missing development projection rows.</summary>
public sealed class DatabentoOptionQuoteBackfill
{
    [Fact]
    public async Task Backfill_missing_quotes_from_Databento()
    {
        if (Environment.GetEnvironmentVariable("IFM_BACKFILL_OPTION_QUOTES") != "1") return;
        string Required(string name) => Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"Set {name} before running maintenance.");
        var underlying = Required("IFM_BACKFILL_UNDERLYING");
        var expiry = DateOnly.Parse(Required("IFM_BACKFILL_EXPIRY"), CultureInfo.InvariantCulture);
        var start = DateTimeOffset.Parse(Required("IFM_BACKFILL_START"), CultureInfo.InvariantCulture);
        var end = DateTimeOffset.Parse(Required("IFM_BACKFILL_END"), CultureInfo.InvariantCulture);
        var date = DateOnly.FromDateTime(start.UtcDateTime);
        Assert.True(end > start && end - start <= TimeSpan.FromHours(24));
        Assert.Equal(date, DateOnly.FromDateTime(end.AddTicks(-1).UtcDateTime));
        Assert.True(expiry >= date);
        var key = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
            ?? Environment.GetEnvironmentVariable("DATABENTO_API_KEY", EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable("DATABENTO_API_KEY", EnvironmentVariableTarget.Machine)
            ?? throw new InvalidOperationException("DATABENTO_API_KEY is unavailable.");
        var settings = new DbConnectionSettings()
            .Add("MarketDataDbConnection", "Contact Points=localhost;Port=9042;Default Keyspace=market_data_test_db", "System.Data.ScyllaDb")
            .Add("SecuritiesDbConnection", "Contact Points=localhost;Port=9042;Default Keyspace=securities_test_db", "System.Data.ScyllaDb");
        var factory = Substitute.For<IDbContextFactory>();
        var logger = Substitute.For<ILogger<DbProvider>>();
        var db = new MarketDataDbContext(settings, factory, Substitute.For<IBlackboardService>(), Substitute.For<ISequenceIdGenerator>(), logger);
        var securities = new SecuritiesDbContext(settings, factory, logger);
        factory.MarketDataDb.Returns(db);
        factory.SecuritiesDb.Returns(securities);
        var definitions = await securities.GetCachedOptionContractDefinitionsAsync(
            Required("IFM_BACKFILL_SYMBOL"), underlying, expiry, Required("IFM_BACKFILL_ROOTS").Split(','), CancellationToken.None);
        var centre = decimal.Parse(Required("IFM_BACKFILL_UNDERLYING_PRICE"), CultureInfo.InvariantCulture);
        var strikes = definitions.Select(x => x.Definition.StrikePrice).Distinct().OrderBy(x => Math.Abs((decimal)x - centre)).Take(80).ToHashSet();
        var selected = definitions.Select(x => x.Definition).Where(x => strikes.Contains(x.StrikePrice)).DistinctBy(x => x.ContractId).ToArray();
        Assert.NotEmpty(selected);
        Assert.All(selected, x => Assert.True(x.InstrumentId is > 0));
        var byId = selected.ToDictionary(x => x.InstrumentId!.Value);
        using var http = new HttpClient { BaseAddress = new Uri("https://hist.databento.com/v0/"), Timeout = TimeSpan.FromMinutes(3) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(key + ":")));
        var fields = new Dictionary<string, string>
        {
            ["dataset"] = "GLBX.MDP3", ["schema"] = "bbo-1m", ["stype_in"] = "instrument_id",
            ["symbols"] = string.Join(',', byId.Keys), ["start"] = start.ToString("O"), ["end"] = end.ToString("O")
        };
        var estimateFields = new Dictionary<string, string>(fields) { ["mode"] = "historical-streaming" };
        using var estimateResponse = await http.PostAsync("metadata.get_cost", new FormUrlEncodedContent(estimateFields));
        if (!estimateResponse.IsSuccessStatusCode) throw new InvalidOperationException(await estimateResponse.Content.ReadAsStringAsync());
        var cost = decimal.Parse(await estimateResponse.Content.ReadAsStringAsync(), CultureInfo.InvariantCulture);
        Console.WriteLine($"Databento estimate: ${cost:F6}; {selected.Length} contracts, {start:O} to {end:O}.");
        Assert.InRange(cost, 0, 1); // Check the $1 maintenance budget before downloading.
        fields["encoding"] = "json";
        fields["compression"] = "none";
        fields["pretty_px"] = "true";
        fields["pretty_ts"] = "false";
        fields["map_symbols"] = "false";
        using var request = new HttpRequestMessage(HttpMethod.Post, "timeseries.get_range") { Content = new FormUrlEncodedContent(fields) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        var latest = new Dictionary<string, FuturesOptionTickDataV2ReadModel>();
        long records = 0;
        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            var row = document.RootElement;
            var id = row.GetProperty("hd").GetProperty("instrument_id").GetUInt32();
            if (!byId.TryGetValue(id, out var definition)) continue;
            records++;
            var level = row.GetProperty("levels")[0];
            double Price(string name) => double.TryParse(level.GetProperty(name).ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
            var bid = Price("bid_px");
            var ask = Price("ask_px");
            if (!double.IsFinite(bid) || !double.IsFinite(ask) || bid <= 0 || ask < bid || ask > 1e8) continue;
            var nanos = long.Parse(row.GetProperty("ts_recv").ToString(), CultureInfo.InvariantCulture);
            var timestamp = DateTimeOffset.UnixEpoch.AddTicks(nanos / 100);
            Assert.Equal(date, DateOnly.FromDateTime(timestamp.UtcDateTime));
            var tick = new FuturesOptionTickDataV2ReadModel
            {
                ContractId = definition.ContractId, ValueDate = date, TickId = nanos,
                TickTime = TimeOnly.FromDateTime(timestamp.UtcDateTime), BidPrice = bid, AskPrice = ask,
                BidSize = level.GetProperty("bid_sz").GetInt32(), AskSize = level.GetProperty("ask_sz").GetInt32(), UnderlyingPrice = (double)centre
            };
            if (!latest.TryGetValue(tick.ContractId, out var old) || old.TickId < tick.TickId) latest[tick.ContractId] = tick;
        }
        Assert.NotEmpty(latest);
        // Conditional insertion cannot replace a newer quote or a concurrent live update.
        foreach (var source in latest.Values)
        {
            var definition = byId.Values.First(x => x.ContractId == source.ContractId);
            var tick = OptionQuoteGreekEnrichment.Calculate(definition, source);
            await db.Use("BackfillOptionQuoteIfAbsent", MarketDataDbCql.UpsertFuturesOptionChainQuoteData.TrimEnd().TrimEnd(';') + " IF NOT EXISTS;")
                .SetParameters(new UpsertFuturesOptionChainQuoteData(underlying, expiry, tick)).ExecuteCommandAsync();
        }
        var stored = await db.GetFuturesOptionChainQuoteDataAsync(underlying, expiry, date);
        foreach (var tick in latest.Values)
        {
            var actual = Assert.Single(stored, x => x.ContractId == tick.ContractId);
            Assert.True(actual.BidPrice > 0 && actual.AskPrice >= actual.BidPrice);
            if (actual.TickId == tick.TickId)
            {
                Assert.Equal(tick.BidPrice, actual.BidPrice);
                Assert.Equal(tick.AskPrice, actual.AskPrice);
                Assert.Equal(tick.TickTime, actual.TickTime);
            }
        }
        Console.WriteLine($"Verified {latest.Count} quoted contracts from {records} BBO records; partition contains {stored.Count} rows. Missing selected contracts: {selected.Length - latest.Count}.");
    }
}





