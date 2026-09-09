# Portfolio financial development qualification

This record concerns the local Windows/.NET 10 development workstation and its local PostgreSQL test database. It is not a production capacity estimate. The broker emulator and production security remain separate deliveries.

## Load budget declared before execution

Run the actual GeneralLedgerStore/CapacityReservationStore, shared PostgresEventTransaction, domain calculations, immutable journals/receipts/events and independent balance queries. Setup is excluded from timing. All scopes are newly generated test scopes; preserve their audit records.

- Single posting: 40 sequential operations, p95 <= 1,000 ms, p99 <= 2,000 ms.
- Independent Portfolios: eight workers, eight Portfolios, eight postings per worker, p95 <= 1,000 ms and p99 <= 2,000 ms.
- Shared Portfolio admission: 16 concurrent reservations against the same revision; exactly one commits, all others have a confirmed revision conflict, and capacity/cash remain conserved. p99 <= 2,000 ms.
- Maximum batch: five batches of 100 two-line journals, p99 <= 2,000 ms. The 256-line journal limit is a separate payload/accounting correctness case; 100 maximum-line journals may exceed the whole-command byte limit and must not be assumed admissible.
- No unexpected exception, unknown commit, lock timeout or statement timeout. Confirmed deadlock/serialization retry counts must be reported.
- Report elapsed time, operations/second, p50/p95/p99, process-wide allocated bytes per operation, managed heap/working set before and after, and successful authority-lock acquisition durations. Process-wide allocation includes runtime/test overhead; it is not thread-local allocation or a BenchmarkDotNet microbenchmark. Small maximum-batch samples do not establish a stable production tail distribution.
- Allocation budget: <= 32 MiB per single operation, <= 128 MiB per 100-journal batch. These conservative development regression ceilings do not justify allocation optimization claims.

`FinancialLoadQualificationTests` writes its machine/runtime and measured workload records to TestResults/financial-load under the integration test output directory. Test output records the same data. An assertion failure is an unmet budget, not a reason to change financial invariants or silently raise the budget.

## Operator recovery

1. For an uncertain mutation, keep its original OperationId, request body and input hash. Use Pending in Ledger administration or the transaction dialog to reconcile the original receipt. Queue acknowledgment is not a commit. Never create a replacement identity because the response was lost.
2. PostgreSQL is authoritative. If it is unavailable, financial reads/actions remain unavailable; do not use legacy Fund balances or Scylla history as cash authority. After restoration, resolve original receipts before attempting new operations.
3. Scylla history may lag a committed receipt. The history recovery service replays committed PostgreSQL events and acknowledges successful projection. Rebuild/repair must not execute the original financial mutation again.
4. Overdrawn and NeedsReconciliation books cannot admit new spending. Record actual financial facts through their normal commands, reconcile journals against balances, then prepare fresh authority from current policy, mandates, assignments and envelopes. An old approval cannot restore capacity.
5. Unconsumed reservations may expire through the durable expiry dispatcher. Consumed, working, filled or unknown execution obligations require matching execution/reconciliation facts; never clear them manually to make cash appear available.
6. Book setup creates no money and enables no spending. Development opening capital is a separate explicit operation on an unqualified Emulator book with Development host policy. Reconcile, qualify the source scope, and review authority separately. Qualification does not publish a policy/deployment or create an active Fund.
7. Fresh-scope qualification installs the legacy writer fence before testing absence. If legacy state exists, stop that qualification and retain the fence/evidence. Do not remove the fence or relabel old balances to bypass migration. Historical scope migration requires the selected migration mode and qualified source disposition.
8. Authority refresh preserves qualified book membership. Adding a Fund to a Portfolio does not qualify it for that book. Do not edit the stored book JSON; the separate membership/migration workflow is still required.
9. Internal accounting exports retain the original immutable payload and source inclusion after an unknown delivery. Corrections are new linked journals. No QuickBooks delivery is implemented or claimed.

## Diagnostics

Meter `TomasAI.IFM.PortfolioFinancial` exposes transaction attempt duration/outcome, confirmed rollback retries, and successful authority-lock acquisition duration. Outcome labels are bounded: committed, rolled_back_retry, unknown, cancelled, lock_timeout, statement_timeout, failed. Metrics contain no Portfolio/Fund IDs, caller names, SQL or financial payloads. Transaction metrics include reads and schema operations as well as writes; a committed read is not a financial posting. Histogram buckets/export configuration belong to the deployment telemetry setup. Inspect authoritative receipt/history/expiry journals for operation-specific recovery; metrics do not authorize mutations.

Remaining gate evidence is tracked in the main implementation plan. This document alone does not close PF-FIN-07.


## Executed local load results - 2026-09-09 UTC

All four cases passed. Environment: Windows 10.0.19045, .NET 10.0.10, 32 logical processors, local PostgreSQL test database. Setup and independent post-run reconciliation are outside timed work. Uncommitted working tree based on `fd066b9b93aaf12a208bf8c0d2f5bfc18ccba26e`; no optimization comparison is claimed.

| Workload | Attempts / commits | Elapsed ms | p50 ms | p95 ms | p99 ms | Attempts/s | Process allocated bytes/op | Max lock acquisition ms |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| single | 40 / 40 | 1091.5 | 26.2 | 32.5 | 38.5 | 36.6 | 191,428 | 1.3 |
| independent | 64 / 64 | 277.6 | 31.9 | 41.1 | 42.4 | 230.5 | 187,522 | 1.4 |
| shared-admission | 16 / 1 | 117.0 | 99.0 | 112.3 | 112.3 | 136.7 | 151,395 | 62.2 |
| batch100 | 5 / 5 | 4579.4 | 877.2 | 1042.0 | 1042.0 | 1.1 | 12,854,464 | 1.3 |

Shared admission includes 15 expected confirmed revision conflicts; its attempts/second is not successful reservations/second. Each batch100 operation contains 100 two-line journals (500 journals total); 1.09 batches/s is approximately 109 journals/s. Every workload independently reconstructed balanced debit/credit totals and expected available cash. Confirmed rollback retries, unknown commits, cancellations and lock/statement timeouts were zero. Heap/working-set samples and transaction outcome counts are retained in each JSON artifact.

Evidence: `TestResults/financial-load/basil_DEV-SERVER_2026-09-08_22_03_09_net10.0.trx` relative to repository root. Machine records: integration project `bin/Debug/net10.0/TestResults/financial-load/*.json`. Reproduce with:

```powershell
dotnet test TomasAI.IFM.Domain.Portfolio.IntegrationTests/TomasAI.IFM.Domain.Portfolio.IntegrationTests.csproj --no-restore -m:1 --filter Category=PortfolioFinancialLoad --logger trx --results-directory TestResults/financial-load
```


Additional correctness qualification: a 256-line adjustment committed within the two-second deadline and independently reconstructed exactly 128 USD debit and 128 USD credit. A 257-line request had no new receipt or balance/revision effect. One hundred individually bounded journals with large line references were rejected by the whole-command uncompressed 1 MiB limit, even though transport compression can make repeated data small. A deliberately throwing metric listener did not change the committed receipt or the 100 USD balance. These cases are included in the 116-test final financial integration run; they are not additional load-percentile samples.

## Retained legacy history maintenance - 2026-09-09

Selected mode: `ReadOnlyHistoryWithDevelopmentCapital`. Original source records remain in FundLegacyDb and are displayed through the explicit PF-31 HistoricalSourceFundId mapping. Portfolio Admin Financials includes a read-only Legacy history tab for those mappings. The displayed original amount/balance keeps source precision and is labelled with unrecorded currency. These values never enter available cash, capacity or a new journal. Unknown kinds/invalid identities remain visibly unqualified.

The development maintenance entry point is `--retain-legacy-history-manifest=<reviewed-json-path>` on the API executable, run from the API project directory with ASPNETCORE_ENVIRONMENT=Development. It starts no actors, feeds or HTTP listener. Its JSON is a LegacyFinancialRetentionRequest containing Scope, Access and Reason. Scope carries a generated stable InventoryId, the existing SourceFundId and exact permanent-Draft destination Portfolio/Fund mapping, Start/End covering all canonical source dates, SourceEnvironment and ImportMode=ReadOnlyHistoryWithDevelopmentCapital. Use the generated mapping identities from the existing historical import; do not repurpose a current trading Fund or infer a mapping from equal numbers.

Retention validates the committed historical mapping, installs the source writer fence, checks PostgreSQL pending write intents plus Scylla write ownership/mutation and in-range projection journals, and streams original canonical records into immutable inventory rows. It verifies full date coverage before preparation and after the source scan. A successful seal records RetainedReadOnly in the immutable migration manifest with zero recognized capital and no financial book change. Repeating the original request returns the original seal even after unrelated configuration advances. A different request cannot replace that identity.

Pending/unknown writes prevent the seal and leave the fence installed. Recover the original writes using their evidence before retry; never delete pending markers to force success. A source range that changes during fencing requires operator investigation; no partial inventory is represented as complete. An unupgraded process bypassing the registered FundDb writer boundary must be stopped before cutover. No application source has been retained or funded automatically by the qualification tests.

Development capital remains an explicit new-ledger operation on an unqualified Emulator book. Reconcile and qualify the new book and separately review authority before enabling spending. The retained legacy mapping remains Draft and is not that spending Fund.

## Final development qualification — 2026-09-09

PF-FIN-01 through PF-FIN-07 are complete for the owner's current development scope. The [gate evidence report](Portfolio-Financial-Gate-Evidence-2026-09-09.md) maps FIN-T01–18 to concrete tests and records the final sequential run: 1,511 passed, no failures/skips, rendered UI, clean API build and verified Development startup. Use `scripts/Test-PortfolioFinancialGates.ps1` with an isolated local NATS URL to reproduce it. Do not run schema-mutating test projects concurrently against these shared test databases.

The final load rerun used the previously declared budgets and independently reconciled committed totals:

| Workload | Attempts / commits | Elapsed ms | p50 ms | p95 ms | p99 ms | Attempts/s | Allocated bytes/attempt | Max lock acquisition ms |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| single | 40 / 40 | 1013.2 | 24.4 | 28.7 | 39.6 | 39.5 | 191,388 | 1.3 |
| independent | 64 / 64 | 275.5 | 31.7 | 44.0 | 45.4 | 232.3 | 187,882 | 1.3 |
| shared-admission | 16 / 1 | 113.6 | 95.8 | 109.0 | 109.0 | 140.8 | 153,600 | 68.0 |
| batch100 | 5 / 5 | 4517.0 | 895.7 | 1020.5 | 1020.5 | 1.1 | 12,880,115 | 1.1 |

Shared admission produced 15 expected revision conflicts. There were zero confirmed-rollback retries, unknown outcomes or lock/statement timeouts in the timed workloads. These are local measurements, not production SLAs. Detailed heap/working-set and transaction outcomes remain in the load TRX/JSON artifacts. Operational application cutover, entry of an actual development capital amount and all external broker/accounting delivery remain separate actions; qualification changed generated test scopes only.
