using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Shared.Storage;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

public static class TdiReceiptVerification
{
    public static async Task VerifyPostgres()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("tdireceipts", includeCql: false);
        await infrastructure.StartAsync();
        var connectionString = new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString)
        { Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString;
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync(); await using var command = new NpgsqlCommand(MarketDataSchemaDb.CreateStatement, connection);
            await command.ExecuteNonQueryAsync();
        }
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection, infrastructure.PostgresConnectionString, "System.Data.Postgres");
        var factory = Substitute.For<IDbContextFactory>();
        var market = new MarketDataDbContext(settings, factory, Substitute.For<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
        factory.MarketDataDb.Returns(market);
        var root = Path.Combine(Path.GetTempPath(), "IFM-tdi-pg-" + Guid.NewGuid().ToString("N"));
        var source = TdiReceiptFixture.Source();
        source = source with { FuturesRsiSignals = source.FuturesRsiSignals.Select(signal => signal with
        { Timestamp = signal.Timestamp.Add(TimeSpan.FromTicks(7)), SourceEventTimestamp = signal.SourceEventTimestamp.AddTicks(7) }).ToArray() };
        FuturesTdiResultReceipt original;
        using (var receipts = new FuturesTdiResultReceipts(root))
        {
            var first = new TdiReceiptFixture.Scenario(receipts) { WriteDatabase = market.InsertFuturesTdiSignalAsync, ThrowAfterDatabase = true };
            await first.Start();
            try { await first.Process(source); throw new InvalidDataException("Injected lost commit response was not exercised."); }
            catch (IOException) { }
            original = receipts.Pending().Single();
        }
        var persisted = await market.GetLastFuturesTdiSignalAsync("ESU6", TdiReceiptFixture.Date, TimeFrameType.OneMinute, FuturesTdiConfiguration.StandardConfigurationId);
        if (persisted?.Cross != FuturesTdiCrossType.Bullish || persisted.SourceEventTimestamp.Ticks % 10 != 0)
            throw new InvalidDataException("Initial PostgreSQL cross/precision did not match the stored row.");
        using (var receipts = new FuturesTdiResultReceipts(root))
        {
            var restored = new TdiReceiptFixture.Scenario(receipts) { WriteDatabase = market.InsertFuturesTdiSignalAsync, Persisted = persisted };
            await restored.Start();
            if (!await restored.State.RecoverAsync(restored.Projector) || !await restored.Process(source)) throw new InvalidDataException("Recovery failed.");
            var reloaded = source with { FuturesRsiSignals = source.FuturesRsiSignals.Select(signal => signal with
            { Timestamp = new TimeOnly(signal.Timestamp.Ticks / 10 * 10), SourceEventTimestamp = new DateTime(signal.SourceEventTimestamp.Ticks / 10 * 10, DateTimeKind.Utc) }).ToArray() };
            if (!await restored.Process(reloaded)) throw new InvalidDataException("Database-precision window failed to match its original receipt.");
            var receipt = receipts.Find(original.Generated.Id)!;
            if (receipt.Generated.Id != original.Generated.Id || receipt.Completed.Id != original.Completed.Id
                || receipt.Generated.FuturesTdiSignal.Cross != FuturesTdiCrossType.Bullish || receipts.Pending().Length != 0
                || receipt.Generated.FuturesTdiSignal.SourceEventTimestamp.Ticks % 10 != 7)
                throw new InvalidDataException("Original receipt changed across PostgreSQL recovery.");
            if (restored.Sources.Count != 0 || restored.Completions.Count != 1) throw new InvalidDataException("Recovered publication prefix was repeated.");
        }
        persisted = await market.GetLastFuturesTdiSignalAsync("ESU6", TdiReceiptFixture.Date, TimeFrameType.OneMinute, FuturesTdiConfiguration.StandardConfigurationId);
        if (persisted?.Cross != FuturesTdiCrossType.Bullish) throw new InvalidDataException("Recovery overwrote the original bullish cross.");
        Console.WriteLine("Disposable PostgreSQL: committed Bullish cross, lost response, restart/retry retained original generated/completion IDs and Bullish row; no repeated source publication.");
    }

    public static async Task VerifyCrash()
    {
        foreach (var stage in new[] { "source", "database", "completion" })
        {
            var root = Path.Combine(Path.GetTempPath(), "IFM-tdi-crash-" + Guid.NewGuid().ToString("N"));
            var start = new System.Diagnostics.ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(typeof(TdiReceiptVerification).Assembly.Location);
            start.ArgumentList.Add("--tdi-receipt-crash-child"); start.ArgumentList.Add(root); start.ArgumentList.Add(stage);
            using var child = System.Diagnostics.Process.Start(start)!;
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            if (child.ExitCode != 0) throw new InvalidDataException("TDI receipt crash child failed.");
            using var store = new FuturesTdiResultReceipts(root);
            var original = store.Pending().Single();
            var after = new TdiReceiptFixture.Scenario(store) { Persisted = original.Generated.FuturesTdiSignal }; await after.Start();
            if (!await after.State.RecoverAsync(after.Projector) || !await after.Process(TdiReceiptFixture.Source())) throw new InvalidDataException("TDI crash recovery failed.");
            var restored = store.Find(original.Generated.Id)!;
            if (restored.Generated.Id != original.Generated.Id || restored.Completed.Id != original.Completed.Id
                || restored.Generated.FuturesTdiSignal.Cross != FuturesTdiCrossType.Bullish || store.Pending().Length != 0)
                throw new InvalidDataException("TDI crash recovery changed its original result/identities.");
            Console.WriteLine($"Abrupt process exit after {stage}: original Bullish result, generated ID and completion ID recovered without a fresh RSI signal.");
        }
    }
}
