# Local Workstation Database Backup and Restore Phase 1 Validation Report

**Gate:** 1 - Durable JetStream event listener

**Status:** Passed

**Date:** 2026-08-12

## Scope completed

- Added `IJSActorEventListener : IActorEventListener` so Core NATS and durable JetStream listeners can be injected
  independently in the same process.
- Added strongly typed JetStream listener options for stream, durable prefix, subject filtering, delivery policy,
  acknowledgement timing, redelivery limits, bounded dispatch, pull-window limits, and NAK delay.
- Implemented `NatsJetStreamEventListener` with:
  - one stable explicit-ack durable consumer per listener/mailbox;
  - existing-stream discovery before configured-stream creation or additive subject update;
  - no implicit stream deletion or replacement to resolve subject overlap;
  - bounded striped dispatch with `MaxAckPending` aligned to admitted capacity;
  - handler invocation only for configured verbs;
  - acknowledgement only after successful handler completion;
  - delayed negative acknowledgement on handler/admission failure;
  - durable restart recovery and deterministic draining of admitted work on stop; and
  - received, failure, redelivery, and pending-delivery metrics.
- Added `AddNatsActorEventListeners` registrations:
  - `IActorEventListener` resolves `NatsActorEventListener`;
  - `IJSActorEventListener` resolves `NatsJetStreamEventListener`.

No DatabaseBackup actor contracts, journals, native database tools, host behavior, or backup/restore operation was
implemented in Phase 1.

## Validation evidence

### Framework messaging unit tests

```text
dotnet test TomasAI.IFM.Framework.Messaging.Nats.UnitTests/
  TomasAI.IFM.Framework.Messaging.Nats.UnitTests.csproj --no-restore

Passed: 65
Failed: 0
Skipped: 0
```

The added tests verify inheritance, independent DI resolution, derived bounded limits, invalid limits, stable durable
names, and pre-connection request validation.

### Real-NATS listener integration tests

```text
dotnet test TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests/
  TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests.csproj --no-restore \
  --filter FullyQualifiedName~NatsJetStreamEventListenerIntegrationTests

Passed: 5
Failed: 0
Skipped: 0
```

The five tests exercise explicit acknowledgement and verb filtering, delayed NAK redelivery, durable restart
recovery, stop/drain behavior, and bounded `MaxAckPending` behavior against the running NATS JetStream server.

### Complete framework messaging integration suite

```text
dotnet test TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests/
  TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests.csproj --no-restore

Passed: 51
Failed: 0
Skipped: 0
```

One parallel diagnostic run initially timed out in the pre-existing concurrent SPSC ring-buffer test. An immediate
serial rerun passed all 51 tests; the bounded Gate 1 listener suite had already passed independently.

### Full solution

```text
dotnet build TomasAI.IFM.sln --no-restore --configuration Debug

Build succeeded.
0 Warning(s)
0 Error(s)
```

Elapsed time was 3 minutes 53 seconds.

## Gate result

Gate 1 passed because:

- durable success is acknowledged only after the handler completes;
- failure is negatively acknowledged and redelivered;
- an unacknowledged delivery resumes under the same stable durable consumer after listener restart;
- admitted work drains before stop returns, while the pull window prevents excess admission;
- existing overlapping streams are reused without deletion;
- Core and JetStream listeners resolve independently; and
- all targeted and complete Framework.Messaging.Nats unit/integration suites pass; and
- the full solution compiles with zero warnings and errors.

Phase 2 (shared DatabaseBackup contracts and typed client API) remains intentionally unstarted pending review.
