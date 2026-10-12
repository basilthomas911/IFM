using System.Collections.Immutable;
using System.Globalization;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.OptionPricing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Api.Server.Core.MarketData.OptionPricing;

/// <summary>Explicit reviewed reference publication without starting actors, live subscriptions or order execution.</summary>
internal static class OptionPricingReferenceMaintenance
{
    public static async Task RunAsync(IConfiguration config, CancellationToken token)
    {
        var root = config["OptionReference:Root"] ?? throw new ArgumentException("OptionReference:Root required.");
        if (root.Length != 3 || root[0] != 'E' || root[1] is < '1' or > '5' || root[2] is not ('B' or 'D'))
            throw new ArgumentException("OptionReference:Root is outside the reviewed ES Tuesday/Thursday profile.");
        var expiry = DateOnly.ParseExact(config["OptionReference:Expiry"]!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (expiry < ReviewedEsOptionReference.Calendar.CoverageFrom || expiry > ReviewedEsOptionReference.Calendar.CoverageUntil)
            throw new ArgumentException("Expiry is outside reviewed profile coverage.");
        using var logs = LoggerFactory.Create(_ => { });
        var settings = TomasAI.IFM.Application.Storage.ApplicationStorageConnections.CreatePostgres(
            config["IFM_TEST_POSTGRES_CONNECTION"] ?? config["Storage:PostgresConnection"]
                ?? config.GetConnectionString("EventSourceActorDbConnection")
                ?? throw new InvalidOperationException("The shared PostgreSQL application connection is required."));
        SecuritiesDbContext? db = null;
        var factory = new DbContextFactory(new DbContextResolver(_ => db!));
        db = new SecuritiesDbContext(settings, factory, logs.CreateLogger<DbProvider>());
        await new SecuritiesSchemaDb(settings, logs.CreateLogger<DbProvider>()).CreateAsync(
            ["securities"], token);
        var query = new DatabentoFeedFactory().CreateMarketDataQueries(DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3")
            with
        { DataSource = FeedDataSourceMode.DatabentoLive });
        var raw = query.GetContractDetails(root, TimeSpan.FromSeconds(45)).Where(x => x.MaturityDate == expiry).ToArray();
        if (raw.Length == 0 || raw.Select(x => x.Underlying).Distinct().Count() != 1) throw new InvalidDataException("Empty or ambiguous expiry scope.");
        var future = query.GetContractDetail(raw[0].Underlying, TimeSpan.FromSeconds(30)) ?? throw new InvalidDataException("Underlying unavailable.");
        var mapped = raw.Select(x => ReviewedEsOptionReference.Create(x, future)).OrderBy(x => x.Candidate.ContractId, StringComparer.Ordinal).ToArray();
        foreach (var item in mapped)
        {
            var source = raw.Single(x => x.Instrument == item.Candidate.Definition.Instrument);
            var definition = CreateDefinition(source, future, item.Candidate, item.Convention);
            var pending = await db.StageReferenceVersionAsync(definition, token);
            await db.CommitReferenceVersionAsync(pending, token);
        }
        var bundle = new OptionPricingReferenceBundle(1, "", ReviewedEsOptionReference.Version, expiry, new()
        {
            Dataset = future.Dataset,
            DomainContractId = mapped[0].Convention.UnderlyingContractId,
            ProviderContractName = future.RawSymbol,
            RootSymbol = "ES",
            AssetTypeId = AssetTypeId.Futures
        }, ReviewedEsOptionReference.Calendar, UsTreasuryPublicationCalendar.Default2026, UsTreasuryCurve.ConversionPolicy,
            mapped.Select(x => x.Candidate).ToImmutableArray()).Seal();
        await new OptionPricingReferenceBundleStore(db).PublishAsync(bundle, token);
        Console.WriteLine($"Published and read back {mapped.Length} exact {root} mappings for {expiry:yyyy-MM-dd}; bundle={bundle.BundleId}; underlying={future.RawSymbol}/{future.Instrument.InstrumentId}; profile={bundle.ProfileVersion}.");
    }
    static FuturesOptionContractReadModel CreateDefinition(ContractDetail source, ContractDetail future,
        OptionDefinitionCandidate candidate, TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionPricingConvention convention)
    {
        var right = source.ContractKind == ContractKind.CallOption ? ReferenceOptionRight.Call : ReferenceOptionRight.Put;
        var definition = new FuturesOptionContractReadModel(candidate.ContractId, source.RawSymbol, source.Ticker,
            source.RawSymbol, "FOP", source.Currency, source.Exchange,
            convention.Multiplier.ToString(CultureInfo.InvariantCulture), candidate.Definition.MaturityDate,
            (double)candidate.Definition.StrikePrice, right.ToString())
        {
            Dataset = source.Dataset,
            PublisherId = source.Instrument.PublisherId,
            InstrumentId = source.Instrument.InstrumentId,
            RawSymbol = source.RawSymbol,
            StrikePriceDecimal = candidate.Definition.StrikePrice,
            UnderlyingContractId = convention.UnderlyingContractId,
            UnderlyingInstrumentId = future.Instrument.InstrumentId,
            UnderlyingPublisherId = future.Instrument.PublisherId,
            UnderlyingAssetType = ReferenceAssetType.Futures,
            PremiumStyle = ReferencePremiumStyle.PremiumPaid,
            OptionRight = right,
            PriceScale = 1,
            // Reviewed profile evidence timestamp; not asserted to be a provider update timestamp.
            DefinitionTimestampUtc = convention.EffectiveFromUtc,
            DefinitionDigest = candidate.DefinitionDigest,
            RawDefinitionReference = "sha256:" + candidate.DefinitionDigest,
            ExchangeTimeZoneId = ReviewedEsOptionReference.Calendar.TimeZoneId
        };
        return ReviewedOptionReference.Apply(definition, convention);
    }
}
