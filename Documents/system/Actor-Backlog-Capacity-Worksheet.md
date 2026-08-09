# Actor Backlog Capacity Worksheet

**Work package:** SWO-02 Tranche A
**Status:** Measurement template active; production enforcement values are not approved
**Created:** 2026-08-09
**Last updated:** 2026-08-09
**Baseline commit:** `e990270d`

## 1. Purpose

This worksheet converts actor payload, queue, transport, and host-memory measurements into safe SWO-02 enforcement limits. It intentionally leaves production message and byte limits at zero while the API server runs in `ObserveOnly` mode. Zero means the dimension is observed but has no would-reject threshold; it must not be used with `Enforce`.

## 2. Current configured geometry

| Capacity | Current value | Source |
| --- | ---: | --- |
| Per-entity actor mailbox | 8,192 messages | `ActorRuntime:Admission:DefaultMailboxMessageLimit` |
| Retained idle entity queues per actor | 1,024 | `ActorRuntime:Admission:RetainedIdleMailboxesPerActor` |
| Actor workers on this host | 64 | `Environment.ProcessorCount * 2`, with 32 logical processors visible to the process |
| Actor worker batch size | 64 messages | `ActorThreadV2.MaxBatchSize` |
| Core NATS dispatcher count | 4 | `Nats:Consumer:DispatcherCount` |
| Core NATS capacity per stripe | 4,096 messages | `Nats:Consumer:DispatcherCapacity` |
| Core NATS subscription capacity | 16,384 messages | Derived as dispatcher count multiplied by stripe capacity |
| JetStream dispatcher count | 4 | `Nats:JetStreamConsumer:DispatcherCount` |
| JetStream capacity per stripe | 4,096 messages | `Nats:JetStreamConsumer:DispatcherCapacity` |
| JetStream `MaxAckPending` | 16,384 messages | Derived from dispatcher count and stripe capacity |
| JetStream requested maximum | 16,384 messages | Derived from the outstanding limit |
| JetStream refill threshold | 4,096 messages | Derived from one stripe capacity |

These are compatibility values, not evidence that they are appropriate production limits. In particular, an 8,192-message entity queue multiplied by many simultaneously active entities remains the aggregate risk addressed by SWO-02.

## 3. Admission benchmarks

### 3.1 Tranche A observe-only baseline

`ActorAdmissionBenchmarks` measures the same 256-operation scheduled enqueue/dequeue path with admission disabled and in observe-only mode.

Environment:

- BenchmarkDotNet 0.15.8;
- .NET SDK 10.0.302;
- .NET runtime 10.0.10, x64 RyuJIT;
- Windows 10.0.19045;
- 3 warmups and 8 measurement iterations; and
- memory and threading diagnosers.

| Mode | Mean | Error | Standard deviation | Allocated | Lock contentions |
| --- | ---: | ---: | ---: | ---: | ---: |
| Disabled | 84.710 ns/op | 0.127 ns | 0.056 ns | 0 B reported | 0 reported |
| ObserveOnly | 150.620 ns/op | 0.588 ns | 0.307 ns | 0 B reported | 0 reported |

Observe-only message/byte accounting adds **65.910 ns per mailbox operation** on this host. This is below the proposed 75 ns Tranche A threshold. At 100,000 mailbox operations per second, the isolated incremental cost extrapolates to about 6.59 milliseconds of one CPU core per second. That is a microbenchmark estimate and excludes NATS, serialization, handlers, storage, OpenTelemetry collection, and operating-system scheduling.

### 3.2 Tranche B enforced runtime

After structured admission, atomic rollback, and non-waiting mailbox enforcement were added, the final scheduled enqueue/dequeue measurement is:

| Mode | Mean | Increment over disabled | Allocated | Lock contentions |
| --- | ---: | ---: | ---: | ---: |
| Disabled | 89.37 ns/op | Baseline | 0 B reported | 0 reported |
| ObserveOnly | 178.11 ns/op | 88.74 ns/op | 0 B reported | 0 reported |
| Enforce | 146.75 ns/op | 57.38 ns/op | 0 B reported | 0 reported |

Enforced accepted admission remains below the 75 ns gate. Rejected reservations range from 4.424 ns for a full global message budget to 91.943 ns for a full entity mailbox including reserve and rollback. These results validate the mechanism, not the still-pending production capacity values.

Reproduce with:

```powershell
dotnet run --project TomasAI.IFM.Framework.Messaging.Nats.Benchmarks -c Release -- --filter "*ActorAdmissionBenchmarks*"
```

## 4. Measurements required from ObserveOnly

Complete this table separately for normal flow, market open, reconnect, and JetStream replay.

| Measurement | Normal | Market open | Reconnect | Replay |
| --- | ---: | ---: | ---: | ---: |
| Duration | Pending | Pending | Pending | Pending |
| Message rate by actor type | Pending | Pending | Pending | Pending |
| Payload p50 by actor type | Pending | Pending | Pending | Pending |
| Payload p95 by actor type | Pending | Pending | Pending | Pending |
| Payload p99 by actor type | Pending | Pending | Pending | Pending |
| Maximum payload by actor type | Pending | Pending | Pending | Pending |
| Maximum admission messages in use | Pending | Pending | Pending | Pending |
| Maximum admission bytes in use | Pending | Pending | Pending | Pending |
| Maximum mailbox depth | Pending | Pending | Pending | Pending |
| Maximum active mailboxes | Pending | Pending | Pending | Pending |
| p95/p99 queue wait | Pending | Pending | Pending | Pending |
| Sustained drain rate | Pending | Pending | Pending | Pending |
| Backlog drain time | Pending | Pending | Pending | Pending |
| Process working-set increase | Pending | Pending | Pending | Pending |
| Allocation and GC change | Pending | Pending | Pending | Pending |

The relevant Tranche A instruments are:

- `ifm.actor.admission.in_use`;
- `ifm.actor.admission.bytes_in_use`;
- `ifm.actor.admission.payload.size`;
- `ifm.actor.admission.would_reject`;
- existing mailbox, ready-queue, queue-wait, handler, and runtime instruments; and
- existing NATS receive, failure, and operation instruments.

## 5. Memory-budget inputs

| Input | Value | Approval/source |
| --- | ---: | --- |
| Physical/container memory available to actor host | Pending | Deployment configuration |
| Normal non-backlog working set | Pending | ObserveOnly baseline |
| Safety reserve for runtime/native/network/storage | Pending | Engineering approval |
| Maximum actor backlog memory budget | Pending | Available minus baseline and reserve |
| Measured queue-envelope cost | Pending | Retained-memory benchmark/profile |
| Measured empty entity-queue cost | Pending | Retained-memory benchmark/profile |
| Maximum registered actor count | Pending | Startup inventory |
| Maximum serialized payload accepted | Pending | Contract and NATS server configuration |

Do not substitute average payload size for the maximum-byte safety calculation. Percentiles are used for expected utilization and performance; configured maximum size is used for the upper bound.

## 6. Limit calculations

Let:

- `B` be the approved actor backlog payload-byte budget;
- `E` be measured queue-envelope bytes per message;
- `Pmax` be the configured maximum serialized payload bytes;
- `Q` be the global message limit;
- `W` be the actor worker count;
- `R` be retained idle queues per actor;
- `A` be registered actor count; and
- `Cempty` be measured empty entity-queue bytes.

The first candidate global limits must satisfy:

```text
GlobalByteLimit <= B
Q * (E + conservative payload charge) <= B
executing payload bound = W * Pmax
retained idle queue bound = A * R * Cempty
```

The full host budget must also include bounded Core subscription/stripe storage and JetStream outstanding/stripe storage. A message can move between those stages, so retained-memory profiling should determine whether a simple sum is excessively conservative. Safety takes precedence over maximizing the configured count.

Per-actor-type limits should be derived from observed traffic and failure consequences:

- Command and Query require enough capacity for timely request/reply processing.
- Event requires enough capacity for market-open and replay bursts but can use JetStream redelivery after enforcement.
- Notify and UI traffic must not consume capacity required for durable business work.
- Per-entity mailbox capacity must be materially below the global limit so one hot entity cannot monopolize the process.

## 7. Candidate values for review

| Setting | Candidate | Status |
| --- | ---: | --- |
| `GlobalMessageLimit` | Pending | Requires memory and traffic measurements |
| `GlobalByteLimit` | Pending | Requires approved actor backlog memory budget |
| `MaximumPayloadBytes` | Pending | Requires payload contract and NATS limit review |
| Command message/byte limits | Pending | Requires Command burst evidence |
| Query message/byte limits | Pending | Requires Query burst evidence |
| Event message/byte limits | Pending | Requires market-open and replay evidence |
| Notify message/byte limits | Pending | Requires traffic classification |
| UI message/byte limits | Pending | Requires traffic classification |
| Per-entity mailbox limit | 8,192 compatibility value | Must be reduced or approved from hot-entity evidence |
| Retained idle queues per actor | 1,024 compatibility value | Requires empty-queue footprint and actor-count evidence |
| JetStream NAK delay | 250 ms candidate | Tranche C validation required |
| Overload error code | `-429` candidate | Contract review required |

## 8. Approval gate

Tranche B must not enable enforcement until the reviewer approves:

1. the actor backlog memory budget;
2. global message and byte limits;
3. maximum payload size;
4. actor-type count and byte limits;
5. per-entity mailbox capacity;
6. retained idle queue count; and
7. the ObserveOnly evidence attached to this worksheet.

## 9. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 0.1 | 2026-08-09 | Recorded compatibility geometry, Tranche A admission benchmark, required ObserveOnly measurements, formulas, and pending enforcement values. |
| 0.2 | 2026-08-09 | Added Tranche B accepted and rejected runtime-enforcement benchmarks while retaining the pending production capacity gate. |
