using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Command;

/// <summary>
/// create command handler reolver
/// </summary>
/// <param name="resolverFunction">function that will return command handler using dependancy injection</param>
public class CommandHandlerResolver(Func<Type, object> resolverFunction) : ICommandHandlerResolver
{
    readonly Func<Type, object> _resolverFunction = IsArgumentNull.Set(resolverFunction);

    /// <summary>
    /// call resolver function to return command handler from depedancy injection container
    /// </summary>
    /// <param name="commandHandlerType"></param>
    /// <returns></returns>
    public object Resolve(Type commandHandlerType)
    {
        try
        {
            return _resolverFunction(commandHandlerType);
        }
        catch 
        {
            return default!;
        }
    }
   
}
