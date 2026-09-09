# Portfolio financial development gate evidence — 2026-09-09

This report covers the current development delivery, based on local commit `7a456468` plus the retained-history and qualification changes. It does not authorize production deployment. The owner selected retained read-only legacy history and separately entered development capital. Historical journal reconstruction, production security, a functioning broker emulator, IBKR and QuickBooks delivery are outside this delivery.

## Reproducible qualification

Run from the repository on Windows with .NET 10 and the repository's local PostgreSQL/Scylla/Redis test services. Supply a separate NATS broker; the runner rejects the application's usual port 4222.

```powershell
./scripts/Test-PortfolioFinancialGates.ps1 -NatsUrl nats://127.0.0.1:14222
```

`-NoBuild` is available after building the current sources. The script executes database suites sequentially because their additive schema and fault-trigger checks share test schemas. It rejects zero discovered tests, skips and failures, renders the financial UI, builds the API and verifies its Development composition root without starting actors, feeds or listeners. It writes TRX reports, rendered images, startup output and `summary.json` under a new timestamped `TestResults` directory. Generated test data uses owned identities; no application legacy records or capital are changed.

## Requirement traceability

Final sequential run: `TestResults/portfolio-financial-20260909-135624`, executed using the checked-in runner with `-NoBuild` against current built sources. **1,511 passed, 0 failed, 0 skipped.** The runner subsequently built the API with zero warnings/errors and passed Development `--verify-startup-only`. This closes PF-FIN-01 through PF-FIN-07 for the stated development scope.

| Suite/filter | Passed |
|---|---:|
| Portfolio units | 194 |
| Portfolio BDD | 34 |
| Financial wire verification | 26 |
| Trade units | 988 |
| Shared Function/mapped Command regressions | 31 |
| Financial presentation | 31 |
| Rendered financial WinForms | 19 |
| Portfolio financial PG/Scylla/NATS/process integration | 142 |
| Five-stage matrix and Risk/workflow runtime | 42 |
| Real-store load qualification | 4 |

The counts above are executions in this one run, not sums of overlapping exploratory runs. The 36 five-stage cases each preserve one horizon and actual upstream outputs, then persist the financial reservation and exact Fund approval. The three additional Risk matrix cases each exercise 12 variants; those internal iterations are not counted as extra tests.

Paths below are relative to their existing test projects. A fixture boundary is named explicitly; source fixtures are not represented as external broker evidence.

| Requirement | Executable evidence |
|---|---|
| FIN-T01 balanced journals and database enforcement | Portfolio integration `GeneralLedgerPostingIntegrationTests.Unbalanced_plan_is_rejected_by_the_database_even_if_domain_validation_is_bypassed`, `FinancialPayloadBoundaryTests`; Portfolio BDD financial features and posting Model units |
| FIN-T02 all legacy kinds, signs, valuation and fees | `LegacyFinancialClassificationTests` compares classification with the entire enum; `LedgerAccountingIntegrationTests` independently asserts cash, valuation clearing, fees and realized results; retained inventory recognizes zero capital |
| FIN-T03 withdrawal versus capacity | `CapacityReservationIntegrationTests.Withdrawal_and_reservation_race_under_the_same_cash_fence`, competing reservation and withdrawal tests; independent connections with authoritative cash checks |
| FIN-T04 cross-Fund limits and revoked authority | `Independently_funded_Funds_share_one_Portfolio_limit_across_connections_and_fresh_retries`, `PortfolioAuthorityFenceTests`, `FinancialBookPreparationIntegrationTests`, stale-source and consumption denial cases |
| FIN-T05 atomic writes, uncertain commit and restart | `FinancialWriteFaultMatrixTests` injects rollback at ten ledger and six reservation write points; `FinancialCommitUncertaintyTests` and `CapacityFunctionActorIntegrationTests` suppress real PostgreSQL COMMIT acknowledgements; `FinancialProcessRestartTests` runs two independent OS processes followed by a fresh replay process |
| FIN-T06 original-operation and source replay | `GeneralLedgerPostingIntegrationTests`, `FinancialRealNatsActorTests`, process restart tests and retained inventory/manifest replay; changed hashes refuse new mutation |
| FIN-T07 periods, corrections and immutable history | `LedgerAccountingIntegrationTests`, `LedgerConfigurationIntegrationTests`, `FinancialQueryIntegrationTests.Database_rejects_posted_journal_extension_configuration_rewrite_and_period_overlap` |
| FIN-T08 transfer neutrality | `LedgerAccountingIntegrationTests.Transfer_is_portfolio_neutral_and_fees_and_settlement_have_the_expected_cash_sign`; atomic batch and ownership checks |
| FIN-T09 actual losses versus admission | Negative-cash settlement, subsequent denial and replenishment/reconciliation/authority-refresh tests in ledger accounting and posting integration |
| FIN-T10 lifecycle conservation | `CapacityReservationIntegrationTests`, `CapacityPositionCloseIntegrationTests`, lifecycle units and financial BDD scenarios; labelled accepted-execution, fill/cancel/fee/reconciliation facts |
| FIN-T11 replay is not renewed execution permission | Consumption source/expiry tests, unconsumed release replay, `CapacityExpiryIntegrationTests`, position-close replay and workflow recovery tests |
| FIN-T12 infrastructure and read recovery | Real NATS actors/processes, PostgreSQL stores, Scylla `FinancialHistoryIntegrationTests`, late-committing lower event recovery, receipt queries and financial UI recovery tests |
| FIN-T13 two capacity Functions and conventional Commands | Portfolio actor/route unit assertions, shared Function lifecycle regressions, Portfolio Command/BDD suites, NATS typed requests and Command history across multiple operations |
| FIN-T14 wire and hash compatibility | Portfolio `FinancialWireContractVerificationTests`, golden manifests, hash/culture units, exact Risk/Fund authorization tests; legacy CandidateSha256 meaning retained |
| FIN-T15 retained source cutover | `LegacyFinancialInventoryIntegrationTests` streams actual Scylla canonical records, rejects incomplete date coverage, fences new writes, seals immutable PostgreSQL retention, replays it and proves no book/capital mutation; `LegacyFinancialWriterFenceIntegrationTests` preserves/drains pending intents |
| FIN-T16 usable financial administration | Financial presentation tests and rendered STA WinForms tests: posting/configuration recovery, generated IDs, authority review, separate development capital, read-only source precision and account/rule version editors |
| FIN-T17 real five-stage calculation and financial handoff | Trade `FiveStageFinancialRuntimeTests`: 12 variants × Daily/Weekly/Monthly through real Regime Discovery, Market Condition, Trade Selection, Order Composition and Risk actors over NATS; actual persisted Risk events drive PostgreSQL reservation and exact fenced Fund authorization. Numeric market/configuration/funding setup is labelled. `RiskFinancialHandoffTests` and recovery integration qualify the saved Authorized intent and stable request identities. Consumption/execution-fact boundaries are separately tested; no broker submission is claimed |
| FIN-T18 export identity and corrections | `AccountingExportIntegrationTests` checks concurrent source inclusion, frozen payload, correction links, unknown/failed delivery and restart without reposting |

The five-stage matrix preserves actual upstream envelopes and hashes. It does not replace intermediate calculation outputs with desired decisions. Market inputs supply bullish, bearish or ranging observations and the applicable volatility history. The normal policies and freshness limits decide eligibility. Each case has a distinct workflow and Portfolio/Fund scope. Financial authorization is checked against the actual Risk event, original reservation completion, exact units and all separate hashes.

The OS-process harness lives only in the Portfolio integration-test assembly. Its actor persistence, PostgreSQL connections and NATS transport are real; mailbox scheduling, sequence allocation and publication are labelled test adapters. Separate tests cover durable projection recovery and notification outage. No application mutation/failure switch was added.

`FinancialWriteFaultMatrixTests` creates a uniquely named test trigger enabled only by a transaction-local setting and removes it in `finally`. The points cover transactions, journals, entries, balances, source and operation receipts, ledger posting receipts, reservation/usage, authority revision, event append and stream revision. `financial_history_receipt` is a post-commit projector acknowledgement, not an authoritative write; its recovery belongs to the Scylla/projector tests.

## Retention and operator boundary

The maintenance request must identify the exact existing PF-31 historical mapping and all canonical source dates. The service verifies membership and source versions, fences source events/writes, checks pending PostgreSQL and Scylla work, inventories original records and seals `RetainedReadOnly` with `CapitalRecognized=0`. Missing evidence never becomes a USD deposit. Pending work leaves the fence installed and prevents a seal. An unupgraded direct-Scylla writer must be stopped before an operational cutover; the service cannot fence code that bypasses its registered write boundary.

Portfolio Admin opens original transactions through the explicit historical source mapping in a read-only tab. Original precision, dates, descriptions and identifiers are retained; currency remains unrecorded. Historical Funds remain permanent Draft. New development Funds/books use separate generated identities and an explicit DevelopmentOpeningCapital operation. Reconciliation, fresh-source qualification and authority review are separate steps before spending.

The integration cutovers are generated test scopes. This report is implementation/qualification evidence, not a statement that an application's existing data has been cut over or that any development capital amount was entered.

## Corrections found during qualification

- RiskManagement was absent from the typed configuration publish/retire table dispatch; the lifecycle now addresses `risk_management_parameter_set` and real publication tests pass.
- Deployment publication now checks Risk policy horizon and product root/currency against the exact deployment.
- The full pipeline fixture had a missing trigger price and an Order Composition deadline beyond the upstream reservation expiry. Correct source input and deadline clamping preserve the actual calculations and contracts.
- Running schema-mutating suites concurrently caused test-database locks/deadlocks. The gate runner now executes these suites sequentially and passes the isolated broker explicitly to every test process. This is a qualification scheduling correction, not a relaxation of transaction timeouts.
- Competing test child processes initially repeated schema setup. The parent now prepares schema before starting the independent actors; child stderr is drained from startup so a failing worker cannot block diagnostics. The final 142-test infrastructure run includes the corrected process tests.

See [development qualification and recovery](Portfolio-Financial-Development-Qualification-v1.0.md) for the declared load budgets and operational recovery procedures, and [contract/storage/source manifests](Portfolio-Financial-Implementation-Manifests-v1.0.md) for the implementation inventory.
