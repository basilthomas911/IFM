using System;
using System.Linq;
using System.Reflection;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.ScyllaDb;

public sealed class ScyllaCqlPolicyTests
{
    [Fact]
    public void AllowFiltering_IsNotUsedByApplicationStorage()
    {
        var cqlTypes = typeof(MarketDataDbContext).Assembly.GetTypes()
            .Where(static type => type == typeof(TradeTickQuoteDbSql)
                || type.Name.EndsWith("Cql", StringComparison.Ordinal));

        var statements = cqlTypes
            .SelectMany(static type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
            .ToArray();

        Assert.NotEmpty(statements);
        Assert.All(statements, static field => Assert.DoesNotContain("ALLOW FILTERING",
            Assert.IsType<string>(field.GetRawConstantValue()), StringComparison.OrdinalIgnoreCase));
    }
}
