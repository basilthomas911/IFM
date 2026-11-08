using System.Collections.Frozen;
using System.Reflection;
using System.Buffers;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Remote;

/// <summary>Immutable operation metadata shared by concurrent client calls.</summary>
internal sealed class MarketDataOperationCatalog
{
    internal sealed class Descriptor
    {
        readonly Type[] parameters;
        readonly byte[] prefix;
        internal MethodInfo Method { get; }
        internal string Signature { get; }
        internal int ParameterCount => parameters.Length;
        internal ReadOnlySpan<byte> Prefix => prefix;

        internal Descriptor(MethodInfo method)
        {
            Method = method;
            Signature = MarketDataApiClient.Signature(method);
            parameters = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(2);
            writer.Write(Signature);
            writer.WriteArrayHeader(parameters.Length);
            writer.Flush();
            prefix = buffer.WrittenSpan.ToArray();
        }

        internal bool Matches(ReadOnlySpan<Type> types)
        {
            if (types.Length != parameters.Length) return false;
            for (var index = 0; index < types.Length; index++)
                if (types[index] != parameters[index]) return false;
            return true;
        }
    }
    readonly FrozenDictionary<string, Descriptor[]> operations;

    internal static MarketDataOperationCatalog Shared { get; } = new();

    internal MarketDataOperationCatalog()
    {
        operations = typeof(IMarketDataApi).GetMethods()
            .Select(method => new Descriptor(method))
            .GroupBy(operation => operation.Method.Name, StringComparer.Ordinal)
            .ToFrozenDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
    }

    internal Descriptor Resolve(string operation, ReadOnlySpan<Type> parameterTypes)
    {
        if (operations.TryGetValue(operation, out var overloads))
            foreach (var overload in overloads)
                if (overload.Matches(parameterTypes)) return overload;
        throw new MissingMethodException(operation);
    }
}
