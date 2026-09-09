# Portfolio financial implementation manifests v1.0

> **Emulator scope correction (2026-09-08):** The IBKR emulator design has not started; its implementation is a future delivery after design approval. Do not list emulator fills, fees, settlement, cancellation or reconciliation as unfinished emulator work in the current Portfolio delivery. Current scope covers Portfolio accounting/capacity contracts, consumers and financial integrity tests using explicitly labelled execution-fact fixtures. Those tests do not qualify an emulator. Existing local submission/admission scaffolding is not a functioning emulator or evidence of broker acceptance. Full emulator integration is deferred; it is not a blocker for completing the current Portfolio development scope.

> **Development scope decision (2026-09-08):** The system is strictly in development while the complete trading system is being built. Production security implementation and qualification are deferred until immediately before production deployment. They do not block current development implementation or PF-FIN gate completion. Retain existing role/scope checks and all financial integrity rules; development principal/role metadata is not authenticated identity. Opening capital remains development-only. Completing development gates does not authorize production deployment.

Status: implementation and qualification in progress, 2026-09-08. Baseline `fd066b9b93aaf12a208bf8c0d2f5bfc18ccba26e`. This manifest does not close PF-FIN gates.

## Actor and transport registry

| Actor | Type | Verb / request | Authority and durable output |
|---|---|---|---|
| GeneralLedgerCommand | Command | Post / PostFundTransactionCommand; PostBatch / PostFundTransactionsCommand | PostgreSQL financial transaction, receipt and event; GeneralLedgerProjector publishes after commit |
| LedgerConfigurationCommand | Command | Configure / ConfigureLedgerCommand | Book/account/rule/period changes, authority refresh and reconciliation; LedgerConfigurationProjector publishes after commit |
| CapacityReservationFunction | Function | Reserve / ReservePortfolioTradeRiskCommand | AtomicBusinessAndEvent; original typed completion replay |
| CapacityConsumptionFunction | Function | Consume / ConsumeCapacityReservationCommand | AtomicBusinessAndEvent; exact accepted execution required |
| CapacityReservationCommand | Command | Change / ChangeCapacityReservationCommand | Remaining reservation lifecycle; CapacityLifecycleProjector; Consume rejected |
| GeneralLedgerQuery | Query | GetPostingReceipt, GetJournal, GetAccountBalances, GetTrialBalance, GetFundTransactionsPage, GetReconciliation | Fenced authoritative PostgreSQL reads |
| CapacityReservationQuery | Query | GetCapacityReservation, GetCapacityUsage, GetFundReservationsPage | Fenced authoritative PostgreSQL reads |
| CapacityReservationQuery | Query | GetFinancialAdmissionSnapshot | One coherent revision with available cash, exact deployment authority and relevant scoped usage; current source versions checked |
| EmulatorExecutionCommand | Command | Submit / SubmitEmulatorOrderCommand | Commits one exact consumed executable order, receipt and event; EmulatorExecutionProjector publishes history after commit |
| RiskManagementPipelineFunction (Trade domain) | Function | Execute / ExecuteRiskManagementPipelineCommand | Calculation completion only, normal completed-state persistence; implements ICapacityAssessmentCompletedEvent for the separate Portfolio admission boundary |
| IntrinsicTimeStrategyWorkflowCommand | Command | PrepareRiskManagement | Resolves the exact published deployment Risk policy and typed Portfolio admission snapshot, then commits the complete invocation before dispatch |

There are exactly two financial Function actors. Configuration does not introduce another Function. Financial requests retain OperationId, semantic hash, scope, expected revision, caller provenance and fixed deadline. Principal provenance is development metadata now; authenticated binding is required in the deferred production security phase. New mutation error IDs are 34120-34126; classified reasons are in `FinancialReasons` (34100–34119). New sequence names append `PortfolioLedger_BookId`, `PortfolioLedger_AccountId`, `PortfolioLedger_JournalId`, `PortfolioLedger_TransactionId`; Int32 Book/Account conversion is checked; journal/transaction IDs are Int64.

All seven request contracts have numeric keys 0–16. Completed events have keys 0–15. The five Function/ledger/lifecycle failed event contracts have keys 0–24. `FinancialAccess.PortfolioIds` is key 2. `FinancialAuthorityReference.EnvelopeId` is a GUID, matching the existing envelope contract. `FinancialFundAuthority.Deployments` key 7 holds exact per-deployment references/limits. `FinancialOperationOutcome.Configuration` is key 5. Configuration action values 0–10 and request keys 0–11 are frozen in `LedgerConfigurationContracts.cs`. Changes to these unreleased contracts must remain explicit and update verification.

Permission names: LedgerRead, LedgerPost, LedgerConfigure, LedgerPeriodReopen, LedgerImport, LedgerAdjust, LedgerReverse, CapacityReserve, CapacityConsume, CapacityLifecycle, EmulatorSubmit. PortfolioAdministrator includes these permissions. Nonadministrators require an explicit matching PortfolioIds grant. Before production, transport credentials/subject ACLs must bind asserted principal metadata; client-supplied role text alone is not an authentication system. This security implementation is deferred by the owner and does not block development. Existing permission and financial-scope validation remains enabled.

`LedgerPostingRequest` keys 17/18 append CapacityReservationId and FundingComponent (Undefined=0, SettlementCash=1, EntryFees=2, MarginFunding=3). The immutable `capacity_funding_receipt` links the ledger transaction to that exact consumed reservation. It changes outstanding funding accounting, not risk/position usage. PositionSlots remains one across partial fill/cancel while any position is retained.

Risk command keys 0–27, completed event keys 0–19 and failed event keys 0–23 are explicit. Command key 27 pins the complete configuration payload hash. IDs are 23025/24031/24032. PrepareRiskManagementCommand has keys 0–7 and error ID 21021. RiskExecution identity adds attempt ordinal; the workflow view appends its frozen invocation at key 33 and the result envelope appends typed RiskResult at key 12. RiskAssessmentResult keys 0–27 preserve distinct composition-result, unit-candidate, sized-order and assessment hashes. The legacy Fund CandidateSha256 meaning is unchanged. RiskUnitResult key 11 records composer fees already included in unit loss. The Risk Function does not increase the count of **Portfolio financial** Function actors; it is the fifth Trade calculation stage.

The Risk route now uses Function, with no legacy Start dispatch. Preparation resolves the exact deployment PipelineParameters RiskManagement reference and verifies its published policy/hash/effectivity. It queries Portfolio through IPortfolioFinancialApi and commits the request before dispatch. Completion recomputes the result and rejects rehashed quantity/side/identity changes. A normal rejection completes NoTrade; an approved proposal remains Started with no Proceed decision until authoritative financial handoff receipts confirm internal emulator submission. The handoff includes preliminary local admission scaffolding; full Portfolio recovery and execution-fact consumer qualification remain open. It is not a functioning emulator. Emulator design, implementation and end-to-end qualification are future work.

RiskParameterSet defines three stable draft profile identities for Daily, Weekly and Monthly. Defaults: maximum 10 strategy units, 1% available-settled-cash risk basis, USD 25,000 emulator margin, USD 5 fees and USD 1,000 variation reserve per gross contract. These are explicit engineering fixtures, not IBKR margin facts. They require Environment=Emulator; no live fallback. Creating a draft does not publish, assign a deployment or activate a Fund.

## Authority writers

`PortfolioEventStore.AppendPortfolioAsync`, `AppendFundAsync`, and `AppendPolicyAsync` use `IPortfolioAuthorityFence` in the API composition root. The fence locks the financial authority row and appends the business event in the same PostgreSQL transaction. Admission-relevant changes increment financial revision/authority epoch and set NeedsRefresh. Only FundCompositionReserved and FundCompositionStateChanged advance the unchanged Fund source fence without revoking its mandate. Unknown new Fund event kinds invalidate authority by default. Snapshot persistence is not a new authority grant.

Create/refresh configuration verifies committed Portfolio/Fund membership, account ownership, active policy, exact deployment permissions/assignments and envelope caps. Storage checks source stream revisions under the same lock before commit. Refresh cannot change migration qualification or clear Overdrawn/NeedsReconciliation. New book creation cannot enable spending.

## Storage manifests

Executable PostgreSQL manifest: `Application.Storage/PortfolioFinancial/PortfolioFinancialSchema.cs`. Tables are co-located with event_log/event_stream_id/event_name_id; schema version is 1. Business operation receipts point at the same committed event. Command projector markers are inserted in that transaction. Function history is recovered by finding unacknowledged committed receipts; there is no lossy global event-ID high-water mark.

Financial history Scylla table: `financial_operation_by_portfolio_month`; partition `(portfolioId, month)`, clustering `(financialRevision DESC, operationId ASC)`. It is rebuildable history and never a spending authority. `financial_history_receipt` acknowledges each projected PostgreSQL event independently, including late commits with lower allocated event IDs.

Internal export uses accounting_export, accounting_export_source and accounting_export_attempt. One destination company cannot include the same journal twice. Payloads, source inclusion and attempt facts are immutable; only delivery status, receipt and attempt count can advance. Each request carries a stable ExportId, Portfolio/book, committed source revision, up to 100 journals and exact account-version mapping. Failed/unknown delivery retains Pending and the original payload; replay reconciles the same identity. Corrections are new journals linked to the original. AccountingExportStore is registered as an internal service; no QuickBooks connector or UI export action is enabled.

Accounting invariants: journals/entries/transactions immutable; balanced journal at deferred commit; entries cannot be added in a subsequent transaction; exact account/rule versions; nonoverlapping accounting periods; operation/source uniqueness; bounded paged reads with pinned membership cut. Reconciliation independently aggregates journal entries and compares materialized balances. A mismatch blocks admission; closing a period rejects reconciliation predating later journal posts.

## Legacy producer, type and sign inventory

The existing FundTransactionType enum has 14 values (0–13). `LegacyFinancialClassification.TypeRules` enumerates all values and a test compares its keys against the actual enum. Unknown values quarantine. No currency is present in FundTransactionReadModel, so currency must come from qualified source mapping; it is never defaulted during classification.

| Type | Observed old behavior / required disposition |
|---|---|
| Unknown | Quarantine |
| OpeningTrade | TradePositionService sets amount to existing Fund balance; history-only snapshot, never capital |
| TradeCommission | Old balance arithmetic adds Amount; new expense interpretation requires independently confirmed cost sign |
| UnrealizedTradePnl | Valuation needs explicit absolute-versus-delta qualification; never blindly add repeatedly to cash |
| RealizedTradePnl | Signed result; clear previously recognized unrealized amount before realization |
| OpeningTradeAdjustment | Snapshot correction; history only |
| TradeCommissionAdjustment | Original-source/journal link and sign qualification required; otherwise quarantine |
| UnrealizedTradePnlAdjustment | Original-source/journal link and valuation interpretation required; otherwise quarantine |
| RealizedTradePnlAdjustment | Original-source/journal link and prior realization reconciliation required; otherwise quarantine |
| EndOfDayProcessed | Operational marker/history only; EOD producer actually supplies UnrealizedTradePnl inputs |
| CashDeposit | Positive amount plus confirmed cash movement evidence; old Balance is not authoritative |
| CashDepositAdjustment | Original-source/journal link required; otherwise quarantine |
| CashWithdrawal | Old code subtracts positive Amount; historical cash outflow is negative under a dedicated qualified import rule |
| CashWithdrawalAdjustment | Old code adds Amount; cannot infer correction direction without original link; quarantine pending mapping |

Entry points inventoried:

- Domain.Fund/Transaction/Command: CreateFundTransaction, CreateFundTransactions, ProcessEndOfDayFundTransaction; their Command actor, state/repository and event projector.
- Application.Api.Nats.Client/FundCommandApi, Application.Api.Client/FundCommandApi and Application.Api.Server/CommandMaps expose legacy mutations.
- Service.TradePosition/TradePositionService creates opening snapshots, commissions, realized and unrealized P&L.
- Domain.Trade/Option/Event/Extensions/OptionTradeEventExtensions sends durable EOD commands after option EOD completion.
- UI.Net.ViewModels/Fund and UI.Net.Views/Fund: FundTransactionEditor, FundCashTransactionViewModel/Editor, AdjustFundTransactionEditor; UI.EventConsumer/FundUIEventConsumer consumes legacy terminals.
- Application.Storage/FundDb/FundDbContext, FundTransactionProjection and FundDbCql own legacy materializations and recovery journals. Fund query APIs and FundQueryService/legacy reports read them.

These entry points are inventoried, not yet cut over. PF-FIN-06 must implement and qualify scoped writer fencing, migration and redirection before any scope is activated.

| Legacy table / field | Destination or retained source role |
|---|---|
| fund_transaction | Classify every record; qualified postings or labelled pre-cut history |
| fund_transaction_identity_v4 | Preserve source identity map and duplicate detection |
| fund_transaction_timeline_v3 | History counts/date reconciliation; rebuildable |
| fund_balance_by_status_day_v3 | Compare summaries, do not post their totals again |
| fund_transaction_amount_v3 | Independent sign/amount reconciliation |
| fund_transaction_projection_state_v3 | Durable projector source cut/state |
| fund_transaction_projection_mutation_v3 | Pending/replayed projection drain evidence |
| fund_transaction_write_mutation_v3 | Pending write drain evidence |
| fund_transaction_write_ownership_v3 | Existing per-Fund write ownership; migration must fence/drain it |
| fund.balance | Legacy comparison only; never a new ledger deposit or available-cash fallback |

Full posted history and opening-balance-plus-labelled-history are mutually exclusive capital recognition modes per migrated scope. Source corrections without qualified journal links remain quarantined. No production financial import or automatic activation has been performed.

Owner clarification (2026-09-08): the current `OpeningBalance` implementation is restricted to development capital. The API root constructs `FinancialDevelopmentPolicy` from `IHostEnvironment.IsDevelopment()`; omitted policy denies new opening capital. `GeneralLedgerStore` additionally requires an Emulator book, Importing/unqualified authority and source system `DevelopmentOpeningCapital` under the financial transaction. A command cannot enable this host policy. Posting/replay retains normal source deduplication and atomic receipt/event semantics; no automatic capital amount, book qualification or spending activation is introduced. Verified production capital is not a prerequisite for development testing. The historical opening-balance mode above is not authorization for production opening capital.


## Subsequent handoff and maintenance contracts - 2026-09-08

- `FundRiskAuthorizationReference` keys 0-19 bind the four separate hashes, exact reservation completion, Fund/order/workflow, units, epoch, expiry and Emulator environment. `AuthorizeFundOrderRisk` is mapped by the existing Fund Command actor and validates the reference inside PortfolioAuthorityFence. `GetFundRiskAuthorization` reads the committed event by the original command ID and enforces Fund/Portfolio scope.
- `AdvanceRiskFinancialHandoffCommand` uses keys 0-8/error 21022; workflow view key 34 contains `RiskFinancialHandoffState` keys 0-12. Phase values are None=0, ReservePending=1, FundPending=2, ConsumePending=3, Consumed=4, Submitted=5, Authorized=6. Current workflows stop at Authorized; they do not dispatch consumption or emulator submission. Execution acceptance key 10 binds ExecutionOrderHash. Standard MessagePack serializes all nested typed DTOs.
- `SubmitEmulatorOrderCommand` keys 0-16/error 34126 and `EmulatorOrderSubmittedEvent` keys 0-15 use the ordinary Command/durable projector convention. The `emulator_order` PostgreSQL row is immutable; ExecutionId, ReservationId and OperationId are unique. This is preliminary local submission admission scaffolding only. It is not a functioning emulator, confirmed broker acceptance, completed execution accounting or a broker connection; review it against the future approved emulator design.
- Capacity ChangeKind RecordPositionClose=9; request key 11, receipt key 13, snapshot key 11 and execution reconciliation key 12 append cumulative ClosedUnits. Open position usage is FilledUnits minus ClosedUnits. Reconciliation must match exact source/quantity/environment before release.
- `GetFinancialPostingConfiguration` returns effective exact rules and period state at the fenced financial revision. Its request has key 0; response keys 0-6 (key 6 is AllowDevelopmentOpeningCapital, derived from trusted host/book state). It routes through GeneralLedgerQuery.
- `capacity_expiry_dispatch` is an operational PostgreSQL outbox with immutable request identity/hash, one Pending row per reservation and Pending/Committed/ExpiredWithoutCommit dispositions. Its worker dispatches normal expiry Commands, observes original committed receipts and never directly modifies financial usage. It does not add a Function actor.
- The UI pending journal persists exact posting requests under the current user's LocalAppData/IFM/Portfolio/PendingFinancialOperations directory. Atomic replacement plus exclusive per-operation locking prevents competing edits/late unknown outcomes from replacing an existing committed result. It contains no transport credentials. It is a recovery journal, not authoritative financial state.

Current evidence and migration requirements (production security is deferred, not a development dependency): [implementation plan section 22.5](Portfolio-Fund-Implementation-Plan-v1.0.md#225-financial-handoff-posting-ui-and-expiry-continuation--2026-09-08).


## Ledger administration and recovery additions - 2026-09-09

- GeneralLedgerQuery adds `GetFinancialLedgerConfiguration` and `PrepareFinancialBook`; it retains frozen parse/receive/exception maps and extension dispatch. Preparation is a query-side draft/sequence allocation service, not an additional Function actor or financial mutation.
- `FinancialLedgerConfiguration` keys 0-9: BookId, Currency, Environment, OperatingState, SourceWatermark, Periods, Accounts, Rules, LatestReconciliation, DevelopmentQualificationBook. Nested periods use keys 0-4, configured accounts 0-1, configured rules 0-3. The empty configuration request uses standard typed transport.
- `PrepareFinancialBookRequest` keys 0-2: ExecutionAccountReference, PeriodStart, PeriodEnd. `FinancialBookSetup` keys 0-2: ExecutionAccounts, FundNames, Draft. Null account selection reads choices; selected configured account prepares IDs/configuration. The existing ConfigureLedgerCommand persists the reviewed draft, subject to current source checks. Generated IDs may have gaps after abandoned preparation.
- Initial development chart: Cash/Debit, Equity/Credit, Expense/Debit, Asset/Debit, UnrealizedPnl/Credit, RealizedPnl/Credit. All require a Fund dimension. Initial rules: OpeningBalance, DepositConfirmed, WithdrawalRequested, WithdrawalCancelled, WithdrawalSettled, FundTransfer, Commission, Valuation, RealizedPnl, Reversal. Deposit, settled withdrawal, commission and realized cash P&L require confirmed movement. Realization clears configured prior valuation. There is no implicit TradeSettlement or arbitrary Adjustment rule.
- `legacy_financial_inventory` stores immutable scope hash, Incomplete/UnfencedInventory state and result. `legacy_financial_inventory_row` stores original JSON, source key/content hashes, disposition/reason and original-precision amount; updates/deletes are rejected. This operational archive is separate from `ledger_migration` and never establishes a writer fence or changes financial authority.
- `FinancialWorkflowRecoveryService` scans committed snapshot pages and sends existing mapped workflow commands. The latest snapshot remains authoritative; invalid stream tails are reported and retried on later full passes. Scylla history and new-signal switches do not determine financial recovery.
- Configuration-operation recovery lives under LocalAppData/IFM/Portfolio/PendingFinancialConfigurations. It persists the exact typed ConfigureLedgerCommand before send and queries original receipts before retry, including book setup when no book is visible yet.

See implementation plan section 22.9 for actual evidence and the remaining gate requirements.


## Authority review, fresh-scope qualification and diagnostics - 2026-09-09

- GeneralLedgerQuery adds `PrepareFinancialAuthority`. Its request key 0 is PermitNewSpending (default false); response FinancialAuthorityDraft keys 0-1 are Draft and Notes. The standard FinancialRead wrapper pins the PostgreSQL financial revision. No extra Function actor, custom MessagePack envelope or financial mutation occurs in preparation.
- `FinancialDeploymentAuthority` appends key 2 MaximumRiskPerTrade, default zero; `FinancialAdmissionSnapshot` appends key 12 of the same name. Missing historical values deserialize as zero and cannot authorize new admission. Exact per-trade policy/envelope loss is enforced separately from aggregate scope loss by both Risk preparation and reservation admission. Historical committed receipts remain replayable.
- Shared underlying scope keys are `U1:<escaped uppercase symbol>|<escaped uppercase exchange>|<escaped uppercase currency>`. They contain no contract expiry, timeframe or deployment identity. Risk validates contract identity separately. Shared per-market Greek limits use the most restrictive enabled cap across the prepared Funds; units and consistent shared caps are revalidated. Refresh refuses nonzero old contract-specific underlying usage until reconciled.
- Authority preparation reads financial state/cash at one revision, releases database locks, then reads committed Portfolio/Fund/policy and exact published catalog versions. It only enables current qualified, active, effective and assigned deployment authority. No eligible products means no authority. Saving rechecks source versions, epoch and expected financial revision. Refresh cannot alter the qualified Fund membership or book ownership. Expiry cannot outlive any source definition. The UI exposes a readonly review, explicit spending checkbox, audit reason and the existing durable configuration-operation recovery.
- LedgerConfigurationAction appends QualifyDevelopmentBook=11; prior values remain unchanged. It requires trusted Development host policy, an exact nonspending/unqualified Emulator book, matched reconciliation and fresh-scope writer/absence evidence. It writes the qualification manifest in ledger_migration and moves to NeedsRefresh; it does not enable spending.
- `legacy_writer_scope` records Legacy/Fenced ownership and verified-empty evidence. `legacy_write_intent` records Pending before Scylla work and Completed only after confirmed completion. PostgreSQL legacy event append and all registered legacy FundDb transaction writes consult the fence. API startup verifies the actual FundDb instance has the fence. A pending or prior legacy write/history prevents fresh-scope qualification; existing scopes require the separately reconciled migration. This does not claim that an unupgraded old process performing direct Scylla writes is fenced.
- A failed absence check does not clear a fence. Qualification replay returns the original receipt even after development funding is disabled. No application Fund was funded, migrated or automatically activated during this qualification; integration data belongs to generated test scopes.
- `TomasAI.IFM.PortfolioFinancial` metrics have bounded outcome labels and contain no payloads, SQL, caller identities or Fund IDs. Attempt duration includes connection/transaction work; authority-lock duration measures successful acquisition. Confirmed-rollback retries and unknown commit are distinct. A throwing telemetry listener cannot change financial completion.

See [development qualification and runbook](Portfolio-Financial-Development-Qualification-v1.0.md) for measured load and recovery boundaries. Existing legacy import/cutover and complete five-stage/multi-host qualification remain open; this addition does not declare all gates complete.
