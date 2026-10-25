using TomasAI.IFM.Domain.Portfolio.Shared.Events;
namespace TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
/// <summary>Defines non-serialized behavior shared by Portfolio FinancialPolicy domain events.</summary>
public interface IPortfolioFinancialPolicyDomainEvent : IPortfolioEvent {
    /// <summary>Gets the accepted projection values when supplied by the current event contract.</summary>
    TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioProjectionFacts? ProjectionFacts => null;
 }
