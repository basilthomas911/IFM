using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.Postgres;

public sealed class PostgresPositionalParameterCatalogTests
{
    static readonly Assembly StorageAssembly = typeof(EventSourceActorDbContext).Assembly;

    static readonly IReadOnlyDictionary<string, string> CqlAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["InsertActorCommandLog"] = "InsertCommandLog",
            ["GetTelemetryLogsByDateRange"] = "GetTelemtryLogsByDateRange",
            ["RetryEventProjectorExecution"] = "TryRetryEventProjectorExecution",
            ["SkipEventProjectorExecution"] = "TrySkipEventProjectorExecution"
        };

    [Theory]
    [InlineData("EventSourceDb", "EventSourceDbSql")]
    [InlineData("LogDb", "LogDbSql")]
    [InlineData("SequenceIdDb", "SequenceIdDbSql")]
    public void EveryPostgresBinding_UsesTypedParametersInDollarOrdinalOrder(
        string catalogNamespace,
        string sqlTypeName)
    {
        var namespaceName = $"TomasAI.IFM.Application.Storage.{catalogNamespace}";
        var sqlType = StorageAssembly.GetType($"{namespaceName}.{sqlTypeName}", throwOnError: true)!;
        var bindTypes = StorageAssembly.GetTypes()
            .Where(type => type.Namespace == namespaceName && typeof(IBindValue).IsAssignableFrom(type))
            .OrderBy(type => type.Name)
            .ToArray();

        Assert.NotEmpty(bindTypes);
        foreach (var bindType in bindTypes)
            AssertBinding(bindType, sqlType);
    }

    static void AssertBinding(Type bindType, Type sqlType)
    {
        var sqlName = CqlAliases.GetValueOrDefault(bindType.Name, bindType.Name);
        var sqlField = sqlType.GetField(sqlName, BindingFlags.Public | BindingFlags.Static);
        Assert.True(sqlField is not null, $"No SQL field maps to {bindType.FullName}.");

        var sql = Assert.IsType<string>(sqlField!.GetValue(null));
        var ordinals = Regex.Matches(sql, @"\$(\d+)")
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToArray();
        var expectedCount = ordinals.Length == 0 ? 0 : ordinals.Max();

        var constructor = Assert.Single(bindType.GetConstructors());
        var constructorParameters = constructor.GetParameters();
        var arguments = constructorParameters
            .Select((parameter, index) => CreateSentinel(parameter.ParameterType, index))
            .ToArray();
        var instance = Assert.IsAssignableFrom<IBindValue>(constructor.Invoke(arguments));
        var parameters = Assert.IsType<NpgsqlParameter[]>(instance.Bind());
        var expectedParameters = constructorParameters
            .SelectMany((parameter, index) => ExpandExpectedParameters(parameter.ParameterType, arguments[index]))
            .ToArray();

        Assert.Equal(expectedCount, parameters.Length);
        Assert.Equal(expectedParameters.Length, parameters.Length);
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            var expected = expectedParameters[index];
            Assert.Equal(string.Empty, parameter.ParameterName);
            if (expected.BoundType is { } boundType)
                Assert.Equal(boundType, parameter.NpgsqlDbType);
            else if (expected.ParameterType == typeof(DateTime))
                Assert.Contains(parameter.NpgsqlDbType, new[] { NpgsqlDbType.Timestamp, NpgsqlDbType.TimestampTz });
            else
                Assert.Equal(ExpectedDbType(expected.ParameterType), parameter.NpgsqlDbType);
            Assert.Equal(expected.Value, parameter.Value);
            Assert.True(parameter.GetType().IsGenericType,
                $"{bindType.Name} parameter {index} is not strongly typed.");
        }
    }

    static IEnumerable<ExpectedParameter> ExpandExpectedParameters(Type type, object value)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (!typeof(IBindValue).IsAssignableFrom(type))
        {
            yield return new ExpectedParameter(type, value, null);
            yield break;
        }

        foreach (var parameter in Assert.IsType<NpgsqlParameter[]>(((IBindValue)value).Bind()))
            yield return new ExpectedParameter(parameter.Value.GetType(), parameter.Value, parameter.NpgsqlDbType);
    }

    static object CreateSentinel(Type type, int index)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (typeof(IBindValue).IsAssignableFrom(type))
        {
            var constructor = Assert.Single(type.GetConstructors());
            var arguments = constructor.GetParameters()
                .Select((parameter, nestedIndex) => CreateSentinel(parameter.ParameterType, index + nestedIndex))
                .ToArray();
            return constructor.Invoke(arguments);
        }

        var value = index + 1;
        if (type == typeof(string)) return $"value-{value}";
        if (type == typeof(int)) return value;
        if (type == typeof(long)) return (long)value;
        if (type == typeof(bool)) return index % 2 == 0;
        if (type == typeof(Guid)) return new Guid(value, 0, 0, new byte[8]);
        if (type == typeof(DateTime)) return new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(index);
        if (type == typeof(byte[])) return new byte[] { (byte)value };
        throw new InvalidOperationException($"No PostgreSQL test sentinel exists for {type}.");
    }

    static NpgsqlDbType ExpectedDbType(Type type)
    {
        if (type == typeof(string)) return NpgsqlDbType.Text;
        if (type == typeof(int)) return NpgsqlDbType.Integer;
        if (type == typeof(long)) return NpgsqlDbType.Bigint;
        if (type == typeof(bool)) return NpgsqlDbType.Boolean;
        if (type == typeof(Guid)) return NpgsqlDbType.Uuid;
        if (type == typeof(DateTime)) return NpgsqlDbType.Timestamp;
        if (type == typeof(byte[])) return NpgsqlDbType.Bytea;
        throw new InvalidOperationException($"No PostgreSQL type expectation exists for {type}.");
    }

    readonly record struct ExpectedParameter(Type ParameterType, object Value, NpgsqlDbType? BoundType);
}
