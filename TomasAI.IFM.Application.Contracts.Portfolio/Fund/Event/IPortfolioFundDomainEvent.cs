using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
/// <summary>Defines non-serialized behavior shared by Portfolio Fund domain events.</summary>
public interface IPortfolioFundDomainEvent : IPortfolioEvent, IRequireDurableProjection {
    /// <summary>Gets the accepted projection values when supplied by the current event contract.</summary>
    TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioProjectionFacts? ProjectionFacts => null;
    bool IRequireDurableProjection.RequiresDurableProjection => ProjectionFacts is not null;
    DurableProjectionRequirement IRequireDurableProjection.RequiredProjection =>
        new("PortfolioFundCommandActor", "PortfolioFundEventProjector", EventProjectorStageType.PublishProcessingEvent);
 }
