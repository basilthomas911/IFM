using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.State;
/// <summary>One incident, replayed from its Started snapshot. No provider resource is held here.</summary>
public sealed class MarketDataFeedRecoveryState : BaseEventSourceActorState<MarketDataFeedRecoveryState>
{
    readonly Dictionary<Guid, string> accepted = [];
    readonly Dictionary<Guid, MarketDataFeedRecoveryFact> facts = [];
    public MarketDataFeedRecoveryFact? FindFact(Guid recordId) => facts.GetValueOrDefault(recordId);
    public override ActorThreadId Id { get; set; } = default!;
    public RecoveryRecord? Latest { get; private set; }
    public RecoveryRecord? Started { get; private set; }
    /// <summary>False is an idempotent duplicate. Conflicts/missing predecessors throw without mutating state.</summary>
    public bool Accept(AppendMarketDataFeedRecoveryCommand command)
    {
        var fact = RecoveryRecord.Parse(command.Payload, command.Digest);
        if (command.EntityId.Value != fact.OperationId) throw new InvalidDataException("Operation route mismatch.");
        if (accepted.TryGetValue(fact.RecordId, out var digest))
        {
            if (digest != command.Digest) throw new InvalidDataException("Recovery RecordId content conflict.");
            return false;
        }
        if (fact.Sequence != (Latest?.Sequence ?? 0) + 1) throw new InvalidDataException("Recovery sequence predecessor missing or conflicting.");
        if (Started is not null && (fact.OperationId != Started.OperationId || fact.ValueDate != Started.ValueDate ||
            fact.Dataset != Started.Dataset || fact.Role != Started.Role || fact.RuntimeId != Started.RuntimeId ||
            fact.StartedAtUtc != Started.StartedAtUtc || fact.RecoveryDeadlineUtc != Started.RecoveryDeadlineUtc))
            throw new InvalidDataException("Immutable incident identity changed.");
        if (Latest?.Kind is "Failed" or "Interrupted" or "MonitoringRestored" ||
            (Latest?.Kind == "Completed" && (fact.Kind != "MonitoringRestored" || Latest.MonitoringReady)))
            throw new InvalidDataException("Incident has already ended.");
        if (fact.Kind=="ResetCompleted" && Latest?.Kind!="ResetStarted" ||
            fact.Kind=="ResetStarted" && Latest?.Kind!="Started" ||
            fact.Kind=="MonitoringRestored" && Latest?.Kind!="Completed")
            throw new InvalidDataException("Invalid native recovery phase transition.");
        if (fact.Kind == "ResetCompleted" && (!fact.OldResourcesRetired || string.IsNullOrWhiteSpace(fact.NewOwner)))
            throw new InvalidDataException("Reset completion requires retirement and replacement identity.");
        MarketDataFeedRecoveryFact source = fact.Kind switch
        {
            "Started" => new MarketDataFeedRecoveryStartedEvent(),
            "ResetStarted" => new MarketDataFeedDatasetResetStartedEvent(),
            "ResetCompleted" => new MarketDataFeedDatasetResetCompletedEvent(),
            "ConnectionRestored" => new MarketDataFeedConnectionRestoredEvent(),
            "Completed" => new MarketDataFeedRecoveryCompletedEvent(),
            "Failed" => new MarketDataFeedRecoveryFailedEvent(),
            "MonitoringRestored" => new MarketDataFeedMonitoringRestoredEvent(),
            "Interrupted" => new MarketDataFeedRecoveryInterruptedEvent(),
            _ => throw new InvalidDataException("Unknown recovery kind.")
        };
        return Update(source with { Id = fact.RecordId, CommandId = command.CommandId, EntityId = command.EntityId,
            Payload = command.Payload, Digest = command.Digest, ReceivedOn = command.ReceivedAtUtc==default?DateTime.UtcNow:command.ReceivedAtUtc,
            Subject = new(ActorType.Event, "MarketDataFeedRecoveryEvent", fact.Kind, command.EntityId.Format()) }, command);
    }
    protected override bool Apply(IEvent source)
    {
        if (source is not MarketDataFeedRecoveryFact fact) return false;
        var record = RecoveryRecord.Parse(fact.Payload, fact.Digest);
        if (accepted.TryGetValue(record.RecordId, out var prior))
        {
            if (prior != fact.Digest) throw new InvalidDataException("Conflicting recovery replay.");
            return true;
        }
        if (record.Sequence != (Latest?.Sequence ?? 0) + 1) throw new InvalidDataException("Non-contiguous recovery replay.");
        accepted.Add(record.RecordId, fact.Digest);
        facts.Add(record.RecordId,fact);
        Started ??= record;
        Latest = record;
        return true;
    }
}
