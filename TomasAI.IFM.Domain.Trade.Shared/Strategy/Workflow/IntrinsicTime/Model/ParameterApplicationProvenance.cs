using MessagePack;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
/// <summary>Exact startup assignment that supplied this execution's immutable parameters.</summary>
[MessagePackObject]
public sealed record ParameterApplicationProvenance(
 [property:Key(0)] Guid StartupRunId,[property:Key(1)] Guid AssignmentId,[property:Key(2)] long AssignmentRevision);
