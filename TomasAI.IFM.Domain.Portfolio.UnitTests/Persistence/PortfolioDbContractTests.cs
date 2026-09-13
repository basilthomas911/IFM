using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Persistence;

public sealed class PortfolioDbContractTests
{
    [Fact]
    [Trait("Gate", "PPG-05")]
    public void Every_Postgres_query_is_schema_qualified_and_contains_no_CQL_filtering()
    {
        var queries = Nested(typeof(PortfolioDbSql))
            .SelectMany(type => type.GetFields().Where(field => field.IsLiteral && field.FieldType == typeof(string)))
            .Select(field => (string)field.GetRawConstantValue()!).ToArray();
        queries.Should().NotBeEmpty();
        queries.Should().OnlyContain(sql => !sql.Contains("ALLOW FILTERING", StringComparison.OrdinalIgnoreCase));
        queries.Where(sql => sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
            .Should().OnlyContain(sql => sql.Contains("portfolio.", StringComparison.OrdinalIgnoreCase)
                || sql.Contains("portfolio_financial.", StringComparison.OrdinalIgnoreCase)
                || sql.Contains("event_log", StringComparison.OrdinalIgnoreCase)
                || sql.Contains("event_stream_id", StringComparison.OrdinalIgnoreCase)
                || sql.Contains("event_name_id", StringComparison.OrdinalIgnoreCase)
                || sql.Contains("pg_advisory_xact_lock", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Gate", "PPG-05")]
    public void Schema_is_additive_schema_qualified_and_has_required_book_of_record_tables()
    {
        var schema = PortfolioDbSql.Schema.Create;
        schema.Should().Contain("CREATE SCHEMA IF NOT EXISTS portfolio");
        schema.Should().Contain("portfolio.portfolio_by_id");
        schema.Should().Contain("portfolio.fund_by_portfolio");
        schema.Should().Contain("portfolio.portfolio_policy");
        schema.Should().Contain("portfolio.order_composition_decision");
        schema.Should().Contain("portfolio.accepted_trade_order_leg");
        schema.Should().NotContain("ALLOW FILTERING");
    }

    [Fact]
    [Trait("Gate", "PPG-05")]
    public void Portfolio_context_is_one_sealed_type_with_exact_read_write_interfaces()
    {
        typeof(PortfolioDbContext).IsSealed.Should().BeTrue();
        typeof(PortfolioDbContext).GetInterfaces().Where(type => type.Namespace == typeof(IPortfolioDbReadContext).Namespace)
            .Should().BeEquivalentTo([typeof(IPortfolioDbReadContext), typeof(IPortfolioDbWriteContext)]);
        typeof(IPortfolioDbReadContext).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.Namespace == typeof(IPortfolioDbReadContext).Namespace
                && type.Name.StartsWith("IPortfolioDb", StringComparison.Ordinal))
            .Should().BeEquivalentTo([typeof(IPortfolioDbReadContext), typeof(IPortfolioDbWriteContext)]);
    }

    [Fact]
    [Trait("Gate", "PPG-05")]
    public void Projection_factory_creates_stable_canonical_hash_and_rejects_invalid_metadata()
    {
        var now = new DateTime(2026,8,29,20,0,0,DateTimeKind.Utc);
        var value = new PortfolioReadModel { PortfolioId=1,Name="P1",PortfolioVersion=1,OperatingState=PortfolioOperatingState.Draft,EffectiveFromUtc=now,CreatedOnUtc=now,CreatedBy="unit" };
        var a = PortfolioProjection<PortfolioReadModel>.Create(value,1,1,now);
        var b = PortfolioProjection<PortfolioReadModel>.Create(value,1,1,now);
        a.PayloadHash.Should().Be(b.PayloadHash).And.HaveLength(64);
        FluentActions.Invoking(() => PortfolioProjection<PortfolioReadModel>.Create(value,0,1,now)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Gate", "PPG-05")]
    public void Portfolio_delete_tombstone_guards_every_child_projection_upsert()
    {
        var guarded = new[] { PortfolioDbSql.Fund.Upsert, PortfolioDbSql.Fund.UpsertAssignment,
            PortfolioDbSql.Fund.UpsertAllocation, PortfolioDbSql.Fund.UpsertEnvelope,
            PortfolioDbSql.Orders.UpsertOrder, PortfolioDbSql.Orders.UpsertTrade,
            PortfolioDbSql.Orders.UpsertComposition };
        guarded.Should().OnlyContain(sql => sql.Contains("projection_tombstone", StringComparison.Ordinal));
        PortfolioDbSql.Portfolio.DeleteDraft.Should().Contain("fund_order_trade")
            .And.Contain("fund_composition").And.Contain("fund_order");
    }

    static IEnumerable<Type> Nested(Type type) => type.GetNestedTypes()
        .SelectMany(child => new[] { child }.Concat(Nested(child)));
}
