using System.Diagnostics.Metrics;
using System.Diagnostics;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

/// <summary>Transport ownership counters are separate from local scheduler queue size.</summary>
public sealed class RealtimeMailboxMetrics
{
    static readonly Meter Meter = new("IFM.RealtimeMailboxes", "1.0");
    static readonly Counter<long> Captures = Meter.CreateCounter<long>("ifm.realtime.capture.count");
    static readonly Counter<long> Publications = Meter.CreateCounter<long>("ifm.realtime.destination.published");
    static readonly Counter<long> Settlements = Meter.CreateCounter<long>("ifm.realtime.delivery.settled");
    static readonly Histogram<double> CaptureTime = Meter.CreateHistogram<double>("ifm.realtime.capture.duration", "ms");
    long captured, published, acknowledged, failed, fenced;
    long setupTicks, setupCount, pullTicks, pullCount, settlementTicks, settlementCount;
    string? lastFailure;
    public long CapturedSources => Interlocked.Read(ref captured);
    public long PublishedDestinations => Interlocked.Read(ref published);
    public long AcknowledgedDeliveries => Interlocked.Read(ref acknowledged);
    public long FailedDeliveries => Interlocked.Read(ref failed);
    public long FencedDeliveries => Interlocked.Read(ref fenced);
    public string? LastFailure => Volatile.Read(ref lastFailure);
    public RealtimeMailboxTiming Timing => new(Interlocked.Read(ref setupCount), TimeSpan.FromTicks(Interlocked.Read(ref setupTicks)).TotalMilliseconds,
        Interlocked.Read(ref pullCount), TimeSpan.FromTicks(Interlocked.Read(ref pullTicks)).TotalMilliseconds,
        Interlocked.Read(ref settlementCount), TimeSpan.FromTicks(Interlocked.Read(ref settlementTicks)).TotalMilliseconds);
    internal void Prepared(long started) { Interlocked.Add(ref setupTicks, Stopwatch.GetElapsedTime(started).Ticks); Interlocked.Increment(ref setupCount); }
    internal void Pulled(long started) { Interlocked.Add(ref pullTicks, Stopwatch.GetElapsedTime(started).Ticks); Interlocked.Increment(ref pullCount); }
    internal void PhysicallySettled(long started) { Interlocked.Add(ref settlementTicks, Stopwatch.GetElapsedTime(started).Ticks); Interlocked.Increment(ref settlementCount); }
    internal void Captured(double milliseconds, int count = 1) { Interlocked.Add(ref captured, count); Captures.Add(count); CaptureTime.Record(milliseconds); }
    internal void Published() { Interlocked.Increment(ref published); Publications.Add(1); }
    internal void Settled(BrokerDeliveryResult result)
    {
        switch (result)
        {
            case BrokerDeliveryResult.Succeeded: Interlocked.Increment(ref acknowledged); break;
            case BrokerDeliveryResult.Fenced: Interlocked.Increment(ref fenced); break;
            default: Interlocked.Increment(ref failed); break;
        }
        Settlements.Add(1, new KeyValuePair<string, object?>("outcome", result.ToString()));
    }
    internal void Failure(Exception error) => Volatile.Write(ref lastFailure, error.GetType().Name + ": " + error.Message);
}

public sealed record RealtimeMailboxTiming(long PreparedMailboxes, double PreparationWaitMilliseconds, long Pulls, double PullWaitMilliseconds,
    long PhysicalSettlements, double SettlementWaitMilliseconds);
