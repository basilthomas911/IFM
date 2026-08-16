# FMP Market Data Rollout

This rollout adds the FMP calendar fields and durable Reject ownership before the host starts importing data. The API
key is supplied only through `FMP_API_KEY`; it is never placed in a connection string or URI.

## 1. Apply the additive Scylla migration

Run these statements once in every MarketData keyspace before deploying the new host:

```sql
ALTER TABLE economic_calendar ADD impact text;
ALTER TABLE economic_calendar ADD unit text;
ALTER TABLE economic_calendar ADD change text;
ALTER TABLE economic_calendar ADD changePercentage text;

ALTER TABLE economic_calendar_by_country_month_v2 ADD impact text;
ALTER TABLE economic_calendar_by_country_month_v2 ADD unit text;
ALTER TABLE economic_calendar_by_country_month_v2 ADD change text;
ALTER TABLE economic_calendar_by_country_month_v2 ADD changePercentage text;

ALTER TABLE economic_calendar_by_month_v1 ADD impact text;
ALTER TABLE economic_calendar_by_month_v1 ADD unit text;
ALTER TABLE economic_calendar_by_month_v1 ADD change text;
ALTER TABLE economic_calendar_by_month_v1 ADD changePercentage text;

CREATE TABLE IF NOT EXISTS market_data_import_ownership_v1 (
    dataset text,
    logicalKey text,
    commandId uuid,
    mayWrite boolean,
    createdOn timestamp,
    PRIMARY KEY ((dataset, logicalKey))
);
```

If a column already exists, record that statement as already applied and continue. The application’s fresh-schema
definitions contain the same columns and ownership table.

## 2. Deploy safely

1. Rotate every FMP or legacy market-data key that ever appeared in repository history.
2. Set `FMP_API_KEY` in the host secret provider and leave it out of JSON configuration.
3. Keep both duplicate policies at `Overwrite` for the first deployment.
4. Deploy the schema, then application binaries.
5. Confirm `/health/ready` reports `fmp_configuration` healthy.
6. Run a small authenticated import through `POST /api/marketdata/fmp/import`.
7. Verify the accepted-command/date result and query the imported rows from MarketData.
8. Enable the schedule only after the manual import is verified.

## 3. Reject policy

`Reject` performs a bounded canonical preflight and records its fail-closed verdict with `IF NOT EXISTS` on
`market_data_import_ownership_v1`. The winning command ID owns that logical key. A retry of the same accepted event
can finish its canonical/projection writes; a different command or a pre-existing canonical row receives
`MarketDataImportDuplicateException`. Treasury and economic-calendar policies are configured independently.

## 4. Rollback

Disable `AppSettings:Fmp:Enabled` or the schedule first. Revert the application binary if necessary. The additive
columns and ownership table can remain during rollback; do not drop them while any Reject event may still be replayed.
