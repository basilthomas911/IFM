# Messaging.Nats performance work

The top ten findings from the performance review and their implementation status are below, in priority order.

| Priority | Improvement | Result |
|---:|---|---|
| 1 | Share one process-level NATS connection and JetStream context | Implemented with `NatsConnectionManager`; both ActorIntegrationTests and Api.Server register the same singleton. |
| 2 | Prevent silent subscriber drops and make backpressure explicit | Implemented with bounded `Wait` channels at NATS ingress and striped dispatch. |
| 3 | Use structured async lifecycle and drain before shutdown | Implemented: subscription/dispatcher tasks are owned and awaited, lifecycle calls are serialized, JetStream uses drain-on-cancel, and the shared connection drains on host disposal. |
| 4 | Remove payload copies | Implemented for outbound messages and the command inbound path. Commands now retain NATS pooled memory through striped dispatch and the actor mailbox, deserialize directly from that memory, and release it immediately after parsing. Queries and events remain on the legacy `byte[]` adapter until their staged migrations. |
| 5 | Tune JetStream flow control and acknowledgement ordering | Implemented with explicit `MaxMsgs`, thresholds, `MaxAckPending`, drain behavior, and ACK only after mailbox delivery and event routing. |
| 6 | Replace JSON-within-JSON durable replay envelopes | Implemented with compressed MessagePack envelopes and runtime payloads; legacy JSON envelopes remain readable. |
| 7 | Correct and consolidate SPSC/ring-buffer behavior | Implemented: monotonic indices, exact-capacity accounting, correct semaphore use, cancellation, disposal, and waiter-aware signaling. |
| 8 | Remove hot reflection, global clear contention, and per-message information logs | Implemented with cached compiled delegates, bounded FIFO duplicate eviction, verb hash sets, and debug-level hot-path logging. |
| 9 | Upgrade NATS.Net | Upgraded from 2.6.11 to 3.0.1, including the newer drain and backpressure behavior. |
| 10 | Add performance gates and host runtime tuning | Implemented with BenchmarkDotNet memory/threading diagnostics, checked-in before/after results, and server GC in both production-like hosts. |

The command stage of the inbound owned-memory migration is complete. Ownership transfers exactly once from the NATS command consumer to striped dispatch, then to the actor mailbox. A rejected handoff disposes at the sender; accepted messages are disposed by the actor thread, and the command actor releases the payload as soon as typed deserialization finishes. Queue shutdown also drains and disposes pending messages.

Queries are the next stage. Events remain last because routed event delivery can fan out to multiple mailboxes and therefore needs an explicit shared/ref-counted ownership design rather than a single-owner transfer.
