using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model;
/// <summary>Frozen workflow proposals ready for command-owned event creation.</summary>
internal sealed record WorkflowSnapshotTransition(IReadOnlyList<WorkflowSnapshotChange> WorkflowChanges, string? RejectionReason = null);
