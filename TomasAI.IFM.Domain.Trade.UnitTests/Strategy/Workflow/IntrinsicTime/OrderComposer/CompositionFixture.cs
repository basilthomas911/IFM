using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Framework.OptionPricer.Black76;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

internal static class CompositionFixture
{
    internal static CompleteOrderCompositionCommand Completion(ExecuteOrderCompositionPipelineCommand c, OrderCompositionResult r) => new()
    {
        CommandId = Guid.NewGuid(), EntityId = c.WorkflowEntityId, WorkflowId = c.WorkflowId, InputWorkflowRevision = c.InputWorkflowRevision,
        SourceEventId = c.CommandId, CorrelationId = c.CorrelationId, CausationId = c.CommandId, CompletedAtUtc = c.EvaluatedAtUtc,
        Result = StrategyStageResultEnvelope.CreateComposition(r), SelectedContracts = r.Candidate is null ? null :
            new(c.MarketSnapshot.ScopeId, r.Candidate.Legs.Select(x => x.InstrumentId).Order(StringComparer.Ordinal).ToImmutableArray())
    };
    public static readonly string[] Variants = ["LongFuture", "ShortFuture", "BullCallDebit", "BearCallCredit", "BullPutCredit", "BearPutDebit",
        "ShortBalancedIronCondor", "ShortBullishIronCondor", "ShortBearishIronCondor", "LongBalancedIronCondor", "LongBullishIronCondor", "LongBearishIronCondor"];
    public static async Task<ExecuteOrderCompositionPipelineCommand> Command(string variant = "LongFuture", TimeFrameType horizon = TimeFrameType.Daily, DateTime? atUtc = null, bool integrationTiming = false, string contractId = "ESZ6",
        ExecuteTradeSelectionPipelineCommand? actualSelectionCommand=null,TradeSelectionResult? actualSelectionResult=null)
    {
        var selection = actualSelectionCommand??await TradeSelectionFixture.Command(variant, horizon, atUtc, contractId: contractId, compositionReady: true, compositionIntegrationTiming: integrationTiming);
        var result = actualSelectionResult??new TradeSelectionEvaluator().Calculate(selection);
        Assert.Equal(SelectionOutcome.Selected, result.Outcome);
        var at = actualSelectionResult is null?selection.EvaluatedAtUtc:DateTime.UtcNow;
        var envelope = StrategyStageResultEnvelope.CreateSelection(result);
        var pending = TradeSelectionHandoff.Pending(result, envelope, 4, result.ResultId, at);
        var reservation = new PortfolioFundCompositionAggregate().Reserve(pending.Request, selection.SelectionBinding.PortfolioSnapshot, 7001, [8001], at, "composition-fixture");
        var view = selection.WorkflowView with { CurrentStage = StrategyWorkflowStage.OrderComposition, WorkflowRevision = 5,
            UpdatedAtUtc = at, TradeSelection = selection.WorkflowView.TradeSelection with { Result = envelope }, SelectionDispatch = selection,
            CompositionHandoff = pending with { Status = CompositionHandoffStatus.Reserved, Reservation = reservation } };
        var binding = CompositionBindingResolver.Resolve(result, selection.SelectionBinding, at);
        var snapshot = Snapshot(binding, new(at));
        if (integrationTiming)
        {
            snapshot = snapshot with { ValidUntilUtc = new DateTimeOffset(at.AddSeconds(5)), Digest = "",
                Instruments = snapshot.Instruments.Select(x => x.Instrument.Pricing is null ? x : x with { Instrument = x.Instrument with
                { Pricing = x.Instrument.Pricing with { ValidUntilUtc = new DateTimeOffset(at.AddSeconds(5)), MaximumQuoteAgeMilliseconds = 5000 } } }).ToImmutableArray() };
            snapshot = snapshot with { Digest = CompositionSemanticHash.Compute(snapshot) };
        }
        var id = new OrderCompositionExecutionId(selection.WorkflowEntityId, selection.WorkflowId, 5);
        var request = new ExecuteOrderCompositionPipelineCommand
        {
            SchemaVersion = 1, CommandId = Guid.NewGuid(), Subject = new(ActorType.Function, ExecuteOrderCompositionPipelineCommand.Actor,
                ExecuteOrderCompositionPipelineCommand.Verb, id.Format()), EntityId = id, InputWorkflowRevision = 5, WorkflowView = view,
            TriggerEvent = view.TriggerEvent, CorrelationId = view.CorrelationId, CausationId = Guid.NewGuid(), RequestedAtUtc = at,
            EvaluatedAtUtc = at, ExpiresAtUtc = integrationTiming ? at.AddSeconds(4) : at.AddMilliseconds(900), AcceptedSelectionEnvelope = envelope,
            SelectionBinding = selection.SelectionBinding, Reservation = reservation, CompositionBinding = binding, MarketSnapshot = snapshot
        };
        if(actualSelectionResult is not null)
            request=request with { ExpiresAtUtc=new[] { request.ExpiresAtUtc,binding.ValidUntilUtc,selection.SelectionBinding.ValidUntilUtc,reservation.Order.ExpiresAtUtc }.Min() };
        return Seal(request);
    }
    public static ExecuteOrderCompositionPipelineCommand Seal(ExecuteOrderCompositionPipelineCommand c) => c with { InputSha256 = c.Fingerprint() };
    public static MarketCompositionSnapshot Snapshot(CompositionBinding binding, DateTimeOffset at)
    {
        var generation = Guid.NewGuid();
        var forward = new OptionPricingQuote("ESZ6", 5002.25m, 5002.75m, 100, 100, at, at, 1, generation);
        var items = ImmutableArray.CreateBuilder<CompositionInstrumentSnapshot>();
        if (binding.BuilderCode == "Future")
            items.Add(new(new("ESZ6", forward, null, null, null, null,
                new("ESZ6", "ES", "GLBX.MDP3", "CME", "USD", at.AddDays(120), 50m, .25m, new('a', 64))), null));
        else
        {
            var days = (int)binding.Rules.VariantRules[0].BaseParameters.TargetDaysToExpiry;
            var expiry = at.AddDays(days); var begin = DateOnly.FromDateTime(at.UtcDateTime).AddDays(-2);
            // A complete explicit fixture calendar. It is not published exchange reference data.
            var dates = Enumerable.Range(0, 130).Select(begin.AddDays).ToImmutableArray();
            var calendar = new OptionPricingCalendar("fixture/v1", "America/New_York", begin, begin.AddDays(129), new(18, 0), dates);
            double rate = 2 * double.LogP1(.04 / 2);
            var treasury = new TreasuryContinuousRate(days < 30 ? TreasuryTenor.OneMonth : days < 60 ? TreasuryTenor.TwoMonth : TreasuryTenor.ThreeMonth,
                4m, rate, DateOnly.FromDateTime(at.UtcDateTime), at, new('b', 64),
                new("USTreasury", "daily-cmt", TreasuryRateConvention.UsTreasuryCmtNominalSemiannual, "fixture/v1", "fixture"), "FlatSelectedCmtProxy/v1");
            for (int k = 4930; k <= 5070; k += 5)
            foreach (bool call in new[] { false, true })
            {
                var name = $"ES.{k}.{call}";
                var contract = new OptionPricingConvention { ContractId = name, Dataset = "GLBX.MDP3", PublisherId = 1,
                    InstrumentId = (uint)(k * 2 + (call ? 1 : 0)), RawSymbol = name, Root = "ES", Exchange = "CME", Currency = "USD",
                    UnderlyingContractId = "ESZ6", ExerciseStyle = OptionExerciseStyle.European, SettlementStyle = OptionSettlementStyle.DeliveryOfFuture,
                    ExpirationUtc = expiry, LastTradingUtc = expiry, DayCount = PricingDayCount.Actual365Fixed, CalendarVersion = calendar.Version,
                    Multiplier = 50, TickSize = .05m, TickRuleVersion = "fixture-fixed/v1", DefinitionDigest = new('c', 64), MappingVersion = "fixture/v1",
                    EvidenceId = "fixture", EffectiveFromUtc = at.AddDays(-1), EffectiveUntilUtc = expiry };
                // Fixed forward standard deviation across horizons provides comparable economic fixtures.
                double vol = .0099 * Math.Sqrt(30d / days);
                var mark = (decimal)OptionModel.Price(5002.5, k, rate, vol, days / 365d, call ? 1 : -1);
                var spread = Math.Min(.02m, mark / 4);
                var quote = forward with { ContractId = name, Bid = mark - spread, Ask = mark + spread };
                items.Add(new(new(name, quote, new(contract, calendar, treasury, at.AddSeconds(1), generation,
                    OptionCalculator.EngineVersion, 1000, 250, "fixture-publication/v1"), k, call, forward), null));
            }
        }
        var snapshot = new MarketCompositionSnapshot(1, Guid.NewGuid(), new('d', 64), "complete-fixture/v1", binding.Rules.SupportedHorizon.ToString(),
            generation, at, at.AddSeconds(1), items.OrderBy(x => x.Instrument.ContractId, StringComparer.Ordinal).ToImmutableArray(), "");
        return snapshot with { Digest = CompositionSemanticHash.Compute(snapshot) };
    }
}
