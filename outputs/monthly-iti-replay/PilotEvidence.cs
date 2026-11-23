using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using System.Globalization;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

internal static class PilotEvidence
{
    public static async Task RunAsync(string barsPath, string referencePath, string outputPath)
    {
        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        var trigger = reference.RootElement.GetProperty("trigger");
        var contract = trigger.GetProperty("contract").GetString()!;
        var triggerAvailableNs = trigger.GetProperty("available_at_ns").GetInt64();
        var scheduled = reference.RootElement.TryGetProperty("evaluation_at_ns", out var evaluationMark);
        var evaluationNs = scheduled ? evaluationMark.GetInt64() : triggerAvailableNs;
        if (triggerAvailableNs > evaluationNs)
            throw new InvalidDataException("The Monthly ITI state was unavailable at the evaluation cutoff.");
        var evaluation = DateTimeOffset.UnixEpoch.AddTicks(evaluationNs / 100);
        var series = MarketSeriesIdentity.ForContract(contract);
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider(new FixedClock(evaluation));
        provider.Clear();
        ResetReplayCaches();
        var checkpoints = new Dictionary<TimeFrameType, FuturesEmaAccumulatorCheckpoint>();
        var results = new Dictionary<TimeFrameType, FuturesEmaSignalReadModel>();
        var bands = new Dictionary<TimeFrameType, FuturesBbAccumulatorCheckpoint>();
        var atrs = new Dictionary<TimeFrameType, FuturesAtrAccumulatorCheckpoint>();
        var rsis = new Dictionary<TimeFrameType, FuturesRsiAccumulatorCheckpoint>();
        var macds = new Dictionary<TimeFrameType, FuturesMacdSignalReadModel>();
        var adxHistory = new Dictionary<TimeFrameType, List<FuturesAdxSignalReadModel>>();
        var structureBars = new Dictionary<TimeFrameType, Queue<FuturesTradeSessionBarReadModel>>();
        var tdi = new FuturesTdiSignalRealtimeState();
        var tdiCheckpoints = new Dictionary<TimeFrameType, FuturesRsiAccumulatorCheckpoint>();
        var tdiWindows = new Dictionary<TimeFrameType, Queue<FuturesRsiSignalReadModel>>();
        var tdiConfiguration = FuturesTdiConfiguration.Standard;
        long sequence = 0;
        foreach (var line in File.ReadLines(barsPath))
        {
            var fields = line.Split(',');
            var frame = Enum.Parse<TimeFrameType>(fields[0]);
            var end = DateTimeOffset.Parse(fields[3], CultureInfo.InvariantCulture);
            if (end > evaluation)
                throw new InvalidDataException("Bar ends after the pilot decision.");
            var close = decimal.Parse(fields[7], CultureInfo.InvariantCulture);
            var observation = new FuturesTradeSessionBarReadModel
            {
                MarketSeriesIdentity = series,
                ContractId = contract,
                TimeFrame = frame,
                ValueDate = DateOnly.ParseExact(fields[1], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                IntervalStartUtc = DateTimeOffset.Parse(fields[2], CultureInfo.InvariantCulture),
                IntervalEndUtc = end,
                LastMarketEventUtc = end,
                CalculatedAtUtc = end,
                Open = decimal.Parse(fields[4], CultureInfo.InvariantCulture),
                High = decimal.Parse(fields[5], CultureInfo.InvariantCulture),
                Low = decimal.Parse(fields[6], CultureInfo.InvariantCulture),
                Close = close,
                Volume = decimal.Parse(fields[8], CultureInfo.InvariantCulture),
                LastSourceSequence = ++sequence,
                IsComplete = true,
                IsValid = true
            };
            checkpoints.TryGetValue(frame, out var previous);
            var advanced = FuturesEmaAccumulator.Apply(previous, observation);
            checkpoints[frame] = advanced.FuturesEmaCheckpoint;
            var ema = advanced.FuturesEmaSignal ?? throw new InvalidDataException("EMA bar did not advance.");
            results[frame] = ema;
            RegimeDiscoverySignalCacheAdapter.Publish(ema, advanced.FuturesEmaCheckpoint);

            var bb = FuturesBbAccumulator.Apply(bands.GetValueOrDefault(frame), observation, ema);
            bands[frame] = bb.FuturesBbCheckpoint;
            if (bb.FuturesBbSignal is { } band)
                RegimeDiscoverySignalCacheAdapter.Publish(band, bb.FuturesBbCheckpoint);

            if (FuturesAtrWilderAccumulator.TryApply(observation, 14, atrs.GetValueOrDefault(frame), out var atr))
            {
                atrs[frame] = atr.Checkpoint;
                if (atr.AtrValue is { } atrValue)
                    RegimeDiscoverySignalCacheAdapter.Publish(new FuturesAtrSignalReadModel
                    {
                        ContractId = contract, ValueDate = observation.ValueDate, TimePeriod = frame,
                        PeriodLength = 14, AtrValue = (double)atrValue, AtrRatio = (double?)atr.AtrRatio, IsWarm = atr.IsWarm,
                        Metadata = MarketAnalyticsSignalMetadataFactory.Create(observation, MarketAnalyticsSignalKind.Atr, "atr-v1", "atr-v1")
                    });
            }
            var rsi = FuturesRsiWilderAccumulator.Apply(rsis.GetValueOrDefault(frame), observation, 14);
            rsis[frame] = rsi.Checkpoint;
            if (rsi.Checkpoint.CurrentRsi is { } rsiValue)
                RegimeDiscoverySignalCacheAdapter.Publish(new FuturesRsiSignalReadModel
                {
                    ContractId = contract, ValueDate = observation.ValueDate, TimePeriod = frame,
                    PeriodLength = 14, RSI = rsiValue, RSISlope = rsi.Slope ?? 0, IsWarm = rsi.IsWarm,
                    Metadata = MarketAnalyticsSignalMetadataFactory.Create(observation, MarketAnalyticsSignalKind.Rsi, "rsi-v1", "rsi-v1")
                });

            if (FuturesTdiConfiguration.IsSupportedIntraday(frame))
            {
                var tdiRsi = FuturesRsiWilderAccumulator.Apply(tdiCheckpoints.GetValueOrDefault(frame),
                    observation, tdiConfiguration.RsiPeriod);
                tdiCheckpoints[frame] = tdiRsi.Checkpoint;
                if (tdiRsi.Checkpoint.CurrentRsi is { } tdiValue)
                {
                    if (!tdiWindows.TryGetValue(frame, out var samples))
                        tdiWindows[frame] = samples = new();
                    samples.Enqueue(new FuturesRsiSignalReadModel
                    {
                        ContractId = contract, ValueDate = observation.ValueDate, TimePeriod = frame,
                        PeriodLength = tdiConfiguration.RsiPeriod, RSI = tdiValue, Price = close,
                        SourceEventTimestamp = end.UtcDateTime, SourceSequence = sequence,
                        Timestamp = TimeOnly.FromDateTime(end.UtcDateTime), IsWarm = tdiRsi.IsWarm
                    });
                    if (samples.Count > tdiConfiguration.RequiredRsiSamples)
                        samples.Dequeue();
                    if (samples.Count == tdiConfiguration.RequiredRsiSamples
                        && tdi.TryEvaluate(new FuturesRsiSignalsGeneratedEvent { CommandId = Guid.NewGuid() },
                            samples.ToArray(), tdiConfiguration, out var calculatedTdi))
                    {
                        tdi.Confirm(calculatedTdi);
                        RegimeDiscoverySignalCacheAdapter.Publish(calculatedTdi.Signal);
                    }
                }
            }

            FuturesMacdSignalCompute.Create(close, macds.TryGetValue(frame, out var priorMacd) ? [priorMacd] : [],
                FuturesMacdConfiguration.Standard, out var macd);
            var macdSignal = new FuturesMacdSignalReadModel
            {
                ContractId = contract, ValueDate = observation.ValueDate, TimePeriod = frame,
                FuturesPrice = close, FastEma = macd.FastEma, SlowEma = macd.SlowEma,
                MacdLine = macd.MacdLine, SignalLine = macd.SignalLine, ObservationCount = macd.ObservationCount,
                IsWarm = macd.IsWarm,
                Metadata = MarketAnalyticsSignalMetadataFactory.Create(observation, MarketAnalyticsSignalKind.Macd, "macd-v1", "macd-v1")
            };
            macds[frame] = macdSignal;
            if (macd.IsWarm)
                RegimeDiscoverySignalCacheAdapter.Publish(macdSignal);

            if (!adxHistory.TryGetValue(frame, out var history))
                adxHistory[frame] = history = [];
            var adxBar = new FuturesAdxSignalReadModel
            {
                ContractId = contract, ValueDate = observation.ValueDate, TimePeriod = frame, PeriodLength = 14,
                FuturesPrice = close,
                Metadata = MarketAnalyticsSignalMetadataFactory.Create(observation, MarketAnalyticsSignalKind.Adx, "adx-v1", "adx-v1")
            };
            history.Add(adxBar);
            FuturesAdxSignalCompute.Create(14, null, history, out var adx);
            if (adx.IsWarm)
                RegimeDiscoverySignalCacheAdapter.Publish(adxBar with
                {
                    PlusDI = adx.PlusDI, MinusDI = adx.MinusDI, AdxValue = adx.AdxValue, IsWarm = true
                });
            if (!structureBars.TryGetValue(frame, out var window))
                structureBars[frame] = window = new();
            window.Enqueue(observation);
            if (window.Count > 21)
                window.Dequeue();
        }

        foreach (var window in structureBars.Values)
            RegimeDiscoverySignalCacheAdapter.RestoreBars(window.ToArray());

        var daily = results[TimeFrameType.Daily];
        if (!daily.IsWarm || daily.Ema200Slope is null)
            throw new InvalidDataException("Pilot Daily EMA200 and slope did not warm.");
        RegimeDiscoverySignalCacheAdapter.Publish(daily, checkpoints[TimeFrameType.Daily]);
        var parameters = RegimeDiscoveryParameterSet.CreateDefault(
            Guid.Parse("6e21414d-0fb7-4a7f-b898-efb858c805fb"),
            Guid.Parse("0b6b6ca4-1f7e-4333-b3a1-52023832118d"),
            TimeFrameType.Monthly);
        var front = reference.RootElement.GetProperty("vx_front");
        var back = reference.RootElement.GetProperty("vx_back");
        var frontTime = DateTimeOffset.UnixEpoch.AddTicks(front.GetProperty("event_ns").GetInt64() / 100);
        var backTime = DateTimeOffset.UnixEpoch.AddTicks(back.GetProperty("event_ns").GetInt64() / 100);
        if (frontTime > evaluation || backTime > evaluation)
            throw new InvalidDataException("VX observation after the pilot decision.");
        var frontPrice = front.GetProperty("price").GetDecimal();
        var backPrice = back.GetProperty("price").GetDecimal();
        RegimeDiscoverySignalCacheAdapter.Publish(new FuturesVxTermStructureSignalReadModel
        {
            ValueDate = DateOnly.Parse(trigger.GetProperty(scheduled ? "decision_day" : "file_value_date").GetString()!,
                CultureInfo.InvariantCulture),
            ConfigurationId = "vx-term-structure-v1",
            FrontVxContractId = front.GetProperty("contract").GetString()!,
            BackVxContractId = back.GetProperty("contract").GetString()!,
            FrontVxPrice = frontPrice, BackVxPrice = backPrice, FrontBackRatio = frontPrice / backPrice,
            FrontSourceTimestampUtc = frontTime, BackSourceTimestampUtc = backTime, CalculatedAtUtc = evaluation,
            FrontSourceSequence = sequence + 1, BackSourceSequence = sequence + 1, IsWarm = true, IsValid = true
        });
        var request = RegimeDiscoverySnapshotRequestFactory.Create(series, parameters);
        var capture = await provider.CaptureAsync(request);

        var quote = reference.RootElement.GetProperty("quote");
        var quoteEventNs = quote.GetProperty("event_ns").GetInt64();
        var quoteReceiptNs = quote.GetProperty("receive_ns").GetInt64();
        var quoteAgeNs = evaluationNs - quoteEventNs;
        if (quoteReceiptNs > evaluationNs || quoteAgeNs is < 0 or >= 2_000_000_000)
            throw new InvalidDataException("Pilot reference quote fails the as-of or freshness check.");

        var required = request.Requirements.Where(r => r.IsRequired)
            .Select(r => (r.Metric, r.TimeFrame)).ToHashSet();
        var missing = capture.Issues.Where(r => required.Contains((r.Metric, r.SignalKey.TimeFrame)))
            .Select(r => new { metric = r.Metric.ToString(), frame = r.SignalKey.TimeFrame.ToString(), availability = r.Availability.ToString() })
            .ToArray();

        if (capture.Snapshot is null || !capture.IsSuccess)
        {
            File.WriteAllText(outputPath, JsonSerializer.Serialize(new
            {
                evaluated_at_utc = evaluation,
                status = "Required regime observations unavailable",
                required_issues = missing
            }));
            return;
        }
        var at = evaluation.UtcDateTime;
        var signalId = FuturesItiSignalEntityId.Create(contract,
            DateOnly.Parse(trigger.GetProperty("bucket").GetString()!, CultureInfo.InvariantCulture), TimeFrameType.Monthly);
        var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(signalId);
        var workflow = new StrategyWorkflowId(Guid.NewGuid());
        var triggerId = Guid.NewGuid();
        var triggerEvent = new FuturesItiSignalGeneratedEvent
        {
            Id = triggerId, CommandId = triggerId, EntityId = signalId,
            CreatedOn = scheduled ? DateTime.UnixEpoch.AddTicks(trigger.GetProperty("source_timestamp_ns").GetInt64() / 100) : at,
            ReceivedOn = DateTime.UnixEpoch.AddTicks(triggerAvailableNs / 100),
            VixFuturesPrice = scheduled ? (double)frontPrice : trigger.GetProperty("vx_price").GetDouble(),
            FuturesItiSignal = new FuturesItiSignalV2ReadModel
            {
                ContractId = contract, ValueDate = DateOnly.Parse(trigger.GetProperty("file_value_date").GetString()!, CultureInfo.InvariantCulture),
                TimeFrameStartValueDate = signalId.ValueDate, TimePeriod = TimeFrameType.Monthly,
                IntrinsicTime = DateTime.UnixEpoch.AddTicks(trigger.GetProperty("source_timestamp_ns").GetInt64() / 100),
                IntrinsicPrice = scheduled ? (quote.GetProperty("bid").GetDouble() + quote.GetProperty("ask").GetDouble()) / 2
                    : trigger.GetProperty("futures_price").GetDouble(),
                IntrinsicTimeTrend = Enum.Parse<IntrinsicTimeTrendType>(trigger.GetProperty("direction").GetString()!),
                IntrinsicTimeMode = Enum.Parse<IntrinsicTimeModeType>(trigger.GetProperty("mode").GetString()!),
                BandLevel = trigger.GetProperty("band_level").GetDouble(),
                ReversalLevel = trigger.GetProperty("reversal_level").GetDouble(),
                SequenceId = trigger.GetProperty("sequence_id").GetInt64()
            }
        };
        var regime = await new RegimeDiscoveryCalculationModel().CalculateAsync(new()
        {
            ResultId = Guid.NewGuid(), WorkflowId = workflow, EntityId = entity, TriggerEvent = triggerEvent,
            TriggerEventId = triggerId, ParameterSet = parameters, Snapshot = capture.Snapshot, ProducedAtUtc = at
        });
        if (!regime.Decision.IsComplete)
            throw new InvalidDataException(regime.SummaryText);
        var envelope = StrategyStageResultEnvelope.CreateRegime(regime);
        var profile = MarketConditionAssessmentParameterSet.CreateDefault("ES.Standard", TimeFrameType.Monthly,
            Guid.Parse("09ef675e-4ab1-48be-93d5-99887022c35c"), parameters.ParameterSetId, parameters.Version);
        var binding = new MarketConditionAssessmentBinding
        {
            Parameters = profile, PayloadSha256 = MarketConditionAssessmentHash.Parameters(profile)
        };
        var view = new IntrinsicTimeStrategyWorkflowView
        {
            WorkflowId = workflow, EntityId = entity, TriggerEventId = triggerId, TriggerEvent = triggerEvent,
            WorkflowRevision = 2, Status = WorkflowStrategyMachineStatus.Started, CurrentStage = StrategyWorkflowStage.MarketCondition,
            UpdatedAtUtc = at, StartedAtUtc = at, ExpiresAtUtc = at.AddMinutes(1), RegimeDiscoveryParameterSet = parameters,
            RegimeDiscoveryParameterPayloadSha256 = RegimeDiscoveryParameterPayload.ComputeSha256(parameters),
            AssessmentBinding = binding,
            RegimeDiscovery = new() { ProcessingStatus = StrategyActorProcessingStatus.Completed, CompletedAtUtc = at, Result = envelope }
        };
        var execution = new MarketConditionAssessmentExecutionId(entity, workflow);
        var command = new ExecuteMarketConditionAssessmentCommand
        {
            CorrelationId = Guid.NewGuid(), CausationId = triggerId, CommandId = Guid.NewGuid(), EntityId = execution,
            Subject = new(ActorType.Function, ExecuteMarketConditionAssessmentCommand.Actor, ExecuteMarketConditionAssessmentCommand.Verb, execution.Format()),
            WorkflowView = view, TriggerEvent = triggerEvent, InputWorkflowRevision = 2, RequestedAtUtc = at,
            ExpiresAtUtc = at.AddSeconds(5), ParameterSet = profile, ParameterPayloadSha256 = binding.PayloadSha256,
            MarketProfileId = profile.MarketProfileId, InstrumentRoot = profile.InstrumentRoot, TargetHorizon = TimeFrameType.Monthly,
            RegimeResultEnvelope = envelope, RegimePayloadSha256 = envelope.PayloadSha256
        };

        var calendar = new CmeFuturesMarketSessionCalendar();
        var session = calendar.GetSession(calendar.GetValueDate(evaluation));
        var isOpen = evaluation >= session.StartUtc && evaluation < session.EndUtc;
        var calendarActive = reference.RootElement.GetProperty("reconstructed_calendar").GetProperty("known_events")
            .EnumerateArray().Any(item =>
            {
                var starts = item.GetProperty("time_utc").GetDateTime();
                var rate = item.GetProperty("category").GetString() == "RateDecision";
                return at >= starts.AddMinutes(-(rate ? profile.RateDecisionBeforeMinutes : profile.HighImpactBeforeMinutes))
                    && at <= starts.AddMinutes(rate ? profile.RateDecisionAfterMinutes : profile.HighImpactAfterMinutes);
            });
        var minutePath = reference.RootElement.TryGetProperty("minute_bars_path", out var path)
            ? path.GetString()!
            : Path.Combine(Path.GetDirectoryName(referencePath)!, "pilot-minute-bars.csv");
        var minuteAtr = ReadMinuteAtr(minutePath, evaluation);
        var vxBaseline = reference.RootElement.GetProperty("vx_5min_prior").GetProperty("price").GetDecimal();
        var vxChange = (frontPrice - vxBaseline) / vxBaseline;
        var quoteEvent = DateTime.UnixEpoch.AddTicks(quoteEventNs / 100);
        var quoteReceipt = DateTime.UnixEpoch.AddTicks(quoteReceiptNs / 100);
        var snapshot = new MarketConditionAssessmentSnapshot
        {
            SnapshotId = Guid.NewGuid(), MarketProfileId = profile.MarketProfileId, InstrumentRoot = "ES",
            TargetHorizon = TimeFrameType.Monthly, ReferenceInstrumentId = contract, EvaluatedAtUtc = at,
            Quote = new(quote.GetProperty("bid").GetDecimal(), quote.GetProperty("ask").GetDecimal(),
                quote.GetProperty("bid_size").GetInt32(), quote.GetProperty("ask_size").GetInt32()),
            SessionState = isOpen ? MarketSessionStatus.Open : MarketSessionStatus.Closed,
            EventContext = calendarActive ? AssessmentEventContext.Elevated : AssessmentEventContext.Clear,
            Observations = profile.Sources.Select(source => new AssessmentObservation
            {
                SourceId = source.SourceId,
                ObservedAtUtc = source.SourceId switch
                {
                    "ReferenceQuote" => quoteEvent,
                    "NormalizedMovement" => minuteAtr.Start.UtcDateTime,
                    "VolatilityChange" => frontTime.UtcDateTime,
                    _ => at
                },
                ReceivedAtUtc = source.SourceId switch
                {
                    "ReferenceQuote" => quoteReceipt,
                    "NormalizedMovement" => minuteAtr.End.UtcDateTime,
                    _ => at
                },
                Sequence = sequence,
                Availability = source.Required || source.SourceId is "LastTrade" or "NormalizedMovement" or "VolatilityChange"
                    ? MarketSourceAvailability.Available
                    : MarketSourceAvailability.Unavailable,
                Value = source.SourceId switch
                {
                    "LastTrade" => trigger.GetProperty("futures_price").GetDecimal(),
                    "NormalizedMovement" => minuteAtr.Ratio,
                    "VolatilityChange" => vxChange,
                    _ => null
                },
                Unit = source.SourceId switch
                {
                    "LastTrade" => "price",
                    "NormalizedMovement" => "ATR ratio",
                    "VolatilityChange" => "5 minute relative change",
                    _ => string.Empty
                },
                Validity = MarketSourceValidity.Valid,
                Reason = source.SourceId switch
                {
                    "FeedHealth" => "Replay health based on verified input row and fresh historical ES reference quote",
                    "EventRiskCalendar" => "Reconstructed public calendar; original FMP coverage unknown",
                    _ => string.Empty
                }
            }).ToArray(),
            CalendarEvidence = new()
            {
                CheckedAtUtc = at, CoverageConfirmed = true, ValidUntilUtc = at.AddMinutes(1),
                Reason = "Reconstructed January 5 schedule, for pilot only"
            }
        }.Seal();
        var assessment = new MarketConditionAssessmentCalculator(calendar).Calculate(command, snapshot, command.CommandId);
        MarketConditionAssessmentContracts.ValidateResult(assessment);

        var selectionPolicy = TradeSelectionDefaultProfiles.EngineeringDefaults()
            .Single(policy => policy.TargetHorizon == TimeFrameType.Monthly);
        var liquidityAllowed = selectionPolicy.AllowedLiquidity.Contains(assessment.Assessment.LiquidityCondition);

        var isPilot = !reference.RootElement.TryGetProperty("annual", out _);
        var stopBeforeSelection = assessment.Assessment.Availability == AssessmentAvailability.Unavailable
            || assessment.Assessment.InheritedRestrictions.Contains(RegimeRestriction.NoNewTrade);
        var selection = stopBeforeSelection ? null
            : TradeSelectionEvaluator.Evaluate(await PilotSelection.CreateAsync(command, assessment));
        var rejectedRules = selection?.GlobalEvidence.Where(rule => rule.Status == SelectionRuleStatus.Rejected)
            .Select(rule => rule.ReasonCode).ToArray() ?? [];
        if (isPilot && (selection?.Outcome != SelectionOutcome.NoTrade || !rejectedRules.Contains("TS.ASSESSMENT.LIQUIDITY")))
            throw new InvalidDataException("The evaluator did not record the expected pilot liquidity rejection.");

        var evidence = new
        {
            evaluated_at_utc = evaluation,
            bar_source = "Historical minute aggregates, provisional ET session buckets; mixed-contract buckets excluded",
            ema = results.Select(pair => new
            {
                frame = pair.Key.ToString(),
                observations = checkpoints[pair.Key].Count,
                market_data_as_of = pair.Value.Metadata.MarketDataAsOfUtc,
                is_warm = pair.Value.IsWarm,
                ema_200 = pair.Value.Ema200,
                ema_200_slope = pair.Value.Ema200Slope
            }),
            required_observations = required.Count,
            required_issues = missing,
            regime_snapshot_accepted = capture.IsSuccess,
            real_reference_quote_age_ms = quoteAgeNs / 1_000_000m,
            reference_quote_received_before_trigger = !scheduled || quoteReceiptNs <= triggerAvailableNs,
            reference_quote_received_by_evaluation = quoteReceiptNs <= evaluationNs,
            regime = regime.SummaryText,
            regime_direction = regime.Decision.Direction.ToString(),
            regime_structure = regime.Decision.StructureClassification.ToString(),
            regime_confidence = regime.Decision.Confidence,
            regime_directional_score = regime.Decision.DirectionalScore,
            regime_phase = regime.Decision.TrendPhase.ToString(),
            iti_state_available_at_utc = DateTimeOffset.UnixEpoch.AddTicks(triggerAvailableNs / 100),
            scheduled_offline_snapshot = scheduled,
            regime_reasons = regime.Reasons.Select(reason => reason.Code).Distinct(),
            regime_restrictions = regime.Decision.Restrictions.Select(value => value.ToString()),
            assessment = new
            {
                availability = assessment.Assessment.Availability.ToString(),
                condition = assessment.Assessment.ConditionType?.ToString(),
                liquidity = assessment.Assessment.LiquidityCondition.ToString(),
                stress = assessment.Assessment.StressState.ToString(),
                event_risk = assessment.Assessment.EventRiskState.ToString()
            },
            monthly_policy_liquidity_allowed = liquidityAllowed,
            monthly_policy_liquidity_rule = "TS.ASSESSMENT.LIQUIDITY",
            trade_selection_evaluator_executed = selection is not null,
            trade_selection_outcome = selection?.Outcome.ToString() ?? "NoTrade",
            market_condition_stopped_workflow = stopBeforeSelection,
            entry_evidence_expires_at_utc = selection?.ValidUntilUtc,
            trade_selection_rejected_rules = rejectedRules,
            trade_selection_authority = "Offline balanced short ES condor catalog and portfolio fixture; historical regime and assessment envelopes",
            minute_atr_ratio = minuteAtr.Ratio,
            vx_5minute_change = vxChange,
            calendar = "Reconstructed public schedule; original FMP download evidence unavailable",
            feed_health = "Reference quote passed temporal checks; full historical feed health unverified"
        };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Pilot Daily EMA200 warm after {checkpoints[TimeFrameType.Daily].Count} bars. Required snapshot issues {missing.Length}/{required.Count}.");
    }

    private static void ResetReplayCaches()
    {
        // Each historical cut is built from scratch. These are process-local
        // producer caches in the offline harness process.
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        foreach (var field in typeof(RegimeDiscoverySignalCacheAdapter).GetFields(flags))
        {
            if (field.IsInitOnly && field.GetValue(null) is System.Collections.IDictionary cache)
                cache.Clear();
        }
    }

    private static (DateTimeOffset Start, DateTimeOffset End, decimal Ratio) ReadMinuteAtr(
        string path, DateTimeOffset evaluation)
    {
        FuturesAtrAccumulatorCheckpoint? checkpoint = null;
        (DateTimeOffset Start, DateTimeOffset End, decimal Ratio)? latest = null;
        foreach (var line in File.ReadLines(path))
        {
            var fields = line.Split(',');
            var start = DateTimeOffset.UnixEpoch.AddTicks(long.Parse(fields[0], CultureInfo.InvariantCulture) / 100);
            var end = DateTimeOffset.UnixEpoch.AddTicks(long.Parse(fields[1], CultureInfo.InvariantCulture) / 100);
            if (end > evaluation)
                throw new InvalidDataException("Minute bar ends after the pilot decision.");
            var observation = new FuturesTradeSessionBarReadModel
            {
                TimeFrame = TimeFrameType.OneMinute, IntervalStartUtc = start, IntervalEndUtc = end,
                High = decimal.Parse(fields[3], CultureInfo.InvariantCulture),
                Low = decimal.Parse(fields[4], CultureInfo.InvariantCulture),
                Close = decimal.Parse(fields[5], CultureInfo.InvariantCulture)
            };
            if (!FuturesAtrWilderAccumulator.TryApply(observation, 14, checkpoint, out var atr))
                continue;
            checkpoint = atr.Checkpoint;
            if (atr is { IsWarm: true, AtrValue: > 0 })
                latest = (start, end, Math.Abs(atr.TrueRange / atr.AtrValue.Value));
        }
        return latest ?? throw new InvalidDataException("Pilot one-minute ATR did not warm.");
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
