using System;
using System.Linq;
using System.Reflection;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.ScyllaDb;

public sealed class ScyllaCqlPolicyTests
{
    [Fact]
    public void AllowFiltering_IsNotUsedByApplicationStorage()
    {
        var cqlTypes = typeof(MarketDataDbContext).Assembly.GetTypes()
            .Where(static type => type.Name.EndsWith("Cql", StringComparison.Ordinal));

        var actual = cqlTypes
            .SelectMany(static type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
            .Where(static field => ((string?)field.GetRawConstantValue())?.Contains("ALLOW FILTERING", StringComparison.OrdinalIgnoreCase) == true)
            .Select(static field => field.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(actual);
    }
}
