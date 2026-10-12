using MessagePack;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record CapacityReservationProjectionFacts
{
    [Key(0)] public int Version { get; init; } = 1;
    [Key(1)] public string ActorName { get; init; } = string.Empty;
    [Key(2)] public CapacityReservationRequest Request { get; init; } = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(3)] public AcceptedCapacityUsage[] Usage { get; init; } = [];
}
