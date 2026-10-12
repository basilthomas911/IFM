using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
public interface IFundRiskTerminalEvent : IEvent { RiskTerminalEvidence? TerminalRisk { get; } }
