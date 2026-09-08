using System.Collections.Immutable;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Framework.OptionPricer.Black76;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class CompositionBusinessProjectionTests
{
    [Theory]
    [InlineData(2, false)] [InlineData(4, false)] [InlineData(2, true)] [InlineData(4, true)]
    public async Task Committed_legs_handoff_to_real_pricing_runtime_restore_after_replacement_and_drain_on_close(int count, bool advanceValueDate)
    {
        await using var fixture = await Fixture.Create();
        var plan = Plan(count, fixture.Scope);
        plan = (plan with { Calendar = plan.Calendar! with { TimeZoneId = "America/New_York",
            TradingDates = Enumerable.Range(0, 25).Select(i => new DateOnly(2026, 9, 8).AddDays(i))
                .Where(x => x.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToImmutableArray() } }).Seal();
        await fixture.SavePlan(plan);
        var selection = new CompositionContractSelection(plan.PlanId, plan.Options.Select(x => x.ContractId).ToImmutableArray());
        var trade = new OptionTradeReadModel { OrderId = 8765, TradeId = 1, TradeState = TradeState.OrderPlaced,
            CompositionContracts = selection, UnderlyingContractId = "ES-future" }
            .AddOptionLegs(selection.ContractIds.Select(x => new OptionTradeLegReadModel { ContractId = x, Quantity = 1 }).ToArray());
        var placed = new OptionTradeOrderPlacedEvent { Id = Guid.NewGuid(), EntityId = trade.EntityId, OptionTrade = trade,
            Subject = new(ActorType.Event, OptionTradeOrderPlacedEvent.Actor, OptionTradeOrderPlacedEvent.Verb, trade.EntityId.Format()) };
        var eventId = await fixture.Append(placed, 1);
        var reference = CommittedCompositionSubscriptionSource.Reference((await fixture.Events.GetEventLogByEventIdAsync(eventId))!, BusinessSubscriptionSourceKind.TradeOrder);
        var clock = new PricingClock(); var oldGeneration = Guid.NewGuid();
        for (var replacement = 0; replacement < 2; replacement++)
        {
            if (replacement == 1 && advanceValueDate) clock.Now = clock.Now.AddDays(1);
            var valueDate = DateOnly.FromDateTime(clock.Now.UtcDateTime);
            // The feed is a controlled UTC transport; consumer, chain session, pricing, snapshot,
            // durable source, coordinator, ownership mapping and PostgreSQL delivery are actual implementations.
            var generation = replacement == 0 ? oldGeneration : Guid.NewGuid();
            var saved = (await fixture.Plans.ReadAsync(plan.PlanId, default))!;
            using var feed = new PricingFeed(); using var prices = new DatabentoLastPriceStore(valueDate, 16);
            prices.RegisterContract("ES-future", AssetTypeId.Futures);
            void Forward() => prices.TryUpdateQuote(new("ES-future", valueDate, 4999.75m, 10, 1, 5000.25m, 10, 1, 1, clock.Now, clock.Now));
            Forward();
            var factory = Substitute.For<IDatabentoFeedFactory>(); factory.CreateOptionChainFeed(Arg.Any<DatabentoFeedOptions>()).Returns(feed);
            var aggregation = Substitute.For<ITickAggregationService>();
            aggregation.GetTickerStatus("ES-future").Returns(new TickAggregationTickerStatus("ES-future", true, true, true));
            await using var worker = new WorkerOptionChainRuntime(generation, valueDate, factory,
                DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"), aggregation, prices, clock);
            var request = new WorkerOptionChainRequest(saved.PlanId, Guid.NewGuid(), generation, valueDate, saved.MaturityDate,
                clock.Now.AddSeconds(60), saved.Options.Select(x => PricedDefinition(saved, x, generation, clock.Now)).ToImmutableArray());
            var acquired = await worker.AcquireAsync(request, default); Assert.True(acquired.Active, acquired.Failure?.Code);
            await using var coordinator = new MarketDataSubscriptionCoordinator(fixture.Scope, "GLBX.MDP3", valueDate, timeProvider: clock);
            var delivery = new DurableSubscriptionDelivery(fixture.Store);
            async Task<DurableRealization> Realize(DesiredSubscriptionManifest manifest, CancellationToken token)
            {
                var committed = await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3", token);
                var result = await worker.ReleaseAsync(CommittedOptionChainOwnership.Create(committed, request), token);
                Assert.Null(result.Failure);
                return new(manifest.Revision, generation, true);
            }
            async Task Release(CancellationToken token)
            { Assert.True((await worker.ReleaseAsync(new(request.ScopeId, request.LeaseId, generation), token)).Active); }
            if (replacement == 0)
                Assert.Equal(DurableIntentResultCode.Committed, (await delivery.HandoffAsync(reference, fixture.Source(), coordinator, Realize, Release, default)).Code);
            else
            {
                Assert.True((await delivery.ReconcileAsync(coordinator, Realize, default)).AllRoutesReady);
                await Release(default);
                Assert.Equal("Recovering", (await worker.AcquireAsync(request with { GenerationId = oldGeneration }, default)).Failure?.Code);
                if (advanceValueDate)
                    Assert.Equal("Recovering", (await worker.AcquireAsync(request with { ValueDate = valueDate.AddDays(-1) }, default)).Failure?.Code);
            }
            Assert.Empty(await fixture.Store.ReadPendingOutboxAsync(fixture.Scope, "GLBX.MDP3"));
            // Discovery has expired. Only PostgreSQL-derived business owners keep every leg alive.
            clock.Now = clock.Now.AddMinutes(3); Forward();
            var owner = (await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3")).Authorities.SelectMany(x => x.Leases).First();
            request = request with { LeaseId = owner.LeaseId, LeaseExpiresAtUtc = clock.Now.AddSeconds(60), ExpectedContextDigest = acquired.ContextDigest,
                Options = request.Options.Select(x => x with { Pricing = x.Pricing with { ValidUntilUtc = clock.Now.AddHours(1) } }).ToImmutableArray() };
            Assert.True((await worker.AcquireAsync(request, default)).Active);
            foreach (var option in request.Options) feed.Push(option.Pricing.Contract.InstrumentId, clock.Now);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!selection.ContractIds.All(x => prices.GetFuturesOptionReader(x, valueDate).TryGetLastQuoteWithGreeks(out _)))
                await Task.Delay(5, deadline.Token);
            var capture = await new MarketCompositionSnapshotProvider(worker, clock).CaptureAsync(
                new(Guid.NewGuid(), saved.PlanId, "Daily", generation, clock.Now, clock.Now.AddSeconds(2), true), default);
            Assert.Null(capture.Failure); Assert.Equal(count, capture.Snapshot!.Instruments.Length);
            Assert.All(capture.Snapshot.Instruments, x => Assert.NotNull(x.Valuation));
            if (replacement == 0)
                await fixture.Append(new OptionTradePositionOpenedEvent { Id = Guid.NewGuid(), EntityId = trade.EntityId, OptionTradeId = trade.EntityId,
                    TradePositionState = TradePositionState.Opened,
                    Subject = new(ActorType.Event, OptionTradePositionOpenedEvent.Actor, OptionTradePositionOpenedEvent.Verb, trade.EntityId.Format()) }, 2);
            else
                await fixture.Append(new OptionTradePositionClosedEvent { Id = Guid.NewGuid(), EntityId = trade.EntityId, OptionTradeId = trade.EntityId,
                    TradePositionState = TradePositionState.Closed,
                    Subject = new(ActorType.Event, OptionTradePositionClosedEvent.Actor, OptionTradePositionClosedEvent.Verb, trade.EntityId.Format()) }, 3);
            await fixture.Projector().ProjectPendingAsync(default);
            Assert.True((await delivery.ReconcileAsync(coordinator, Realize, default)).AllRoutesReady);
            Assert.Equal(replacement == 0 ? 0 : 1, feed.Stops);
        }
        Assert.All((await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3")).Authorities, x => Assert.Empty(x.Leases));
        Assert.Empty(await fixture.Store.ReadPendingOutboxAsync(fixture.Scope, "GLBX.MDP3"));
    }

    static WorkerOptionDefinition PricedDefinition(CompositionRoutePlan plan, OptionDefinitionCandidate candidate, Guid generation, DateTimeOffset at)
    {
        var contract = new OptionPricingConvention { ContractId = candidate.ContractId, Dataset = plan.Dataset,
            PublisherId = candidate.Definition.Instrument.PublisherId, InstrumentId = candidate.Definition.Instrument.InstrumentId,
            RawSymbol = candidate.Definition.RawSymbol, Root = "ES", Exchange = "XCME", Currency = "USD", UnderlyingContractId = "ES-future",
            ExerciseStyle = OptionExerciseStyle.European, SettlementStyle = OptionSettlementStyle.DeliveryOfFuture,
            ExpirationUtc = new(2026, 10, 2, 16, 0, 0, TimeSpan.Zero), LastTradingUtc = new(2026, 10, 2, 16, 0, 0, TimeSpan.Zero),
            DayCount = PricingDayCount.Actual365Fixed, CalendarVersion = plan.Calendar!.Version, Multiplier = 50, TickSize = .25m,
            TickRuleVersion = "fixture/v1", DefinitionDigest = new('a', 64), MappingVersion = "fixture/v1", EvidenceId = "synthetic",
            EffectiveFromUtc = at.AddDays(-1), EffectiveUntilUtc = at.AddDays(30) };
        var curve = new TreasuryCurveSnapshot(DateOnly.FromDateTime(at.UtcDateTime), [new(TreasuryTenor.OneMonth, 5m)], at, "FinancialModelingPrep");
        var rate = TreasuryRateConversion.Convert(curve, TreasuryTenor.OneMonth, plan.Conversion!).Value!;
        return new(new(contract, plan.Calendar, rate, at.AddHours(1), generation, OptionCalculator.EngineVersion, 1000, 250, "fixture/v1"), candidate.Definition.StrikePrice, true);
    }
    sealed class PricingClock : TimeProvider
    { public DateTimeOffset Now = new(2026, 9, 8, 16, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
    sealed class PricingFeed : IDatabentoOptionChainFeed
    {
        readonly BoundedBatchChannel channel = new(4, 64); public int Stops;
        public ISynchronousBatchReader<MarketDataBatch64> Reader => channel;
        public void Subscribe(OptionChainSubscription subscription, TimeSpan timeout) { }
        public void Start(TimeSpan timeout, Action<TimeSpan> consumer) => consumer(timeout);
        public void Push(uint id, DateTimeOffset at)
        {
            var ns = checked((at.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100);
            var batch = channel.RentBatch(() => false);
            batch.Add(new(new QuoteRecord64(new(id, 1, MarketRecordKind.Quote, 0, ns, ns, 1), 99_750_000_000, 100_250_000_000, 10, 10, 1, 1)));
            Assert.True(channel.Publish(batch, () => false));
        }
        public void Stop(TimeSpan timeout) { Stops++; channel.Complete(); }
        public FeedHealthSnapshot GetHealth() => throw new NotSupportedException();
        public void Dispose() => channel.Complete();
    }
}
