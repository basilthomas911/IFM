using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

/// <summary>Deterministic migration failure tests: no external databases or production configuration.</summary>
public sealed class TradeStrategyFamilyMigrationTests
{
    static readonly DateTime Created = new(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Fresh_catalog_and_restart_allocate_only_once()
    {
        var fixture = new Fixture();
        var first = await fixture.Run();
        var second = await fixture.Run();
        TradeStrategyFamilySeed.Validate(first);
        second.Should().BeEquivalentTo(first);
        fixture.Allocations.Should().Be(3);
    }

    [Fact]
    public async Task Legacy_catalog_preserves_every_identity_version_and_audit_without_allocations()
    {
        var fixture = new Fixture(withLegacy: true);
        var original = fixture.Legacy.ToArray();
        var result = await fixture.Run();
        TradeStrategyFamilySeed.Validate(result);
        result.Select(x => (x.TradeStrategyFamilyId, x.DefinitionVersion, x.CreatedOnUtc, x.CreatedBy))
            .Should().Equal(original.Select(x => (x.TradeStrategyFamilyId, x.DefinitionVersion, x.CreatedOnUtc, x.CreatedBy)));
        fixture.Legacy.Should().Equal(original);
        fixture.Allocations.Should().Be(0);
    }

    [Fact]
    public async Task Interrupted_migration_resumes_after_last_committed_row()
    {
        var fixture = new Fixture(withLegacy: true) { FailOnceOnWrite = 2 };
        var interrupted = () => fixture.Run();
        await interrupted.Should().ThrowAsync<InvalidOperationException>().WithMessage("simulated insert failure");
        fixture.Current.Should().HaveCount(1);
        var first = fixture.Current[0];
        var recovered = await fixture.Run();
        recovered.Should().HaveCount(3).And.Contain(first);
        fixture.Allocations.Should().Be(0);
        (await fixture.Run()).Should().BeEquivalentTo(recovered);
    }

    [Fact]
    public async Task Identity_conflict_fails_without_rewriting_either_catalog()
    {
        var fixture = new Fixture(withLegacy: true);
        fixture.Current.Add(TradeStrategyFamilySeed.Definitions[0].Create(999, Created, "test"));
        var run = () => fixture.Run();
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*identities conflict*");
        fixture.Current.Should().ContainSingle().Which.TradeStrategyFamilyId.Should().Be(999);
        fixture.Legacy[0].TradeStrategyFamilyId.Should().Be(71);
        fixture.Writes.Should().Be(0);
    }

    [Theory]
    [InlineData("UNKNOWN", 1, TradeStrategyFamilyState.Active)]
    [InlineData("FUTURES", 2, TradeStrategyFamilyState.Active)]
    [InlineData("FUTURES", 1, TradeStrategyFamilyState.Retired)]
    public async Task Unsupported_legacy_data_requires_review(string key, long version, TradeStrategyFamilyState state)
    {
        var fixture = new Fixture(withLegacy: true);
        fixture.Legacy[0] = fixture.Legacy[0] with { SystemKey = key, DefinitionVersion = version, State = state };
        var run = () => fixture.Run();
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*requires review*");
        fixture.Writes.Should().Be(0);
    }

    [Fact]
    public async Task Concurrent_initializers_are_idempotent()
    {
        var fixture = new Fixture(withLegacy: true);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => fixture.Run()));
        results.Should().OnlyContain(x => x.Count == 3);
        fixture.Writes.Should().Be(3);
        fixture.Allocations.Should().Be(0);
    }

    [Fact]
    public async Task Cancellation_before_start_does_not_write()
    {
        var fixture = new Fixture();
        var run = () => fixture.Run(new CancellationToken(true));
        await run.Should().ThrowAsync<OperationCanceledException>();
        fixture.Writes.Should().Be(0);
    }

    [Fact]
    public void Storage_is_additive_and_binds_all_typed_fields_in_column_order()
    {
        ReferenceSchemaCql.CreateLegacyTradeStrategyFamilyTable.Should().Contain("trade_strategy_family_v2");
        ReferenceSchemaCql.CreateTradeStrategyFamilyTable.Should().Contain("trade_strategy_family_v3");
        ReferenceDbCql.GetLegacyTradeStrategyFamilies.Should().Contain("FROM trade_strategy_family_v2");
        ReferenceDbCql.GetTradeStrategyFamilies.Should().Contain("systemKey,family,strategy,timeFrame,symbol,currency,description,state");
        ReferenceDbCql.InsertTradeStrategyFamily.Should().Contain("trade_strategy_family_v3").And.Contain("IF NOT EXISTS");
        new InsertTradeStrategyFamily("V1", 71, 1, "Futures-Futures", "Futures", "Futures", "Daily", "ES", "USD", "Daily ES futures", "Active", Created, "test")
            .Bind().Should().BeEquivalentTo(new object[] { "V1", 71, 1L, "Futures-Futures", "Futures", "Futures", "Daily", "ES", "USD", "Daily ES futures", "Active", Created, "test" }, o => o.WithStrictOrdering());
    }

    sealed class Fixture
    {
        readonly IReferenceDbContext _db = Substitute.For<IReferenceDbContext>();
        readonly ISequenceIdGenerator _ids = Substitute.For<ISequenceIdGenerator>();
        public List<TradeStrategyFamilyReadModel> Current { get; } = [];
        public List<LegacyTradeStrategyFamily> Legacy { get; } = [];
        public int Allocations { get; private set; }
        public int Writes { get; private set; }
        public int FailOnceOnWrite { get; init; }

        public Fixture(bool withLegacy = false)
        {
            if (withLegacy)
                Legacy.AddRange(TradeStrategyFamilySeed.Definitions.Select((x, i) =>
                    new LegacyTradeStrategyFamily(71 + i, 1, x.LegacySystemKey, x.Description, TradeStrategyFamilyState.Active, Created, "original")));
            _db.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IReadOnlyList<TradeStrategyFamilyReadModel>>(Current.ToArray()));
            _db.GetLegacyTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IReadOnlyList<LegacyTradeStrategyFamily>>(Legacy.ToArray()));
            _ids.GetSequenceIdAsync(SequenceName.Reference_TradeStrategyFamilyId, Arg.Any<CancellationToken>())
                .Returns(_ => ValueTask.FromResult(1000L + ++Allocations));
            _db.InsertTradeStrategyFamilyAsync(Arg.Any<TradeStrategyFamilyReadModel>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    if (++Writes == FailOnceOnWrite) throw new InvalidOperationException("simulated insert failure");
                    var row = call.Arg<TradeStrategyFamilyReadModel>();
                    if (!Current.Any(x => x.SystemKey == row.SystemKey && x.DefinitionVersion == row.DefinitionVersion)) Current.Add(row);
                    return Task.CompletedTask;
                });
        }

        public Task<IReadOnlyList<TradeStrategyFamilyReadModel>> Run(CancellationToken cancellationToken = default) =>
            new TradeStrategyFamilyBootstrapper(_db, _ids).EnsureV1Async(cancellationToken);
    }
}
