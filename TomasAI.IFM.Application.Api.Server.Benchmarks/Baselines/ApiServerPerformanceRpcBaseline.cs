using System.Reflection;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;

/// <summary>Frozen November 7 working-tree client construction, before the performance refactor.</summary>
internal static class ApiServerPerformanceRpcBaseline
{
    internal static string Signature(string operation, Type[] types)
    {
        var method = typeof(IMarketDataApi).GetMethod(operation, types)
            ?? throw new MissingMethodException(operation);
        return method.Name + "(" + string.Join(",", method.GetParameters()
            .Select(parameter => parameter.ParameterType.FullName)) + ")";
    }

    internal static MarketDataApiCall CreateCall(string operation, Type[] types, object?[] arguments)
    {
        var signature = Signature(operation, types);
        var values = arguments.Select(value => value is null ? [] : MessagePackSerializer.Serialize(
            value.GetType(), value, MarketDataHostConnection.Serialization)).ToArray();
        return new(signature, values);
    }

    internal static byte[] Encode(string operation, Type[] types, object?[] arguments)
        => MarketDataHostConnection.Encode(CreateCall(operation, types, arguments));
}
