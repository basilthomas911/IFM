using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

public static class BarRecoveryVerification
{
    public static async Task VerifyCrash()
    {
        foreach (var scenario in new[] { "open", "pending", "batch-open", "batch-pending" })
        {
            var root = Path.Combine(Path.GetTempPath(), "IFM-bar-crash-" + Guid.NewGuid().ToString("N"));
            var start = new System.Diagnostics.ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(typeof(BarRecoveryVerification).Assembly.Location);
            start.ArgumentList.Add("--bar-recovery-crash-child"); start.ArgumentList.Add(root); start.ArgumentList.Add(scenario);
            using var child = System.Diagnostics.Process.Start(start)!;
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            if (child.ExitCode != 0) throw new InvalidDataException("Bar crash child failed.");
            using var store = new FuturesTradeSessionBarCheckpointStore(root);
            var recovered = new RealtimeMailboxSafetyFixture.Handler(store);
            if (scenario.StartsWith("batch-", StringComparison.Ordinal))
            {
                var inputs = Enumerable.Range(1, 64).Select(n => (TomasAI.IFM.Shared.EventSourcing.IEvent)RealtimeMailboxSafetyFixture.Trade(n)).ToArray();
                await recovered.ReceiveBatch(inputs); await recovered.ReceiveBatch(inputs);
                var id = recovered.Context.Accumulators.Resolve("ESU6", RealtimeMailboxSafetyFixture.ValueDate);
                await recovered.ReceiveBatch([new TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor.FuturesTradeSessionBarSignalBarrierRealtimeEvent
                {
                    EntityId = id, Subject = new(TomasAI.IFM.Shared.EventModelActor.ActorType.Realtime,
                        TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor.FuturesTradeSessionBarSignalRealtimeActor.ActorName, "Barrier", id.Format()),
                    BarrierUtc = new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero)
                }]);
                var daily = recovered.Persisted.Single(bar => bar.TimeFrame == TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily);
                if (daily.TradeCount != 64 || daily.Open != 101m || daily.Close != 164m || daily.LastSourceSequence != 64)
                    throw new InvalidDataException("Batch restart lost input lineage or duplicated an input.");
                Console.WriteLine($"Abrupt process exit ({scenario}): all 64 inputs recovered exactly once with original OHLCV and sequence.");
                continue;
            }
            await recovered.Receive(RealtimeMailboxSafetyFixture.Trade(2));
            await recovered.Receive(RealtimeMailboxSafetyFixture.Trade(2));
            if (recovered.Persisted.Count != 1 || recovered.Persisted[0].Open != 101m || recovered.Persisted[0].TradeCount != 1)
                throw new InvalidDataException("Bar recovery lost an acknowledged input or duplicated publication.");
            Console.WriteLine($"Abrupt process exit ({scenario}): original OHLCV and source lineage recovered; duplicate delivery produced no extra bar.");
        }
    }
}
