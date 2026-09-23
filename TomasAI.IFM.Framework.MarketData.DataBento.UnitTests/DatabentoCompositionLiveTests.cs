using Xunit.Abstractions;
using System.Globalization;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

/// <summary>Opt-in actual-provider observations. Passing this does not publish option-pricing mappings or enable trading.</summary>
public sealed class DatabentoCompositionLiveTests(ITestOutputHelper output)
{
    [DatabentoWindowLiveFact]
    [Trait("Category", "Live")]
    public void Native_provider_observes_all_option_strikes_in_bollinger_window()
    {
        var optionRoot = Environment.GetEnvironmentVariable("IFM_DATABENTO_QUALIFICATION_OPTION_ROOT")!;
        var expiry = DateOnly.ParseExact(Environment.GetEnvironmentVariable("IFM_DATABENTO_QUALIFICATION_EXPIRY")!, "yyyy-MM-dd");
        var centre = decimal.Parse(Environment.GetEnvironmentVariable("IFM_DATABENTO_WINDOW_CENTER")!, CultureInfo.InvariantCulture);
        var sigma = decimal.Parse(Environment.GetEnvironmentVariable("IFM_DATABENTO_WINDOW_SIGMA")!, CultureInfo.InvariantCulture);
        Assert.True(centre > 0 && sigma > 0);
        var lower = centre - 2.5m * sigma;
        var upper = centre + 2.5m * sigma;
        var options = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3")
            with { DataSource = FeedDataSourceMode.DatabentoLive };
        var factory = new DatabentoFeedFactory();
        var definitions = factory.CreateMarketDataQueries(options).GetChainDefinitions(new()
        {
            Dataset = "GLBX.MDP3", Underlying = optionRoot, MaturityDate = expiry,
            UniversePolicy = OptionUniversePolicy.ExplicitOptionRoots,
            ExplicitOptionRoots = [optionRoot], Rights = OptionRightSelection.Both
        }, TimeSpan.FromSeconds(30));
        var selected = definitions.Contracts.Where(x => x.StrikePrice >= lower && x.StrikePrice <= upper)
            .OrderBy(x => x.StrikePrice).ThenBy(x => x.Right).ToArray();
        Assert.NotEmpty(selected);
        var strikes = selected.Select(x => x.StrikePrice).Distinct().ToArray();
        var byInstrument = selected.ToDictionary(x => x.Instrument.InstrumentId);
        using var feed = factory.CreateOptionChainFeed(options);
        feed.Subscribe(new()
        {
            Underlying = selected[0].Underlying, MaturityDate = expiry,
            Strikes = strikes, Rights = OptionRightSelection.Both,
            ResolvedContracts = selected, DataKinds = MarketDataKinds.Quote
        }, TimeSpan.FromSeconds(20));
        var quoted = new HashSet<uint>();
        var fresh = new HashSet<uint>();
        var twoSided = new HashSet<uint>();
        var quoteRecords = 0;
        feed.Start(TimeSpan.FromSeconds(20), _ => { });
        try
        {
            var until = DateTimeOffset.UtcNow.AddSeconds(20);
            while (DateTimeOffset.UtcNow < until)
            {
                if (!feed.Reader.TryRead(TimeSpan.FromMilliseconds(100), out var batch)) continue;
                using (batch)
                    for (var index = 0; index < batch!.Count; index++)
                    {
                        var record = batch.Records[index];
                        if (record.Header.RecordKind != MarketRecordKind.Quote
                            || !byInstrument.ContainsKey(record.Header.InstrumentId)) continue;
                        quoteRecords++;
                        quoted.Add(record.Header.InstrumentId);
                        if (!IsFresh(record.Header.EventTimestampNanoseconds)) continue;
                        fresh.Add(record.Header.InstrumentId);
                        if (record.Quote.BidPrice > 0 && record.Quote.AskPrice >= record.Quote.BidPrice
                            && record.Quote.AskPrice != long.MaxValue)
                            twoSided.Add(record.Header.InstrumentId);
                    }
            }
        }
        finally { feed.Stop(TimeSpan.FromSeconds(5)); }
        var responsiveStrikes = selected.Where(x => fresh.Contains(x.Instrument.InstrumentId))
            .Select(x => x.StrikePrice).Distinct().Count();
        output.WriteLine("ES expiry {0}; BB centre {1}; sigma {2}; bounds {3}..{4}; definitions {5}; selected contracts {6}; strikes {7}; quote records {8}; any quote contracts {9}; fresh quote contracts {10}; fresh two-sided contracts {11}; responsive strikes {12}.",
            expiry, centre, sigma, lower, upper, definitions.Contracts.Count, selected.Length,
            strikes.Length, quoteRecords, quoted.Count, fresh.Count, twoSided.Count, responsiveStrikes);
        output.WriteLine("No fresh quote observed for: {0}", string.Join(", ", selected
            .Where(x => !fresh.Contains(x.Instrument.InstrumentId))
            .Select(x => x.RawSymbol)));
    }

    [DatabentoCompositionLiveFact]
    [Trait("Category", "Live")]
    public void Native_provider_returns_live_future_quote_and_one_explicit_option_chain_quote()
    {
        try
        {
            var futureSymbol = Environment.GetEnvironmentVariable("IFM_DATABENTO_QUALIFICATION_FUTURE")!;
            var optionRoot = Environment.GetEnvironmentVariable("IFM_DATABENTO_QUALIFICATION_OPTION_ROOT")!;
            var expiry = DateOnly.ParseExact(Environment.GetEnvironmentVariable("IFM_DATABENTO_QUALIFICATION_EXPIRY")!, "yyyy-MM-dd");
            var factory = new DatabentoFeedFactory();
            // Use the production profile when explicitly qualifying its memory/GC/affinity requirements.
            var profile = Environment.GetEnvironmentVariable("IFM_DATABENTO_QUALIFICATION_STRICT") == "true"
                ? FeedDeploymentProfile.Production : FeedDeploymentProfile.Development;
            var options = DatabentoFeedOptions.ForProfile(profile, "GLBX.MDP3")
                with { DataSource = FeedDataSourceMode.DatabentoLive };
            Assert.Equal(FeedDataSourceMode.DatabentoLive, options.DataSource);
            var latest = factory.CreateLatestPriceClient(options);
            var futureRequest = new LatestPriceRequest
            {
                Dataset = "GLBX.MDP3", Symbol = futureSymbol, PricePolicy = LatestPricePolicy.QuoteMidpoint,
                FreshnessPolicy = LatestPriceFreshnessPolicy.NextObserved
            };
            var futureUntil = DateTimeOffset.UtcNow.AddSeconds(20);
            var future = latest.GetLatestPrice(futureRequest, TimeSpan.FromSeconds(10));
            while (!IsFresh(future.EventTimestampNanoseconds) && DateTimeOffset.UtcNow < futureUntil)
            {
                Thread.Sleep(250);
                future = latest.GetLatestPrice(futureRequest, TimeSpan.FromSeconds(5));
            }
            Assert.True(future.IsLive && future.HasBid && future.HasAsk);
            Assert.True(future.BidPrice > 0 && future.AskPrice >= future.BidPrice);
            Fresh(future.EventTimestampNanoseconds);
            var chain = factory.CreateMarketDataQueries(options).GetChainDefinitions(new()
            {
                Dataset = "GLBX.MDP3", Underlying = optionRoot, MaturityDate = expiry,
                UniversePolicy = OptionUniversePolicy.ExplicitOptionRoots, ExplicitOptionRoots = [optionRoot], Rights = OptionRightSelection.Call
            }, TimeSpan.FromSeconds(20));
            Assert.NotEmpty(chain.Contracts);
            var definition = chain.Contracts.OrderBy(x => Math.Abs(x.StrikePrice - future.SelectedPrice / 1_000_000_000m)).First();
            Assert.NotNull(definition.ExpirationTimestampNanoseconds);
            using var feed = factory.CreateOptionChainFeed(options);
            feed.Subscribe(new()
            {
                Underlying = definition.Underlying, MaturityDate = expiry, Strikes = [definition.StrikePrice],
                Rights = OptionRightSelection.Call, ResolvedContracts = [definition], DataKinds = MarketDataKinds.Quote
            }, TimeSpan.FromSeconds(10));
            var observed = false;
            var quoteCount = 0;
            long lastBid = 0, lastAsk = 0, lastEvent = 0;
            var minimumAge = double.PositiveInfinity;
            var maximumAge = double.NegativeInfinity;
            feed.Start(TimeSpan.FromSeconds(10), _ => { });
            try
            {
                var until = DateTimeOffset.UtcNow.AddSeconds(10);
                while (!observed && DateTimeOffset.UtcNow < until)
                {
                    if (!feed.Reader.TryRead(TimeSpan.FromMilliseconds(100), out var batch)) continue;
                    using (batch)
                        for (var index = 0; index < batch!.Count; index++)
                        {
                            var record = batch.Records[index];
                            if (record.Header.RecordKind != MarketRecordKind.Quote) continue;
                            quoteCount++; lastBid = record.Quote.BidPrice; lastAsk = record.Quote.AskPrice; lastEvent = record.Header.EventTimestampNanoseconds;
                            var sourceAge = (DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch.AddTicks(lastEvent / 100)).TotalSeconds;
                            minimumAge = Math.Min(minimumAge, sourceAge); maximumAge = Math.Max(maximumAge, sourceAge);
                            Assert.Equal(definition.Instrument.InstrumentId, record.Header.InstrumentId);
                            if (!IsFresh(record.Header.EventTimestampNanoseconds)) continue;
                            if (record.Quote.BidPrice > 0 && record.Quote.AskPrice >= record.Quote.BidPrice && record.Quote.AskPrice != long.MaxValue)
                                observed = true;
                        }
                }
            }
            finally { feed.Stop(TimeSpan.FromSeconds(5)); }
            Assert.True(observed, $"No fresh two-sided option quote within 10 seconds: symbol={definition.RawSymbol}; quotes={quoteCount}; bid={lastBid}; ask={lastAsk}; lastEventNs={lastEvent}; observedAgeSeconds={minimumAge:F6}..{maximumAge:F6}.");
            output.WriteLine("Native live provider observed future {0}, option {1}, expiry {2}; definition scope count {3}. Pricing metadata remains separately qualified.",
                futureSymbol, definition.RawSymbol, expiry, chain.Contracts.Count);
        }
        catch (Exception error)
        {
            var message = error.Message;
            var key = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
            if (!string.IsNullOrEmpty(key)) message = message.Replace(key, "[REDACTED]");
            throw new InvalidOperationException($"Live qualification failed ({error.GetType().Name}): {message}");
        }
    }

    static void Fresh(long nanoseconds)
    {
        var eventAt = DateTimeOffset.UnixEpoch.AddTicks(nanoseconds / 100);
        Assert.InRange((DateTimeOffset.UtcNow - eventAt).TotalSeconds, -2, 5);
    }
    static bool IsFresh(long nanoseconds)
        => (DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch.AddTicks(nanoseconds / 100)).TotalSeconds is >= -2 and <= 5;
}

public sealed class DatabentoCompositionLiveFactAttribute : FactAttribute
{
    public DatabentoCompositionLiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IFM_DATABENTO_COMPOSITION_LIVE_TESTS") != "true"
            || new[] { "DATABENTO_API_KEY", "IFM_DATABENTO_QUALIFICATION_FUTURE", "IFM_DATABENTO_QUALIFICATION_OPTION_ROOT", "IFM_DATABENTO_QUALIFICATION_EXPIRY" }
                .Any(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))))
            Skip = "Requires explicit live qualification opt-in, credential, exact future, option root and expiry; build with DatabentoEnableLive=true.";
    }
}

public sealed class DatabentoWindowLiveFactAttribute : FactAttribute
{
    public DatabentoWindowLiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IFM_DATABENTO_COMPOSITION_LIVE_TESTS") != "true"
            || new[] { "DATABENTO_API_KEY", "IFM_DATABENTO_QUALIFICATION_OPTION_ROOT",
                "IFM_DATABENTO_QUALIFICATION_EXPIRY", "IFM_DATABENTO_WINDOW_CENTER",
                "IFM_DATABENTO_WINDOW_SIGMA" }
                .Any(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))))
            Skip = "Requires live Databento opt-in, an option root and expiry, and explicit Bollinger centre and sigma.";
    }
}
