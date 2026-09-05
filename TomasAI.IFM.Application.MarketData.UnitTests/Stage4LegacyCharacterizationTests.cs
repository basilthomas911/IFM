using System.Reflection;
using System.Runtime.ExceptionServices;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.UnitTests.Harness;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

/// <summary>
/// S4G-01 baseline only: these assertions describe the legacy runtime, not Stage 4
/// canonical physical sharing, durable ownership or qualified composition.
/// </summary>
public sealed class Stage4LegacyCharacterizationTests
{
    private const string Future = "ES-202609";
    private const string Call = "ES20260918C6500";
    private const string Put = "ES20260918P6500";
    private const string OtherCall = "ES20260918C6550";
    private static readonly DateOnly Maturity = new(2026, 9, 18);

    [Fact]
    public void Production_registry_individual_reservation_is_idempotent_not_a_lease_count()
    {
        var routes = new ProductionRoutes();

        Assert.True(routes.StartIndividual(Call));
        Assert.False(routes.StartIndividual(Call));
        Assert.True(routes.StopIndividual(Call));
        Assert.False(routes.StopIndividual(Call));
        Assert.True(routes.ReserveChain(Future, Maturity, Call));
    }

    [Fact]
    public void Production_registry_identical_chain_normalizes_duplicates_and_order_but_has_no_owner_count()
    {
        var routes = new ProductionRoutes();

        Assert.True(routes.ReserveChain(Future, Maturity, Call, Put));
        Assert.False(routes.ReserveChain(Future, Maturity, Put, Call, Put));

        // The second reservation is not an independently releasable lease.
        Assert.True(routes.ReleaseChain(Future, Maturity));
        Assert.False(routes.ReleaseChain(Future, Maturity));
        Assert.True(routes.StartIndividual(Call));
        Assert.True(routes.StartIndividual(Put));
    }

    [Fact]
    public void Production_registry_different_universe_conflicts_without_replacing_the_active_chain()
    {
        var routes = new ProductionRoutes();
        Assert.True(routes.ReserveChain(Future, Maturity, Call, Put));

        var error = Assert.Throws<OptionChainConflictException>(() =>
            routes.ReserveChain(Future, Maturity, Call, OtherCall));

        Assert.Equal(Future, error.FuturesContractId);
        Assert.Equal(Maturity, error.MaturityDate);
        Assert.False(routes.ReserveChain(Future, Maturity, Put, Call));
        Assert.Throws<MarketDataRouteConflictException>(() => routes.StartIndividual(Put));
        Assert.True(routes.StartIndividual(OtherCall));
    }

    [Fact]
    public void Production_registry_chain_conflicting_with_individual_reserves_no_partial_contract_set()
    {
        var routes = new ProductionRoutes();
        Assert.True(routes.StartIndividual(Put));

        var error = Assert.Throws<MarketDataRouteConflictException>(() =>
            routes.ReserveChain(Future, Maturity, Call, Put));

        Assert.Equal(Put, error.ContractId);
        Assert.Equal("individual", error.ExistingOwner);
        Assert.False(routes.ReleaseChain(Future, Maturity));
        Assert.True(routes.StartIndividual(Call));
        Assert.False(routes.StartIndividual(Put));
    }

    [Fact]
    public void Production_registry_individual_cannot_release_or_overlap_a_chain_route()
    {
        var routes = new ProductionRoutes();
        Assert.True(routes.ReserveChain(Future, Maturity, Call, Put));

        Assert.False(routes.StopIndividual(Call));
        var error = Assert.Throws<MarketDataRouteConflictException>(() =>
            routes.StartIndividual(Call));
        Assert.Equal($"chain:{Future}:2026-09-18", error.ExistingOwner);
        Assert.False(routes.ReserveChain(Future, Maturity, Call, Put));
    }

    [Fact]
    public void Production_registry_capacity_rejection_does_not_reserve_routes_and_duplicate_works_at_capacity()
    {
        var routes = new ProductionRoutes(maximumChains: 1);
        Assert.True(routes.ReserveChain(Future, Maturity, Call));
        Assert.False(routes.ReserveChain(Future, Maturity, Call));

        var error = Assert.Throws<MarketDataCapacityExceededException>(() =>
            routes.ReserveChain(Future, Maturity.AddDays(7), OtherCall));

        Assert.Equal("option chains", error.ResourceName);
        Assert.Equal(1, error.Capacity);
        Assert.True(routes.StartIndividual(OtherCall));
        Assert.True(routes.ReleaseChain(Future, Maturity));
        Assert.True(routes.ReserveChain(Future, Maturity, Put));
    }

    [Fact]
    public void Production_registry_distinct_nonoverlapping_maturities_are_independent()
    {
        var routes = new ProductionRoutes();
        var nextMaturity = Maturity.AddDays(7);
        Assert.True(routes.ReserveChain(Future, Maturity, Call));
        Assert.True(routes.ReserveChain(Future, nextMaturity, OtherCall));

        Assert.True(routes.ReleaseChain(Future, Maturity));

        Assert.True(routes.StartIndividual(Call));
        Assert.Throws<MarketDataRouteConflictException>(() => routes.StartIndividual(OtherCall));
        Assert.True(routes.ReleaseChain(Future, nextMaturity));
        Assert.True(routes.StartIndividual(OtherCall));
    }

    [Fact]
    public async Task Production_registry_concurrent_identical_reservations_have_one_winner()
    {
        var routes = new ProductionRoutes();

        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(index =>
            Task.Run(() => index % 2 == 0
                ? routes.ReserveChain(Future, Maturity, Call, Put)
                : routes.ReserveChain(Future, Maturity, Put, Call))));

        Assert.Equal(1, results.Count(result => result));
        Assert.Equal(31, results.Count(result => !result));
        Assert.True(routes.ReleaseChain(Future, Maturity));
        Assert.False(routes.ReleaseChain(Future, Maturity));
    }

    [Fact]
    public void Production_registry_clear_forgets_all_epoch_local_ownership()
    {
        var routes = new ProductionRoutes();
        Assert.True(routes.StartIndividual(OtherCall));
        Assert.True(routes.ReserveChain(Future, Maturity, Call, Put));

        routes.Clear();

        Assert.False(routes.ReleaseChain(Future, Maturity));
        Assert.False(routes.StopIndividual(OtherCall));
        Assert.True(routes.ReserveChain(Future, Maturity, OtherCall, Call, Put));
    }

    [Theory]
    [InlineData(nameof(IMarketDataApi.StartStreamingFuturesTickDataAsync))]
    [InlineData(nameof(IMarketDataApi.StopStreamingFuturesTickDataAsync))]
    [InlineData(nameof(IMarketDataApi.StartStreamingFuturesOptionTickDataAsync))]
    [InlineData(nameof(IMarketDataApi.StopStreamingFuturesOptionTickDataAsync))]
    public void Legacy_ticker_bool_overload_and_optional_owner_remain_source_compatible(string name)
    {
        // Search by exact signature so additive typed overloads are permitted.
        var method = typeof(IMarketDataApi).GetMethod(name, [typeof(string), typeof(TickerStreamOwner?)]);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<bool>), method.ReturnType);
        var owner = method.GetParameters()[1];
        Assert.True(owner.IsOptional);
        Assert.Null(owner.DefaultValue);
    }

    [Fact]
    public void Legacy_chain_bool_overloads_remain_ownerless_and_source_compatible()
    {
        var start = typeof(IMarketDataApi).GetMethod(
            nameof(IMarketDataApi.StartStreamingFuturesOptionChainDataAsync),
            [typeof(string), typeof(DateOnly), typeof(string[])]);
        var stop = typeof(IMarketDataApi).GetMethod(
            nameof(IMarketDataApi.StopStreamingFuturesOptionChainDataAsync),
            [typeof(string), typeof(DateOnly)]);

        Assert.NotNull(start);
        Assert.NotNull(stop);
        Assert.Equal(typeof(Task<bool>), start.ReturnType);
        Assert.Equal(typeof(Task<bool>), stop.ReturnType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Production_api_ownerless_compatibility_registration_does_not_remove_an_explicit_owner(bool option)
    {
        // Uses the real public API and a fake epoch to observe API owner forwarding;
        // this test does not claim native-source allocation or physical sharing.
        var context = new MarketDataApiTestContext();
        await using var api = context.Api;
        await context.StartAsync();
        var contract = option ? MarketDataApiTestContext.CallId : MarketDataApiTestContext.FutureId;
        var explicitOwner = new TickerStreamOwner("Workflow", "retained-owner", "leg-1");
        var compatibilityOwner = new TickerStreamOwner(
            nameof(DatabentoMarketDataApi),
            $"compatibility:{MarketDataApiTestContext.ValueDate:yyyy-MM-dd}",
            option ? "option" : "futures");

        Task<bool> Start(TickerStreamOwner? owner = null) => option
            ? api.StartStreamingFuturesOptionTickDataAsync(contract, owner)
            : api.StartStreamingFuturesTickDataAsync(contract, owner);
        Task<bool> Stop(TickerStreamOwner? owner = null) => option
            ? api.StopStreamingFuturesOptionTickDataAsync(contract, owner)
            : api.StopStreamingFuturesTickDataAsync(contract, owner);

        Assert.True(await Start(explicitOwner));
        Assert.True(await Start());
        Assert.False(await Start());
        Assert.Contains((contract, compatibilityOwner), context.Epoch.ActiveStreamOwners);
        Assert.Contains((contract, explicitOwner), context.Epoch.ActiveStreamOwners);

        Assert.True(await Stop());
        Assert.False(await Stop());
        Assert.True(api.IsTickDataStreamActive(contract));
        Assert.Contains((contract, explicitOwner), context.Epoch.ActiveStreamOwners);
        Assert.True(await Stop(explicitOwner));
        Assert.False(api.IsTickDataStreamActive(contract));
        Assert.True(context.Epoch.TickAggregation.ServiceRunning);
    }

    /// <summary>
    /// Invokes the actual internal production registry without widening the production
    /// assembly's visibility or copying its implementation into a test double.
    /// </summary>
    private sealed class ProductionRoutes
    {
        private static readonly Type RegistryType = typeof(DatabentoMarketDataApi).Assembly.GetType(
            "TomasAI.IFM.Application.MarketData.Databento.DatabentoOptionRouteRegistry", throwOnError: true)!;
        private readonly object _registry;

        public ProductionRoutes(int maximumChains = 8) => _registry = Activator.CreateInstance(
            RegistryType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, args: [maximumChains], culture: null)!;

        public bool StartIndividual(string contract) => Invoke<bool>("StartIndividual", contract);
        public bool StopIndividual(string contract) => Invoke<bool>("StopIndividual", contract);
        public bool ReserveChain(string future, DateOnly maturity, params string[] contracts) =>
            Invoke<bool>("ReserveChain", future, maturity, contracts);
        public bool ReleaseChain(string future, DateOnly maturity) => Invoke<bool>("ReleaseChain", future, maturity);
        public void Clear() => Invoke<object?>("Clear");

        private T Invoke<T>(string methodName, params object[] arguments)
        {
            var method = RegistryType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            try { return (T)method.Invoke(_registry, arguments)!; }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }
    }
}
