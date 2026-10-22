using MessagePack;
using System.Reflection;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
namespace TomasAI.IFM.Application.MarketData.Remote;

[MessagePackObject]
public sealed record MarketDataApiCall([property: Key(0)] string Signature, [property: Key(1)] byte[][] Arguments);

/// <summary>Dispatches only declared asynchronous public API methods, never arbitrary reflected types.</summary>
public sealed class MarketDataApiDispatcher(IMarketDataApi backend, bool allowLifecycle = true)
{
    readonly Dictionary<string, MethodInfo> methods = typeof(IMarketDataApi).GetMethods()
        .Where(method => typeof(Task).IsAssignableFrom(method.ReturnType))
        .ToDictionary(MarketDataApiClient.Signature, StringComparer.Ordinal);
    public async Task<byte[]> InvokeAsync(MarketDataApiCall call, CancellationToken token)
    {
        if (!methods.TryGetValue(call.Signature, out var method)
            || !allowLifecycle && (method.Name is nameof(IMarketDataApi.StartAsync) or nameof(IMarketDataApi.StopAsync)
                || method.GetCustomAttribute<MarketDataHostOperationAttribute>() is not null)) throw new InvalidDataException("Unknown market-data API operation.");
        var parameters = method.GetParameters();
        if (parameters.Length != call.Arguments.Length) throw new InvalidDataException("Market-data argument count differs.");
        var arguments = parameters.Select((parameter, index) => parameter.ParameterType == typeof(CancellationToken)
            ? (object)token : call.Arguments[index].Length == 0 ? null
            : MessagePackSerializer.Deserialize(parameter.ParameterType, call.Arguments[index], MarketDataHostConnection.Serialization)).ToArray();
        Task pending;
        try { pending = (Task)method.Invoke(backend, arguments)!; }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        await pending.WaitAsync(token).ConfigureAwait(false);
        var value = method.ReturnType.IsGenericType ? method.ReturnType.GetProperty("Result")!.GetValue(pending) : true;
        var resultType = method.ReturnType.IsGenericType ? method.ReturnType.GenericTypeArguments[0] : typeof(bool);
        return MessagePackSerializer.Serialize(resultType, value, MarketDataHostConnection.Serialization);
    }
}
