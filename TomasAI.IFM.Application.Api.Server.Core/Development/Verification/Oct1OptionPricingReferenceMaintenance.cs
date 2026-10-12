using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Api.Server.Core.Development.Verification;

/// <summary>Explicit development qualification of the cached 2026-10-01 ES E1D scope.</summary>
internal static class Oct1OptionPricingReferenceMaintenance
{
    static readonly DateOnly Expiry = new(2026, 10, 1);
    const string Evidence = "https://www.cmegroup.com/articles/faqs/e-mini-s-p-500-tuesday-and-thursday-options-frequently-asked-questions.html";

    public static async Task RunAsync(IConfiguration config, CancellationToken token)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase)
            || !config.GetValue<bool>("MarketDataRecovery:Stage3:AllowDevelopmentLiveQualification"))
            throw new InvalidOperationException("Oct 1 reference publication requires Development and explicit live-qualification opt-in.");

        using var logs = LoggerFactory.Create(_ => { });
        var settings = TomasAI.IFM.Application.Storage.ApplicationStorageConnections.CreatePostgres(
            config["IFM_TEST_POSTGRES_CONNECTION"] ?? config["Storage:PostgresConnection"]
                ?? config.GetConnectionString("EventSourceActorDbConnection")
                ?? throw new InvalidOperationException("The shared PostgreSQL application connection is required."));
        SecuritiesDbContext? securities = null;
        var factory = new DbContextFactory(new DbContextResolver(_ => securities!));
        securities = new SecuritiesDbContext(settings, factory, logs.CreateLogger<DbProvider>());
        var rows = await securities.GetCachedOptionContractDefinitionsAsync("ES", "ES20261218", Expiry, ["E1D"], token);
        var selected = rows.Select(row => new Cached(row.UnderlyingContractId, row.ProviderRoot, row.Definition))
            .DistinctBy(row => row.Definition.ContractId).ToArray();
        if (selected.Length == 0) throw new InvalidDataException("No cached Oct 1 E1D definitions for ES20261218.");
        await new SecuritiesSchemaDb(settings, logs.CreateLogger<DbProvider>()).CreateAsync(
            ["securities"], token);
        foreach (var row in selected)
        {
            var definition = ReviewedOptionReference.Apply(row.Definition with { PremiumStyle = ReferencePremiumStyle.PremiumPaid }, Create(row));
            var pending = await securities.StageReferenceVersionAsync(definition, token);
            await securities.CommitReferenceVersionAsync(pending, token);
        }
        Console.WriteLine($"Published and read back {selected.Length} exact Oct 1 E1D contract versions from the published definition cache.");
    }

    static OptionPricingConvention Create(Cached row)
    {
        var value = row.Definition;
        var expected = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
            Expiry.ToDateTime(new TimeOnly(16, 0)),
            TimeZoneInfo.FindSystemTimeZoneById("America/New_York")));
        if (value.Dataset != "GLBX.MDP3" || value.Symbol != "E1D"
            || value.Exchange != "XCME" || value.Currency != "USD"
            || value.UnderlyingContractId != row.Underlying
            || value.OptionRight is not (TomasAI.IFM.Domain.MarketData.Shared.ViewModels.ReferenceOptionRight.Call
                or TomasAI.IFM.Domain.MarketData.Shared.ViewModels.ReferenceOptionRight.Put)
            || value.PublisherId is null or 0 || value.InstrumentId is null or 0
            || value.MultiplierValue is not (null or 50) || value.TickSize is not (null or .05m)
            || value.RawSymbol is null || !value.RawSymbol.StartsWith("E1D", StringComparison.Ordinal)
            || value.DefinitionDigest is not { Length: 64 } || value.MappingVersion is null
            || value.ExpirationUtc != expected || value.GetExactStrikePrice() <= 0)
            throw new InvalidDataException($"Cached E1D definition {value.ContractId} differs from the reviewed CME scope: dataset={value.Dataset}, symbol={value.Symbol}, exchange={value.Exchange}, currency={value.Currency}, underlying={value.UnderlyingContractId}, multiplier={value.MultiplierValue}, tick={value.TickSize}, expiry={value.ExpirationUtc:O}, expected={expected:O}.");
        return new OptionPricingConvention
        {
            SchemaVersion = 2,
            ContractId = value.ContractId,
            Dataset = value.Dataset,
            PublisherId = value.PublisherId.Value,
            InstrumentId = value.InstrumentId.Value,
            RawSymbol = value.RawSymbol,
            Root = "ES",
            Exchange = value.Exchange,
            Currency = value.Currency,
            UnderlyingContractId = row.Underlying,
            ExerciseStyle = OptionExerciseStyle.European,
            SettlementStyle = OptionSettlementStyle.DeliveryOfFuture,
            ExpirationUtc = expected,
            LastTradingUtc = expected,
            DayCount = PricingDayCount.Actual365Fixed,
            CalendarVersion = "IFM-MarketDates",
            Multiplier = 50,
            TickSize = .05m,
            PremiumTickRule = OptionPremiumTickRule.CmeEsGlobex358A,
            TickRuleVersion = OptionPremiumTicks.CmeEsGlobexVersion,
            DefinitionDigest = value.DefinitionDigest,
            MappingVersion = value.MappingVersion,
            EvidenceId = Evidence,
            EffectiveFromUtc = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            EffectiveUntilUtc = expected
        };
    }

    sealed record Cached(string Underlying, string Root,
        TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel Definition);
}
