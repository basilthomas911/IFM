using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
public interface IRiskWorkflowTerminalEvent : IEvent { RiskTerminalEvidence? TerminalRisk { get; } }
