using MessagePack;
using TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.Baselines;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave;

public static class PerformanceWaveVerification
{
    public static async Task VerifyAbruptWalExit()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-wave-crash-" + Guid.NewGuid().ToString("N"));
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(typeof(PerformanceWaveVerification).Assembly.Location);
            start.ArgumentList.Add("--wave-wal-crash-child"); start.ArgumentList.Add(root);
            using var child = System.Diagnostics.Process.Start(start)!;
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            if (child.ExitCode != 0) throw new InvalidDataException("WAL child failed.");
            await using var wal = new TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.FuturesItiInputWal(root);
            var count = 0;
            await foreach (var record in wal.ReplayAsync())
                if (BitConverter.ToInt32(record.Payload) != count++) throw new InvalidDataException("WAL recovery order changed.");
            if (count != 64) throw new InvalidDataException("Acknowledged WAL records were lost.");
            Console.WriteLine("Abrupt process exit: all 64 acknowledged WAL records recovered in order without disposal.");
        }
        finally
        {
            if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("ifm-wave-crash-", StringComparison.Ordinal))
                Directory.Delete(root, true);
        }
    }

    public static void Verify()
    {
        foreach (var count in new[] { 0, 1, 40, 1000 })
        {
            var fixture = new PositionWaveBenchmarks { Count = count }; fixture.Setup();
            foreach (var history in new[] { fixture.History, fixture.History.Reverse().ToArray(), fixture.History.OrderBy(_ => Guid.NewGuid()).ToArray() })
            {
                var original = MessagePackBinarySerializer.Shared.Serialize(history)!;
                var before = BaselineStrategyPositionHistoryProjection.EnrichHistory(history, fixture.Trade);
                var after = StrategyPositionHistoryProjection.EnrichHistory(history, fixture.Trade);
                if (!MessagePackBinarySerializer.Shared.Serialize(before)!.AsSpan().SequenceEqual(MessagePackBinarySerializer.Shared.Serialize(after)))
                    throw new InvalidDataException($"Position financial result changed: {count} rows.");
                if (!original.AsSpan().SequenceEqual(MessagePackBinarySerializer.Shared.Serialize(history)))
                    throw new InvalidDataException("Position enrichment mutated its input.");
            }
        }
        var metrics = new MetricWaveBenchmarks(); metrics.Run(false); metrics.Run(true);
        Console.WriteLine("Financial equivalence passed for empty, single, ordered, reversed and shuffled histories; all metric points exported unchanged.");
    }
}
