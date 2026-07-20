using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order;

/// <summary>
/// Represents the context for executing commands related to option trade orders within a bounded context.
/// </summary>
/// <remarks>This context is used to manage the state and behavior of option trade order commands, leveraging the
/// specified event repository. It inherits from <see cref="BaseCommandContext{TradeOrderBoundedContextState}"/> to
/// provide base functionality for command execution.</remarks>
/// <param name="eventRepo"></param>
public class OptionTradeOrderCommandContext(IEventRepository<TradeOrderBoundedContextState> eventRepo)
    : BaseCommandContext<TradeOrderBoundedContextState>(eventRepo)
{
}

/// <summary>
/// Represents the bounded context for managing trade orders within the system.
/// </summary>
/// <remarks>This class provides the necessary infrastructure to handle trade order operations, leveraging the
/// state and command resolution mechanisms specific to the trade order domain.</remarks>
/// <param name="boundedContextState"></param>
/// <param name="commandResolver"></param>
public class TradeOrderBoundedContext(
    IBoundedContextState<TradeOrderBoundedContextState> boundedContextState,
    IBoundedContextCommandResolver commandResolver) : BaseBoundedContext<TradeOrderBoundedContextState>(boundedContextState, commandResolver)
{
}
