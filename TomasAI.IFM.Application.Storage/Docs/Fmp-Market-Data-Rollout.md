# FMP Market Data Rollout

The FMP API key is supplied only through `FMP_API_KEY`. Calendar cutover uses a new table because ScyllaDB cannot
change the legacy primary key in place.

## 1. Prerequisites

1. Rotate every FMP or legacy market-data key that appeared in repository history.
2. Pause scheduled and manual economic-calendar imports.
3. Back up the MarketData keyspace and record the restore point.
4. Keep the current application binary available for rollback.

## 2. Create cutover objects

Apply these objects before deploying the new runtime binary:

```sql
CREATE TABLE IF NOT EXISTS economic_calendar_v2 (
    countryCode text,
    monthBucket int,
    eventDate timestamp,
    eventName text,
    actual text,
    forecast text,
    prior text,
    impact text,
    unit text,
    change text,
    changePercentage text,
    createdOn timestamp,
    createdBy text,
    commandId uuid,
    PRIMARY KEY ((countryCode, monthBucket), eventDate, eventName)
) WITH CLUSTERING ORDER BY (eventDate DESC, eventName ASC);

CREATE TABLE IF NOT EXISTS economic_calendar_country_code (
    lookupId int,
    countryCode text,
    PRIMARY KEY ((lookupId), countryCode)
) WITH CLUSTERING ORDER BY (countryCode ASC);

CREATE TABLE IF NOT EXISTS economic_calendar_cutover_v2 (
    cutoverId int PRIMARY KEY,
    sourceRows bigint,
    targetRows bigint,
    sourceFingerprint text,
    targetFingerprint text,
    verified boolean,
    updatedOn timestamp
);

CREATE TABLE IF NOT EXISTS market_data_import_ownership (
    dataset text,
    logicalKey text,
    commandId uuid,
    mayWrite boolean,
    createdOn timestamp,
    PRIMARY KEY ((dataset, logicalKey))
);
```

The ownership table remains required for Treasury Reject imports. Calendar Reject ownership is stored directly in
`economic_calendar_v2.commandId` and claimed with `IF NOT EXISTS`.

## 3. Backfill and verify

Run the projection migration tool against the MarketData connection while imports remain paused:

```powershell
$env:IFM_STORAGE_MIGRATION_MARKET_DATA_SCYLLA_CONNECTION = '<credential-free MarketData connection string>'
dotnet run --project TomasAI.IFM.Application.Storage.ProjectionMigration -- market --apply-schema
```

The tool:

1. reads only legacy `economic_calendar` as the source;
2. normalizes timestamps to UTC and calculates `monthBucket`;
3. rebuilds `economic_calendar_v2` from the paused legacy source in bounded batches;
4. rebuilds the observed-country catalog;
5. compares source/target row counts and order-independent fingerprints; and
6. writes the verdict to `economic_calendar_cutover_v2`.

Do not deploy if the command returns reconciliation exit code `3` or if `verified` is false.

## 4. Deploy and smoke test

1. Set `FMP_API_KEY` in the host secret provider.
2. Keep Treasury and calendar duplicate policies at `Overwrite` for the first deployment.
3. Deploy the application binary.
4. Confirm `/health/ready` reports `fmp_configuration` healthy.
5. Query a small page through `/api/marketdata/economiccalendar/page` with UTC bounds and explicit countries.
6. Run a small authenticated import through `POST /api/marketdata/fmp/import`.
7. Verify the accepted command/date result and read the imported rows through the paged endpoint.
8. Exercise a second page and confirm the opaque continuation token produces no duplicates.
9. Enable the schedule only after the manual import succeeds.

## 5. Rollback

1. Disable FMP imports and the schedule.
2. Re-deploy the previous application binary.
3. The previous binary continues to use the untouched legacy tables.
4. Leave `economic_calendar_v2`, the cutover record, and lookup table in place for diagnosis or a restartable retry.
5. If legacy writes resumed during rollback, pause them and rerun the backfill before attempting cutover again.

Do not drop legacy tables during the rollback window.

## 6. Cleanup after the verification window

After operational sign-off and backup retention are confirmed, drop the retired row/projection tables:

```sql
DROP TABLE IF EXISTS economic_calendar;
DROP TABLE IF EXISTS economic_calendar_by_country_month_v2;
DROP TABLE IF EXISTS economic_calendar_by_month_v1;
DROP TABLE IF EXISTS economic_calendar_month_v1;
```

Keep `economic_calendar_country_code`, `economic_calendar_cutover_v2`, and
`market_data_import_ownership`. The deprecated all-calendar and external-calendar API contracts can be removed in
the next breaking API release; they no longer perform external storage reads.
