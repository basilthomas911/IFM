using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record RiskTerminalEvidence([property:Key(0)] Guid SourceCommandId, [property:Key(1)] Guid SourceEventId,
    [property:Key(2)] Guid WorkflowId, [property:Key(3)] int PortfolioId, [property:Key(4)] int FundId,
    [property:Key(5)] int OrderId, [property:Key(6)] string CompositionHash, [property:Key(7)] string TargetStatus,
    [property:Key(8)] Guid RiskResultId, [property:Key(9)] string RiskResultHash, [property:Key(10)] string Reason,
    [property:Key(11)] DateTime DecidedAtUtc);
public interface IRiskWorkflowTerminalEvent : IEvent { RiskTerminalEvidence? TerminalRisk { get; } }
public interface IFundRiskTerminalEvent : IEvent { RiskTerminalEvidence? TerminalRisk { get; } }
