namespace TomasAI.IFM.Shared.EventSourcing;

public class BoundedContextParameterResolver : IBoundedContextParameterResolver
{
    Func<Type, object>? _resolverFunction;
    Dictionary<Type, object>? _resolverMap;

    /// <summary>
    /// create aggregate parameter resolver
    /// </summary>
    /// <param name="resolverFunction">function that will return aggregate parameter using dependancy injection</param>
    public BoundedContextParameterResolver(Func<Type, object> resolverFunction = null!)
    {
        if (resolverFunction is not null)
            _resolverFunction = resolverFunction;
        else
            _resolverMap = [];
    }

    /// <summary>
    /// add aggregate parameter to resolver map
    /// </summary>
    /// <param name="parameterType"></param>
    /// <param name="cmdHndlr"></param>
    public void Add(Type parameterType, object cmdHndlr)
        => _resolverMap?.TryAdd(parameterType, cmdHndlr);

    /// <summary>
    /// call resolver function to return aggregate parameter from depedancy injection container
    /// </summary>
    /// <param name="parameterType"></param>
    /// <returns></returns>
    public object? Resolve(Type parameterType)
    {
        try
        {
            return _resolverMap is null
                ? _resolverFunction?.Invoke(parameterType)
                : _resolverMap.ContainsKey(parameterType) ? _resolverMap[parameterType] : default;
        }
        catch { }
        {
            return default;
        }
    }

}
