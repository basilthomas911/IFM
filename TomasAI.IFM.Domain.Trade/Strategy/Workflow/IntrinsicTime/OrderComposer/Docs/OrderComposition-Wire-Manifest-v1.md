# Order Composition Wire Manifest v1

Date: 2026-09-08. Append-only integer keys; shared MessagePack boundary serialization. The contract tests check contiguous unique keys and exercise populated request/result/pricing round trips. Main Execute keys are 0..20, completed event 0..19, failed event 0..23, workflow view/state append at 32/28, and envelope composition content appends at 11.

## CompositionCandidate

| Key | Field |
| --- | --- |
| 0 | `SchemaVersion` |
| 1 | `CandidateId` |
| 2 | `OrderId` |
| 3 | `PrimaryTradeId` |
| 4 | `PortfolioId` |
| 5 | `FundId` |
| 6 | `AssignmentVersion` |
| 7 | `DeploymentKey` |
| 8 | `StrategyKey` |
| 9 | `StructureKey` |
| 10 | `VariantKey` |
| 11 | `Product` |
| 12 | `TargetHorizon` |
| 13 | `Side` |
| 14 | `Bias` |
| 15 | `PremiumMode` |
| 16 | `Legs` |
| 17 | `UnitQuantity` |
| 18 | `LiquidityCapacityUnits` |
| 19 | `Pricing` |
| 20 | `Greeks` |
| 21 | `RiskEvidence` |
| 22 | `ExecutionEnvelope` |
| 23 | `ParameterResolutionHash` |
| 24 | `SnapshotHash` |
| 25 | `BindingHash` |
| 26 | `PricerVersion` |
| 27 | `EvaluatedAtUtc` |
| 28 | `ValidUntilUtc` |
| 29 | `ApprovalState` |
| 30 | `CandidateHash` |

## CompositionLeg

| Key | Field |
| --- | --- |
| 0 | `InstrumentId` |
| 1 | `RawSymbol` |
| 2 | `UnderlyingInstrumentId` |
| 3 | `InstrumentClass` |
| 4 | `Side` |
| 5 | `Ratio` |
| 6 | `Right` |
| 7 | `Strike` |
| 8 | `ExpirationUtc` |
| 9 | `Multiplier` |
| 10 | `TickRuleId` |
| 11 | `Quote` |
| 12 | `Valuation` |
| 13 | `DefinitionHash` |

## CompositionValuation

| Key | Field |
| --- | --- |
| 0 | `ImpliedVolatility` |
| 1 | `Delta` |
| 2 | `Gamma` |
| 3 | `Theta` |
| 4 | `Vega` |
| 5 | `Rho` |
| 6 | `TheoreticalPrice` |
| 7 | `TimeToExpiry` |
| 8 | `ContextDigest` |

## CompositionPrices

| Key | Field |
| --- | --- |
| 0 | `NaturalDebit` |
| 1 | `MidDebit` |
| 2 | `BestDebit` |
| 3 | `LimitDebit` |
| 4 | `WorstDebit` |
| 5 | `ComboTick` |
| 6 | `ComboSpread` |
| 7 | `CostReserve` |

## CompositionGreeks

| Key | Field |
| --- | --- |
| 0 | `Delta` |
| 1 | `Gamma` |
| 2 | `Theta` |
| 3 | `Vega` |
| 4 | `Rho` |
| 5 | `DeltaUnits` |
| 6 | `VegaUnits` |
| 7 | `ThetaUnits` |

## CompositionRisk

| Key | Field |
| --- | --- |
| 0 | `RiskBound` |
| 1 | `MaximumLoss` |
| 2 | `MaximumProfit` |
| 3 | `PayoffRewardToRisk` |
| 4 | `Notional` |
| 5 | `PlannedLoss` |
| 6 | `StressLoss` |

## CompositionExecutionEnvelope

| Key | Field |
| --- | --- |
| 0 | `SchemaVersion` |
| 1 | `OrderType` |
| 2 | `TimeInForce` |
| 3 | `Atomic` |
| 4 | `ProposedSignedDebit` |
| 5 | `WorstSignedDebit` |
| 6 | `Tick` |
| 7 | `TickRuleVersion` |
| 8 | `ValidUntilUtc` |
| 9 | `AllowLegging` |
| 10 | `AllowMarketEscalation` |

## CompositionDecisionContext

| Key | Field |
| --- | --- |
| 0 | `SelectionResultId` |
| 1 | `SelectionResultHash` |
| 2 | `InputHash` |
| 3 | `BindingHash` |
| 4 | `SnapshotHash` |
| 5 | `ValueDate` |
| 6 | `PricerVersion` |
| 7 | `AlgorithmVersion` |
| 8 | `PortfolioId` |
| 9 | `FundId` |

## CompositionCounts

| Key | Field |
| --- | --- |
| 0 | `Generated` |
| 1 | `Eligible` |
| 2 | `Rejected` |

## CompositionRejection

| Key | Field |
| --- | --- |
| 0 | `ReasonCode` |
| 1 | `Count` |

## CompositionRanking

| Key | Field |
| --- | --- |
| 0 | `DteDistance` |
| 1 | `DeltaDistance` |
| 2 | `LegDeltaDistance` |
| 3 | `SpreadTicks` |
| 4 | `RewardToRisk` |
| 5 | `CanonicalKey` |

## OrderCompositionResult

| Key | Field |
| --- | --- |
| 0 | `SchemaVersion` |
| 1 | `ResultId` |
| 2 | `WorkflowId` |
| 3 | `EntityId` |
| 4 | `InvocationId` |
| 5 | `InputWorkflowRevision` |
| 6 | `InputSha256` |
| 7 | `EvaluatedAtUtc` |
| 8 | `ProducedAtUtc` |
| 9 | `TargetHorizon` |
| 10 | `Outcome` |
| 11 | `Candidate` |
| 12 | `DecisionContext` |
| 13 | `ResolvedParameters` |
| 14 | `CandidateCounts` |
| 15 | `CandidateDiagnostics` |
| 16 | `Reasons` |
| 17 | `ValidUntilUtc` |
| 18 | `SummaryText` |
| 19 | `Ranking` |

## Token

| Key | Field |
| --- | --- |
| 0 | `Schema` |
| 1 | `PortfolioId` |
| 2 | `FundId` |
| 3 | `ValueDate` |
| 4 | `PageSize` |
| 5 | `State` |

## CompositionQueryAccess

| Key | Field |
| --- | --- |
| 0 | `Principal` |
| 1 | `Roles` |

## OrderCompositionProjection

| Key | Field |
| --- | --- |
| 0 | `Completion` |
| 1 | `WorkflowAccepted` |
| 2 | `AcceptanceUnknown` |
| 3 | `SuspectedOrphan` |

## OrderCompositionHistoryRow

| Key | Field |
| --- | --- |
| 0 | `PortfolioId` |
| 1 | `FundId` |
| 2 | `ValueDate` |
| 3 | `OccurredAtUtc` |
| 4 | `WorkflowId` |
| 5 | `InvocationId` |
| 6 | `EventId` |
| 7 | `TargetHorizon` |
| 8 | `Outcome` |
| 9 | `ReasonCode` |
| 10 | `ResultId` |
| 11 | `ResultSha256` |

## OrderCompositionHistoryPage

| Key | Field |
| --- | --- |
| 0 | `Items` |
| 1 | `PagingState` |

## GetOrderCompositionInvocationQuery

| Key | Field |
| --- | --- |
| 0 | `Subject` |
| 1 | `EntityId` |
| 2 | `Access` |
| 3 | `WorkflowId` |
| 4 | `InvocationId` |

## GetOrderCompositionResultQuery

| Key | Field |
| --- | --- |
| 0 | `Subject` |
| 1 | `EntityId` |
| 2 | `Access` |
| 3 | `WorkflowId` |
| 4 | `InvocationId` |
| 5 | `ResultId` |

## GetOrderCompositionHistoryPageQuery

| Key | Field |
| --- | --- |
| 0 | `Subject` |
| 1 | `EntityId` |
| 2 | `Access` |
| 3 | `PortfolioId` |
| 4 | `FundId` |
| 5 | `ValueDate` |
| 6 | `PageSize` |
| 7 | `PagingState` |

## CompositionParameters

| Key | Field |
| --- | --- |
| 0 | `LoadingMilliseconds` |
| 1 | `ExecutionMilliseconds` |
| 2 | `CandidateLifetimeMilliseconds` |
| 3 | `MaximumQuoteAgeMilliseconds` |
| 4 | `MaximumQuoteSkewMilliseconds` |
| 5 | `MinimumDisplayedSize` |
| 6 | `ParticipationFraction` |
| 7 | `MaximumUnderlyingSpreadTicks` |
| 8 | `MaximumLegSpreadTicks` |
| 9 | `MaximumComboSpreadTicks` |
| 10 | `TargetDaysToExpiry` |
| 11 | `MinimumDaysToExpiry` |
| 12 | `MaximumDaysToExpiry` |
| 13 | `TargetLegDelta` |
| 14 | `LegDeltaTolerance` |
| 15 | `TargetPutDelta` |
| 16 | `TargetCallDelta` |
| 17 | `TargetNetDelta` |
| 18 | `BalanceTolerance` |
| 19 | `MinimumCreditToWidth` |
| 20 | `MaximumDebitToWidth` |
| 21 | `MinimumCreditTicks` |
| 22 | `MinimumRewardToRisk` |
| 23 | `MidpointToNaturalFraction` |
| 24 | `MaximumAdverseMoveTicks` |
| 25 | `FeePerContract` |
| 26 | `SlippageTicksPerLeg` |
| 27 | `FuturesPlannedDistance` |
| 28 | `FuturesStressDistance` |
| 29 | `FuturesRollHours` |

## CompositionParameterBound

| Key | Field |
| --- | --- |
| 0 | `Parameter` |
| 1 | `Minimum` |
| 2 | `Maximum` |
| 3 | `Grid` |

## CompositionPredicate

| Key | Field |
| --- | --- |
| 0 | `Comparison` |
| 1 | `Feature` |
| 2 | `Values` |
| 3 | `Children` |
| 4 | `Required` |

## CompositionAdjustment

| Key | Field |
| --- | --- |
| 0 | `Code` |
| 1 | `Priority` |
| 2 | `Predicate` |
| 3 | `Parameter` |
| 4 | `Operation` |
| 5 | `Operand` |

## CompositionRuleEvidence

| Key | Field |
| --- | --- |
| 0 | `Code` |
| 1 | `Status` |
| 2 | `Parameter` |
| 3 | `Before` |
| 4 | `Unclamped` |
| 5 | `After` |

## CompositionResolvedParameters

| Key | Field |
| --- | --- |
| 0 | `Values` |
| 1 | `Evidence` |
| 2 | `Hash` |

## CompositionVariantRules

| Key | Field |
| --- | --- |
| 0 | `VariantKey` |
| 1 | `StructureKey` |
| 2 | `BaseParameters` |
| 3 | `HardBounds` |
| 4 | `AdjustmentRules` |
| 5 | `AllowedWidths` |
| 6 | `RequireSymmetricWings` |
| 7 | `DeltaUnits` |
| 8 | `RankingVersion` |

## OrderCompositionRules

| Key | Field |
| --- | --- |
| 0 | `SchemaVersion` |
| 1 | `AlgorithmVersion` |
| 2 | `PricerVersion` |
| 3 | `SupportedHorizon` |
| 4 | `InstrumentRoot` |
| 5 | `Currency` |
| 6 | `VariantRules` |

## CompositionBinding

| Key | Field |
| --- | --- |
| 0 | `SchemaVersion` |
| 1 | `Selected` |
| 2 | `RulesDefinition` |
| 3 | `RulesSchema` |
| 4 | `Rules` |
| 5 | `BuilderCode` |
| 6 | `BuilderVersion` |
| 7 | `FrozenAtUtc` |
| 8 | `ValidUntilUtc` |
| 9 | `BindingSha256` |

## CompositionFutureDefinition

| Key | Field |
| --- | --- |
| 0 | `ContractId` |
| 1 | `Root` |
| 2 | `Dataset` |
| 3 | `Exchange` |
| 4 | `Currency` |
| 5 | `LastTradingUtc` |
| 6 | `Multiplier` |
| 7 | `TickSize` |
| 8 | `DefinitionDigest` |

## CompositionMarketInstrument

| Key | Field |
| --- | --- |
| 0 | `ContractId` |
| 1 | `Quote` |
| 2 | `Pricing` |
| 3 | `Strike` |
| 4 | `IsCall` |
| 5 | `Underlying` |
| 6 | `FutureDefinition` |

## CompositionInstrumentSnapshot

| Key | Field |
| --- | --- |
| 0 | `Instrument` |
| 1 | `Valuation` |

## MarketCompositionSnapshot

| Key | Field |
| --- | --- |
| 0 | `SchemaVersion` |
| 1 | `SnapshotId` |
| 2 | `ScopeId` |
| 3 | `ScopeToken` |
| 4 | `Horizon` |
| 5 | `GenerationId` |
| 6 | `EvaluatedAtUtc` |
| 7 | `ValidUntilUtc` |
| 8 | `Instruments` |
| 9 | `Digest` |

## OptionPricingValue

| Key | Field |
| --- | --- |
| 0 | `ImpliedVolatility` |
| 1 | `Delta` |
| 2 | `Gamma` |
| 3 | `Theta` |
| 4 | `Vega` |
| 5 | `Rho` |
| 6 | `TheoreticalPrice` |
| 7 | `TimeToExpiry` |
| 8 | `ContextDigest` |

## OptionPricingFailure

| Key | Field |
| --- | --- |
| 0 | `Code` |
| 1 | `Input` |
| 2 | `ContractId` |
| 3 | `Detail` |
| 4 | `Retryable` |

## OptionPricingConvention

| Key | Field |
| --- | --- |
| 0 | `SchemaVersion` |
| 1 | `ContractId` |
| 2 | `Dataset` |
| 3 | `PublisherId` |
| 4 | `InstrumentId` |
| 5 | `RawSymbol` |
| 6 | `Root` |
| 7 | `Exchange` |
| 8 | `Currency` |
| 9 | `UnderlyingContractId` |
| 10 | `ExerciseStyle` |
| 11 | `SettlementStyle` |
| 12 | `ExpirationUtc` |
| 13 | `LastTradingUtc` |
| 14 | `DayCount` |
| 15 | `CalendarVersion` |
| 16 | `Multiplier` |
| 17 | `TickSize` |
| 18 | `TickRuleVersion` |
| 19 | `DefinitionDigest` |
| 20 | `MappingVersion` |
| 21 | `EvidenceId` |
| 22 | `EffectiveFromUtc` |
| 23 | `EffectiveUntilUtc` |

## OptionPricingCalendar

| Key | Field |
| --- | --- |
| 0 | `Version` |
| 1 | `TimeZoneId` |
| 2 | `CoverageFrom` |
| 3 | `CoverageUntil` |
| 4 | `ValueDateRollover` |
| 5 | `TradingDates` |

## OptionPricingQuote

| Key | Field |
| --- | --- |
| 0 | `ContractId` |
| 1 | `Bid` |
| 2 | `Ask` |
| 3 | `BidSize` |
| 4 | `AskSize` |
| 5 | `EventAtUtc` |
| 6 | `ReceivedAtUtc` |
| 7 | `Sequence` |
| 8 | `GenerationId` |

## OptionPricingContext

| Key | Field |
| --- | --- |
| 0 | `Contract` |
| 1 | `Calendar` |
| 2 | `Rate` |
| 3 | `ValidUntilUtc` |
| 4 | `GenerationId` |
| 5 | `PricerVersion` |
| 6 | `MaximumQuoteAgeMilliseconds` |
| 7 | `MaximumQuoteSkewMilliseconds` |
| 8 | `PublicationPolicyVersion` |

## TreasuryRateConversionPolicy

| Key | Field |
| --- | --- |
| 0 | `Source` |
| 1 | `SourceSeriesId` |
| 2 | `Convention` |
| 3 | `Version` |
| 4 | `EvidenceId` |

## TreasuryContinuousRate

| Key | Field |
| --- | --- |
| 0 | `Tenor` |
| 1 | `RatePercent` |
| 2 | `AnnualContinuousRate` |
| 3 | `ValueDate` |
| 4 | `ObservedAtUtc` |
| 5 | `CurveDigest` |
| 6 | `Conversion` |
| 7 | `ModelingPolicy` |
