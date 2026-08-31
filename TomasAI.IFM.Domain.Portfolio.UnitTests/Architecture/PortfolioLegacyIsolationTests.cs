using FluentAssertions;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Projection;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Architecture;

public sealed class PortfolioLegacyIsolationTests
{
    [Fact]
    [Trait("Gate", "PF-19")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public void Legacy_context_exposes_queries_and_no_mutation_contract()
    {
        typeof(IFundLegacyDbContext).GetInterfaces().Should().NotContain(typeof(IFundDbWriteContext));
        typeof(IFundLegacyDbContext).GetProperties().Should().ContainSingle(x => x.PropertyType == typeof(IFundDbReadContext));
        typeof(IFundLegacyDbContext).GetMethods().Should().NotContain(x =>
            x.Name.StartsWith("Create", StringComparison.Ordinal) ||
            x.Name.StartsWith("Update", StringComparison.Ordinal) ||
            x.Name.StartsWith("Delete", StringComparison.Ordinal) ||
            x.Name.StartsWith("Insert", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Gate", "PF-15")]
    [Trait("Gate", "PF-19")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public void Portfolio_authoritative_types_do_not_depend_on_legacy_or_execution_boundaries()
    {
        var productionTypes = new[] { typeof(PortfolioFundAggregate), typeof(PortfolioProjectionHandler) };
        var referenced = productionTypes.SelectMany(type =>
            type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                .Select(field => field.FieldType)
            .Concat(type.GetConstructors().SelectMany(ctor => ctor.GetParameters().Select(parameter => parameter.ParameterType))))
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        referenced.Should().NotContain(name => name.Contains("FundLegacy", StringComparison.Ordinal));
        referenced.Should().NotContain(name => name.Contains("TradeDb", StringComparison.Ordinal));
        referenced.Should().NotContain(name => name.Contains("Broker", StringComparison.Ordinal));
        referenced.Should().NotContain(name => name.Contains("OrderExecution", StringComparison.Ordinal));
    }
}
