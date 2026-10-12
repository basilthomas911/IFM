using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

/// <summary>Typed request boundary for the atomic Portfolio order-composition Function actor.</summary>
public interface IPortfolioOrderCompositionApi
{
    ValueTask<ServiceResult<FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent>>> EvaluateAsync(
        EvaluatePortfolioOrderCompositionCommand request, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<FunctionResult<PortfolioCloseOrderCompositionCompletedEvent, PortfolioCloseOrderCompositionFailedEvent>>> EvaluateCloseAsync(
        EvaluatePortfolioCloseOrderCompositionCommand request, CancellationToken cancellationToken = default);
}
