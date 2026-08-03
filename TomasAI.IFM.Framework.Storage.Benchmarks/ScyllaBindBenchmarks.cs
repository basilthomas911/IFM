using System.Reflection;
using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

[MemoryDiagnoser]
public class ScyllaBindBenchmarks
{
    static readonly string[] MarkerNames =
    [
        "fundId",
        "orderId",
        "orderDate",
        "orderStatus",
        "baseContractId",
        "tradeDate",
        "maturityDate",
        "reference",
        "createdOn",
        "createdBy",
        "updatedOn",
        "updatedBy"
    ];

    static readonly IReadOnlyDictionary<string, PropertyInfo> ReflectionProperties =
        typeof(ReflectionOrderParameters)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

    readonly ReflectionOrderBindValue _reflection = new();
    readonly PositionalOrderBindValue _positional = new();

    [Params(100, 1000)]
    public int BatchSize { get; set; }

    [Benchmark(Baseline = true)]
    public object?[] CachedReflectionSingle()
        => BindByCachedReflection(_reflection.Bind());

    [Benchmark]
    public object?[] PositionalSingle()
        => (object?[])_positional.Bind();

    [Benchmark]
    public object?[][] CachedReflectionBatch()
    {
        var result = new object?[BatchSize][];
        for (var index = 0; index < result.Length; index++)
            result[index] = BindByCachedReflection(_reflection.Bind());
        return result;
    }

    [Benchmark]
    public object?[][] PositionalBatch()
    {
        var result = new object?[BatchSize][];
        for (var index = 0; index < result.Length; index++)
            result[index] = (object?[])_positional.Bind();
        return result;
    }

    static object?[] BindByCachedReflection(object parameterValue)
    {
        var values = new object?[MarkerNames.Length];
        for (var index = 0; index < MarkerNames.Length; index++)
            values[index] = ReflectionProperties[MarkerNames[index]].GetValue(parameterValue);
        return values;
    }

    readonly record struct ReflectionOrderBindValue : IBindValue
    {
        public object Bind() => new ReflectionOrderParameters(
            101,
            202,
            new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            "Open",
            "ES",
            new DateOnly(2026, 1, 2),
            new DateOnly(2026, 3, 20),
            "benchmark",
            new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            "benchmark",
            null,
            "benchmark");
    }

    readonly record struct PositionalOrderBindValue : IBindValue
    {
        public object Bind() => new object?[]
        {
            101,
            202,
            new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            "Open",
            "ES",
            new DateOnly(2026, 1, 2),
            new DateOnly(2026, 3, 20),
            "benchmark",
            new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            "benchmark",
            null,
            "benchmark"
        };
    }

    sealed record ReflectionOrderParameters(
        int fundId,
        int orderId,
        DateTime orderDate,
        string orderStatus,
        string baseContractId,
        DateOnly tradeDate,
        DateOnly maturityDate,
        string reference,
        DateTime createdOn,
        string createdBy,
        DateTime? updatedOn,
        string updatedBy);
}
