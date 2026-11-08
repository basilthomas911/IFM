using System.Collections.Frozen;
using System.Diagnostics.Metrics;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

/// <summary>Compiled route policy with bounded atomic Query/Command sampling counters.</summary>
public sealed class ActorInformationLoggingPolicy
{
    static readonly Meter LogMeter = new("TomasAI.IFM.ActorLogging", "1.0");
    static readonly Counter<long> SuppressedSuccesses = LogMeter.CreateCounter<long>("actor.logging.query_command.successes_suppressed");
    readonly FrozenSet<ActorType> _suppressedActorTypes;
    readonly FrozenSet<ActorInformationLogRoute> _suppressedRoutes;
    readonly int queryEvery;
    readonly int commandEvery;
    readonly TimeSpan? slowThreshold;
    long querySequence;
    long commandSequence;
    long sampledOut;
    internal ActorInformationLoggingPolicy(
        FrozenSet<ActorType> suppressedActorTypes,
        FrozenSet<ActorInformationLogRoute> suppressedRoutes, int queryEvery = 1, int commandEvery = 1,
        TimeSpan? slowThreshold = null)
    {
        _suppressedActorTypes = suppressedActorTypes;
        _suppressedRoutes = suppressedRoutes;
        this.queryEvery = queryEvery;
        this.commandEvery = commandEvery;
        this.slowThreshold = slowThreshold;
    }
    public static ActorInformationLoggingPolicy Default { get; } = new ActorInformationLoggingOptions().Compile();
    public bool SuppressesRoutineInformation(ActorThreadId actorThreadId, string verb)
        => _suppressedActorTypes.Contains(actorThreadId.ActorType)
           || _suppressedRoutes.Contains(new(actorThreadId.ActorType, actorThreadId.Name, verb));

    /// <summary>Counts suppressed successful Query/Command pairs, not failures, slow overrides or business/audit events.</summary>
    public long SampledOutCount => Interlocked.Read(ref sampledOut);

    /// <summary>Selects once at entry; failure and cancellation completion bypass this decision.</summary>
    public bool SelectRoutineInformation(ActorThreadId id, string verb)
    {
        if (SuppressesRoutineInformation(id, verb)) return false;
        var every = id.ActorType == ActorType.Query ? queryEvery : id.ActorType == ActorType.Command ? commandEvery : 1;
        if (every == 1) return true;
        var sequence = id.ActorType == ActorType.Query
            ? Interlocked.Increment(ref querySequence) : Interlocked.Increment(ref commandSequence);
        var selected = unchecked(sequence - 1) % every == 0;
        return selected;
    }

    /// <summary>Records suppression only after the runtime knows the message succeeded without a slow override.</summary>
    public void RecordSuppressedSuccess(ActorThreadId id)
    {
        if (id.ActorType is not (ActorType.Query or ActorType.Command)) return;
        Interlocked.Increment(ref sampledOut);
        SuppressedSuccesses.Add(1);
    }

    /// <summary>Captures slow sampled calls while keeping explicitly suppressed routes/types suppressed.</summary>
    public bool CapturesSlowInformation(ActorThreadId id, string verb, long started)
        => slowThreshold is { } threshold && id.ActorType is ActorType.Query or ActorType.Command
            && !SuppressesRoutineInformation(id, verb)
            && System.Diagnostics.Stopwatch.GetElapsedTime(started) >= threshold;
}
