# Securities symbol projections

The Securities store uses the explicit `futures_contract_by_symbol_v2` and
`futures_option_contract_by_symbol_v2` query tables instead of `ALLOW
FILTERING`. The contract-ID tables remain canonical. Application writes
maintain both forms, and a repair or backfill can be repeated safely.

Projection rows do not prove that a projection is complete. A partially
populated, non-empty V2 partition must never hide canonical rows. Read cutover
is authorized only by one of these fresh state tables:

- `securities_projection_state_v3` records completion for an entire projection.
- `securities_symbol_projection_state_v3` records completion for one symbol,
  including a known-empty symbol. Its composite partition key keeps independent
  symbols off a shared hot partition.

V3 is intentional. Older boolean-only state tables can already exist, and
`CREATE TABLE IF NOT EXISTS` cannot evolve them to the concurrency-safe shape.
Each V3 row has a generation, a completion flag, and a set of active operation
IDs.

## Failure and concurrency protocol

Before changing projection state or data, a writer journals its operation in
`securities_projection_operation_v3` in an inert preparation phase and records
every global/symbol scope in `securities_projection_operation_scope_v3`.
The scope journal also records the expected scope count so an incomplete or
stale replica read cannot silently clear only part of an operation. Neither
table has a TTL. Only after all scopes are durable does the writer
conditionally mark the journal as potentially active, then change the
generation, add its operation ID, and set completion to
false globally and for every affected symbol. It publishes completion with a
conditional update only when the generation is unchanged and its ID is the
sole active operation. State IDs are removed before the journal returns to its
inert phase and before journal rows are deleted during normal cleanup, so an
interrupted preparation or cleanup can be classified and replayed. A partial failure or a
competing mutation therefore leaves the projection incomplete.

A reader trusts a completion marker, reads the V2 partition, and then reads the
same marker again. If completion or generation changed during the data read, it
discards that result and uses the canonical fallback. The protocol uses Scylla
state and lightweight transactions; it has no process-local semaphore or lock.

When no valid completion marker exists, fallback streams the canonical table,
retains only the requested symbol, resets that entire target partition,
repopulates it, and verifies the exact key set. Streaming avoids materializing
unrelated rows and reduces migration-time GC pressure. Only then can fallback
conditionally publish per-symbol completion. An empty result is also recorded,
so a successfully repaired unknown symbol does not repeatedly full-scan.

## Existing-data cutover

1. Create both V2 query tables, both V3 completion-state tables, and both V3
   operation-journal tables through `SecuritiesSchemaDb`.
2. Prefer quiescing Securities writes. If that is not possible, capture and
   replay mutations made during the backfill window; concurrent operations are
   detected and prevent completion from being published.
3. Run `BackfillSymbolProjectionsAsync`. It disables global completion before
   validation, then verifies that each canonical contract ID maps to one
   unambiguous key. It inventories source and target symbols, invalidates their
   per-symbol completion, deletes every relevant target partition (including
   target-only stale or wrong-symbol partitions), and streams canonical rows
   back in bounded batches.
4. The backfill runs exact-key reconciliation. Require `IsConsistent`, including
   zero missing and zero unexpected keys for both projections. Completion is
   published last and only if no mutation or repair raced the operation.
5. If backfill, reconciliation, or conditional completion fails, keep the
   canonical fallback enabled, resolve the reported ambiguity or competing
   writer, and rerun the backfill.
6. Replay captured mutations when applicable, reconcile again, and retain the
   canonical tables for at least one rollback and backup window.

An operation left by process death is not cleared based on age automatically.
After draining every Securities projection writer and verifying that an old
process cannot resume, an operator may pass an explicit UTC
`staleOperationCutoffUtc` to `BackfillSymbolProjectionsAsync`. The backfill
removes only operation IDs at or before that instant from only their
journaled global/symbol scopes, using idempotent collection-element deletes,
then deletes those exact journal rows. Leaving the argument null performs no
stale recovery. A cutoff is not a lease: using it while an old writer can
resume is unsafe.

Backfill is idempotent. A cancellation or partial write never authorizes V2
reads because completion remains false until a later exact reconciliation wins
the conditional cutover.
