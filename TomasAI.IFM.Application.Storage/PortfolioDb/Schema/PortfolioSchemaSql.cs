
namespace TomasAI.IFM.Application.Storage.PortfolioDb.Schema;

/// <summary>Canonical Portfolio tables, indexes and additive schema changes.</summary>
public static class PortfolioSchemaSql
{
    /// <summary>Creates the financialhistory persistence tables.</summary>
    public const string FinancialHistory = """
        CREATE TABLE IF NOT EXISTS portfolio.financial_operation_by_portfolio_month(
          portfolio_id int NOT NULL, month int NOT NULL, financial_revision bigint NOT NULL,
          operation_id uuid NOT NULL, source_event_id bigint NOT NULL,
          event_type text NOT NULL, committed_at_utc timestamptz NOT NULL,
          payload_json jsonb NOT NULL,
          PRIMARY KEY(portfolio_id,month,financial_revision,operation_id));
        """;

    /// <summary>Creates the financial persistence tables.</summary>
    public const string Financial = """
        CREATE SCHEMA IF NOT EXISTS portfolio_financial;
        DROP TRIGGER IF EXISTS financial_legacy_event_fence ON event_log;
        DO $$ BEGIN
          IF to_regclass('public.event_log') IS NOT NULL THEN
            EXECUTE 'DROP TRIGGER IF EXISTS financial_legacy_event_fence ON public.event_log';
          END IF;
        END $$;
        DROP FUNCTION IF EXISTS portfolio_financial.guard_legacy_event_writer();
        DROP TABLE IF EXISTS portfolio_financial.legacy_write_intent CASCADE;
        DROP TABLE IF EXISTS portfolio_financial.legacy_financial_inventory_row CASCADE;
        DROP TABLE IF EXISTS portfolio_financial.legacy_financial_inventory CASCADE;
        DROP TABLE IF EXISTS portfolio_financial.legacy_writer_scope CASCADE;
        DROP TABLE IF EXISTS portfolio_financial.ledger_migration CASCADE;
        CREATE TABLE IF NOT EXISTS portfolio_financial.schema_version(singleton boolean PRIMARY KEY CHECK(singleton), version int NOT NULL);
        INSERT INTO portfolio_financial.schema_version VALUES(true,2) ON CONFLICT(singleton) DO UPDATE SET version=EXCLUDED.version;
        CREATE TABLE IF NOT EXISTS portfolio_financial.financial_projection_checkpoint(
          source_event_id uuid PRIMARY KEY, portfolio_id int NOT NULL,
          event_version bigint NOT NULL UNIQUE REFERENCES event_log(eventVersion), financial_revision bigint NOT NULL);
        CREATE TABLE IF NOT EXISTS portfolio_financial.financial_history_receipt(
          event_version bigint PRIMARY KEY REFERENCES event_log(eventVersion), projected_at_utc timestamptz NOT NULL);
        CREATE TABLE IF NOT EXISTS portfolio_financial.financial_operation_receipt(
          portfolio_id int NOT NULL, operation_id uuid NOT NULL, actor_name text NOT NULL, input_hash text NOT NULL,
          event_version bigint NOT NULL UNIQUE REFERENCES event_log(eventVersion), financial_revision bigint NOT NULL,
          PRIMARY KEY(portfolio_id,operation_id));
        CREATE TABLE IF NOT EXISTS portfolio_financial.ledger_book(
          book_id int PRIMARY KEY CHECK(book_id>0), accounting_entity_id uuid NOT NULL,
          portfolio_id int NOT NULL CHECK(portfolio_id>0), base_currency text NOT NULL CHECK(base_currency='USD'),
          execution_account_ref text NOT NULL, environment text NOT NULL, version bigint NOT NULL CHECK(version>0),
          status text NOT NULL CHECK(status IN ('Draft','Active','Closed')),
          UNIQUE(book_id,portfolio_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ledger_book_exclusive_account ON portfolio_financial.ledger_book(environment,execution_account_ref) WHERE status='Active';
        CREATE UNIQUE INDEX IF NOT EXISTS ledger_book_active_portfolio ON portfolio_financial.ledger_book(portfolio_id) WHERE status='Active';
        CREATE TABLE IF NOT EXISTS portfolio_financial.ledger_account(
          book_id int NOT NULL REFERENCES portfolio_financial.ledger_book, account_id int NOT NULL CHECK(account_id>0),
          version bigint NOT NULL CHECK(version>0), category text NOT NULL, normal_side int NOT NULL CHECK(normal_side IN (1,2)),
          currency text NOT NULL CHECK(currency='USD'), fund_dimension_required boolean NOT NULL,
          status text NOT NULL CHECK(status IN ('Draft','Active','Retired')), content_hash text NOT NULL,
          PRIMARY KEY(book_id,account_id,version));
        CREATE TABLE IF NOT EXISTS portfolio_financial.ledger_posting_rule(
          book_id int NOT NULL REFERENCES portfolio_financial.ledger_book, rule_id uuid NOT NULL, version bigint NOT NULL CHECK(version>0),
          kind int NOT NULL CHECK(kind BETWEEN 1 AND 12), payload jsonb NOT NULL, content_hash text NOT NULL,
          status text NOT NULL CHECK(status IN ('Draft','Active','Retired')), effective_from date NOT NULL, effective_to date,
          PRIMARY KEY(book_id,rule_id,version), CHECK(effective_to IS NULL OR effective_to>=effective_from));
        CREATE TABLE IF NOT EXISTS portfolio_financial.financial_authority(
          portfolio_id int PRIMARY KEY CHECK(portfolio_id>0), book_id int NOT NULL,
          financial_revision bigint NOT NULL DEFAULT 0 CHECK(financial_revision>=0), authority_epoch bigint NOT NULL DEFAULT 0,
          operating_state text NOT NULL DEFAULT 'Disabled', policy_source_versions jsonb NOT NULL DEFAULT '{}',
          source_watermark text NOT NULL DEFAULT '', valuation_watermark text NOT NULL DEFAULT '',
          migration_state text NOT NULL DEFAULT 'Unqualified', FOREIGN KEY(book_id,portfolio_id) REFERENCES portfolio_financial.ledger_book(book_id,portfolio_id));
        CREATE TABLE IF NOT EXISTS portfolio_financial.broker_accounting_intent(
          portfolio_id int NOT NULL CHECK(portfolio_id>0), operation_id uuid NOT NULL,
          evidence_hash text NOT NULL CHECK(length(evidence_hash)=64), command_payload jsonb NOT NULL,
          command_hash text NOT NULL CHECK(length(command_hash)=64),
          created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
          PRIMARY KEY(portfolio_id,operation_id));
        CREATE TABLE IF NOT EXISTS portfolio_financial.broker_closing_basis_claim(
          portfolio_id int NOT NULL, position_key text NOT NULL, opening_leg_id uuid NOT NULL,
          operation_id uuid NOT NULL, execution_attempt_id uuid NOT NULL,
          opening_hash text NOT NULL CHECK(length(opening_hash)=64),
          closed_quantity numeric NOT NULL CHECK(closed_quantity>0), allocated_signed_basis numeric(28,2) NOT NULL,
          PRIMARY KEY(portfolio_id,position_key,opening_leg_id,operation_id),
          UNIQUE(portfolio_id,position_key,opening_leg_id,execution_attempt_id),
          FOREIGN KEY(portfolio_id,operation_id) REFERENCES portfolio_financial.broker_accounting_intent);
        CREATE TABLE IF NOT EXISTS portfolio_financial.ledger_transaction(
          transaction_id bigint PRIMARY KEY CHECK(transaction_id>0), book_id int NOT NULL, portfolio_id int NOT NULL,
          fund_id int NOT NULL CHECK(fund_id>0), operation_id uuid NOT NULL, item_ordinal int NOT NULL CHECK(item_ordinal>0),
          kind int NOT NULL CHECK(kind BETWEEN 1 AND 12), source_system text NOT NULL, source_event_key text NOT NULL,
          source_hash text NOT NULL, amount numeric(28,2) NOT NULL, currency text NOT NULL CHECK(currency='USD'),
          accounting_date date NOT NULL, value_date date NOT NULL, settlement_date date, obligation_id uuid,
          related_journal_id bigint, payload jsonb NOT NULL, UNIQUE(portfolio_id,operation_id,item_ordinal),
          FOREIGN KEY(book_id,portfolio_id) REFERENCES portfolio_financial.ledger_book(book_id,portfolio_id));
        CREATE TABLE IF NOT EXISTS portfolio_financial.ledger_journal(
          journal_id bigint PRIMARY KEY CHECK(journal_id>0), transaction_id bigint UNIQUE NOT NULL REFERENCES portfolio_financial.ledger_transaction,
          book_id int NOT NULL, portfolio_id int NOT NULL, fund_id int, operation_id uuid NOT NULL,
          accounting_date date NOT NULL, value_date date NOT NULL, settlement_date date, kind int NOT NULL,
          source_hash text NOT NULL, rule_id uuid NOT NULL, rule_version bigint NOT NULL,
          reversal_journal_id bigint REFERENCES portfolio_financial.ledger_journal, committed_at_utc timestamptz NOT NULL,
          financial_revision bigint NOT NULL CHECK(financial_revision>0), journal_hash text NOT NULL,
          FOREIGN KEY(book_id,portfolio_id) REFERENCES portfolio_financial.ledger_book(book_id,portfolio_id),
          FOREIGN KEY(book_id,rule_id,rule_version) REFERENCES portfolio_financial.ledger_posting_rule(book_id,rule_id,version),
          UNIQUE(journal_id,book_id));
        ALTER TABLE portfolio_financial.ledger_journal ADD COLUMN IF NOT EXISTS created_transaction_id xid8 NOT NULL DEFAULT pg_current_xact_id();
        CREATE TABLE IF NOT EXISTS portfolio_financial.ledger_entry(
          journal_id bigint NOT NULL, ordinal int NOT NULL CHECK(ordinal>0), book_id int NOT NULL,
          account_id int NOT NULL, account_version bigint NOT NULL, fund_id int CHECK(fund_id>0),
          debit numeric(28,2) NOT NULL CHECK(debit>=0), credit numeric(28,2) NOT NULL CHECK(credit>=0),
          currency text NOT NULL CHECK(currency='USD'), source_line_reference text NOT NULL,
          order_id int CHECK(order_id>0), trade_id int CHECK(trade_id>0),
          CHECK((debit>0 AND credit=0) OR (credit>0 AND debit=0)), PRIMARY KEY(journal_id,ordinal),
          FOREIGN KEY(journal_id,book_id) REFERENCES portfolio_financial.ledger_journal(journal_id,book_id),
          FOREIGN KEY(book_id,account_id,account_version) REFERENCES portfolio_financial.ledger_account(book_id,account_id,version));
        CREATE TABLE IF NOT EXISTS portfolio_financial.ledger_account_balance(
          book_id int NOT NULL, account_id int NOT NULL, fund_id int CHECK(fund_id>0), currency text NOT NULL CHECK(currency='USD'),
          debit_total numeric(28,2) NOT NULL CHECK(debit_total>=0), credit_total numeric(28,2) NOT NULL CHECK(credit_total>=0),
          balance numeric(28,2) NOT NULL, revision bigint NOT NULL CHECK(revision>0),
          CHECK(balance=debit_total-credit_total), UNIQUE NULLS NOT DISTINCT(book_id,account_id,fund_id,currency));
        CREATE TABLE IF NOT EXISTS portfolio_financial.ledger_posting_receipt(
          portfolio_id int NOT NULL, operation_id uuid NOT NULL, execution_id text NOT NULL, input_hash text NOT NULL,
          receipt_type text NOT NULL, manifest jsonb NOT NULL, completion_event_id uuid NOT NULL UNIQUE,
          committed_at_utc timestamptz NOT NULL, financial_revision bigint NOT NULL CHECK(financial_revision>0), payload jsonb NOT NULL,
          PRIMARY KEY(portfolio_id,operation_id));
        CREATE TABLE IF NOT EXISTS portfolio_financial.financial_source_receipt(
          book_id int NOT NULL REFERENCES portfolio_financial.ledger_book, source_system text NOT NULL,
          source_event_key text NOT NULL, posting_purpose text NOT NULL, source_content_hash text NOT NULL,
          operation_id uuid NOT NULL, journal_id bigint REFERENCES portfolio_financial.ledger_journal,
          PRIMARY KEY(book_id,source_system,source_event_key,posting_purpose));
        CREATE TABLE IF NOT EXISTS portfolio_financial.financial_encumbrance(
          obligation_id uuid PRIMARY KEY, portfolio_id int NOT NULL, book_id int NOT NULL, fund_id int NOT NULL CHECK(fund_id>0),
          source_identity text NOT NULL, kind text NOT NULL, amount numeric(28,2) NOT NULL CHECK(amount>=0),
          currency text NOT NULL CHECK(currency='USD'), status text NOT NULL CHECK(status IN ('Pending','Settled','Cancelled')),
          revision bigint NOT NULL CHECK(revision>0), evidence jsonb NOT NULL,
          FOREIGN KEY(book_id,portfolio_id) REFERENCES portfolio_financial.ledger_book(book_id,portfolio_id));
        CREATE TABLE IF NOT EXISTS portfolio_financial.capacity_reservation(
          reservation_id uuid PRIMARY KEY, portfolio_id int NOT NULL, operation_id uuid NOT NULL, fund_id int NOT NULL CHECK(fund_id>0),
          book_id int NOT NULL, order_id int NOT NULL CHECK(order_id>0), candidate_hash text NOT NULL, risk_hash text NOT NULL,
          sized_order_hash text NOT NULL, strategy_units int NOT NULL CHECK(strategy_units>0), requirements jsonb NOT NULL,
          environment text NOT NULL, expires_at_utc timestamptz NOT NULL, status int NOT NULL CHECK(status BETWEEN 1 AND 9),
          version bigint NOT NULL CHECK(version>0), completion_event_id uuid NOT NULL, request jsonb NOT NULL,
          receipt jsonb NOT NULL, input_hash text NOT NULL, execution_id uuid, filled_units int NOT NULL DEFAULT 0,
          cancelled_units int NOT NULL DEFAULT 0, remaining_units int NOT NULL CHECK(remaining_units>=0),
          CHECK(filled_units>=0 AND cancelled_units>=0 AND filled_units+cancelled_units+remaining_units=strategy_units),
          UNIQUE(portfolio_id,operation_id), FOREIGN KEY(book_id,portfolio_id) REFERENCES portfolio_financial.ledger_book(book_id,portfolio_id));
        CREATE UNIQUE INDEX IF NOT EXISTS capacity_one_active_order ON portfolio_financial.capacity_reservation(portfolio_id,order_id) WHERE status NOT IN (8,9);
        CREATE TABLE IF NOT EXISTS portfolio_financial.capacity_funding_receipt(
          transaction_id bigint PRIMARY KEY REFERENCES portfolio_financial.ledger_transaction,
          reservation_id uuid NOT NULL REFERENCES portfolio_financial.capacity_reservation,
          component int NOT NULL CHECK(component BETWEEN 1 AND 3), cash_paid numeric(28,2) NOT NULL CHECK(cash_paid>0));
        CREATE INDEX IF NOT EXISTS capacity_funding_by_reservation ON portfolio_financial.capacity_funding_receipt(reservation_id);
        CREATE TABLE IF NOT EXISTS portfolio_financial.capacity_usage(
          portfolio_id int NOT NULL, scope_kind int NOT NULL, scope_key text NOT NULL, measure int NOT NULL, unit int NOT NULL,
          held numeric(38,10) NOT NULL, working numeric(38,10) NOT NULL, position numeric(38,10) NOT NULL,
          revision bigint NOT NULL CHECK(revision>0), PRIMARY KEY(portfolio_id,scope_kind,scope_key,measure,unit));
        ALTER TABLE portfolio_financial.capacity_reservation ADD COLUMN IF NOT EXISTS closed_units int NOT NULL DEFAULT 0
          CHECK(closed_units>=0 AND closed_units<=filled_units);
        CREATE TABLE IF NOT EXISTS portfolio_financial.capacity_expiry_dispatch(
          operation_id uuid PRIMARY KEY, reservation_id uuid NOT NULL REFERENCES portfolio_financial.capacity_reservation,
          portfolio_id int NOT NULL, fund_id int NOT NULL, request jsonb NOT NULL, input_hash text NOT NULL,
          status text NOT NULL CHECK(status IN ('Pending','Committed','ExpiredWithoutCommit')),
          created_at_utc timestamptz NOT NULL, reconciled_at_utc timestamptz);
        CREATE UNIQUE INDEX IF NOT EXISTS capacity_expiry_pending_reservation ON portfolio_financial.capacity_expiry_dispatch(reservation_id) WHERE status='Pending';
        CREATE INDEX IF NOT EXISTS capacity_expiry_pending_age ON portfolio_financial.capacity_expiry_dispatch(created_at_utc,operation_id) WHERE status='Pending';
        CREATE TABLE IF NOT EXISTS portfolio_financial.emulator_order(
          execution_id uuid PRIMARY KEY,portfolio_id int NOT NULL,fund_id int NOT NULL,order_id int NOT NULL,
          reservation_id uuid NOT NULL UNIQUE REFERENCES portfolio_financial.capacity_reservation(reservation_id),
          operation_id uuid NOT NULL UNIQUE,request jsonb NOT NULL,receipt jsonb NOT NULL,input_hash text NOT NULL,
          submitted_at_utc timestamptz NOT NULL);
        CREATE TABLE IF NOT EXISTS portfolio_financial.capacity_lifecycle(
          reservation_id uuid NOT NULL REFERENCES portfolio_financial.capacity_reservation, version bigint NOT NULL CHECK(version>0),
          source_identity text NOT NULL, operation_id uuid NOT NULL, prior_status int NOT NULL, new_status int NOT NULL,
          usage_delta jsonb NOT NULL, filled_units int NOT NULL CHECK(filled_units>=0), cancelled_units int NOT NULL CHECK(cancelled_units>=0),
          remaining_units int NOT NULL CHECK(remaining_units>=0), execution_id uuid, execution_revision bigint NOT NULL,
          committed_at_utc timestamptz NOT NULL, completion_event_id uuid NOT NULL, input_hash text NOT NULL, receipt jsonb NOT NULL,
          PRIMARY KEY(reservation_id,version), UNIQUE(reservation_id,source_identity), UNIQUE(reservation_id,operation_id));
        CREATE TABLE IF NOT EXISTS portfolio_financial.ledger_period(
          book_id int NOT NULL REFERENCES portfolio_financial.ledger_book, period_id uuid NOT NULL,
          start_date date NOT NULL, end_date date NOT NULL CHECK(end_date>=start_date),
          state text NOT NULL CHECK(state IN ('Open','Closing','Closed')), revision bigint NOT NULL CHECK(revision>0),
          source_cut text NOT NULL, evidence jsonb NOT NULL, PRIMARY KEY(book_id,period_id));
        CREATE TABLE IF NOT EXISTS portfolio_financial.ledger_valuation(
          book_id int NOT NULL REFERENCES portfolio_financial.ledger_book, fund_id int NOT NULL,
          position_key text NOT NULL, amount numeric(28,2) NOT NULL, source_sequence bigint NOT NULL,
          revision bigint NOT NULL, PRIMARY KEY(book_id,fund_id,position_key));
        CREATE TABLE IF NOT EXISTS portfolio_financial.ledger_reconciliation(
          reconciliation_id uuid PRIMARY KEY, book_id int NOT NULL REFERENCES portfolio_financial.ledger_book,
          portfolio_id int NOT NULL, fund_id int, source_cut text NOT NULL, counts jsonb NOT NULL, totals jsonb NOT NULL,
          content_hash text NOT NULL, differences jsonb NOT NULL, resolution_links jsonb NOT NULL, status text NOT NULL);
        CREATE TABLE IF NOT EXISTS portfolio_financial.accounting_export(
          destination_company text NOT NULL, export_id uuid NOT NULL, source_set jsonb NOT NULL, source_cut text NOT NULL,
          payload_hash text NOT NULL, mapping_version bigint NOT NULL, delivery_status text NOT NULL,
          external_receipt text, retry_count int NOT NULL DEFAULT 0, metadata jsonb NOT NULL,
          PRIMARY KEY(destination_company,export_id));
        CREATE TABLE IF NOT EXISTS portfolio_financial.accounting_export_source(
          destination_company text NOT NULL, journal_id bigint NOT NULL REFERENCES portfolio_financial.ledger_journal,
          export_id uuid NOT NULL, PRIMARY KEY(destination_company,journal_id),
          FOREIGN KEY(destination_company,export_id) REFERENCES portfolio_financial.accounting_export);
        CREATE TABLE IF NOT EXISTS portfolio_financial.accounting_export_attempt(
          destination_company text NOT NULL, export_id uuid NOT NULL, attempt_id uuid NOT NULL,
          outcome text NOT NULL CHECK(outcome IN ('Delivered','Failed','Unknown')), external_receipt text,
          PRIMARY KEY(destination_company,export_id,attempt_id),
          FOREIGN KEY(destination_company,export_id) REFERENCES portfolio_financial.accounting_export,
          CHECK((outcome='Delivered' AND external_receipt IS NOT NULL) OR (outcome<>'Delivered' AND external_receipt IS NULL)));
        CREATE OR REPLACE FUNCTION portfolio_financial.check_journal_balance() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE entry_count int; debits numeric; credits numeric;
        BEGIN
          SELECT count(*),coalesce(sum(debit),0),coalesce(sum(credit),0) INTO entry_count,debits,credits
            FROM portfolio_financial.ledger_entry WHERE journal_id=NEW.journal_id;
          IF entry_count<2 OR debits<>credits THEN RAISE EXCEPTION 'GL.JOURNAL.UNBALANCED' USING ERRCODE='23514'; END IF;
          RETURN NULL;
        END $$;
        DROP TRIGGER IF EXISTS ledger_journal_balanced ON portfolio_financial.ledger_journal;
        CREATE CONSTRAINT TRIGGER ledger_journal_balanced AFTER INSERT ON portfolio_financial.ledger_journal
          DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION portfolio_financial.check_journal_balance();
        DROP TRIGGER IF EXISTS ledger_entry_balanced ON portfolio_financial.ledger_entry;
        CREATE CONSTRAINT TRIGGER ledger_entry_balanced AFTER INSERT ON portfolio_financial.ledger_entry
          DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION portfolio_financial.check_journal_balance();
        CREATE OR REPLACE FUNCTION portfolio_financial.reject_history_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'Posted financial history is immutable' USING ERRCODE='23514'; END $$;
        DROP TRIGGER IF EXISTS immutable_emulator_order ON portfolio_financial.emulator_order;
        CREATE TRIGGER immutable_emulator_order BEFORE UPDATE OR DELETE ON portfolio_financial.emulator_order
          FOR EACH ROW EXECUTE FUNCTION portfolio_financial.reject_history_mutation();
        DROP TRIGGER IF EXISTS immutable_export_source ON portfolio_financial.accounting_export_source;
        CREATE TRIGGER immutable_export_source BEFORE UPDATE OR DELETE ON portfolio_financial.accounting_export_source
          FOR EACH ROW EXECUTE FUNCTION portfolio_financial.reject_history_mutation();
        DROP TRIGGER IF EXISTS immutable_export_attempt ON portfolio_financial.accounting_export_attempt;
        CREATE TRIGGER immutable_export_attempt BEFORE UPDATE OR DELETE ON portfolio_financial.accounting_export_attempt
          FOR EACH ROW EXECUTE FUNCTION portfolio_financial.reject_history_mutation();
        CREATE OR REPLACE FUNCTION portfolio_financial.reject_export_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP='DELETE' OR (to_jsonb(NEW)-ARRAY['delivery_status','external_receipt','retry_count'])
             IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['delivery_status','external_receipt','retry_count']) THEN
            RAISE EXCEPTION 'Accounting export payload is immutable' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        DROP TRIGGER IF EXISTS immutable_export_payload ON portfolio_financial.accounting_export;
        CREATE TRIGGER immutable_export_payload BEFORE UPDATE OR DELETE ON portfolio_financial.accounting_export
          FOR EACH ROW EXECUTE FUNCTION portfolio_financial.reject_export_rewrite();
        DROP TRIGGER IF EXISTS immutable_journal ON portfolio_financial.ledger_journal;
        CREATE TRIGGER immutable_journal BEFORE UPDATE OR DELETE ON portfolio_financial.ledger_journal FOR EACH ROW EXECUTE FUNCTION portfolio_financial.reject_history_mutation();
        DROP TRIGGER IF EXISTS immutable_entry ON portfolio_financial.ledger_entry;
        CREATE TRIGGER immutable_entry BEFORE UPDATE OR DELETE ON portfolio_financial.ledger_entry FOR EACH ROW EXECUTE FUNCTION portfolio_financial.reject_history_mutation();
        DROP TRIGGER IF EXISTS immutable_transaction ON portfolio_financial.ledger_transaction;
        CREATE TRIGGER immutable_transaction BEFORE UPDATE OR DELETE ON portfolio_financial.ledger_transaction FOR EACH ROW EXECUTE FUNCTION portfolio_financial.reject_history_mutation();
        DROP TRIGGER IF EXISTS immutable_funding_receipt ON portfolio_financial.capacity_funding_receipt;
        CREATE TRIGGER immutable_funding_receipt BEFORE UPDATE OR DELETE ON portfolio_financial.capacity_funding_receipt FOR EACH ROW EXECUTE FUNCTION portfolio_financial.reject_history_mutation();
        CREATE OR REPLACE FUNCTION portfolio_financial.require_new_journal() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF NOT EXISTS(SELECT 1 FROM portfolio_financial.ledger_journal WHERE journal_id=NEW.journal_id
              AND created_transaction_id=pg_current_xact_id()) THEN
            RAISE EXCEPTION 'Posted journal cannot receive new entries' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        DROP TRIGGER IF EXISTS entry_requires_new_journal ON portfolio_financial.ledger_entry;
        CREATE TRIGGER entry_requires_new_journal BEFORE INSERT ON portfolio_financial.ledger_entry
          FOR EACH ROW EXECUTE FUNCTION portfolio_financial.require_new_journal();
        CREATE OR REPLACE FUNCTION portfolio_financial.reject_configuration_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP='DELETE' OR (to_jsonb(NEW)-'status') IS DISTINCT FROM (to_jsonb(OLD)-'status') THEN
            RAISE EXCEPTION 'Financial configuration versions are immutable' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        DROP TRIGGER IF EXISTS immutable_account_version ON portfolio_financial.ledger_account;
        CREATE TRIGGER immutable_account_version BEFORE UPDATE OR DELETE ON portfolio_financial.ledger_account
          FOR EACH ROW EXECUTE FUNCTION portfolio_financial.reject_configuration_rewrite();
        DROP TRIGGER IF EXISTS immutable_posting_rule_version ON portfolio_financial.ledger_posting_rule;
        CREATE TRIGGER immutable_posting_rule_version BEFORE UPDATE OR DELETE ON portfolio_financial.ledger_posting_rule
          FOR EACH ROW EXECUTE FUNCTION portfolio_financial.reject_configuration_rewrite();
        CREATE OR REPLACE FUNCTION portfolio_financial.reject_overlapping_period() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          PERFORM 1 FROM portfolio_financial.ledger_book WHERE book_id=NEW.book_id FOR UPDATE;
          IF EXISTS(SELECT 1 FROM portfolio_financial.ledger_period WHERE book_id=NEW.book_id AND period_id<>NEW.period_id
              AND start_date<=NEW.end_date AND end_date>=NEW.start_date) THEN
            RAISE EXCEPTION 'Ledger accounting periods cannot overlap' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        DROP TRIGGER IF EXISTS nonoverlapping_period ON portfolio_financial.ledger_period;
        CREATE TRIGGER nonoverlapping_period BEFORE INSERT OR UPDATE ON portfolio_financial.ledger_period
          FOR EACH ROW EXECUTE FUNCTION portfolio_financial.reject_overlapping_period();
        """;

    /// <summary>Creates the portfolio persistence tables.</summary>
    public const string Portfolio = """
            CREATE SCHEMA IF NOT EXISTS portfolio;
            CREATE TABLE IF NOT EXISTS portfolio.schema_version(singleton boolean PRIMARY KEY CHECK(singleton),version int NOT NULL);
            INSERT INTO portfolio.schema_version VALUES(true,1) ON CONFLICT(singleton) DO NOTHING;
            CREATE TABLE IF NOT EXISTS portfolio.projection_tombstone(entity_kind text NOT NULL,entity_key text NOT NULL,source_event_id bigint NOT NULL,PRIMARY KEY(entity_kind,entity_key));
            CREATE TABLE IF NOT EXISTS portfolio.portfolio_by_id(portfolio_id int NOT NULL,portfolio_version bigint NOT NULL,operating_state text NOT NULL,schema_version int NOT NULL,aggregate_version bigint NOT NULL,source_event_id bigint NOT NULL,updated_on_utc timestamptz NOT NULL,payload_json jsonb NOT NULL,payload_hash text NOT NULL,PRIMARY KEY(portfolio_id,portfolio_version));
            CREATE TABLE IF NOT EXISTS portfolio.portfolio_by_state(operating_state text NOT NULL,state_bucket int NOT NULL,portfolio_id int NOT NULL,portfolio_version bigint NOT NULL,schema_version int NOT NULL,aggregate_version bigint NOT NULL,source_event_id bigint NOT NULL,updated_on_utc timestamptz NOT NULL,payload_json jsonb NOT NULL,payload_hash text NOT NULL,PRIMARY KEY(operating_state,state_bucket,portfolio_id));
            CREATE TABLE IF NOT EXISTS portfolio.fund_by_portfolio(portfolio_id int NOT NULL,fund_id int NOT NULL,fund_mandate_version bigint NOT NULL,operating_state text NOT NULL,schema_version int NOT NULL,aggregate_version bigint NOT NULL,source_event_id bigint NOT NULL,updated_on_utc timestamptz NOT NULL,payload_json jsonb NOT NULL,payload_hash text NOT NULL,PRIMARY KEY(portfolio_id,fund_id,fund_mandate_version));
            CREATE INDEX IF NOT EXISTS fund_by_id ON portfolio.fund_by_portfolio(fund_id,fund_mandate_version DESC);
            CREATE TABLE IF NOT EXISTS portfolio.active_fund_by_portfolio_horizon(portfolio_id int NOT NULL,trading_year int NOT NULL,decision_horizon text NOT NULL,effective_from_utc timestamptz NOT NULL,fund_id int NOT NULL,fund_mandate_version bigint NOT NULL,schema_version int NOT NULL,aggregate_version bigint NOT NULL,source_event_id bigint NOT NULL,updated_on_utc timestamptz NOT NULL,payload_json jsonb NOT NULL,payload_hash text NOT NULL,PRIMARY KEY(portfolio_id,trading_year,decision_horizon,effective_from_utc,fund_id));
            CREATE TABLE IF NOT EXISTS portfolio.fund_template_assignment(portfolio_id int NOT NULL,fund_id int NOT NULL,fund_mandate_version bigint NOT NULL,trade_template_id uuid NOT NULL,trade_template_version bigint NOT NULL,schema_version int NOT NULL,aggregate_version bigint NOT NULL,source_event_id bigint NOT NULL,updated_on_utc timestamptz NOT NULL,payload_json jsonb NOT NULL,payload_hash text NOT NULL,PRIMARY KEY(portfolio_id,fund_id,fund_mandate_version,trade_template_id,trade_template_version));
            CREATE TABLE IF NOT EXISTS portfolio.fund_allocation(portfolio_id int NOT NULL,fund_id int NOT NULL,allocation_version bigint NOT NULL,schema_version int NOT NULL,aggregate_version bigint NOT NULL,source_event_id bigint NOT NULL,updated_on_utc timestamptz NOT NULL,payload_json jsonb NOT NULL,payload_hash text NOT NULL,PRIMARY KEY(portfolio_id,fund_id,allocation_version));
            CREATE TABLE IF NOT EXISTS portfolio.fund_risk_envelope(portfolio_id int NOT NULL,fund_id int NOT NULL,envelope_version bigint NOT NULL,schema_version int NOT NULL,aggregate_version bigint NOT NULL,source_event_id bigint NOT NULL,updated_on_utc timestamptz NOT NULL,payload_json jsonb NOT NULL,payload_hash text NOT NULL,PRIMARY KEY(portfolio_id,fund_id,envelope_version));
            CREATE TABLE IF NOT EXISTS portfolio.fund_order(portfolio_id int NOT NULL,fund_id int NOT NULL,order_month date NOT NULL,created_on_utc timestamptz NOT NULL,order_id int PRIMARY KEY,status text NOT NULL,schema_version int NOT NULL,aggregate_version bigint NOT NULL,source_event_id bigint NOT NULL,updated_on_utc timestamptz NOT NULL,payload_json jsonb NOT NULL,payload_hash text NOT NULL);
            CREATE INDEX IF NOT EXISTS fund_order_timeline ON portfolio.fund_order(portfolio_id,fund_id,order_month,created_on_utc DESC,order_id DESC);
            CREATE TABLE IF NOT EXISTS portfolio.fund_order_trade(order_id int NOT NULL,trade_id int PRIMARY KEY,portfolio_id int NOT NULL,fund_id int NOT NULL,schema_version int NOT NULL,aggregate_version bigint NOT NULL,source_event_id bigint NOT NULL,updated_on_utc timestamptz NOT NULL,payload_json jsonb NOT NULL,payload_hash text NOT NULL);
            CREATE INDEX IF NOT EXISTS fund_order_trade_by_order ON portfolio.fund_order_trade(order_id,trade_id);
            CREATE TABLE IF NOT EXISTS portfolio.fund_composition(workflow_id uuid NOT NULL,order_id int NOT NULL,portfolio_id int NOT NULL,fund_id int NOT NULL,status text NOT NULL,schema_version int NOT NULL,aggregate_version bigint NOT NULL,source_event_id bigint NOT NULL,updated_on_utc timestamptz NOT NULL,payload_json jsonb NOT NULL,payload_hash text NOT NULL,PRIMARY KEY(workflow_id,order_id));
            CREATE TABLE IF NOT EXISTS portfolio.portfolio_policy(policy_id int NOT NULL,policy_version bigint NOT NULL,portfolio_id int NOT NULL,operating_state text NOT NULL,schema_version int NOT NULL,aggregate_version bigint NOT NULL,source_event_id bigint NOT NULL,updated_on_utc timestamptz NOT NULL,payload_json jsonb NOT NULL,payload_hash text NOT NULL,PRIMARY KEY(policy_id,policy_version));
            CREATE INDEX IF NOT EXISTS portfolio_policy_by_portfolio ON portfolio.portfolio_policy(portfolio_id,policy_id DESC,policy_version DESC);
            CREATE TABLE IF NOT EXISTS portfolio.active_portfolio_policy(portfolio_id int PRIMARY KEY,policy_id int NOT NULL,policy_version bigint NOT NULL,schema_version int NOT NULL,aggregate_version bigint NOT NULL,source_event_id bigint NOT NULL,updated_on_utc timestamptz NOT NULL,payload_json jsonb NOT NULL,payload_hash text NOT NULL);
            CREATE TABLE IF NOT EXISTS portfolio.order_composition_decision(operation_id uuid PRIMARY KEY,composition_id uuid NOT NULL,workflow_id uuid NOT NULL,portfolio_id int NOT NULL,status smallint NOT NULL,financial_revision bigint NOT NULL,input_hash text NOT NULL,receipt jsonb NOT NULL,committed_at_utc timestamptz NOT NULL,UNIQUE(portfolio_id,composition_id));
            CREATE TABLE IF NOT EXISTS portfolio.order_composition_fund_decision(operation_id uuid NOT NULL REFERENCES portfolio.order_composition_decision(operation_id) ON DELETE RESTRICT,fund_id int NOT NULL,accepted boolean NOT NULL,reason_code text NOT NULL,order_id int NULL,PRIMARY KEY(operation_id,fund_id));
            CREATE TABLE IF NOT EXISTS portfolio.accepted_trade_order(order_id int PRIMARY KEY,operation_id uuid NOT NULL REFERENCES portfolio.order_composition_decision(operation_id) ON DELETE RESTRICT,portfolio_id int NOT NULL,fund_id int NOT NULL,status smallint NOT NULL,definition_hash text NOT NULL,definition jsonb NOT NULL,valid_until_utc timestamptz NOT NULL,UNIQUE(operation_id,fund_id));
            CREATE TABLE IF NOT EXISTS portfolio.accepted_trade_order_leg(order_id int NOT NULL REFERENCES portfolio.accepted_trade_order(order_id) ON DELETE RESTRICT,component_id uuid NOT NULL,trade_leg_id uuid NOT NULL,ordinal int NOT NULL,definition jsonb NOT NULL,PRIMARY KEY(order_id,component_id,trade_leg_id));
            CREATE TABLE IF NOT EXISTS portfolio.accepted_trade_order_capacity(order_id int NOT NULL REFERENCES portfolio.accepted_trade_order(order_id) ON DELETE RESTRICT,portfolio_id int NOT NULL,fund_id int NOT NULL,scope_kind int NOT NULL,scope_key text NOT NULL,measure int NOT NULL,unit int NOT NULL,amount numeric(38,10) NOT NULL,method_version int NOT NULL,financial_revision bigint NOT NULL,PRIMARY KEY(order_id,scope_kind,scope_key,measure,unit));
            CREATE TABLE IF NOT EXISTS portfolio.accepted_position_close(
              operation_id uuid PRIMARY KEY REFERENCES portfolio.order_composition_decision(operation_id) ON DELETE RESTRICT,
              target_position_id text NOT NULL UNIQUE,target_order_id int NOT NULL,target_trade_id int NOT NULL,
              close_order_id int NOT NULL UNIQUE REFERENCES portfolio.accepted_trade_order(order_id) ON DELETE RESTRICT,
              accepted_at_utc timestamptz NOT NULL);
            """ + FinancialHistory;

    /// <summary>Drops the Portfolio projection schema.</summary>
    public const string Drop = "DROP SCHEMA IF EXISTS portfolio CASCADE;";
}
