using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoCurrentFuturesContractReconciliationTests
{
    static readonly DateOnly ValueDate = new(2026, 8, 21);
    static readonly DateOnly RolloverDate = new(2026, 9, 18);

    [Fact]
    public async Task ReusesNonExpiredAssignmentOnlyWhenPersistedContractIsCurrent()
    {
        var store = Substitute.For<IFuturesContractRolloverStore>();
        var resolver = Substitute.For<IDatabentoCurrentFuturesContractResolver>();
        var row = Rollover();
        store.GetFuturesContractRolloverAsync("ES", Arg.Any<CancellationToken>())
            .Returns(row);
        store.GetPersistedFuturesContractAsync(row.ContractId!, Arg.Any<CancellationToken>())
            .Returns(Contract(onTheRun: true));
        var api = CreateApi(store, resolver);

        var changed = await api.UpdateOnTheRunFuturesContractAsync("es", ValueDate);

        changed.Should().BeFalse();
        await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default);
        await store.DidNotReceiveWithAnyArgs().ReplaceOnTheRunFuturesContractAsync(
            default!, default!);
    }

    [Theory]
    [InlineData(PersistedAssignmentProblem.Missing)]
    [InlineData(PersistedAssignmentProblem.NotCurrent)]
    [InlineData(PersistedAssignmentProblem.WrongSymbol)]
    public async Task RepairsNonExpiredAssignmentWhenPersistedContractIsInvalid(
        PersistedAssignmentProblem problem)
    {
        var store = Substitute.For<IFuturesContractRolloverStore>();
        var resolver = Substitute.For<IDatabentoCurrentFuturesContractResolver>();
        var row = Rollover();
        var resolvedContract = Contract(onTheRun: true);
        store.GetFuturesContractRolloverAsync("ES", Arg.Any<CancellationToken>())
            .Returns(row);
        store.GetPersistedFuturesContractAsync(row.ContractId!, Arg.Any<CancellationToken>())
            .Returns(problem switch
            {
                PersistedAssignmentProblem.NotCurrent => Contract(onTheRun: false),
                PersistedAssignmentProblem.WrongSymbol => Contract(onTheRun: true) with
                {
                    Symbol = "NQ"
                },
                _ => null
            });
        resolver.ResolveAsync("ES", ValueDate, Arg.Any<CancellationToken>())
            .Returns(new ResolvedCurrentFuturesContract(resolvedContract, RolloverDate));
        var api = CreateApi(store, resolver);

        var changed = await api.UpdateOnTheRunFuturesContractAsync("ES", ValueDate);

        changed.Should().BeFalse("repairing the contract row does not change the rollover date");
        await resolver.Received(1).ResolveAsync("ES", ValueDate, Arg.Any<CancellationToken>());
        await store.Received(1).ReplaceOnTheRunFuturesContractAsync(
            Arg.Is<FuturesContractRolloverReadModel>(replacement =>
                replacement.Symbol == "ES"
                && replacement.ContractId == resolvedContract.ContractId
                && replacement.NextRolloverDate == RolloverDate),
            resolvedContract,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PersistsFrontAndBackVxAsTheCompleteCurrentSet()
    {
        var store = Substitute.For<IFuturesContractRolloverStore>();
        var resolver = Substitute.For<IDatabentoCurrentFuturesContractResolver>();
        var front = VxContract("VX20260916", "VX/U6", new DateOnly(2026, 9, 16));
        var back = VxContract("VX20261021", "VX/V6", new DateOnly(2026, 10, 21), false);
        store.GetFuturesContractRolloverAsync("VX", Arg.Any<CancellationToken>())
            .Returns(new FuturesContractRolloverReadModel
            {
                Symbol = "VX",
                CreatedOn = DateTime.UtcNow,
                CreatedBy = "test"
            });
        resolver.ResolveEligibleAsync("VX", ValueDate, 2, Arg.Any<CancellationToken>())
            .Returns([back, front]);
        var api = CreateApi(store, resolver);

        var changed = await api.UpdateFuturesTermStructureContractsAsync("vx", ValueDate);

        changed.Should().BeTrue();
        await store.Received(1).ReplaceFuturesRolloverSetAsync(
            Arg.Is<FuturesContractRolloverReadModel>(row =>
                row.Symbol == "VX"
                && row.ContractId == front.ContractId
                && row.NextRolloverDate == front.LastTradeDate),
            Arg.Is<IReadOnlyCollection<FuturesContractV3ReadModel>>(contracts =>
                contracts.Select(contract => contract.ContractId)
                    .SequenceEqual(new[] { front.ContractId, back.ContractId })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReusesPersistedVxPairUntilTheFrontRolloverDate()
    {
        var store = Substitute.For<IFuturesContractRolloverStore>();
        var resolver = Substitute.For<IDatabentoCurrentFuturesContractResolver>();
        var front = VxContract("VX20260916", "VX/U6", new DateOnly(2026, 9, 16));
        var back = VxContract("VX20261021", "VX/V6", new DateOnly(2026, 10, 21), false);
        store.GetFuturesContractRolloverAsync("VX", Arg.Any<CancellationToken>())
            .Returns(new FuturesContractRolloverReadModel
            {
                Symbol = "VX",
                ContractId = front.ContractId,
                NextRolloverDate = front.LastTradeDate,
                UpdatedOn = DateTime.UtcNow,
                UpdatedBy = "test",
                CreatedOn = DateTime.UtcNow,
                CreatedBy = "test"
            });
        store.GetFuturesRolloverSetAsync("VX", Arg.Any<CancellationToken>())
            .Returns([back, front]);
        var api = CreateApi(store, resolver);

        var changed = await api.UpdateFuturesTermStructureContractsAsync("VX", ValueDate);

        changed.Should().BeFalse();
        await resolver.DidNotReceiveWithAnyArgs().ResolveEligibleAsync(default!, default, default);
        await store.DidNotReceiveWithAnyArgs().ReplaceFuturesRolloverSetAsync(
            default!, default!);
    }

    [Fact]
    public async Task StorageFailureCannotPublishPartialRuntimeAssignment()
    {
        var store = Substitute.For<IFuturesContractRolloverStore>();
        var resolver = Substitute.For<IDatabentoCurrentFuturesContractResolver>();
        var options = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = DatabentoFeedOptions.ForProfile(
                FeedDeploymentProfile.SyntheticCi,
                "GLBX.MDP3"),
            Contracts = []
        };
        var registry = new DatabentoContractRegistrationRegistry([], options);
        store.GetFuturesContractRolloverAsync("ES", Arg.Any<CancellationToken>())
            .Returns(Rollover() with { ContractId = null, NextRolloverDate = null });
        var resolved = Contract(onTheRun: true);
        resolver.ResolveAsync("ES", ValueDate, Arg.Any<CancellationToken>())
            .Returns(new ResolvedCurrentFuturesContract(resolved, RolloverDate));
        store.ReplaceOnTheRunFuturesContractAsync(
                Arg.Any<FuturesContractRolloverReadModel>(),
                Arg.Any<FuturesContractV3ReadModel>(),
                Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Injected durable failure."));
        var api = new DatabentoMarketDataApi(
            Substitute.For<IDatabentoMarketDataEpochFactory>(),
            new DatabentoMarketDataApiOptions(),
            TimeProvider.System,
            resolver,
            store,
            registry);

        var act = () => api.UpdateOnTheRunFuturesContractAsync("ES", ValueDate);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Injected durable failure.");
        registry.TryGetOnTheRunFuturesContract("ES", out _).Should().BeFalse();
        registry.Should().BeEmpty();
    }

    static DatabentoMarketDataApi CreateApi(
        IFuturesContractRolloverStore store,
        IDatabentoCurrentFuturesContractResolver resolver)
        => new(
            Substitute.For<IDatabentoMarketDataEpochFactory>(),
            new DatabentoMarketDataApiOptions(),
            TimeProvider.System,
            resolver,
            store);

    static FuturesContractRolloverReadModel Rollover() => new()
    {
        Symbol = "ES",
        ContractId = "ES20260918",
        NextRolloverDate = RolloverDate,
        UpdatedOn = DateTime.UtcNow,
        UpdatedBy = "test",
        CreatedOn = DateTime.UtcNow,
        CreatedBy = "test"
    };

    static FuturesContractV3ReadModel Contract(bool onTheRun) => new(
        "ES20260918",
        "ES September 2026",
        "ES",
        "ESU6",
        "FUT",
        "USD",
        "CME",
        "50",
        RolloverDate,
        onTheRun);

    static FuturesContractV3ReadModel VxContract(
        string contractId,
        string localSymbol,
        DateOnly lastTradeDate,
        bool onTheRun = true) => new(
            contractId,
            "VX contract",
            "VX",
            localSymbol,
            "FUT",
            "USD",
            "CFE",
            "1000",
            lastTradeDate,
            onTheRun,
            true);

    public enum PersistedAssignmentProblem
    {
        Missing,
        NotCurrent,
        WrongSymbol
    }
}
