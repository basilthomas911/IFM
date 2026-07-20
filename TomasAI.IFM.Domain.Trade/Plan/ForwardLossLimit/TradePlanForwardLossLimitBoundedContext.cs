using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit;

/// <summary>
/// Represents the context for executing commands related to the forward loss limit in a trade plan.
/// </summary>
/// <remarks>This context is used to manage the state and events associated with the forward loss limit within a
/// trade plan. It provides the necessary infrastructure to handle commands that affect the forward loss limit,
/// leveraging the specified event repository.</remarks>
/// <param name="forwardLossLimitEventRepo"></param>
public class TradePlanForwardLossLimitCommandContext(IEventRepository<TradePlanForwardLossLimitBoundedContextState> forwardLossLimitEventRepo) 
    : BaseCommandContext<TradePlanForwardLossLimitBoundedContextState>(forwardLossLimitEventRepo)
{
}

/// <summary>
/// Represents a bounded context for managing the forward loss limit within a trade plan.
/// </summary>
/// <remarks>This class provides the necessary context for handling operations related to the forward loss limit
/// in a trading plan. It extends the <see cref="BaseBoundedContext{TState}"/> to leverage common bounded context
/// functionalities while focusing on the specific requirements of the forward loss limit.</remarks>
/// <param name="boundedContextState"></param>
/// <param name="commandResolver"></param>
public class TradePlanForwardLossLimitBoundedContext(
    IBoundedContextState<TradePlanForwardLossLimitBoundedContextState> boundedContextState, IBoundedContextCommandResolver commandResolver) 
    : BaseBoundedContext<TradePlanForwardLossLimitBoundedContextState>(boundedContextState, commandResolver)
{
}
