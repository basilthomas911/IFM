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
