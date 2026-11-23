using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class MarketDataRolloverProjectionTests
{
    static readonly DateOnly Date = new(2026, 11, 18);
    static FuturesContractV3ReadModel Contract(string id, bool front, DateOnly expiry) => new()
    { Symbol = "VX", ContractId = id, OnTheRun = front, Rollover = true, LastTradeDate = expiry };
    static MarketDataReadSnapshot Snapshot(bool healthy = true) => new(
        new(true, Date, [new("Futures", "XCBF.PITCH", Guid.NewGuid(), Guid.NewGuid(), 1, true, healthy,
            healthy ? "Healthy" : "Recovering", null, Date)]), [],
        [Contract("VX20270120", false, new(2027, 1, 20)), Contract("VX20261216", true, new(2026, 12, 16))], []);
    static IFuturesContractRolloverStore Store()
    {
        var store = Substitute.For<IFuturesContractRolloverStore>();
        store.GetFuturesContractRolloverAsync("VX", Arg.Any<CancellationToken>()).Returns(new FuturesContractRolloverReadModel
        { Symbol = "VX", ContractId = "VX20261118", NextRolloverDate = Date,
            CreatedOn = DateTime.UtcNow, CreatedBy = "test" });
        return store;
    }

    [Fact]
    public async Task Projects_active_native_set_once_and_preserves_the_source_flags()
    {
        var store = Store(); var projection = new MarketDataRolloverProjection(store, TimeProvider.System);
        var snapshot = Snapshot();
        await projection.ApplyAsync(snapshot, default);
        await projection.ApplyAsync(snapshot with { Contracts = [.. snapshot.Contracts] }, default);
        await store.Received(1).ReplaceFuturesRolloverSetAsync(
            Arg.Is<FuturesContractRolloverReadModel>(row => row.ContractId == "VX20261216" && row.NextRolloverDate == new DateOnly(2026, 12, 16)),
            Arg.Is<IReadOnlyCollection<FuturesContractV3ReadModel>>(rows => rows.First().OnTheRun && rows.Last().ContractId == "VX20270120"), default);
    }

    [Fact]
    public async Task Does_not_project_an_unhealthy_dataset_or_a_future_preparation_set()
    {
        var store = Store(); var projection = new MarketDataRolloverProjection(store, TimeProvider.System);
        await projection.ApplyAsync(Snapshot(false), default);
        var snapshot = Snapshot();
        await projection.ApplyAsync(snapshot with { Status = snapshot.Status with { ValueDate = Date.AddDays(1) } }, default);
        Assert.Empty(store.ReceivedCalls());
    }

    [Fact]
    public async Task Invalid_or_incomplete_native_set_does_not_replace_the_catalogue()
    {
        var store = Store(); var projection = new MarketDataRolloverProjection(store, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidDataException>(() => projection.ApplyAsync(Snapshot() with
        { Contracts = [Contract("VX20261118", true, Date)] }, default));
        Assert.Empty(store.ReceivedCalls());
    }

    [Fact]
    public async Task Failed_projection_is_retried_instead_of_memoized()
    {
        var store = Store(); var projection = new MarketDataRolloverProjection(store, TimeProvider.System);
        var attempts = 0;
        store.ReplaceFuturesRolloverSetAsync(Arg.Any<FuturesContractRolloverReadModel>(), Arg.Any<IReadOnlyCollection<FuturesContractV3ReadModel>>(), default)
            .Returns(_ => ++attempts == 1 ? Task.FromException(new IOException("retry")) : Task.CompletedTask);
        await Assert.ThrowsAsync<IOException>(() => projection.ApplyAsync(Snapshot(), default));
        await projection.ApplyAsync(Snapshot(), default);
        Assert.Equal(2, attempts);
    }
}
