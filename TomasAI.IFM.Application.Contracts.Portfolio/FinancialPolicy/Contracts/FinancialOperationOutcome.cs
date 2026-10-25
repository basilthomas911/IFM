using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record FinancialOperationOutcome
{
    [Key(0)] public LedgerPostingCompletedEvent? Posting { get; init; }
    [Key(1)] public LedgerPostingBatchCompletedEvent? Batch { get; init; }
    [Key(2)] public CapacityReservationCompletedEvent? Reservation { get; init; }
    [Key(3)] public CapacityConsumptionCompletedEvent? Consumption { get; init; }
    [Key(4)] public CapacityLifecycleCompletedEvent? Lifecycle { get; init; }
    [Key(5)] public LedgerConfigurationCompletedEvent? Configuration { get; init; }
    [Key(6)] public EmulatorOrderSubmittedEvent? EmulatorSubmission { get; init; }
}
