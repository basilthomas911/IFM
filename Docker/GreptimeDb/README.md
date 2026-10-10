# IFM GreptimeDB and OpenTelemetry Collector

## Start and stop

From the repository root:

```powershell
docker compose -f Docker/GreptimeDb/docker-compose.yml up -d
docker compose -f Docker/GreptimeDb/docker-compose.yml ps -a
docker compose -f Docker/GreptimeDb/docker-compose.yml down
```

`down` preserves the named database volume. `down -v` deletes stored telemetry.

GreptimeDB v1.2.1 and Collector v0.147.0 run with restart policies. The one-shot retention initializer waits for SQL readiness and applies the database TTLs before the Collector starts. The initializer should show `Exited (0)`.

## Endpoints

| Endpoint | Purpose |
|---|---|
| http://localhost:4000/dashboard | GreptimeDB dashboard |
| http://localhost:4000/v1/sql | SQL queries |
| http://localhost:4318/v1/logs | Application OTLP HTTP logs |
| localhost:4317 | Application OTLP gRPC metrics |
| http://localhost:13133 | Collector health (process readiness, not ingestion proof) |

Ports are bound to localhost. This local setup has no authentication. Docker services communicate on the Compose network.

## Rolling retention

- Database `ifm_logs`, log table `ifm_logs`: **5 days**.
- Database `ifm_metrics`, auto-created metric tables: **30 days**.

Retention is set in GreptimeDB using database TTLs, with matching ingestion hints for new tables. Existing tables with explicit TTL overrides take precedence and must be reviewed if imported into these databases. Expiration is based on telemetry timestamps and cleanup happens asynchronously; it is not an exact wall-clock disk deletion deadline. This deployment does not purge tables at API startup.

Both exporters have bounded queues and retry for up to five minutes. Their queues are in memory, so Collector restarts or long outages can lose unsent telemetry. Accepted data resides in the named GreptimeDB volume. TTL is a time retention policy, not a disk-size quota. Docker's own diagnostic logs rotate separately at 3 x 10 MB per long-running container.

## Application configuration

API Development settings enable structured OTLP logs and IFM/.NET metrics. UI Development settings enable structured OTLP logs. Restart the applications to load these settings. Base/Production settings remain opt-in; use equivalent deployment settings there if needed.

`Telemetry:Traces:Enabled` is false. The Collector has only logs and metrics pipelines. Trace export is independently opt-in now, rather than being implicitly enabled with metrics.

## Verify storage

```powershell
dotnet run --project Docker/GreptimeDb/SmokeTest/SmokeTest.csproj
```

The smoke test uses the real .NET Serilog bridge and metrics provider, sends a structured log over HTTP and a gauge over gRPC through the Collector, reads them back with SQL, verifies 5/30-day database TTLs, and checks that the trace provider is disabled. It writes identifiable test telemetry and requires the stack to be running.

Useful SQL:

```sql
SHOW CREATE DATABASE ifm_logs;
SHOW CREATE DATABASE ifm_metrics;
-- Execute in database ifm_logs:
SELECT timestamp, severity_text, body, log_attributes, resource_attributes
FROM ifm_logs ORDER BY timestamp DESC LIMIT 100;
-- Execute in database ifm_metrics:
SHOW TABLES;
```

Structured method names and business arguments are retained in `log_attributes`; service identity is in `resource_attributes`. These are JSON attributes, not automatically separate indexed columns.

References: [GreptimeDB Collector configuration](https://docs.greptime.com/user-guide/ingest-data/for-observability/otel-collector/) and [retention policies](https://docs.greptime.com/user-guide/manage-data/overview/).

## API/UI lifetime garbage collection history

Both app entry points start `ProcessGcStatisticsRecorder` before application initialization. Every
five seconds it saves cumulative CLR collection counts (Gen 0/1/2), approximate allocated bytes,
total GC pause time, uptime, working set and the last collection's heap/fragmentation/LOH sizes.
Counts are since process start, not since the previous sample. Differences between observations
provide interval counts, allocation rates and pause ratios. No forced GC or heap dump is used.

History is newline-delimited JSON, one file per service/process start, under
`.artifacts/telemetry/gc` when `IFM_REPOSITORY_ROOT` is set. Otherwise it uses the user's local
application-data `IFM/telemetry/gc` directory. Configure `Telemetry:GcHistory:OutputDirectory`,
`Enabled`, `SampleIntervalSeconds` (default 5) and `RetentionDays` (default 30). Startup removes
expired history files. The final `Stopped` observation is saved on normal exit. Forced process
termination cannot execute a final write; the most recent durable sample remains, normally within
five seconds of termination. File write failures are reported at most once per minute and do not
stop trading. This captures no retrospectively reconstructed history for an earlier app run.

The OTel meter `TomasAI.IFM.ProcessGc` exports `ifm.process.gc.collections` (generation tag),
`ifm.process.gc.allocated.bytes`, `ifm.process.gc.pause.seconds`, uptime and memory gauges.
The UI now owns and disposes an OTel metric provider, alongside its existing log sink. Both API/UI
resource attributes include `service.instance.id`, `process.pid` and `process.start_time` so restarts
remain distinct. Existing Collector export stores these metrics in `ifm_metrics` with 30-day TTL;
traces remain disabled. Local history also remains available during a Collector/storage outage.

Use `scripts/Development/Get-IFMGcStatistics.ps1` for the latest lifetime totals. Include
`-IncludeStopped` to examine the most recent saved runs after shutdown. UTC sample timestamps and
run IDs can be compared with scheduled market-open/close run timestamps; GC tracking itself does
not start or stop with the market session.
