using TomasAI.IFM.Domain.Portfolio.Shared.Events;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
/// <summary>Defines non-serialized behavior shared by Portfolio Fund domain events.</summary>
public interface IPortfolioFundDomainEvent : IPortfolioEvent {
    /// <summary>Gets the accepted projection values when supplied by the current event contract.</summary>
    TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioProjectionFacts? ProjectionFacts => null;
 }
