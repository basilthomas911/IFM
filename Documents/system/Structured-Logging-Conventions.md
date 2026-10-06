# Structured logging conventions

## Contract

All selected operation boundaries emit Component, Method, named scalar arguments or a bounded Arguments summary, Outcome, numeric ElapsedMilliseconds, and applicable Subject/CommandId/EventId/OperationId/EntityId. Keep event IDs stable. Use business names. Never serialize context/state/service objects, connection strings, tokens or entire tick/chain collections. Record state revision, item count, collection identity and selected values instead.

Method is the business caller, supplied by nameof or CallerMemberName, never reflection/stack inspection. Cancellation is a distinct outcome. A completion record describes the completed stage; dispatch acceptance is not a fill or financial commit.

## Performance

Use LoggerMessage compiled helpers. Check IsEnabled before constructing summaries, performing ToString/join, or allocating scopes. Preserve actor InformationLogging suppression. Routine realtime/tick processing uses counters/histograms, existing sampling and slow thresholds; do not add entry/exit logs to each accumulator or state.Apply. Report slow stages with queue wait separate from handler/database/publication time. Repeated overloads must be summarized with counts without suppressing the underlying rejection metric.

Operational log delivery is bounded/nonblocking and cannot replace the authoritative command audit or financial journal. Never wait for a remote collector during trading. Critical financial decisions remain in durable business records.

## Correlation and export

Use existing ActorTrace W3C NATS headers. Capture ActivityContext explicitly before queue/worker handoff and restore a processing Activity around worker execution. Keep the Activity alive through failure and completion logging. Business IDs remain independent of trace/span IDs.

API file logs are newline-delimited Serilog JSON. Telemetry:Logs enables a single Serilog-to-OpenTelemetry bridge; do not also register another application ILogger OTLP provider. OTLP uses a bounded batch queue (4096 records, batches 256, scheduled 1s), configurable absolute endpoint and protocol. HTTP/protobuf can target a Collector logs endpoint or GreptimeDB's documented OTLP logs endpoint; supply appropriate headers via deployment secrets. Export stays disabled until the destination is explicitly configured. Existing independent fatal recovery export remains available.

Queryable log attributes must be mapped in the Collector/Greptime pipeline. IDs are log attributes, never metric labels. service.name identifies the host; it is not an order/entity ID. Enable `Telemetry:Traces:Enabled` when spans are required; metrics do not implicitly enable trace export.

## Selected boundaries

Transport: admission rejection, malformed subject, ACK/NAK failure, durable cursor recovery and purge. Actor runtime: failure, business rejection, slow completion, configured routine command/query completion. Feed: EOD queue wait/processing, projector stages, replay readiness and recovery transitions. Trade: workflow decisions, broker place/update/cancel, observations and fills. Pricing: bulk quote-query timing, cache/source selection and lease changes. Operations: startup/warmup/shutdown/job/backup stage summaries. Do not log each UI keystroke, quote or tick.

## Related conventions

Follow [actor implementation conventions](Actor-Implementation-Conventions.md) and [event modeling conventions](Actor-Event-Modeling-Conventions.md). Logging does not move business decisions or mutation into actors.

## Collector rollout

The local Docker deployment is configured in [Docker/GreptimeDb](../../Docker/GreptimeDb/README.md), with five-day log and thirty-day metric retention. Development hosts enable export; other deployments should keep `Telemetry:Logs:Enabled` false until their Collector is running. For HTTP/protobuf set `Telemetry:Logs:OtlpEndpoint` to the Collector logs URL, for example `http://localhost:4318/v1/logs`, and `Telemetry:Logs:OtlpProtocol` to `http/protobuf`. Set a distinct service name for API and UI. Authentication headers belong in deployment configuration, never source control.

API and UI file sinks use nonblocking queues of 4096 records. Observe `logging.file.pending`, `logging.file.capacity`, and `logging.file.dropped` through the `TomasAI.IFM.Logging` meter. OTLP export also has a bounded SDK queue; SDK diagnostic reporting is needed to diagnose exporter drops and connectivity. An unavailable Collector does not stop trading; logging is diagnostic, not durable audit storage.

Local verification covers a real HTTP/protobuf receiver, scalar attribute types, native trace/span correlation, severity, invalid configuration, overload suppression, file-buffer metrics, and zero allocations for disabled compiled logging. This verifies OTLP serialization locally; it does not assert a deployed Collector or GreptimeDB ingestion.

