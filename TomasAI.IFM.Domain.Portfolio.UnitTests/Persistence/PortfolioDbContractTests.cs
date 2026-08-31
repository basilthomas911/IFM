using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Persistence;

public sealed class PortfolioDbContractTests
{
    [Fact]
    [Trait("Gate", "PF-08")]
    public void Every_query_is_partitioned_bounded_and_never_uses_allow_filtering()
    {
        var queries = typeof(PortfolioDbCql).GetFields().Where(x => x.Name.StartsWith("Get", StringComparison.Ordinal)).Select(x => (string)x.GetValue(null)!).ToArray();
        queries.Should().HaveCount(19);
        queries.Should().OnlyContain(x => x.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
        queries.Should().OnlyContain(x => !x.Contains("ALLOW FILTERING", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Gate", "PF-08")]
    public void Schema_defines_all_sixteen_new_domain_tables_without_legacy_names()
    {
        var schemas = typeof(PortfolioSchemaCql).GetFields().Select(x => (string)x.GetValue(null)!).ToArray();
        schemas.Should().HaveCount(16);
        schemas.Should().OnlyContain(x => x.Contains("IF NOT EXISTS", StringComparison.OrdinalIgnoreCase));
        schemas.Should().OnlyContain(x => !x.Contains(" fund ", StringComparison.OrdinalIgnoreCase) && !x.Contains("fund_order ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Gate", "PF-08")]
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
    [Trait("Gate", "PF-09")]
    public void Draft_deletion_cql_covers_parent_and_child_projections_with_event_fenced_tombstones()
    {
        var deletes = typeof(PortfolioDbCql).GetFields()
            .Where(x => x.Name.StartsWith("Delete", StringComparison.Ordinal))
            .Select(x => (string)x.GetValue(null)!).ToArray();

        deletes.Should().HaveCount(10);
        deletes.Should().OnlyContain(x => x.Contains("DELETE FROM", StringComparison.OrdinalIgnoreCase));
        deletes.Should().OnlyContain(x => x.Contains("USING TIMESTAMP :projectionWriteTimestamp", StringComparison.Ordinal));
        deletes.Should().OnlyContain(x => x.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
    }
}
