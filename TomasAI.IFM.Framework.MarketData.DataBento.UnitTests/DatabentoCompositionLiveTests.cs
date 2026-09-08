using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

/// <summary>Opt-in actual-provider observations. Passing this does not publish option-pricing mappings or enable trading.</summary>
public sealed class DatabentoCompositionLiveTests(ITestOutputHelper output)
{
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
        Assert.InRange((DateTimeOffset.UtcNow - eventAt).TotalSeconds, 0, 5);
    }
    static bool IsFresh(long nanoseconds)
        => (DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch.AddTicks(nanoseconds / 100)).TotalSeconds is >= 0 and <= 5;
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
