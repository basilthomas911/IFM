using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application;

/// <summary>
/// Represents the context for executing application commands within a bounded context.
/// </summary>
/// <remarks>This class provides the necessary context for handling commands in the application, leveraging the
/// event repository to manage state changes.</remarks>
/// <param name="eventRepo"></param>
public class ApplicationCommandContext(IEventRepository<ApplicationBoundedContextState> eventRepo) 
    : BaseCommandContext<ApplicationBoundedContextState>(eventRepo)
{
}

/// <summary>
/// Represents a bounded context within the application, managing its state and command resolution.
/// </summary>
/// <remarks>This class is responsible for encapsulating the state and command resolution logic specific to the
/// application's bounded context. It inherits from <see cref="BaseBoundedContext{TState}"/> to leverage common bounded
/// context functionality.</remarks>
/// <param name="boundedContextState"></param>
/// <param name="commandResolver"></param>
public class ApplicationBoundedContext(IBoundedContextState<ApplicationBoundedContextState> boundedContextState, IBoundedContextCommandResolver commandResolver) 
    : BaseBoundedContext<ApplicationBoundedContextState>(boundedContextState, commandResolver)
{
}
