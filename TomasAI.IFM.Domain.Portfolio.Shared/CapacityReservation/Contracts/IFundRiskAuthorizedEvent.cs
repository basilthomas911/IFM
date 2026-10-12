using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Storage validates a new Fund authorization in the same transaction as its Fund event.</summary>
public interface IFundRiskAuthorizedEvent : IEvent
{
    FundRiskAuthorizationReference? FinancialAuthorization { get; }
}
