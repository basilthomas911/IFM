# Qualification gate fixes and verification

The two integration failures from the combined campaign are corrected in source and passed targeted reruns. Production services, database schema and optimized-writer activation remain unchanged.

## Subscription readiness

NatsActorConsumer previously launched its asynchronous subscription loop and returned from startup before the broker necessarily received SUB. Immediate requests could receive No responders.

All five owned/legacy command, query and realtime subscription paths now create an explicit subscription, await PONG on the same connection, publish readiness, and then consume its channel. StartAsync awaits that readiness with the caller's cancellation token. Startup errors/cancellation propagate; cleanup cancels and disposes the loop/subscription and dispatchers. A faulted loop is logged without preventing the remaining shutdown cleanup. No startup sleeps, request retries or polling were added.

The real actor tests now cover both owned and legacy command payload paths, original/batched writers and normal/omitted first handoff. Each case recreates its runtime and tests immediate requests, duplicates, changed-content rejection, continuation after restart, stale versions and projection completion.

Real broker integration directly exercises both command payload paths. The query/realtime paths share the helper and have messaging unit/routing regression coverage; this campaign did not add exhaustive live query/realtime startup tests or throughput measurements for the new helper.

## Financial error contract

FinancialCommandFailure now maps the explicit CommandAuditPayloadConflictException to FinancialReasons.RequestMismatch (34114). It previously fell through to PersistenceFailed (34118). Other persistence failures keep their existing mapping, and unknown-commit handling is unchanged.

This does not turn conflicts into success, bypass audit checks or retry uncertain financial commits. The previously failing financial integration test now reaches and passes its final receipt and cash-balance assertions after the rejected changed-content request.

## Verification

| Final suite | Passed |
|---|---:|
| Expanded candidate actor/JetStream/projector/guards | 51 |
| Expanded four-index control actor/JetStream/projector/guards | 51 |
| Candidate financial messaging/process/ledger/recovery/fence | 23 |
| Four-index financial control | 23 |
| Focused financial error mapping unit tests | 2 |
| Full messaging unit regression rerun | 110 |
| Total final suite executions | 260 |

Zero failures/skips in those final runs. An earlier post-fix candidate suite also passed 47/47 before legacy-payload coverage was added.

The initial full messaging unit run passed 109 and failed one existing serializer allocation-budget assertion (232 allocated bytes versus a 128-byte limit). An isolated full rerun passed 110/110 without modifying the serializer or weakening the assertion. Both reports are retained; this is an observed intermittent allocation-test failure, not a claim that it is permanently resolved.

Release builds passed with zero warnings/errors. No benchmark speedup is claimed for these fixes.

## Remaining acceptance boundary

Integration gates exercised here now pass. The prior 262,144-event migration rehearsal remains 22/22 passed; it was not rerun because these changes do not modify migration SQL or persistence layout.

The full API/UI host acceptance stage is still not completed. Existing PortfolioLiveHostEndToEndTests require an actual API host and default to the normal broker endpoint; they were not pointed at production. An isolated full-host configuration with all stores and candidate routing is required before those tests or application acceptance can be called qualified. These integration fixtures are not a substitute for that host.

The candidate remains the three-index, durable-audit/fenced layout, not the original stripped-down schema. No production promotion is approved by this report.

## Artifacts and cleanup

BenchmarkDotNet.Artifacts/event-log-v2/gate-fixes-20260920 contains:

- candidate-1.trx (47)
- candidate-expanded.trx (51)
- control-expanded.trx (51)
- financial-fixed.trx (23)
- financial-control-fixed.trx (23)
- financial-mapping.trx (2)
- nats-regression.trx (initial 109 passed / 1 failed)
- nats-regression-repeat.trx (110)

The three inventoried disposable fixture databases were removed after tests. The owned ifm-schema-fixes-20260920 JetStream container and synthetic stream data were removed; the benchmark PostgreSQL container was stopped. Test reports remain; synthetic database/broker data has no backup.
