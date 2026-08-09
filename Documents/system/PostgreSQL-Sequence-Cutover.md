# PostgreSQL sequence cutover

## Guarantees

`ISequenceIdGenerator` is the only application-facing ID allocator. PostgreSQL owns one sequence per
`SequenceName`; each `nextval` reserves 100 IDs for one application instance. IDs are unique across instances,
may contain gaps, and are not globally ordered by request completion time.

`GetHighWatermarkAsync` returns the highest ID reserved by PostgreSQL, not the highest ID already handed to a
caller. Business workflows must retain the ID returned by `GetSequenceIdAsync` rather than querying a "current"
ID later.

## Values to inventory

Before cutover, record the greatest value for every affected named sequence from all applicable sources:

- Persisted domain tables and event/read models.
- Legacy Redis keys `SequenceCounter:StreamingRequest_RequestId`,
  `SequenceCounter:OptionQuote_QuoteId`, and `SequenceCounter:FuturesTradeSignal_SequenceId`.
- Legacy Scylla `seed_id` and `seed_id_v2` rows for `FundId`, `OrderId`, `TradeId`, and `ScheduledJobId`.
- The existing PostgreSQL sequence high watermark.

Use the greatest value found for each sequence. Do not infer safety from only one backing store.

## Cutover procedure

1. Stop or drain every process capable of allocating an ID.
2. Back up the Sequence ID PostgreSQL database and the legacy Redis/Scylla values.
3. Deploy/apply `SequenceIdSchemaDb` so every `SequenceName` exists with `INCREMENT BY 100`.
4. Populate the temporary table below with the audited maximum for every sequence being migrated.
5. Run the block while writers remain stopped. It validates the increment and advances sequences only to the
   first block strictly above both the supplied maximum and PostgreSQL's current high watermark.
6. Start only the new application version. Verify several IDs from each migrated sequence are greater than the
   audited maxima and remain unique across two application instances.
7. Retain the old Redis keys and Scylla tables through the rollback window. They are no longer read or written and
   can be removed later through a separately reviewed data-retention change.

```sql
BEGIN;

CREATE TEMP TABLE sequence_cutover_maximum
(
    sequence_name text PRIMARY KEY,
    max_existing_id bigint NOT NULL CHECK (max_existing_id >= 0)
) ON COMMIT DROP;

-- Replace each audited value before running. Add every sequence being migrated.
-- INSERT INTO sequence_cutover_maximum VALUES
-- ('StreamingRequest_RequestId', <audited maximum>),
-- ('OptionQuote_QuoteId', <audited maximum>),
-- ('FuturesTradeSignal_SequenceId', <audited maximum>),
-- ('Fund_FundId', <audited maximum>),
-- ('Trade_OrderId', <audited maximum>),
-- ('Trade_TradeId', <audited maximum>),
-- ('ScheduledJob_JobId', <audited maximum>);

DO $cutover$
DECLARE
    item record;
    configured_increment bigint;
    postgres_high_watermark bigint;
    safe_maximum bigint;
    next_range_start bigint;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sequence_cutover_maximum) THEN
        RAISE EXCEPTION 'sequence_cutover_maximum is empty';
    END IF;

    FOR item IN SELECT * FROM sequence_cutover_maximum ORDER BY sequence_name
    LOOP
        SELECT increment_by
        INTO configured_increment
        FROM pg_sequences
        WHERE schemaname = 'public'
          AND sequencename = lower(item.sequence_name);

        IF configured_increment IS NULL THEN
            RAISE EXCEPTION 'PostgreSQL sequence public.% does not exist', item.sequence_name;
        END IF;
        IF configured_increment <> 100 THEN
            RAISE EXCEPTION 'Sequence public.% has increment %, expected 100',
                item.sequence_name, configured_increment;
        END IF;

        EXECUTE format(
            'SELECT CASE WHEN is_called THEN last_value + %s - 1 ELSE 0 END FROM public.%I',
            configured_increment,
            lower(item.sequence_name))
        INTO postgres_high_watermark;

        safe_maximum := greatest(item.max_existing_id, postgres_high_watermark);
        IF safe_maximum > 9223372036854775700 THEN
            RAISE EXCEPTION 'Sequence public.% is too close to bigint overflow', item.sequence_name;
        END IF;

        next_range_start :=
            ((safe_maximum + configured_increment - 1) / configured_increment)
            * configured_increment + 1;

        EXECUTE format(
            'SELECT setval(%L::regclass, $1, false)',
            'public.' || item.sequence_name)
        USING next_range_start;
    END LOOP;
END
$cutover$;

COMMIT;
```

If verification fails, stop writers again. Do not reset a PostgreSQL sequence downward and do not restart an old
binary that can write Redis or Scylla counters. Correct configuration or application issues, advance affected
PostgreSQL sequences beyond any IDs emitted during the failed attempt, and retry.
