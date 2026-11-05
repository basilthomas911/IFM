using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventProjector;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Events;

/// <summary>Defines root Portfolio domain event behavior.</summary>
public interface IPortfolioDomainEvent : IPortfolioEvent, IRequireDurableProjection {
    /// <summary>Gets the accepted projection values when supplied by the current event contract.</summary>
    TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioProjectionFacts? ProjectionFacts => null;
    bool IRequireDurableProjection.RequiresDurableProjection => ProjectionFacts is not null;
    DurableProjectionRequirement IRequireDurableProjection.RequiredProjection =>
        new("PortfolioCommandActor", "PortfolioEventProjector", EventProjectorStageType.PublishProcessingEvent);
 }
