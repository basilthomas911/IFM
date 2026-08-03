using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.ScyllaDb;

public sealed class ScyllaDbPositionalParameterCatalogTests
{
    static readonly Assembly StorageAssembly = typeof(FundDbContext).Assembly;

    static readonly IReadOnlyDictionary<string, string> CqlAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["InsertOptionPricerDevice"] = "InsertIOptionPricerDevice",
            ["UpdateSpreadDistributionJobStatus"] = "UpdateSreadDistributionJobStatus",
            ["GetFuturesItiSignalTrendDataByDateRange"] = "GetFuturesItiSignalsByDateRange",
            ["GetLastFuturesAtrDailySignal"] = "GetLastFuturesDailyAtrSignal",
            ["GetOptionLegsWithValueDate"] = "GetOptionLegs",
            ["GetTradePlansByTradeId"] = "GetTradePlansByValueDate",
            ["DeleteTradePositionLowerCase"] = "DeleteTradePosition",
            ["DeleteOptionLegDataLowerCase"] = "DeleteOptionLegData",
            ["InsertTradeLimitNoMaxLoss"] = "InsertTradeLimit",
            ["InsertTradePlanForwardLossRatioShort"] = "InsertTradePlan"
        };

    static readonly IReadOnlyDictionary<string, string> ParameterAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GetScheduledJobId:jobId"] = "jobName",
            ["UpdateSpreadDistributionJobStatus:jobFaild"] = "jobFailed"
        };

    [Theory]
    [InlineData("OptionPricerDb", "OptionPricerDbCql")]
    [InlineData("ReferenceDb", "ReferenceDbCql")]
    [InlineData("SecuritiesDb", "SecuritiesDbCql")]
    [InlineData("MarketDataDb", "MarketDataDbCql")]
    [InlineData("TradeDb", "TradeDbCql")]
    public void EveryScyllaParameterCatalogBinding_MatchesCqlMarkerOrder(
        string catalogNamespace,
        string cqlTypeName)
    {
        var namespaceName = $"TomasAI.IFM.Application.Storage.{catalogNamespace}";
        var cqlType = StorageAssembly.GetType($"{namespaceName}.{cqlTypeName}", throwOnError: true)!;
        var bindTypes = StorageAssembly.GetTypes()
            .Where(type => type.Namespace == namespaceName && typeof(IBindValue).IsAssignableFrom(type))
            .OrderBy(type => type.Name)
            .ToArray();

        Assert.NotEmpty(bindTypes);
        foreach (var bindType in bindTypes)
            AssertBindingMatchesCql(bindType, cqlType);
    }

    static void AssertBindingMatchesCql(Type bindType, Type cqlType)
    {
        var cqlName = CqlAliases.GetValueOrDefault(bindType.Name, bindType.Name);
        var cqlField = cqlType.GetField(cqlName, BindingFlags.Public | BindingFlags.Static);
        Assert.True(cqlField is not null, $"No CQL field maps to {bindType.FullName}.");

        var cql = Assert.IsType<string>(cqlField!.GetValue(null));
        var markers = Regex.Matches(cql, @":(\w+)")
            .Select(match => match.Groups[1].Value)
            .ToArray();

        var constructor = Assert.Single(bindType.GetConstructors());
        var constructorParameters = constructor.GetParameters();
        var arguments = constructorParameters
            .Select((parameter, index) => CreateSentinel(parameter.ParameterType, index))
            .ToArray();
        var valuesByName = constructorParameters
            .Select((parameter, index) => (parameter.Name!, Value: arguments[index]))
            .ToDictionary(entry => entry.Item1, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

        var instance = Assert.IsAssignableFrom<IBindValue>(constructor.Invoke(arguments));
        var actual = Assert.IsType<object?[]>(instance.Bind());
        Assert.True(markers.Length == actual.Length,
            $"{bindType.Name} emitted {actual.Length} values for {markers.Length} CQL markers.");

        for (var index = 0; index < markers.Length; index++)
        {
            var marker = markers[index];
            var aliasKey = $"{bindType.Name}:{marker}";
            var parameterName = ParameterAliases.GetValueOrDefault(aliasKey, marker);
            valuesByName.TryGetValue(parameterName, out var expected);

            Assert.True(Equals(expected, actual[index]),
                $"{bindType.Name} value {index} for :{marker} was '{actual[index]}' instead of '{expected}'.");
        }
    }

    static object? CreateSentinel(Type parameterType, int index)
    {
        var type = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
        var value = index + 1;

        if (type == typeof(string)) return $"value-{value}";
        if (type == typeof(DateOnly)) return new DateOnly(2026, 1, 1).AddDays(index);
        if (type == typeof(DateTime)) return new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(index);
        if (type == typeof(DateTimeOffset)) return new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(index);
        if (type == typeof(TimeOnly)) return new TimeOnly(0, 0).AddMinutes(index);
        if (type == typeof(Guid)) return new Guid(value, 0, 0, new byte[8]);
        if (type == typeof(bool)) return index % 2 == 0;
        if (type == typeof(char)) return (char)('A' + index);
        if (type.IsEnum)
        {
            var enumValues = Enum.GetValues(type);
            return enumValues.GetValue(index % enumValues.Length);
        }
        if (type.IsPrimitive || type == typeof(decimal))
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        if (type.IsArray)
            return Array.CreateInstance(type.GetElementType()!, 0);
        if (type.IsGenericType)
        {
            var listType = typeof(List<>).MakeGenericType(type.GetGenericArguments()[0]);
            if (type.IsAssignableFrom(listType))
                return Activator.CreateInstance(listType);
        }
        if (typeof(IEnumerable).IsAssignableFrom(type))
            return Activator.CreateInstance(type);

        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Cannot create a sentinel for {parameterType}.");
    }
}
