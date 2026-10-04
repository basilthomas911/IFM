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

/// <summary>Opt-in maintenance integration. Loads official daily statistics into existing development quote rows.</summary>
public sealed class DatabentoOptionStatisticsBackfill
{
    [Fact]
    public async Task Backfill_statistics_from_Databento()
    {
        if (Environment.GetEnvironmentVariable("IFM_BACKFILL_OPTION_STATISTICS") != "1") return;
        string Required(string name) => Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"Set {name} before running maintenance.");
        var underlying = Required("IFM_BACKFILL_UNDERLYING");
        var expiry = DateOnly.Parse(Required("IFM_BACKFILL_EXPIRY"), CultureInfo.InvariantCulture);
        var start = DateTimeOffset.Parse(Required("IFM_BACKFILL_START"), CultureInfo.InvariantCulture);
        var end = DateTimeOffset.Parse(Required("IFM_BACKFILL_END"), CultureInfo.InvariantCulture);
        var date = DateOnly.FromDateTime(start.UtcDateTime);
        Assert.True(end > start && end - start <= TimeSpan.FromHours(48));
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
            ["dataset"] = "GLBX.MDP3", ["schema"] = "statistics", ["stype_in"] = "instrument_id",
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
        var latest = new Dictionary<(string ContractId, int Type), (long Quantity, DateOnly Date, long Received)>();
        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            var row = document.RootElement;
            var id = row.GetProperty("hd").GetProperty("instrument_id").GetUInt32();
            if (!byId.TryGetValue(id, out var definition)) continue;
            var type = row.GetProperty("stat_type").GetInt32();
            if (type is not (6 or 9) || row.GetProperty("update_action").GetInt32() != 1) continue;
            if (!long.TryParse(row.GetProperty("quantity").ToString(), out var quantity)
                || quantity < 0 || quantity == long.MaxValue) continue;
            if (!long.TryParse(row.GetProperty("ts_ref").ToString(), out var reference)
                || reference <= 0) continue;
            var referenceDate = DateOnly.FromDateTime(DateTimeOffset.UnixEpoch.AddTicks(reference / 100).UtcDateTime);
            if (referenceDate > date) continue;
            var received = long.Parse(row.GetProperty("ts_recv").ToString(), CultureInfo.InvariantCulture);
            var identity = (definition.ContractId, type);
            if (!latest.TryGetValue(identity, out var previous) || referenceDate > previous.Date
                || (referenceDate == previous.Date && received > previous.Received))
                latest[identity] = (quantity, referenceDate, received);
        }
        Assert.NotEmpty(latest);
        var before = await db.GetFuturesOptionChainQuoteDataAsync(underlying, expiry, date);
        foreach (var quote in before.Where(x => selected.Any(d => d.ContractId == x.ContractId)))
        {
            var hasVolume = latest.TryGetValue((quote.ContractId, 6), out var volume);
            var hasInterest = latest.TryGetValue((quote.ContractId, 9), out var interest);
            if (!hasVolume && !hasInterest) continue;
            // Preserve fields if no newer reference-date statistic was published for them.
            var replaceVolume = hasVolume && (quote.VolumeValueDate is null || volume.Date >= quote.VolumeValueDate);
            var replaceInterest = hasInterest && (quote.OpenInterestValueDate is null || interest.Date >= quote.OpenInterestValueDate);
            await db.Use("BackfillOptionChainStatistics", MarketDataDbCql.UpdateOptionChainStatistics)
                .SetParameters(new UpdateOptionChainStatisticsParameters(underlying, expiry, date, quote.ContractId,
                    replaceVolume ? volume.Quantity : quote.Volume,
                    replaceInterest ? interest.Quantity : quote.OpenInterest,
                    replaceVolume ? volume.Date : quote.VolumeValueDate,
                    replaceInterest ? interest.Date : quote.OpenInterestValueDate)).ExecuteCommandAsync();
        }
        var after = await db.GetFuturesOptionChainQuoteDataAsync(underlying, expiry, date);
        foreach (var quote in before)
        {
            var actual = Assert.Single(after, x => x.ContractId == quote.ContractId);
            Assert.Equal(quote.BidPrice, actual.BidPrice);
            Assert.Equal(quote.AskPrice, actual.AskPrice);
            Assert.Equal(quote.TickId, actual.TickId);
            foreach (var type in new[] { 6, 9 })
                if (latest.TryGetValue((quote.ContractId, type), out var statistic))
                {
                    var actualDate = type == 6 ? actual.VolumeValueDate : actual.OpenInterestValueDate;
                    if (actualDate == statistic.Date)
                        Assert.Equal(statistic.Quantity, type == 6 ? actual.Volume : actual.OpenInterest);
                }
        }
        var current = after.Where(x => selected.Any(d => d.ContractId == x.ContractId)).ToArray();
        Console.WriteLine($"Verified statistics: volume {current.Count(x => x.Volume.HasValue)}/{selected.Length}; open interest {current.Count(x => x.OpenInterest.HasValue)}/{selected.Length}.");
        foreach (var sample in current.Take(3)) Console.WriteLine($"{sample.ContractId}: volume {sample.Volume} ({sample.VolumeValueDate}), OI {sample.OpenInterest} ({sample.OpenInterestValueDate}).");
    }
}
