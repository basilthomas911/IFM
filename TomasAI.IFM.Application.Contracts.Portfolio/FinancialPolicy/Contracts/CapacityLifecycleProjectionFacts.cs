using MessagePack;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record CapacityLifecycleProjectionFacts
{
    [Key(0)] public int Version { get; init; } = 1;
    [Key(1)] public string ActorName { get; init; } = string.Empty;
    [Key(2)] public CapacityLifecycleRequest Request { get; init; } = new();
    [Key(3)] public ReservationStatus PreviousStatus { get; init; }
    [Key(4)] public CapacityTransition Transition { get; init; } = new(ReservationStatus.Reserved,0,0,0,0,0,0,0);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(5)] public AcceptedCapacityUsage[] Usage { get; init; } = [];
}
