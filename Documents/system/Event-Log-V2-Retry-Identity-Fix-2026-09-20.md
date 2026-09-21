# Retry identity correction and verification

## Outcome

The identical-retry failure reproduced by the isolated event-log load test is fixed. The candidate has not been cut over to production. No database schema or stored audit payload migration was needed.

## Implementation

- Added the opt-in `ICommandRetryIdentity` contract. Portfolio's generic command envelope (used by Portfolio, Fund and policy commands) removes only correlation ID and requested timestamp from its retry-comparison copy. Business payload, command ID, routing, entity, access principal and roles remain part of identity.
- `CommandAuditEnvelope` retains the complete original serialized payload and its SHA-256. A separate in-memory retry hash drives local duplicate coordination. Other command types retain their previous exact-byte identity.
- On a cold-cache duplicate, PostgreSQL audit handling reads the original payload, verifies its stored hash, verifies lossless decoding under the current command type, and compares normalized identities. Existing full-payload audit rows are not overwritten. The compatibility read is on the duplicate path, not the normal successful append path.
- Opted-in duplicates re-enter validation and actor authorization/committed-state handling. They no longer receive unconditional success from the base actor's duplicate shortcut. Non-opted-in commands keep their existing behavior.
- Missing, corrupt, unsupported or lossy audit evidence is not accepted as a matching normalized retry. Legacy JSON-only rows cannot establish this MessagePack retry identity and fail closed; no silent conversion was added.

The complete first audit envelope is retained; this change does not introduce a separate persisted audit record for every retry attempt.

## Verification

| Suite | Passed |
| --- | --- |
| Audit persistence and duplicate coordinator | 20 |
| Base actor audit/duplicate dispatch | 7 |
| Portfolio client envelope, operational guard and financial error mapping | 14 |
| Portfolio command actor integration fixtures | 4 |
| Live-host concurrent write/retry/conflict test | 1 |
| Fresh API-process retry test | 1 |

47 selected tests passed. Builds used for live-host tests completed with zero warnings/errors.

The concurrent test verified 256 committed creates, 512 simultaneous identical retries, 256 changed-payload rejections, and 256 exact event authorities/projections at concurrency 8. Every Portfolio retained business revision 1. Mixed command time was 3.468 seconds, or 295.296 requests/second; create latency p50 54.05 ms, p95 80.44 ms, p99 164.31 ms. This is a bounded correctness/load smoke, excludes identity allocation and final verification, and is not a sustained soak or before/after performance comparison.

After stopping the owned API process and launching a new one, all 256 saved business requests replayed successfully against durable audit rows, with every Portfolio still at revision 1. The fixture includes a synthetic manifest saved only after successful initial verification.

Focused regressions verify original audit bytes/hash survive reopening the writer, changed payload/principal/route/access roles remain distinguishable, same-window retries normalize correctly, corrupted audit bytes are rejected, and an opted-in duplicate cannot bypass revoked authorization in actor dispatch.

## Evidence

- `BenchmarkDotNet.Artifacts/event-log-v2/retry-fix-20260920/retry-audit-final.trx`
- `BenchmarkDotNet.Artifacts/event-log-v2/retry-fix-20260920/retry-actor.trx`
- `BenchmarkDotNet.Artifacts/event-log-v2/retry-fix-20260920/retry-portfolio.trx`
- `BenchmarkDotNet.Artifacts/event-log-v2/retry-fix-20260920/retry-domain-actors.trx`
- `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/Load-20260920015918.trx`
- `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/LoadRestart-20260920020006.trx`

Re-run `Invoke-IsolatedAcceptance.ps1 -RunId <run> -Action Run -AcceptanceSuite Load`, then `LoadRestart`. Load allocates fresh identities; LoadRestart requires the manifest created by the successful Load run. The launcher stops its owned API child afterward. Synthetic stores and reports are retained.

## Remaining qualification

Follow-up: [post-fix paired benchmark](Event-Log-V2-Post-Retry-Paired-Benchmark-2026-09-20.md) passed four retained-history samples and reproduced a 3.35% paired throughput benefit and approximately 5.1% lower WAL/event for three indexes. The backed-up acceptance database was restored and all 256 cold-cache API retries passed again. These short windows do not close the longer-duration or full-workflow gates.

The database still has exactly the three candidate indexes and `financial_legacy_event_fence`. Full current-policy trading workflow, sustained-load/paired performance testing, broader recovery and UI acceptance remain outstanding. The replay fix does not qualify the original fully stripped-down schema or authorize a production migration.
