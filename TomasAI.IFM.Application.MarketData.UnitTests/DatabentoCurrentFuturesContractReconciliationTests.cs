using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

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
            .Returns(Contract(currentlyTraded: true));
        var api = CreateApi(store, resolver);

        var changed = await api.UpdateCurrentlyTradedFuturesContractAsync("es", ValueDate);

        changed.Should().BeFalse();
        await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default);
        await store.DidNotReceiveWithAnyArgs().ReplaceCurrentlyTradedFuturesContractAsync(
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
        var resolvedContract = Contract(currentlyTraded: true);
        store.GetFuturesContractRolloverAsync("ES", Arg.Any<CancellationToken>())
            .Returns(row);
        store.GetPersistedFuturesContractAsync(row.ContractId!, Arg.Any<CancellationToken>())
            .Returns(problem switch
            {
                PersistedAssignmentProblem.NotCurrent => Contract(currentlyTraded: false),
                PersistedAssignmentProblem.WrongSymbol => Contract(currentlyTraded: true) with
                {
                    Symbol = "NQ"
                },
                _ => null
            });
        resolver.ResolveAsync("ES", ValueDate, Arg.Any<CancellationToken>())
            .Returns(new ResolvedCurrentFuturesContract(resolvedContract, RolloverDate));
        var api = CreateApi(store, resolver);

        var changed = await api.UpdateCurrentlyTradedFuturesContractAsync("ES", ValueDate);

        changed.Should().BeFalse("repairing the contract row does not change the rollover date");
        await resolver.Received(1).ResolveAsync("ES", ValueDate, Arg.Any<CancellationToken>());
        await store.Received(1).ReplaceCurrentlyTradedFuturesContractAsync(
            Arg.Is<FuturesContractRolloverReadModel>(replacement =>
                replacement.Symbol == "ES"
                && replacement.ContractId == resolvedContract.ContractId
                && replacement.NextRolloverDate == RolloverDate),
            resolvedContract,
            Arg.Any<CancellationToken>());
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

    static FuturesContractV2ReadModel Contract(bool currentlyTraded) => new(
        "ES20260918",
        "ES September 2026",
        "ES",
        "ESU6",
        "FUT",
        "USD",
        "CME",
        "50",
        RolloverDate,
        currentlyTraded);

    public enum PersistedAssignmentProblem
    {
        Missing,
        NotCurrent,
        WrongSymbol
    }
}
