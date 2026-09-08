using System.Collections.Immutable;
using System.Globalization;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Api.Server;

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
        var settings = new DbConnectionSettings().Add("reference", config.GetConnectionString("ReferenceDbConnection")
            ?? throw new InvalidOperationException("ReferenceDbConnection required."), "System.Data.ScyllaDb");
        var db = new Repository(settings["reference"], logs.CreateLogger<DbProvider>());
        await db.Use("OptionReference.Schema", OptionPricingConventionStore.CreateTable).ExecuteCommandAsync(token);
        await db.Use("OptionReference.BundleSchema", OptionPricingReferenceBundleStore.CreateTable).ExecuteCommandAsync(token);
        var query = new DatabentoFeedFactory().CreateMarketDataQueries(DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3")
            with { DataSource = FeedDataSourceMode.DatabentoLive });
        var raw = query.GetContractDetails(root, TimeSpan.FromSeconds(45)).Where(x => x.MaturityDate == expiry).ToArray();
        if (raw.Length == 0 || raw.Select(x => x.Underlying).Distinct().Count() != 1) throw new InvalidDataException("Empty or ambiguous expiry scope.");
        var future = query.GetContractDetail(raw[0].Underlying, TimeSpan.FromSeconds(30)) ?? throw new InvalidDataException("Underlying unavailable.");
        var mapped = raw.Select(x => ReviewedEsOptionReference.Create(x, future)).OrderBy(x => x.Candidate.ContractId, StringComparer.Ordinal).ToArray();
        var store = new OptionPricingConventionStore(db);
        foreach (var item in mapped) await store.InsertReviewedAsync(item.Convention, token);
        var bundle = new OptionPricingReferenceBundle(1, "", ReviewedEsOptionReference.Version, expiry, new()
        {
            Dataset = future.Dataset, DomainContractId = mapped[0].Convention.UnderlyingContractId, ProviderContractName = future.RawSymbol,
            RootSymbol = "ES", AssetTypeId = AssetTypeId.Futures
        }, ReviewedEsOptionReference.Calendar, UsTreasuryPublicationCalendar.Default2026, UsTreasuryCurve.ConversionPolicy,
            mapped.Select(x => x.Candidate).ToImmutableArray()).Seal();
        await new OptionPricingReferenceBundleStore(db).PublishAsync(bundle, token);
        Console.WriteLine($"Published and read back {mapped.Length} exact {root} mappings for {expiry:yyyy-MM-dd}; bundle={bundle.BundleId}; underlying={future.RawSymbol}/{future.Instrument.InstrumentId}; profile={bundle.ProfileVersion}.");
    }
    sealed class Repository(IDbConnectionSetting setting, ILogger<DbProvider> logger) : ObjectDataRepository<Repository>(setting, logger)
    { public override IObjectRepository Database => this; }
}
