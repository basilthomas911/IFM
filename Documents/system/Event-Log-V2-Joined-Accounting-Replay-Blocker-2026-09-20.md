# Joined accounting qualification: confirmed replay blocker

## Status

**Update:** this reproduced defect is now fixed in source and verified with expanded joined/storage tests. See [Durable accounting replay fix](Event-Log-V2-Durable-Accounting-Replay-Fix-2026-09-20.md). Production cutover and the remaining broader acceptance gates are still outstanding. The original failure evidence and analysis below are retained historically.

**Cutover remains blocked.** Two newly added joined tests fail reproducibly at duplicate delivery, after the first posting succeeds. No production accounting code, safety check or schema was changed for this increment.

The earlier component gates remain passed; they did not exercise this joined boundary.

## New coverage

BrokerAccountingRuntimeTests in Domain.Trade.IntegratedTests runs the isolated actor host with the real Portfolio actor assembly enabled. It creates a labelled synthetic financial book, funds it through the actual General Ledger command actor, and calls the production IPortfolioTradeAccountingApi with completed execution evidence.

Cases:

1. Fully filled opening execution.
2. Cancelled opening execution with one confirmed fill out of two ordered units.

Both traverse BrokerExecutionAccountingApi -> IActorService/NATS -> General Ledger command actor -> PostgreSQL transaction/receipt/event persistence. Financial queries then verify the durable balance and revision.

This test starts from constructed, internally consistent execution evidence. It does **not** yet obtain that evidence from live emulator callbacks, test a closing execution, or perform a process restart. Those remain later joined acceptance work.

The test requires EventLogEngineQualification's exact owned-fixture routing. PostgreSQL remains ifm_eventlog_bench_092020260032_synthetic_host on 127.0.0.1:25432; NATS, Redis and Scylla remain on their isolated ports. No live broker or feed was used.

## Verified failure

Reports under BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/emulator-qualification/:

- joined-accounting-initial.trx: 0/2; first postings succeeded, identical retries failed.
- joined-accounting-confirmed.trx: 0/2; additionally asserts unchanged cash and revision after the failed retry before reporting failure.

Final synthetic portfolios: Filled = 317028239; Cancelled = 849268894.

For each case:

- Funding: 10,000.00.
- Confirmed settlement: 1 x 100 x 50 = 5,000.00.
- Separate commission: 2.50.
- First posting succeeds, cash becomes 4,997.50, financial revision becomes 2.
- Identical execution, source event and confirmation timestamp are delivered again.
- Response fails: Financial command ID is already associated with different request content.
- Cash remains 4,997.50 and revision remains 2. The retry did not double-post.

Build succeeded with the existing four generated MessagePack resolver type-conflict warnings.

## Cause

BrokerExecutionAccountingApi derives stable command/operation identities from execution.Id, but queries the **current** posting configuration and uses selected.FinancialRevision to rebuild PostFundTransactionsCommand on every delivery.

FinancialCanonicalHash.Request deliberately includes ExpectedFinancialRevision, RequestedAtUtc, ExpiresAtUtc, Body and Principal. The first command expects revision 1; after it commits, reconstruction expects revision 2 under the same command identity. The changed hash correctly causes the financial command identity guard to reject it.

This is an application replay-construction defect, not evidence that the three-index event-log layout loses events. The exact same source event/time was reused in the test, so changing confirmation metadata is not needed to reproduce it.

## Required remediation before qualification can proceed

Preserve an immutable posting intent/command for each execution accounting operation using durable, atomically claimed state, then retry that original command rather than reconstructing it from current revision/rules.

The design must:

1. Freeze the original selected rules, expected revision, canonical body, timestamps and command identity.
2. Validate incoming execution economics against the original evidence; changed fill quantity, price, commission or identity must remain a conflict.
3. Survive concurrent duplicate delivery, ambiguous acknowledgement and process restart.
4. Preserve authorization checks and optimistic concurrency on genuinely new operations.
5. Avoid treating an existing operation ID alone as proof that changed incoming evidence is equivalent.
6. Avoid globally removing ExpectedFinancialRevision from the financial hash or assigning a fresh command ID to every retry.
7. Reuse an existing suitable durable command/audit/outbox record if its atomicity and retention guarantees support this contract; do not introduce a new schema before checking that option.

After remediation, rerun both cases, then add changed-evidence rejection, concurrent duplicate delivery, intervening unrelated ledger postings, host restart and closing-basis coverage. The broader adapter-callback-to-ledger and UI gates remain outstanding.

No production remediation has been applied yet. The failing tests are intentionally retained as regression evidence.
