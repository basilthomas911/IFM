using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm;

/// <summary>
/// Represents the context for executing commands within the Option Trade Algorithm bounded context.
/// </summary>
/// <remarks>This context is used to manage and execute commands related to option trade algorithms, leveraging
/// the event repository to maintain the state of the bounded context. It inherits from  <see
/// cref="BaseCommandContext{TState}"/> to provide base functionality for command execution and state
/// management.</remarks>
/// <param name="eventRepo"></param>
public class OptionTradeAlgorithmCommandContext(IEventRepository<OptionTradeAlgorithmBoundedContextState> eventRepo)
    : BaseCommandContext<OptionTradeAlgorithmBoundedContextState>(eventRepo)
{
}

/// <summary>
/// Represents the bounded context for managing option trade algorithms within the system.
/// </summary>
/// <remarks>This class provides the infrastructure for handling the state and command resolution specific to
/// option trade algorithms. It extends the <see cref="BaseBoundedContext{TState}"/> to leverage common bounded context
/// functionalities while focusing on the domain of option trades.</remarks>
/// <param name="boundedContextState"></param>
/// <param name="commandResolver"></param>
public class OptionTradeAlgorithmBoundedContext(
    IBoundedContextState<OptionTradeAlgorithmBoundedContextState> boundedContextState,
    IBoundedContextCommandResolver commandResolver) 
    : BaseBoundedContext<OptionTradeAlgorithmBoundedContextState>(boundedContextState, commandResolver)
{
}
