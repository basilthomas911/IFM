-- Explicit destructive reset for the authorized local integration database only.
-- Stop application writers first. Global identities and financial projections are retained.
BEGIN;
SET LOCAL lock_timeout = '5s';
DO $guard$
BEGIN
    IF current_database() <> 'event-source-test-db' THEN
        RAISE EXCEPTION 'This reset is restricted to event-source-test-db';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid()) THEN
        RAISE EXCEPTION 'Stop other database clients before resetting the event log';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public'
                   AND table_name='event_log' AND column_name='eventdata') THEN
        RAISE EXCEPTION 'Legacy JSON column is absent; refusing to reset a binary event log again';
    END IF;
END $guard$;
LOCK TABLE event_log IN ACCESS EXCLUSIVE MODE;
-- Remove deduplication records for the events being discarded.
DO $$ BEGIN RAISE NOTICE 'Removing event-linked command deduplication records'; END $$;
DELETE FROM command_log c USING (SELECT DISTINCT commandid FROM event_log) e WHERE c.commandid=e.commandid;
-- Explicit dependency list: unexpected additional dependencies cause rollback.
DO $$ BEGIN RAISE NOTICE 'Clearing event rows and dependent receipts'; END $$;
TRUNCATE TABLE event_projector_outbox, event_projector_state,
    business_subscription_projection_receipt,
    portfolio_financial.financial_history_receipt,
    portfolio_financial.financial_operation_receipt,
    event_projector_stream_checkpoint, event_log;
DO $$ BEGIN RAISE NOTICE 'Resetting stream positions'; END $$;
UPDATE event_stream_id SET CurrentVersion=0;
ALTER TABLE event_log DROP COLUMN eventdata;
ALTER TABLE event_log ADD COLUMN EventPayload bytea NOT NULL CHECK (octet_length(EventPayload)>0);
DO $$ BEGIN RAISE NOTICE 'Binary schema installed; committing reset'; END $$;
COMMIT;
