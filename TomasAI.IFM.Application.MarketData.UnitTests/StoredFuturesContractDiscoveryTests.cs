using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class StoredFuturesContractDiscoveryTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 17, 23, 0, 0, TimeSpan.Zero);
    static readonly DateOnly ValueDate = new(2026, 11, 18);
    static readonly Guid Snapshot = Guid.Parse("19484c46-2f90-4ec6-b969-2d33d990dc01");

    [Fact]
    public async Task Concurrent_expiry_day_reconciliation_registers_next_contract_once_and_reuses_committed_pair()
    {
        var rows = Existing();
        var catalog = Catalog(rows);
        var definitions = Definitions(Definition());
        var publisher = Publisher(rows);
        var discovery = new StoredFuturesContractDiscovery(definitions, catalog, publisher, new FixedClock());
        var assignments = new InMemoryMarketDataServiceStore();
        var registry = Substitute.For<IDatabentoContractRegistrationRegistry>();
        using var authority = new DatabentoContractAuthority(assignments, catalog, registry, new FixedClock(), discovery);

        var results = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => authority.ReconcileAsync(ValueDate, "rollover-test", CancellationToken.None)));

        foreach (var result in results)
        {
            result.Where(row => row.RootSymbol == "VX").Select(row => row.ContractId)
                .Should().Equal("VX20261216", "VX20270120");
            result.Select(row => row.RowVersion).Should().OnlyContain(version => version == 1);
        }
        await publisher.Received(1).PublishAsync(Arg.Is<FuturesContractV3ReadModel>(row =>
            row.ContractId == "VX20270120" && row.LocalSymbol == "VX/F7"
            && row.Currency == "USD" && row.Multiplier == "1000"
            && row.InstrumentId == 191071 && row.DefinitionDigest == new string('a', 64)
            && !row.OnTheRun && !row.Rollover), Arg.Any<CancellationToken>());
        await definitions.Received(1).GetSnapshotAsync(Arg.Any<CancellationToken>());
        registry.Received(4).ReplaceFuturesRolloverSet("VX", Arg.Is<IReadOnlyCollection<FuturesContractV3ReadModel>>(pair =>
            pair.Count == 2 && pair.Count(row => row.OnTheRun) == 1 && pair.All(row => row.Rollover)));

        var followingSession = await authority.ReconcileAsync(ValueDate.AddDays(1), "next-session-test", CancellationToken.None);
        followingSession.Where(row => row.RootSymbol == "VX").Select(row => row.ContractId)
            .Should().Equal("VX20261216", "VX20270120");
        followingSession.Select(row => row.RowVersion).Should().OnlyContain(version => version == 1);
        await definitions.Received(1).GetSnapshotAsync(Arg.Any<CancellationToken>());
        await publisher.Received(1).PublishAsync(Arg.Any<FuturesContractV3ReadModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Discovery_pages_the_snapshot_and_registers_only_the_nearest_missing_expiry()
    {
        var rows = Existing();
        var store = Definitions();
        store.GetSelectionPageAsync(Arg.Any<InstrumentDefinitionPageRequest>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<InstrumentDefinitionPageRequest>().ContinuationToken is null
                ? new InstrumentDefinitionPage(Snapshot, Now.UtcDateTime,
                    [Definition() with { RawSymbol = "VX/G7", InstrumentId = 193320, ExpirationUtc = Now.AddMonths(3) }], "second")
                : new InstrumentDefinitionPage(Snapshot, Now.UtcDateTime, [Definition()], null)));
        var publisher = Publisher(rows);
        var discovery = new StoredFuturesContractDiscovery(store, Catalog(rows), publisher, new FixedClock());

        await discovery.EnsureEligibleAsync("VX", ValueDate, 2, CancellationToken.None);

        await publisher.Received(1).PublishAsync(Arg.Is<FuturesContractV3ReadModel>(row => row.ContractId == "VX20270120"), Arg.Any<CancellationToken>());
        rows.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("deleted")]
    [InlineData("expiry-day")]
    [InlineData("wrong-root")]
    [InlineData("wrong-snapshot")]
    [InlineData("not-active")]
    [InlineData("bad-digest")]
    public async Task Unqualified_provider_candidates_never_register_or_replace_assignments(string problem)
    {
        var candidate = problem switch
        {
            "deleted" => Definition() with { Deleted = true },
            "expiry-day" => Definition() with { ExpirationUtc = new(2026, 11, 18, 14, 0, 0, TimeSpan.Zero) },
            "wrong-root" => Definition() with { Root = "ES" },
            "wrong-snapshot" => Definition() with { SnapshotId = Guid.NewGuid() },
            "not-active" => Definition() with { ActivationUtc = Now.AddDays(1) },
            _ => Definition() with { DefinitionDigest = "unverified" }
        };
        var rows = Existing();
        var publisher = Publisher(rows);
        var discovery = new StoredFuturesContractDiscovery(Definitions(candidate), Catalog(rows), publisher, new FixedClock());

        var discover = () => discovery.EnsureEligibleAsync("VX", ValueDate, 2, CancellationToken.None);

        await discover.Should().ThrowAsync<InvalidOperationException>();
        await publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
        rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Retry_after_partial_registration_keeps_the_committed_contract_and_finishes_the_pair()
    {
        var rows = Existing();
        rows.RemoveAt(1);
        var catalog = Catalog(rows);
        var definitions = Definitions(Definition(), Definition() with
        {
            RawSymbol = "VX/Z6", InstrumentId = 189061,
            ExpirationUtc = new(2026, 12, 16, 14, 0, 0, TimeSpan.Zero)
        });
        var publisher = Substitute.For<IDiscoveredFuturesContractPublisher>();
        var januaryAttempts = 0;
        publisher.PublishAsync(Arg.Any<FuturesContractV3ReadModel>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var contract = call.Arg<FuturesContractV3ReadModel>();
                if (contract.ContractId == "VX20270120" && ++januaryAttempts == 1)
                    throw new InvalidOperationException("Injected publication interruption");
                rows.Add(contract);
                return Task.CompletedTask;
            });
        var discovery = new StoredFuturesContractDiscovery(definitions, catalog, publisher, new FixedClock());
        var assignments = new InMemoryMarketDataServiceStore();
        using var authority = new DatabentoContractAuthority(assignments, catalog,
            Substitute.For<IDatabentoContractRegistrationRegistry>(), new FixedClock(), discovery);

        var first = () => authority.ReconcileAsync(ValueDate, "rollover-test", CancellationToken.None);
        await first.Should().ThrowAsync<InvalidOperationException>();
        (await assignments.ListAssignmentsAsync()).Should().BeEmpty();
        var retried = await authority.ReconcileAsync(ValueDate, "rollover-test", CancellationToken.None);

        retried.Where(row => row.RootSymbol == "VX").Select(row => row.ContractId)
            .Should().Equal("VX20261216", "VX20270120");
        rows.Select(row => row.ContractId).Should().OnlyHaveUniqueItems();
        await publisher.Received(1).PublishAsync(Arg.Is<FuturesContractV3ReadModel>(row => row.ContractId == "VX20261216"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Conflicting_root_metadata_is_not_guessed_when_the_provider_omits_it()
    {
        var rows = Existing();
        rows[0] = rows[0] with { Multiplier = "1" };
        var publisher = Publisher(rows);
        var discovery = new StoredFuturesContractDiscovery(Definitions(Definition()), Catalog(rows), publisher, new FixedClock());

        var discover = () => discovery.EnsureEligibleAsync("VX", ValueDate, 2, CancellationToken.None);

        await discover.Should().ThrowAsync<InvalidDataException>();
        await publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
    }

    [Fact]
    public async Task Projection_wait_is_cancellable_and_does_not_publish_an_uncommitted_rollover_pair()
    {
        var rows = Existing();
        var catalog = Catalog(rows);
        var publisher = Substitute.For<IDiscoveredFuturesContractPublisher>();
        var discovery = new StoredFuturesContractDiscovery(Definitions(Definition()), catalog, publisher, new FixedClock());
        var assignments = new InMemoryMarketDataServiceStore();
        var registry = Substitute.For<IDatabentoContractRegistrationRegistry>();
        using var authority = new DatabentoContractAuthority(assignments, catalog, registry, new FixedClock(), discovery);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        var reconcile = () => authority.ReconcileAsync(ValueDate, "rollover-test", cancellation.Token);

        await reconcile.Should().ThrowAsync<OperationCanceledException>();
        (await assignments.ListAssignmentsAsync()).Should().BeEmpty();
        registry.DidNotReceiveWithAnyArgs().ReplaceFuturesRolloverSet(default!, default!);
    }

    static List<FuturesContractV3ReadModel> Existing() =>
    [
        Contract("VX20261118", "VX/X6", ValueDate),
        Contract("VX20261216", "VX/Z6", new(2026, 12, 16))
    ];

    static FuturesContractV3ReadModel Contract(string id, string raw, DateOnly expiry)
        => new(id, raw, "VX", raw, "FUT", "USD", "XCBF", "1000", expiry, false, false);

    static ICurrentFuturesContractCatalog Catalog(List<FuturesContractV3ReadModel> rows)
    {
        var catalog = Substitute.For<ICurrentFuturesContractCatalog>();
        catalog.GetFuturesContractsBySymbolAsync("VX", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<ICollection<FuturesContractV3ReadModel>>(rows.ToArray()));
        catalog.GetFuturesContractsBySymbolAsync("ES", Arg.Any<CancellationToken>())
            .Returns([new("ES20261218", "ESZ6", "ES", "ESZ6", "FUT", "USD", "XCME", "50", new(2026, 12, 18), false, false)]);
        return catalog;
    }

    static IDiscoveredFuturesContractPublisher Publisher(List<FuturesContractV3ReadModel> rows)
    {
        var publisher = Substitute.For<IDiscoveredFuturesContractPublisher>();
        publisher.PublishAsync(Arg.Any<FuturesContractV3ReadModel>(), Arg.Any<CancellationToken>())
            .Returns(call => { rows.Add(call.Arg<FuturesContractV3ReadModel>()); return Task.CompletedTask; });
        return publisher;
    }

    static IInstrumentDefinitionStore Definitions(params InstrumentDefinitionSelection[] values)
    {
        var store = Substitute.For<IInstrumentDefinitionStore>();
        store.GetSnapshotAsync(Arg.Any<CancellationToken>()).Returns(new InstrumentDefinitionSnapshot(Snapshot, Now.UtcDateTime, values.Length, ["XCBF.PITCH"]));
        store.GetSelectionPageAsync(Arg.Any<InstrumentDefinitionPageRequest>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new InstrumentDefinitionPage(Snapshot, Now.UtcDateTime, values, null));
        return store;
    }

    static InstrumentDefinitionSelection Definition() => new()
    {
        SnapshotId = Snapshot, Dataset = "XCBF.PITCH", Root = "VX", InstrumentClass = "F",
        PublisherId = 105, InstrumentId = 191071, RawSymbol = "VX/F7", Exchange = "XCBF",
        ExpirationUtc = new(2027, 1, 20, 14, 0, 0, TimeSpan.Zero), DefinitionTimestampUtc = Now.AddDays(-1),
        DefinitionDigest = new string('a', 64), RawDefinitionReference = "instrument_definition:verified:42"
    };

    sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
