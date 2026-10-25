using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Api.Server.Core.MarketData.OptionPricing;

/// <summary>Repairs the former ES premium mapping in development without rewriting historical reference versions.</summary>
internal static class EsOptionPremiumMaintenance
{
    /// <summary>Returns a new reviewed mapping version, retaining provider identity and all unrelated economic terms.</summary>
    internal static FuturesOptionContractReadModel Repair(FuturesOptionContractReadModel definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!CmeEsOptionPremiumConvention.AppliesTo(definition.Symbol, definition.Exchange)
            || definition.ReviewState != ReferenceReviewState.Reviewed
            || definition.PremiumStyle != ReferencePremiumStyle.FuturesStyleVariation)
            return definition;
        var version = definition.MappingVersion.StartsWith("StoredInstrumentDefinition/v2/", StringComparison.Ordinal)
            ? definition.MappingVersion.Replace("StoredInstrumentDefinition/v2/", "StoredInstrumentDefinition/v3/", StringComparison.Ordinal)
            : definition.MappingVersion + "/" + CmeEsOptionPremiumConvention.Version;
        var repaired = definition with { PremiumStyle = CmeEsOptionPremiumConvention.PremiumStyle, MappingVersion = version };
        _ = ReferencePayloadCodec.Write(repaired);
        return repaired;
    }

    /// <summary>Atomically replaces current ES catalog payloads and publishes a corrected immutable calendar generation.</summary>
    public static async Task RunAsync(IConfiguration config, CancellationToken token)
    {
        var connection = config["Storage:PostgresConnection"]
            ?? throw new InvalidOperationException("Storage:PostgresConnection is required.");
        var identity = PostgresDatabaseIdentity.Parse(connection);
        if (identity.Database != "ifm_application_development" || identity.Port != 5432
            || identity.Host is not ("LOCALHOST" or "127.0.0.1"))
            throw new InvalidOperationException("ES premium repair is restricted to the local development database.");
        var settings = ApplicationStorageConnections.CreatePostgres(connection);
        using var logs = LoggerFactory.Create(_ => { });
        SecuritiesDbContext? securities = null;
        var factory = new DbContextFactory(new DbContextResolver(_ => securities!));
        securities = new SecuritiesDbContext(settings, factory, logs.CreateLogger<DbProvider>());
        var result = await new PostgresEventTransaction(settings).ExecuteOnceAsync(async (db, ct) =>
        {
            await db.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended($1,0));",
                ["securities.option-definitions:ES"], ct);
            var states = await db.QueryAsync("""
                SELECT generation,coveragefrom,coveragethrough FROM securities.option_contract_expiry_calendar_state
                WHERE symbol='ES' FOR UPDATE;
                """, [], row => (Generation: row.GetGuid(0), From: row.GetFieldValue<DateOnly>(1), Through: row.GetFieldValue<DateOnly>(2)), ct);
            var catalog = await db.QueryAsync("""
                SELECT referencepayload FROM securities.futures_option_contract
                WHERE symbol='ES' AND referencepayload IS NOT NULL;
                """, [], row => Decode(row.GetFieldValue<byte[]>(0)), ct);
            var definitions = await db.QueryAsync("""
                SELECT c.expirydate,c.providerroot,c.contractid,c.underlyingcontractid,c.optionfamily,c.definitionpayload
                FROM securities.option_contract_expiry_calendar c JOIN securities.option_contract_expiry_calendar_state s
                ON c.symbol=s.symbol AND c.generation=s.generation WHERE c.symbol='ES';
                """, [], row => (Expiry: row.GetFieldValue<DateOnly>(0), Root: row.GetString(1), ContractId: row.GetString(2),
                    UnderlyingId: row.GetString(3), Family: row.GetString(4), Definition: Decode(row.GetFieldValue<byte[]>(5))), ct);
            var catalogChanges = catalog.Select(original => (Original: original, Repaired: Repair(original)))
                .Where(pair => pair.Original != pair.Repaired).ToArray();
            var calendarChanges = definitions.Count(row => Repair(row.Definition) != row.Definition);
            // Validate the entire replacement before writing anything; an incomplete review rolls back the repair.
            var replacement = definitions.Select(row => (row.Expiry, row.Root, row.ContractId, row.UnderlyingId, row.Family,
                Payload: ReferencePayloadCodec.Write(Repair(row.Definition)))).ToArray();
            foreach (var pair in catalogChanges)
                await securities.ProjectOptionContractAsync(db, pair.Repaired, pair.Original.ContractId, ct);
            var generation = states.Count == 1 ? states[0].Generation : Guid.Empty;
            if (calendarChanges > 0)
            {
                var state = states.Single();
                generation = Guid.NewGuid();
                await new PostgresSecuritiesReferenceStore().PublishDefinitionsAsync(db, "ES", generation,
                    state.From, state.Through, DateTime.UtcNow, replacement, ct);
            }
            return (Catalog: catalogChanges.Length, Calendar: calendarChanges, Generation: generation);
        }, token);
        Console.WriteLine($"ES premium repair committed: catalog={result.Catalog}, calendar={result.Calendar}, generation={result.Generation}; historical reference versions retained; convention={CmeEsOptionPremiumConvention.Version}.");
    }

    // This explicit maintenance boundary must decode the invalid former convention before qualifying its replacement.
    static FuturesOptionContractReadModel Decode(byte[] payload)
    {
        var definition = MessagePackBinarySerializer.Shared.Deserialize<FuturesOptionContractReadModel>(payload)
            ?? throw new InvalidDataException("An ES reference payload was empty.");
        return ReferencePayloadCodec.ReadOption(payload, definition);
    }
}
