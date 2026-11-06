using MessagePack;
using System.Collections.Frozen;
using System.Reflection;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
namespace TomasAI.IFM.Application.MarketData.Remote;

[MessagePackObject]
public sealed record MarketDataApiCall([property: Key(0)] string Signature, [property: Key(1)] byte[][] Arguments);

/// <summary>Dispatches only declared asynchronous public API methods, never arbitrary reflected types.</summary>
public sealed class MarketDataApiDispatcher(IMarketDataApi backend, bool allowLifecycle = true)
{
    private sealed class Operation(MethodInfo method)
    {
        public MethodInfo Method { get; } = method;
        public Type[] Parameters { get; } = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
        public Type ResultType { get; } = method.ReturnType.IsGenericType ? method.ReturnType.GenericTypeArguments[0] : typeof(bool);
        public PropertyInfo? ResultProperty { get; } = method.ReturnType.IsGenericType ? method.ReturnType.GetProperty(nameof(Task<object>.Result)) : null;
        public bool RequiresHost { get; } = method.Name is nameof(IMarketDataApi.StartAsync) or nameof(IMarketDataApi.StopAsync)
            || method.GetCustomAttribute<MarketDataHostOperationAttribute>() is not null;
    }

    private static readonly FrozenDictionary<string, Operation> Operations = typeof(IMarketDataApi).GetMethods()
        .Where(method => typeof(Task).IsAssignableFrom(method.ReturnType))
        .ToFrozenDictionary(MarketDataApiClient.Signature, static method => new Operation(method), StringComparer.Ordinal);

    public async Task<byte[]> InvokeAsync(MarketDataApiCall call, CancellationToken token)
    {
        if (string.IsNullOrEmpty(call.Signature) || !Operations.TryGetValue(call.Signature, out var operation)
            || !allowLifecycle && operation.RequiresHost)
            throw new MarketDataApiRequestRejectedException("Unknown market-data API operation.");

        var parameters = operation.Parameters;
        var payloads = call.Arguments;
        if (payloads is null || parameters.Length != payloads.Length || Array.Exists(payloads, static argument => argument is null))
            throw new MarketDataApiRequestRejectedException("Market-data argument count or payload differs.");

        object?[] arguments = parameters.Length == 0 ? [] : new object?[parameters.Length];
        try
        {
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                arguments[index] = parameter == typeof(CancellationToken)
                    ? token
                    : payloads[index].Length == 0
                        ? null
                        : MessagePackSerializer.Deserialize(parameter, payloads[index], MarketDataHostConnection.Serialization);
            }
        }
        catch (MessagePackSerializationException error)
        { throw new MarketDataApiRequestRejectedException("Market-data argument payload is invalid.", error); }
        Task pending;
        try { pending = (Task)operation.Method.Invoke(backend, arguments)!; }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        await pending.WaitAsync(token).ConfigureAwait(false);
        var value = operation.ResultProperty is { } result ? result.GetValue(pending) : true;
        return MessagePackSerializer.Serialize(operation.ResultType, value, MarketDataHostConnection.Serialization);
    }
}
