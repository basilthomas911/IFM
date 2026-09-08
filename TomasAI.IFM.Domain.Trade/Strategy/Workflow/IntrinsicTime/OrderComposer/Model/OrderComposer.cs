using System.Collections.Immutable;
using System.Globalization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.CompositionRulesContract;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

public interface IOrderComposer
{
    OrderCompositionResult Calculate(ExecuteOrderCompositionPipelineCommand request, CancellationToken token = default);
}

/// <summary>Enumerates the complete bounded scope and ranks valid one-unit constructions deterministically.</summary>
public sealed class OrderComposer(IFuturesOptionComposerPricer pricer) : IOrderComposer
{
    public OrderCompositionResult Calculate(ExecuteOrderCompositionPipelineCommand c, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var binding = c.CompositionBinding;
        var intent = binding.Selected;
        var selected = TradeSelectionContracts.ReadResult(c.AcceptedSelectionEnvelope);
        var rule = binding.Rules.VariantRules.Single(x => x.VariantKey == intent.VariantKey);
        Require(binding.Rules.AlgorithmVersion == AlgorithmVersion && binding.Rules.PricerVersion == pricer.Version,
            "OC.CONFIG.CAPABILITY_UNSUPPORTED");
        var snapshot = c.MarketSnapshot;
        ValidateSnapshot(snapshot, c, rule.BaseParameters);
        var valuations = new Dictionary<string, CompositionValuation>(StringComparer.Ordinal);
        // Every required instrument is priced before ranking. A solver failure cannot silently reduce scope.
        foreach (var item in snapshot.Instruments.Where(x => x.Instrument.Pricing is not null))
        {
            token.ThrowIfCancellationRequested();
            valuations.Add(item.Instrument.ContractId, pricer.Calculate(item.Instrument, snapshot.EvaluatedAtUtc));
        }
        var features = new Dictionary<CompositionFeature, decimal>
        {
            [CompositionFeature.SelectionConfidence] = selected.SelectionConfidence,
            [CompositionFeature.RegimeConfidence] = selected.DecisionContext.RegimeResultEnvelope.ReadRegimeResult().Decision.Confidence
        };
        var forwards = snapshot.Instruments.Select(x => x.Instrument.Underlying ?? x.Instrument.Quote)
            .GroupBy(x => x.ContractId, StringComparer.Ordinal).ToArray();
        if (forwards.Length == 1) features[CompositionFeature.ForwardPrice] = (forwards[0].First().Bid + forwards[0].First().Ask) / 2;
        if (valuations.Count > 0) features[CompositionFeature.ImpliedVolatility] = valuations.Values.Average(x => x.ImpliedVolatility);
        var resolved = CompositionParameterResolver.Resolve(rule, features, token);
        var p = resolved.Values;
        ValidateSnapshot(snapshot, c, p);
        CompositionBindingResolver.ValidateResolved(c, p, rule);
        var session = selected.DecisionContext.AssessmentResultEnvelope.ReadAssessmentResult().Assessment.SessionState;
        Require(session != Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model.MarketSessionStatus.Unknown, "OC.MARKET.REFERENCE_UNAVAILABLE");
        bool closed = session == Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model.MarketSessionStatus.Closed;
        var rejected = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int generated = 0, eligible = 0;
        CompositionCandidate? best = null;
        CompositionRanking? bestRank = null;
        var collections = closed ? Enumerable.Empty<(CompositionInstrumentSnapshot Instrument, int Sign)[]>() : binding.BuilderCode == "Future"
            ? EsFuturesOrderComposer.Enumerate(snapshot, p, intent.Side)
            : OptionCandidates(snapshot, binding.BuilderCode, intent.Side, rule, token);
        foreach (var legs in collections)
        {
            token.ThrowIfCancellationRequested();
            Require(++generated <= 4096, "OC.CALCULATION.LIMIT");
            var (candidate, ranking, reason) = Build(c, resolved, rule, legs, valuations);
            if (candidate is null)
            {
                rejected[reason!] = rejected.GetValueOrDefault(reason!) + 1;
                continue;
            }
            eligible++;
            if (bestRank is null || Compare(ranking!, bestRank) < 0) { best = candidate; bestRank = ranking; }
        }
        var reasons = best is null ? (generated == 0 ? ImmutableArray.Create(closed ? "OC.MARKET.SESSION_CLOSED" : "OC.CANDIDATE.NO_CONTRACT") : rejected.Keys.ToImmutableArray())
            : ImmutableArray.Create("OC.COMPOSED");
        return new()
        {
            ResultId = c.CommandId, WorkflowId = c.WorkflowId, EntityId = c.WorkflowEntityId, InvocationId = c.CommandId,
            InputWorkflowRevision = c.InputWorkflowRevision, InputSha256 = c.InputSha256, EvaluatedAtUtc = c.EvaluatedAtUtc,
            ProducedAtUtc = c.EvaluatedAtUtc, TargetHorizon = binding.Rules.SupportedHorizon,
            Outcome = best is null ? CompositionOutcome.NoCandidate : CompositionOutcome.Composed, Candidate = best,
            DecisionContext = new() { SelectionResultId = selected.ResultId, SelectionResultHash = c.AcceptedSelectionEnvelope.PayloadSha256,
                InputHash = c.InputSha256, BindingHash = binding.BindingSha256, SnapshotHash = snapshot.Digest,
                ValueDate = c.SelectionBinding.RequestedTradeDate, PortfolioId = selected.PortfolioId, FundId = selected.FundId, PricerVersion = pricer.Version, AlgorithmVersion = AlgorithmVersion },
            ResolvedParameters = resolved, CandidateCounts = new() { Generated = generated, Eligible = eligible, Rejected = generated - eligible },
            CandidateDiagnostics = rejected.Select(x => new CompositionRejection { ReasonCode = x.Key, Count = x.Value }).ToImmutableArray(),
            Reasons = reasons, ValidUntilUtc = best?.ValidUntilUtc, SummaryText = best is null ? "No eligible construction." : "One unapproved strategy unit.",
            Ranking = bestRank
        };
    }

    static IEnumerable<(CompositionInstrumentSnapshot Instrument, int Sign)[]> OptionCandidates(
        MarketCompositionSnapshot snapshot, string builder, string side, CompositionVariantRules rule, CancellationToken token)
    {
        foreach (var group in snapshot.Instruments.Where(x => x.Instrument.Pricing is not null)
            .GroupBy(x => (x.Instrument.Pricing!.Contract.ExpirationUtc, x.Instrument.Pricing.Contract.UnderlyingContractId))
            .OrderBy(x => x.Key.ExpirationUtc).ThenBy(x => x.Key.UnderlyingContractId, StringComparer.Ordinal))
        {
            var options = group.ToArray();
            var candidates = builder switch
            {
                "CallVertical" => EsVerticalSpreadOrderComposer.Enumerate(options, true, side, rule, token),
                "PutVertical" => EsVerticalSpreadOrderComposer.Enumerate(options, false, side, rule, token),
                "IronCondor" => EsIronCondorOrderComposer.Enumerate(options, side, rule, token),
                _ => throw new CompositionException("OC.CONFIG.UNSUPPORTED_STRUCTURE")
            };
            foreach (var candidate in candidates) yield return candidate;
        }
    }

    (CompositionCandidate? Candidate, CompositionRanking? Ranking, string? Reason) Build(
        ExecuteOrderCompositionPipelineCommand c, CompositionResolvedParameters resolved, CompositionVariantRules rule,
        (CompositionInstrumentSnapshot Instrument, int Sign)[] input, Dictionary<string, CompositionValuation> values)
    {
        static (CompositionCandidate?, CompositionRanking?, string) Reject(string code) => (null, null, "OC.CANDIDATE." + code);
        var p = resolved.Values; var intent = c.CompositionBinding.Selected;
        bool option = input[0].Instrument.Instrument.Pricing is not null;
        var instruments = input.Select(x => x.Instrument.Instrument).ToArray();
        var expiration = option ? instruments[0].Pricing!.Contract.ExpirationUtc.UtcDateTime : instruments[0].FutureDefinition!.LastTradingUtc.UtcDateTime;
        var dte = (decimal)(expiration - c.EvaluatedAtUtc).TotalDays;
        if (option && (dte < p.MinimumDaysToExpiry || dte > p.MaximumDaysToExpiry)) return Reject("NO_EXPIRY");
        decimal multiplier = option ? instruments[0].Pricing!.Contract.Multiplier : instruments[0].FutureDefinition!.Multiplier;
        if (option)
        {
            var first = instruments[0].Pricing!.Contract;
            Require(instruments.All(x => x.Pricing!.Contract.Multiplier == multiplier && x.Pricing.Contract.SettlementStyle == first.SettlementStyle
                && x.Pricing.Contract.TickRuleVersion == first.TickRuleVersion && x.Pricing.Contract.Currency == first.Currency), "OC.CONTRACT.VALUE_RANGE");
            if (instruments.Any(x => Math.Abs(values[x.ContractId].Delta) > 1 || (x.IsCall == true ? values[x.ContractId].Delta < 0 : values[x.ContractId].Delta > 0)))
                throw new CompositionException("OC.PRICING.GREEKS_CALCULATION_FAILED");
        }
        var natural = input.Sum(x => x.Sign * (x.Sign > 0 ? x.Instrument.Instrument.Quote.Ask : x.Instrument.Instrument.Quote.Bid));
        var best = input.Sum(x => x.Sign * (x.Sign > 0 ? x.Instrument.Instrument.Quote.Bid : x.Instrument.Instrument.Quote.Ask));
        var mid = (natural + best) / 2;
        var raw = mid + p.MidpointToNaturalFraction * (natural - mid);
        var tick = pricer.Tick(instruments[0], raw, false);
        decimal limit = decimal.Floor(raw / tick) * tick;
        // Resolve the resulting premium band again so a boundary crossing never leaves an off-grid price.
        tick = pricer.Tick(instruments[0], limit, false);
        limit = decimal.Floor(limit / tick) * tick;
        decimal worst = limit + p.MaximumAdverseMoveTicks * tick;
        var worstTick = pricer.Tick(instruments[0], worst, false);
        worst = decimal.Floor(worst / worstTick) * worstTick;
        if (worst > natural || natural - best > p.MaximumComboSpreadTicks * tick) return Reject("PREMIUM");
        var ticks = instruments.Select(x => pricer.Tick(x, (x.Quote.Bid + x.Quote.Ask) / 2, true)).ToArray();
        if (instruments.Where((x, i) => x.Quote.Ask - x.Quote.Bid > (option ? p.MaximumLegSpreadTicks : p.MaximumUnderlyingSpreadTicks) * ticks[i]).Any()) return Reject("LIQUIDITY");
        // V1 admits reviewed full-size ES only; its futures price increment is 0.25 index points.
        if (option && instruments.Any(x => x.Underlying!.Ask - x.Underlying.Bid > p.MaximumUnderlyingSpreadTicks * .25m)) return Reject("LIQUIDITY");
        var minSize = input.Min(x => x.Sign > 0 ? x.Instrument.Instrument.Quote.AskSize : x.Instrument.Instrument.Quote.BidSize);
        var capacity = decimal.Floor(minSize * p.ParticipationFraction);
        if (minSize < p.MinimumDisplayedSize || capacity < 1) return Reject("LIQUIDITY");
        int units = (int)Math.Min(capacity, int.MaxValue);
        decimal cost = input.Length * p.FeePerContract + ticks.Sum(x => x * p.SlippageTicksPerLeg * multiplier);
        var delta = option ? input.Sum(x => x.Sign * values[x.Instrument.Instrument.ContractId].Delta) : input[0].Sign;
        if (Math.Abs(delta - p.TargetNetDelta) > p.BalanceTolerance || intent.Bias == "Bullish" && delta <= 0
            || intent.Bias == "Bearish" && delta >= 0 || intent.Bias == "Balanced" && p.TargetNetDelta != 0) return Reject("DELTA");
        decimal legError = 0;
        if (option)
        {
            if (input.Length == 2)
            {
                var target = input.Single(x => x.Sign == (intent.PremiumMode == "Debit" ? 1 : -1));
                legError = Math.Abs(Math.Abs(values[target.Instrument.Instrument.ContractId].Delta) - p.TargetLegDelta);
                if (legError > p.LegDeltaTolerance) return Reject("DELTA");
            }
            else
            {
                var putError = Math.Abs(Math.Abs(values[instruments[1].ContractId].Delta) - p.TargetPutDelta);
                var callError = Math.Abs(Math.Abs(values[instruments[2].ContractId].Delta) - p.TargetCallDelta);
                if (putError > p.LegDeltaTolerance || callError > p.LegDeltaTolerance) return Reject("DELTA");
                legError = putError + callError;
            }
            var width = input.Length == 2 ? instruments[1].Strike!.Value - instruments[0].Strike!.Value
                : Math.Min(instruments[1].Strike!.Value - instruments[0].Strike!.Value, instruments[3].Strike!.Value - instruments[2].Strike!.Value);
            if (intent.PremiumMode == "Credit" ? limit >= 0 || worst >= 0 || -worst < p.MinimumCreditTicks * tick || -worst >= width || -worst / width < p.MinimumCreditToWidth
                : limit <= 0 || worst <= 0 || worst >= width || worst / width > p.MaximumDebitToWidth) return Reject("PREMIUM");
        }
        var risk = option ? Payoff(input.Select((x, i) => (instruments[i].Strike!.Value, instruments[i].IsCall!.Value, x.Sign)).ToArray(), worst, multiplier, cost)
            : new CompositionRisk { RiskBound = "Unbounded", Notional = Math.Abs(limit) * multiplier,
                PlannedLoss = p.FuturesPlannedDistance * multiplier + cost, StressLoss = p.FuturesStressDistance * multiplier + cost };
        if (option && (risk.MaximumLoss <= 0 || risk.MaximumProfit <= 0 || risk.PayoffRewardToRisk < p.MinimumRewardToRisk)) return Reject("PAYOFF");
        var valid = new[] { c.ExpiresAtUtc, c.SelectionBinding.ValidUntilUtc, c.CompositionBinding.ValidUntilUtc,
            c.Reservation.Order.ExpiresAtUtc, c.MarketSnapshot.ValidUntilUtc.UtcDateTime,
            c.EvaluatedAtUtc.AddMilliseconds(p.CandidateLifetimeMilliseconds), expiration,
            instruments.Min(x => x.Pricing?.Contract.LastTradingUtc.UtcDateTime ?? x.FutureDefinition!.LastTradingUtc.UtcDateTime),
            instruments.Min(x => x.Quote.EventAtUtc.UtcDateTime.AddMilliseconds(p.MaximumQuoteAgeMilliseconds)),
            instruments.Min(x => (x.Underlying ?? x.Quote).EventAtUtc.UtcDateTime.AddMilliseconds(p.MaximumQuoteAgeMilliseconds)),
            instruments.Min(x => x.Pricing?.ValidUntilUtc.UtcDateTime ?? DateTime.MaxValue) }.Min();
        if (valid <= c.EvaluatedAtUtc) return Reject("NO_VALIDITY_REMAINING");
        var legs = input.Select((x, i) => new CompositionLeg
        {
            InstrumentId = instruments[i].ContractId, RawSymbol = instruments[i].Pricing?.Contract.RawSymbol ?? instruments[i].ContractId,
            UnderlyingInstrumentId = instruments[i].Pricing?.Contract.UnderlyingContractId ?? instruments[i].ContractId,
            InstrumentClass = option ? "FuturesOption" : "Futures", Side = x.Sign > 0 ? "Buy" : "Sell", Ratio = 1,
            Right = instruments[i].IsCall, Strike = instruments[i].Strike, ExpirationUtc = expiration, Multiplier = multiplier,
            TickRuleId = instruments[i].Pricing?.Contract.TickRuleVersion ?? instruments[i].FutureDefinition!.DefinitionDigest,
            Quote = instruments[i].Quote, Valuation = option ? values[instruments[i].ContractId] : null,
            DefinitionHash = instruments[i].Pricing?.Contract.DefinitionDigest ?? instruments[i].FutureDefinition!.DefinitionDigest
        }).ToImmutableArray();
        var candidate = new CompositionCandidate
        {
            CandidateId = c.CommandId, OrderId = c.Reservation.Order.OrderId, PrimaryTradeId = c.Reservation.Trades.Single().TradeId,
            PortfolioId = c.Reservation.Order.PortfolioId, FundId = c.Reservation.Order.FundId, AssignmentVersion = intent.AssignmentVersion,
            DeploymentKey = intent.DeploymentKey, StrategyKey = intent.StrategyKey, StructureKey = intent.StructureKey, VariantKey = intent.VariantKey,
            Product = intent.Product, TargetHorizon = c.CompositionBinding.Rules.SupportedHorizon, Side = intent.Side, Bias = intent.Bias,
            PremiumMode = intent.PremiumMode, Legs = legs, LiquidityCapacityUnits = units,
            Pricing = new() { NaturalDebit = natural, BestDebit = best, MidDebit = mid, LimitDebit = limit, WorstDebit = worst,
                ComboTick = tick, ComboSpread = natural - best, CostReserve = cost },
            Greeks = new() { Delta = delta, Gamma = option ? input.Sum(x => x.Sign * values[x.Instrument.Instrument.ContractId].Gamma) : 0,
                Theta = option ? input.Sum(x => x.Sign * values[x.Instrument.Instrument.ContractId].Theta) : 0,
                Vega = option ? input.Sum(x => x.Sign * values[x.Instrument.Instrument.ContractId].Vega) : 0,
                Rho = option ? input.Sum(x => x.Sign * values[x.Instrument.Instrument.ContractId].Rho) : 0 },
            RiskEvidence = risk, ExecutionEnvelope = new() { Atomic = option, ProposedSignedDebit = limit, WorstSignedDebit = worst,
                Tick = tick, TickRuleVersion = legs[0].TickRuleId, ValidUntilUtc = valid }, ParameterResolutionHash = resolved.Hash,
            SnapshotHash = c.MarketSnapshot.Digest, BindingHash = c.CompositionBinding.BindingSha256, PricerVersion = pricer.Version,
            EvaluatedAtUtc = c.EvaluatedAtUtc, ValidUntilUtc = valid
        };
        candidate = candidate with { CandidateHash = CompositionHash.Candidate(candidate) };
        var key = string.Join("|", legs.Select(x => string.Create(CultureInfo.InvariantCulture,
            $"{x.ExpirationUtc.Ticks:D19}:{x.UnderlyingInstrumentId}:{x.Right}:{x.Strike:00000000000000000000000000000.0000000000000000000000000000}:{x.InstrumentId}:{x.Side}:{x.Ratio}")));
        var ranking = new CompositionRanking { DteDistance = option ? Math.Abs(dte - p.TargetDaysToExpiry) : 0,
            DeltaDistance = Math.Abs(delta - p.TargetNetDelta), LegDeltaDistance = legError, SpreadTicks = (natural - best) / tick,
            RewardToRisk = risk.PayoffRewardToRisk ?? 0, CanonicalKey = key };
        return (candidate, ranking, null);
    }

    /// <summary>Evaluates every payoff breakpoint and asymptotic slope, including unequal condor wings.</summary>
    public static CompositionRisk Payoff((decimal Strike, bool Call, int Sign)[] legs, decimal premium, decimal multiplier, decimal costs)
    {
        Require(legs.Length is 2 or 4 && legs.All(x => x.Strike > 0 && x.Sign is 1 or -1), "OC.CONTRACT.VALUE_RANGE");
        Require(legs.Where(x => x.Call).Sum(x => x.Sign) == 0 && legs.Where(x => !x.Call).Sum(x => x.Sign) == 0, "OC.CONTRACT.UNBOUNDED_TOPOLOGY");
        var values = legs.Select(x => x.Strike).Append(0m).Distinct().Select(f => multiplier *
            (legs.Sum(x => x.Sign * Math.Max(0, x.Call ? f - x.Strike : x.Strike - f)) - premium)).ToArray();
        var loss = Math.Max(0, -values.Min()) + costs; var profit = values.Max() - costs;
        return new() { RiskBound = "Bounded", MaximumLoss = loss, MaximumProfit = profit, PayoffRewardToRisk = loss > 0 ? profit / loss : null };
    }

    static int Compare(CompositionRanking x, CompositionRanking y)
    {
        foreach (var pair in new[] { (x.DteDistance, y.DteDistance), (x.DeltaDistance, y.DeltaDistance),
            (x.LegDeltaDistance, y.LegDeltaDistance), (x.SpreadTicks, y.SpreadTicks), (-x.RewardToRisk, -y.RewardToRisk) })
        { int c = pair.Item1.CompareTo(pair.Item2); if (c != 0) return c; }
        return StringComparer.Ordinal.Compare(x.CanonicalKey, y.CanonicalKey);
    }

    static void ValidateSnapshot(MarketCompositionSnapshot s, ExecuteOrderCompositionPipelineCommand c, CompositionParameters p)
    {
        Require(s.SchemaVersion == 1 && s.SnapshotId != Guid.Empty && s.GenerationId != Guid.Empty && !string.IsNullOrEmpty(s.ScopeToken)
            && !s.Instruments.IsDefault && s.Instruments.Length <= 512 && s.EvaluatedAtUtc.UtcDateTime == c.EvaluatedAtUtc
            && s.Horizon == c.CompositionBinding.Rules.SupportedHorizon.ToString() && s.ValidUntilUtc >= s.EvaluatedAtUtc,
            "OC.MARKET.INCOMPLETE");
        Require(s.Instruments.Select(x => x.Instrument.ContractId).Distinct(StringComparer.Ordinal).Count() == s.Instruments.Length,
            "OC.CONTRACT.IDENTITY");
        Require(s.Instruments.Select(x => x.Instrument.Pricing?.Contract.ExpirationUtc).Where(x => x is not null).Distinct().Count() <= 8
            && s.Instruments.Count(x => x.Instrument.FutureDefinition is not null) <= 16, "OC.SNAPSHOT.LIMIT");
        var forwards = s.Instruments.Where(x => x.Instrument.Underlying is not null).Select(x => x.Instrument.Underlying!).GroupBy(x => x.ContractId, StringComparer.Ordinal);
        Require(forwards.All(x => x.Distinct().Count() == 1), "OC.MARKET.INCOHERENT_FORWARD");
        var times = new List<DateTimeOffset>();
        foreach (var item in s.Instruments)
        {
            var i = item.Instrument;
            Require(i.ContractId == i.Quote.ContractId, "OC.CONTRACT.IDENTITY");
            if (i.Pricing is { } pricing)
                Require(pricing.Contract.ExerciseStyle == OptionExerciseStyle.European && pricing.Contract.Root == "ES"
                    && pricing.Contract.Multiplier == 50m && pricing.Contract.ContractId == i.ContractId
                    && pricing.Contract.Currency == c.CompositionBinding.Selected.Product.Currency
                    && pricing.Contract.Exchange == c.CompositionBinding.Selected.Product.Exchange
                    && pricing.Contract.UnderlyingContractId == i.Underlying?.ContractId && i.Strike > 0 && i.IsCall is not null,
                    "OC.CONTRACT.REQUIRED_FIELD");
            else Require(i.FutureDefinition is { Root: "ES", Currency: "USD", Multiplier: 50m, TickSize: .25m }
                && i.FutureDefinition.ContractId == i.ContractId && i.Strike is null && i.IsCall is null && i.FutureDefinition.Exchange == c.CompositionBinding.Selected.Product.Exchange,
                "OC.CONTRACT.REQUIRED_FIELD");
            foreach (var q in i.Underlying is null ? new[] { i.Quote } : new[] { i.Quote, i.Underlying })
            {
                Require(q.GenerationId == s.GenerationId && q.Sequence >= 0, "OC.MARKET.RECOVERING");
                Require(q.Bid > 0 && q.Ask >= q.Bid && q.BidSize >= 0 && q.AskSize >= 0
                    && q.EventAtUtc.Offset == TimeSpan.Zero && q.ReceivedAtUtc.Offset == TimeSpan.Zero
                    && q.EventAtUtc <= s.EvaluatedAtUtc && q.ReceivedAtUtc <= s.EvaluatedAtUtc, "OC.MARKET.INVALID_QUOTE");
                Require((s.EvaluatedAtUtc - q.EventAtUtc).TotalMilliseconds <= p.MaximumQuoteAgeMilliseconds, "OC.MARKET.STALE");
                times.Add(q.EventAtUtc);
            }
        }
        Require(times.Count == 0 || (times.Max() - times.Min()).TotalMilliseconds <= p.MaximumQuoteSkewMilliseconds, "OC.MARKET.SKEW");
    }
}
