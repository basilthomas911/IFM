using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

public sealed record RiskHistoryProjectionResult(
    RiskHistoryProjectionDisposition Disposition,
    Guid WorkflowId = default,
    Guid InvocationId = default,
    long Revision = 0,
    string? StoredHash = null,
    string? IncomingHash = null);
