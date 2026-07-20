using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Plan;

/// <summary>
/// Represents the context for executing trade plan commands within a bounded context.
/// </summary>
/// <remarks>This context is used to manage the state and behavior of trade plan commands, leveraging the provided
/// event repository to interact with the trade plan bounded context state.</remarks>
/// <param name="tradeStrategyEventRepo"></param>
public class TradePlanCommandContext(IEventRepository<TradePlanBoundedContextState> tradeStrategyEventRepo) 
    : BaseCommandContext<TradePlanBoundedContextState>(tradeStrategyEventRepo)
{
}

/// <summary>
/// Represents the bounded context for managing trade plans within the application.
/// </summary>
/// <remarks>This class provides the necessary infrastructure to handle trade plan operations, leveraging the
/// specified bounded context state and command resolver.</remarks>
/// <param name="boundedContextState"></param>
/// <param name="commandResolver"></param>
public class TradePlanBoundedContext(
    IBoundedContextState<TradePlanBoundedContextState> boundedContextState,
    IBoundedContextCommandResolver commandResolver) : BaseBoundedContext<TradePlanBoundedContextState>(boundedContextState, commandResolver)
{
}
