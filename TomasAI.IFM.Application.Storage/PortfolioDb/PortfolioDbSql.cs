namespace TomasAI.IFM.Application.Storage.PortfolioDb;

public static class PortfolioDbSql
{
    public static class Financial
    {
        public const string ReadBook = "SELECT policy_source_versions::text FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;";
        public const string ReadActiveBookByExecutionAccount = """
        SELECT a.policy_source_versions::text
        FROM portfolio_financial.ledger_book b
        JOIN portfolio_financial.financial_authority a
          ON a.portfolio_id=b.portfolio_id AND a.book_id=b.book_id
        WHERE b.environment=$1 AND b.execution_account_ref=$2 AND b.status='Active';
        """;

        public static class AccountingExportStore
        {
            public const string Select01 = """
                    SELECT journal_id,transaction_id,book_id,fund_id,accounting_date,journal_hash,financial_revision,reversal_journal_id
                    FROM portfolio_financial.ledger_journal WHERE portfolio_id=$1 AND book_id=$2 AND journal_id=$3 AND financial_revision<=$4;
                    """;
            public const string Select02 = """
                    SELECT ordinal,account_id,account_version,fund_id,debit,credit,source_line_reference
                    FROM portfolio_financial.ledger_entry WHERE journal_id=$1 ORDER BY ordinal LIMIT 257;
                    """;
            public const string Insert01 = """
                INSERT INTO portfolio_financial.accounting_export(destination_company,export_id,source_set,source_cut,payload_hash,mapping_version,delivery_status,metadata)
                VALUES($1,$2,$3,$4,$5,$6,'Pending',$7);
                """;
            public const string Insert02 = """
                    INSERT INTO portfolio_financial.accounting_export_source(destination_company,journal_id,export_id) VALUES($1,$2,$3);
                    """;
            public const string Select03 = """
                SELECT outcome,external_receipt FROM portfolio_financial.accounting_export_attempt
                WHERE destination_company=$1 AND export_id=$2 AND attempt_id=$3;
                """;
            public const string Insert03 = """
                INSERT INTO portfolio_financial.accounting_export_attempt(destination_company,export_id,attempt_id,outcome,external_receipt)
                VALUES($1,$2,$3,$4,$5);
                """;
            public const string Update01 = """
                UPDATE portfolio_financial.accounting_export SET retry_count=$3,delivery_status=$4,external_receipt=$5
                WHERE destination_company=$1 AND export_id=$2;
                """;
            public const string Select04 = "SELECT metadata::text,delivery_status,retry_count,external_receipt,payload_hash FROM portfolio_financial.accounting_export WHERE destination_company=$1 AND export_id=$2";
        }

        public static class CapacityExpiryDispatchStore
        {
            public const string Select01 = """
            SELECT r.portfolio_id,r.fund_id,r.reservation_id,r.version,r.strategy_units,r.requirements->>'ContentHash',
              a.financial_revision,r.expires_at_utc
            FROM portfolio_financial.capacity_reservation r JOIN portfolio_financial.financial_authority a USING(portfolio_id)
            WHERE r.status=1 AND r.execution_id IS NULL AND r.expires_at_utc<=$1 AND ($2::int IS NULL OR r.portfolio_id=$2)
              AND NOT EXISTS(SELECT 1 FROM portfolio_financial.capacity_expiry_dispatch d WHERE d.reservation_id=r.reservation_id AND d.status='Pending')
            ORDER BY r.expires_at_utc,r.reservation_id LIMIT 32;
            """;
            public const string Insert01 = """
                INSERT INTO portfolio_financial.capacity_expiry_dispatch(operation_id,reservation_id,portfolio_id,fund_id,request,input_hash,status,created_at_utc)
                VALUES($1,$2,$3,$4,$5,$6,'Pending',$7) ON CONFLICT DO NOTHING;
                """;
            public const string Select02 = """
            SELECT fund_id,request::text FROM portfolio_financial.capacity_expiry_dispatch WHERE status='Pending' AND ($1::int IS NULL OR portfolio_id=$1)
            ORDER BY created_at_utc,operation_id LIMIT 32;
            """;
            public const string Update01 = """
            UPDATE portfolio_financial.capacity_expiry_dispatch SET status=$3,reconciled_at_utc=$4
            WHERE operation_id=$1 AND input_hash=$2 AND status='Pending';
            """;
        }

        public static class CapacityReservationStore
        {
            public const string Select01 = """
            SELECT u.scope_kind,u.scope_key,u.measure,u.unit,u.held,u.working,u.position
            FROM portfolio_financial.capacity_usage u
            WHERE u.portfolio_id=$1 AND EXISTS (
              SELECT 1 FROM jsonb_to_recordset($2::jsonb) AS requested(scope_kind int,scope_key text,measure int,unit int)
              WHERE requested.scope_kind=u.scope_kind AND requested.scope_key=u.scope_key AND requested.measure=u.measure AND requested.unit=u.unit)
            LIMIT 257;
            """;
            public const string Select02 = """
            SELECT reservation_id FROM portfolio_financial.capacity_reservation
            WHERE reservation_id=$1 OR (portfolio_id=$2 AND order_id=$3 AND status NOT IN (8,9));
            """;
            public const string Insert01 = """
            INSERT INTO portfolio_financial.capacity_reservation(reservation_id,portfolio_id,operation_id,fund_id,book_id,order_id,
              candidate_hash,risk_hash,sized_order_hash,strategy_units,requirements,environment,expires_at_utc,status,version,
              completion_event_id,request,receipt,input_hash,remaining_units)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,1,1,$14,$15,$16,$17,$10);
            """;
            public const string Select03 = "SELECT operation_id FROM portfolio_financial.capacity_lifecycle WHERE reservation_id=$1 AND source_identity=$2;";
            public const string Update01 = """
            UPDATE portfolio_financial.capacity_reservation SET status=$3,version=$4,execution_id=$5,
              filled_units=$6,cancelled_units=$7,remaining_units=$8,closed_units=$9 WHERE reservation_id=$1 AND portfolio_id=$2;
            """;
            public const string Insert02 = """
            INSERT INTO portfolio_financial.capacity_lifecycle(reservation_id,version,source_identity,operation_id,prior_status,new_status,
              usage_delta,filled_units,cancelled_units,remaining_units,execution_id,execution_revision,committed_at_utc,completion_event_id,input_hash,receipt)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16);
            """;
            public const string Select04 = """
            SELECT r.version,r.status,r.strategy_units,r.filled_units,r.cancelled_units,r.remaining_units,
              r.requirements->>'ContentHash',r.expires_at_utc,r.execution_id,
              coalesce((SELECT max(l.execution_revision) FROM portfolio_financial.capacity_lifecycle l WHERE l.reservation_id=r.reservation_id),0),r.request::text,r.closed_units
            FROM portfolio_financial.capacity_reservation r WHERE portfolio_id=$1 AND reservation_id=$2;
            """;
            public const string Select05 = """
            SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.EventPayload,e.commandid,e.eventtimestamp::text,e.streamversion
            FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid WHERE e.commandid=$1 ORDER BY e.eventversion LIMIT 65;
            """;
            public const string Insert03 = """
        INSERT INTO portfolio_financial.capacity_usage(portfolio_id,scope_kind,scope_key,measure,unit,held,working,position,revision)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9) ON CONFLICT(portfolio_id,scope_kind,scope_key,measure,unit)
        DO UPDATE SET held=capacity_usage.held+EXCLUDED.held,working=capacity_usage.working+EXCLUDED.working,
          position=capacity_usage.position+EXCLUDED.position,revision=EXCLUDED.revision;
        """;
        }

        public static class EmulatorExecutionStore
        {
            public const string Select01 = "SELECT operation_id FROM portfolio_financial.emulator_order WHERE execution_id=$1;";
            public const string Select02 = """
            SELECT status,version,execution_id,request::text FROM portfolio_financial.capacity_reservation
            WHERE portfolio_id=$1 AND reservation_id=$2;
            """;
            public const string Insert01 = """
            INSERT INTO portfolio_financial.emulator_order(execution_id,portfolio_id,fund_id,order_id,reservation_id,operation_id,
              request,receipt,input_hash,submitted_at_utc) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10);
            """;
        }

        public static class FinancialAuthorityPreparationStore
        {
            public const string Select01 = """
            SELECT policy_source_versions::text,financial_revision,authority_epoch,operating_state
            FROM portfolio_financial.financial_authority WHERE portfolio_id=$1 FOR SHARE;
            """;
        }

        public static class FinancialHistoryJournal
        {
            public const string Select01 = """
            SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.EventPayload,e.commandid,e.eventtimestamp::text,e.streamversion
            FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
            JOIN portfolio_financial.financial_operation_receipt o ON o.event_version=e.eventversion
            WHERE n.eventname=ANY($1) AND ($2::int IS NULL OR o.portfolio_id=$2)
              AND NOT EXISTS(SELECT 1 FROM portfolio_financial.financial_history_receipt r WHERE r.event_version=e.eventversion)
            ORDER BY e.eventversion LIMIT 32;
            """;
            public const string Insert01 = """
            INSERT INTO portfolio_financial.financial_history_receipt(event_version,projected_at_utc)
            VALUES($1,$2) ON CONFLICT DO NOTHING;
            """;
        }

        public static class FinancialHistoryProjection
        {
            public const string Create01 = """
        CREATE TABLE IF NOT EXISTS portfolio.financial_operation_by_portfolio_month(
          portfolio_id int NOT NULL, month int NOT NULL, financial_revision bigint NOT NULL,
          operation_id uuid NOT NULL, source_event_id bigint NOT NULL,
          event_type text NOT NULL, committed_at_utc timestamptz NOT NULL,
          payload_json jsonb NOT NULL,
          PRIMARY KEY(portfolio_id,month,financial_revision,operation_id));
        """;
            public const string Insert01 = """
        INSERT INTO portfolio.financial_operation_by_portfolio_month(
          portfolio_id,month,financial_revision,operation_id,source_event_id,event_type,committed_at_utc,payload_json)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8)
        ON CONFLICT(portfolio_id,month,financial_revision,operation_id) DO NOTHING;
        """;
        }

        public static class FinancialQueryStore
        {
            public const string Select01 = "SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;";
            public const string Select02 = """
            SELECT period_id,start_date,end_date,revision,state FROM portfolio_financial.ledger_period
            WHERE book_id=$1 ORDER BY start_date DESC LIMIT 257;
            """;
            public const string Select03 = """
            SELECT DISTINCT ON (account_id) account_id,version,category,normal_side,fund_dimension_required,content_hash,status
            FROM portfolio_financial.ledger_account WHERE book_id=$1 ORDER BY account_id,version DESC LIMIT 257;
            """;
            public const string Select04 = """
            SELECT DISTINCT ON (rule_id) payload::text,status,effective_from,effective_to FROM portfolio_financial.ledger_posting_rule
            WHERE book_id=$1 ORDER BY rule_id,version DESC LIMIT 257;
            """;
            public const string Select05 = """
            SELECT reconciliation_id,(counts->>'FinancialRevision')::bigint,(counts->>'JournalCount')::bigint,
                (counts->>'EntryCount')::bigint,(totals->>'Debits')::numeric,(totals->>'Credits')::numeric,
                differences::text,source_cut,content_hash
            FROM portfolio_financial.ledger_reconciliation WHERE book_id=$1 AND fund_id IS NULL
            ORDER BY (counts->>'FinancialRevision')::bigint DESC,reconciliation_id DESC LIMIT 1;
            """;
            public const string Select06 = """
            SELECT payload::text FROM portfolio_financial.ledger_posting_rule WHERE book_id=$1 AND status='Active'
              AND effective_from<=$2 AND (effective_to IS NULL OR effective_to>=$2) ORDER BY kind,rule_id,version LIMIT 257;
            """;
            public const string Select07 = "SELECT state='Open' FROM portfolio_financial.ledger_period WHERE book_id=$1 AND start_date<=$2 AND end_date>=$2;";
            public const string Select08 = "SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;";
            public const string Select09 = "SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;";
            public const string Select10 = """
            SELECT scope_kind,scope_key,measure,unit,held,working,position FROM portfolio_financial.capacity_usage
            WHERE portfolio_id=$1 AND ((scope_kind=1 AND scope_key=$2) OR (scope_kind=3 AND scope_key=$3)
              OR (scope_kind=2 AND scope_key=$4) OR (scope_kind=4 AND scope_key=$5))
            ORDER BY scope_kind,scope_key,measure,unit LIMIT 257;
            """;
            public const string Select11 = """
                    SELECT count(*)>0 AND bool_and(fund_id=$3) FROM portfolio_financial.ledger_transaction WHERE portfolio_id=$1 AND operation_id=$2;
                    """;
            public const string Select12 = """
            SELECT journal_id,transaction_id,book_id,fund_id,accounting_date,journal_hash FROM portfolio_financial.ledger_journal
            WHERE portfolio_id=$1 AND journal_id=$2 AND ($3::int IS NULL OR fund_id=$3);
            """;
            public const string Select13 = """
            SELECT ordinal,account_id,account_version,fund_id,debit,credit,source_line_reference
            FROM portfolio_financial.ledger_entry WHERE journal_id=$1 ORDER BY ordinal LIMIT 257;
            """;
            public const string Select14 = """
            SELECT coalesce(sum(amount),0) FROM portfolio_financial.financial_encumbrance
            WHERE book_id=$1 AND ($2::int IS NULL OR fund_id=$2) AND status='Pending';
            """;
            public const string Select15 = "SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;";
            public const string Select16 = """
            SELECT scope_kind,scope_key,measure,unit,held,working,position FROM portfolio_financial.capacity_usage
            WHERE portfolio_id=$1 AND ($2::text IS NULL OR (scope_kind=3 AND scope_key=$2))
            ORDER BY scope_kind,scope_key,measure,unit LIMIT 4097;
            """;
            public const string Select17 = """
            SELECT t.transaction_id,t.operation_id,o.financial_revision,t.item_ordinal,t.payload::text,j.journal_id
            FROM portfolio_financial.ledger_transaction t JOIN portfolio_financial.financial_operation_receipt o
              ON o.portfolio_id=t.portfolio_id AND o.operation_id=t.operation_id
            LEFT JOIN portfolio_financial.ledger_journal j ON j.transaction_id=t.transaction_id
            WHERE t.portfolio_id=$1 AND t.fund_id=$2 AND o.financial_revision<=$3
              AND (o.financial_revision,t.item_ordinal)>($4,$5)
            ORDER BY o.financial_revision,t.item_ordinal LIMIT $6;
            """;
            public const string Select18 = """
            SELECT reconciliation_id,source_cut,status,content_hash,differences::text FROM portfolio_financial.ledger_reconciliation
            WHERE portfolio_id=$1 AND reconciliation_id=$2 AND ($3::int IS NULL OR fund_id=$3);
            """;
            public const string Select19 = """
                SELECT policy_source_versions::text,financial_revision FROM portfolio_financial.financial_authority WHERE portfolio_id=$1 FOR SHARE;
                """;
            public const string Select20 = """
            SELECT account_id,fund_id,currency,debit_total,credit_total,balance FROM portfolio_financial.ledger_account_balance
            WHERE book_id=$1 AND ($2::int IS NULL OR fund_id=$2) ORDER BY account_id,fund_id LIMIT 4097;
            """;
            public const string Select21 = "SELECT fund_id FROM portfolio_financial.capacity_reservation WHERE portfolio_id=$1 AND reservation_id=$2;";
            public const string Select22 = """
            SELECT r.receipt::text,r.reservation_id,r.version,r.status,r.strategy_units,r.filled_units,r.cancelled_units,r.remaining_units,
              r.requirements->>'ContentHash',r.expires_at_utc,r.execution_id,
              coalesce((SELECT max(l.execution_revision) FROM portfolio_financial.capacity_lifecycle l WHERE l.reservation_id=r.reservation_id),0),r.closed_units
            FROM portfolio_financial.capacity_reservation r JOIN portfolio_financial.financial_operation_receipt o
              ON o.portfolio_id=r.portfolio_id AND o.operation_id=r.operation_id
            WHERE r.portfolio_id=$1 AND ($2::int IS NULL OR r.fund_id=$2) AND ($3::uuid IS NULL OR r.reservation_id=$3)
              AND o.financial_revision>$4 AND o.financial_revision<=$5 ORDER BY o.financial_revision LIMIT $6;
            """;
        }

        public static class FinancialWorkflowRecoveryJournal
        {
            public const string With01 = """
                WITH page AS (
                    SELECT eventstreamid FROM event_stream_id
                    WHERE eventstreamid>$1 AND eventstream LIKE 'Command.IntrinsicTimeStrategyWorkflowCommand.%'
                      AND ($2::text IS NULL OR eventstream=$2)
                    ORDER BY eventstreamid LIMIT 32
                )
                SELECT p.eventstreamid::bigint,n.eventname,n.eventtypename,e.eventversion,e.EventPayload,
                       e.commandid,e.eventtimestamp::text,e.streamversion
                FROM page p
                LEFT JOIN LATERAL (
                    SELECT * FROM event_log l WHERE l.eventstreamid=p.eventstreamid
                    ORDER BY streamversion DESC LIMIT 1
                ) e ON true
                LEFT JOIN event_name_id n ON n.eventnameid=e.eventnameid
                ORDER BY p.eventstreamid;
                """;
        }

        public static class FundRiskAuthorizationStore
        {
            public const string Select01 = """
            SELECT status,version,request::text,receipt::text FROM portfolio_financial.capacity_reservation
            WHERE portfolio_id=$1 AND reservation_id=$2;
            """;
        }

        public static class GeneralLedgerStore
        {
            public const string Select01 = """
                    SELECT source_content_hash,operation_id FROM portfolio_financial.financial_source_receipt
                    WHERE book_id=$1 AND source_system=$2 AND source_event_key=$3 AND posting_purpose=$4;
                    """;
            public const string Select02 = """
                    SELECT state FROM portfolio_financial.ledger_period WHERE book_id=$1 AND start_date<=$2 AND end_date>=$2;
                    """;
            public const string Select03 = """
                    SELECT payload::text FROM portfolio_financial.ledger_posting_rule WHERE book_id=$1 AND rule_id=$2 AND version=$3
                    AND status='Active' AND effective_from<=$4 AND (effective_to IS NULL OR effective_to>=$4);
                    """;
            public const string Select04 = """
                    SELECT amount,source_sequence FROM portfolio_financial.ledger_valuation WHERE book_id=$1 AND fund_id=$2 AND position_key=$3;
                    """;
            public const string Select05 = "SELECT fund_id FROM portfolio_financial.ledger_journal WHERE book_id=$1 AND journal_id=$2;";
            public const string Select06 = """
                        SELECT coalesce(sum(t.amount),0) FROM portfolio_financial.ledger_journal j JOIN portfolio_financial.ledger_transaction t ON t.transaction_id=j.transaction_id
                        WHERE j.reversal_journal_id=$1;
                        """;
            public const string Insert01 = """
                    INSERT INTO portfolio_financial.ledger_transaction(transaction_id,book_id,portfolio_id,fund_id,operation_id,item_ordinal,kind,
                    source_system,source_event_key,source_hash,amount,currency,accounting_date,value_date,settlement_date,obligation_id,related_journal_id,payload)
                    VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'USD',$12,$13,$14,$15,$16,$17);
                    """;
            public const string Insert02 = """
                        INSERT INTO portfolio_financial.ledger_journal(journal_id,transaction_id,book_id,portfolio_id,fund_id,operation_id,accounting_date,value_date,settlement_date,
                        kind,source_hash,rule_id,rule_version,reversal_journal_id,committed_at_utc,financial_revision,journal_hash)
                        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17);
                        """;
            public const string Insert03 = """
                    INSERT INTO portfolio_financial.financial_source_receipt(book_id,source_system,source_event_key,posting_purpose,source_content_hash,operation_id,journal_id)
                    VALUES($1,$2,$3,$4,$5,$6,$7);
                    """;
            public const string Insert04 = """
                        INSERT INTO portfolio_financial.ledger_valuation(book_id,fund_id,position_key,amount,source_sequence,revision) VALUES($1,$2,$3,$4,$5,$6)
                        ON CONFLICT(book_id,fund_id,position_key) DO UPDATE SET amount=EXCLUDED.amount,source_sequence=EXCLUDED.source_sequence,revision=EXCLUDED.revision;
                        """;
            public const string Update01 = "UPDATE portfolio_financial.financial_authority SET operating_state='Overdrawn' WHERE portfolio_id=$1;";
            public const string Insert05 = """
                INSERT INTO portfolio_financial.ledger_posting_receipt(portfolio_id,operation_id,execution_id,input_hash,receipt_type,manifest,completion_event_id,committed_at_utc,financial_revision,payload)
                VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10);
                """;
            public const string Select07 = """
            SELECT coalesce(sum(b.balance),0) FROM portfolio_financial.ledger_account_balance b
            WHERE b.book_id=$1 AND b.fund_id=$2 AND EXISTS(SELECT 1 FROM portfolio_financial.ledger_account a
              WHERE a.book_id=b.book_id AND a.account_id=b.account_id AND a.category='Cash');
            """;
            public const string Select08 = "SELECT coalesce(sum(amount),0) FROM portfolio_financial.financial_encumbrance WHERE book_id=$1 AND fund_id=$2 AND status='Pending';";
            public const string With01 = """
            WITH paid AS (
              SELECT f.reservation_id,f.component,sum(greatest(0,f.cash_paid-coalesce((
                SELECT sum(e.debit-e.credit) FROM portfolio_financial.ledger_journal reversal
                  JOIN portfolio_financial.ledger_entry e ON e.journal_id=reversal.journal_id
                WHERE reversal.reversal_journal_id=j.journal_id AND e.fund_id=$2 AND EXISTS(
                  SELECT 1 FROM portfolio_financial.ledger_account a WHERE a.book_id=e.book_id AND a.account_id=e.account_id AND a.category='Cash')),0))) amount
              FROM portfolio_financial.capacity_funding_receipt f JOIN portfolio_financial.ledger_journal j ON j.transaction_id=f.transaction_id
              WHERE j.book_id=$1 AND j.fund_id=$2 GROUP BY f.reservation_id,f.component),
            holds AS (
              SELECT r.reservation_id,(r.remaining_units+r.filled_units-r.closed_units)::numeric/r.strategy_units fraction,
                coalesce((r.requirements->>'SettlementCash')::numeric,0) settlement,
                coalesce((r.requirements->>'MarginFunding')::numeric,0) margin,
                coalesce((r.requirements->>'FeeReserve')::numeric,0) fee,
                coalesce((r.requirements->>'VariationReserve')::numeric,0) variation
              FROM portfolio_financial.capacity_reservation r WHERE r.book_id=$1 AND r.fund_id=$2 AND r.status NOT IN (8,9))
            SELECT coalesce(sum(ceil(100*(greatest(0,h.settlement*h.fraction-coalesce(s.amount,0))+
              greatest(0,h.margin*h.fraction-coalesce(m.amount,0))+greatest(0,h.fee*h.fraction-coalesce(f.amount,0))+h.variation*h.fraction))/100),0)
            FROM holds h LEFT JOIN paid s ON s.reservation_id=h.reservation_id AND s.component=1
              LEFT JOIN paid f ON f.reservation_id=h.reservation_id AND f.component=2
              LEFT JOIN paid m ON m.reservation_id=h.reservation_id AND m.component=3;
            """;
            public const string Select09 = """
            SELECT order_id,execution_id,request::text FROM portfolio_financial.capacity_reservation
            WHERE reservation_id=$1 AND book_id=$2 AND fund_id=$3;
            """;
            public const string Select10 = """
            SELECT coalesce(sum(e.credit-e.debit),0) FROM portfolio_financial.ledger_entry e WHERE e.journal_id=$1 AND e.fund_id=$2
            AND EXISTS(SELECT 1 FROM portfolio_financial.ledger_account a WHERE a.book_id=e.book_id AND a.account_id=e.account_id AND a.category='Cash');
            """;
            public const string Insert06 = "INSERT INTO portfolio_financial.capacity_funding_receipt(transaction_id,reservation_id,component,cash_paid) VALUES($1,$2,$3,$4);";
            public const string Insert07 = """
                INSERT INTO portfolio_financial.financial_encumbrance(obligation_id,portfolio_id,book_id,fund_id,source_identity,kind,amount,currency,status,revision,evidence)
                VALUES($1,$2,$3,$4,$5,'Withdrawal',$6,'USD','Pending',$7,$8);
                """;
            public const string Update02 = """
                UPDATE portfolio_financial.financial_encumbrance SET amount=amount-$1,status=CASE WHEN amount=$1 THEN $2 ELSE 'Pending' END,revision=$3,evidence=$4
                WHERE obligation_id=$5 AND portfolio_id=$6 AND book_id=$7 AND fund_id=$8 AND status='Pending' AND amount>=$1;
                """;
            public const string Select11 = """
            SELECT fund_dimension_required FROM portfolio_financial.ledger_account WHERE book_id=$1 AND account_id=$2 AND version=$3
            AND (status='Active' OR ($4 AND status='Retired'));
            """;
            public const string Insert08 = """
            INSERT INTO portfolio_financial.ledger_entry(journal_id,ordinal,book_id,account_id,account_version,fund_id,debit,credit,currency,source_line_reference,order_id,trade_id)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,'USD',$9,$10,$11);
            """;
            public const string Insert09 = """
            INSERT INTO portfolio_financial.ledger_account_balance(book_id,account_id,fund_id,currency,debit_total,credit_total,balance,revision)
            VALUES($1,$2,$3,'USD',$4,$5,$4-$5,$6)
            ON CONFLICT(book_id,account_id,fund_id,currency) DO UPDATE SET debit_total=portfolio_financial.ledger_account_balance.debit_total+EXCLUDED.debit_total,
            credit_total=portfolio_financial.ledger_account_balance.credit_total+EXCLUDED.credit_total,
            balance=portfolio_financial.ledger_account_balance.balance+EXCLUDED.balance,revision=EXCLUDED.revision;
            """;
            public const string Select12 = """
        SELECT ordinal,account_id,account_version,fund_id,debit,credit,order_id,trade_id,source_line_reference FROM portfolio_financial.ledger_entry WHERE journal_id=$1 ORDER BY ordinal;
        """;
        }

        public static class LedgerConfigurationStore
        {
            public const string Select01 = "SELECT pg_advisory_xact_lock(34100,$1);";
            public const string Update01 = """
                        UPDATE portfolio_financial.financial_authority SET policy_source_versions=$2,migration_state='QualifiedDevelopmentFresh',operating_state=$3 WHERE portfolio_id=$1;
                        """;
            public const string Select03 = "SELECT coalesce(max(version),0) FROM portfolio_financial.ledger_account WHERE book_id=$1 AND account_id=$2;";
            public const string Select04 = "SELECT category FROM portfolio_financial.ledger_account WHERE book_id=$1 AND account_id=$2 ORDER BY version DESC LIMIT 1;";
            public const string Insert02 = """
                            INSERT INTO portfolio_financial.ledger_account(book_id,account_id,version,category,normal_side,currency,fund_dimension_required,status,content_hash)
                            VALUES($1,$2,$3,$4,$5,'USD',$6,'Active',$7);
                            """;
            public const string Select05 = "SELECT coalesce(max(version),0) FROM portfolio_financial.ledger_posting_rule WHERE book_id=$1 AND rule_id=$2;";
            public const string Select06 = "SELECT status FROM portfolio_financial.ledger_account WHERE book_id=$1 AND account_id=$2 AND version=$3;";
            public const string Insert03 = """
                            INSERT INTO portfolio_financial.ledger_posting_rule(book_id,rule_id,version,kind,payload,content_hash,status,effective_from)
                            VALUES($1,$2,$3,$4,$5,$6,'Active',$7);
                            """;
            public const string Select07 = """
                            SELECT count(*) FROM portfolio_financial.ledger_posting_rule WHERE book_id=$1 AND status='Active'
                            AND (payload->'Debit'->>'AccountId'=$2 OR payload->'Credit'->>'AccountId'=$2
                            OR payload->'ValuationAsset'->>'AccountId'=$2 OR payload->'UnrealizedPnl'->>'AccountId'=$2);
                            """;
            public const string Update02 = "UPDATE portfolio_financial.ledger_account SET status='Retired' WHERE book_id=$1 AND account_id=$2 AND version=$3 AND status='Active';";
            public const string Update03 = "UPDATE portfolio_financial.ledger_posting_rule SET status='Retired' WHERE book_id=$1 AND rule_id=$2 AND version=$3 AND status='Active';";
            public const string Insert04 = """
                        INSERT INTO portfolio_financial.ledger_period(book_id,period_id,start_date,end_date,state,revision,source_cut,evidence)
                        VALUES($1,$2,$3,$4,'Open',1,$5,$6);
                        """;
            public const string Update04 = """
                        UPDATE portfolio_financial.ledger_period SET state=$4,revision=revision+1,source_cut=$5,evidence=$6
                        WHERE book_id=$1 AND period_id=$2 AND revision=$3 AND state=$7;
                        """;
            public const string Select08 = """
                            SELECT count(*) FROM portfolio_financial.capacity_usage WHERE portfolio_id=$1 AND scope_kind=$2
                              AND scope_key NOT LIKE 'U1:%' AND (held<>0 OR working<>0 OR position<>0);
                            """;
            public const string Select09 = "SELECT authority_epoch FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;";
            public const string Update05 = """
                        UPDATE portfolio_financial.financial_authority SET policy_source_versions=$2,source_watermark=$3,valuation_watermark=$4,operating_state=$5 WHERE portfolio_id=$1;
                        """;
            public const string Insert05 = """
                        INSERT INTO portfolio_financial.ledger_reconciliation(reconciliation_id,book_id,portfolio_id,fund_id,source_cut,counts,totals,content_hash,differences,resolution_links,status)
                        VALUES($1,$2,$3,NULL,$4,$5,$6,$7,$8,'[]',$9);
                        """;
            public const string Update06 = "UPDATE portfolio_financial.financial_authority SET operating_state=$2 WHERE portfolio_id=$1;";
            public const string Update07 = "UPDATE portfolio_financial.financial_authority SET operating_state=$2 WHERE portfolio_id=$1;";
            public const string Select10 = "SELECT currentversion FROM event_stream_id WHERE eventstream=$1 FOR SHARE;";
            public const string Select11 = """
            SELECT r.status='Matched' AND r.source_cut=$3 AND NOT EXISTS(
              SELECT 1 FROM portfolio_financial.ledger_journal j WHERE j.book_id=r.book_id AND j.financial_revision>(r.counts->>'FinancialRevision')::bigint)
            FROM portfolio_financial.ledger_reconciliation r WHERE r.book_id=$1 AND r.reconciliation_id=$2;
            """;
            public const string Select12 = """
            SELECT count(DISTINCT j.journal_id),count(e.ordinal),coalesce(sum(e.debit),0),coalesce(sum(e.credit),0)
            FROM portfolio_financial.ledger_journal j LEFT JOIN portfolio_financial.ledger_entry e ON e.journal_id=j.journal_id WHERE j.book_id=$1;
            """;
            public const string With01 = """
            WITH actual AS (SELECT account_id,fund_id,sum(debit) d,sum(credit) c FROM portfolio_financial.ledger_entry WHERE book_id=$1 GROUP BY account_id,fund_id),
            recorded AS (SELECT account_id,fund_id,debit_total d,credit_total c FROM portfolio_financial.ledger_account_balance WHERE book_id=$1),
            keys AS (SELECT account_id,fund_id FROM actual UNION SELECT account_id,fund_id FROM recorded)
            SELECT k.account_id,k.fund_id,coalesce(a.d,0),coalesce(a.c,0),coalesce(r.d,0),coalesce(r.c,0)
            FROM keys k LEFT JOIN actual a ON a.account_id=k.account_id AND a.fund_id IS NOT DISTINCT FROM k.fund_id
            LEFT JOIN recorded r ON r.account_id=k.account_id AND r.fund_id IS NOT DISTINCT FROM k.fund_id
            WHERE coalesce(a.d,0)<>coalesce(r.d,0) OR coalesce(a.c,0)<>coalesce(r.c,0) ORDER BY k.account_id,k.fund_id;
            """;
        }

        public static class PortfolioAuthorityFence
        {
            public const string Select01 = "SELECT pg_advisory_xact_lock(34100,$1);";
            public const string Select02 = """
            SELECT policy_source_versions::text,authority_epoch,financial_revision,operating_state FROM portfolio_financial.financial_authority
            WHERE portfolio_id=$1 FOR UPDATE;
            """;
            public const string Select03 = "SELECT reservation_id FROM portfolio_financial.capacity_reservation WHERE portfolio_id=$1 AND order_id=$2 AND status NOT IN (8,9) LIMIT 1;";
            public const string Update01 = "UPDATE portfolio_financial.financial_authority SET financial_revision=financial_revision+1 WHERE portfolio_id=$1;";
            public const string Update02 = """
                UPDATE portfolio_financial.financial_authority SET authority_epoch=$2,financial_revision=$3,operating_state='NeedsRefresh'
                WHERE portfolio_id=$1;
                """;
            public const string Update03 = "UPDATE portfolio_financial.financial_authority SET policy_source_versions=$2 WHERE portfolio_id=$1;";
        }

        public static class PortfolioFinancialSchema
        {
            public const string Select01 = "SELECT version FROM portfolio_financial.schema_version WHERE singleton=true;";
            public const string Create01 = """
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
        }

        public static class PortfolioDbFinancialSupport
        {
            public const string Select01 = "SELECT pg_advisory_xact_lock(34100,$1);";
            public const string Insert01 = """
            INSERT INTO portfolio_financial.ledger_book(book_id,accounting_entity_id,portfolio_id,base_currency,execution_account_ref,environment,version,status)
            VALUES($1,$2,$3,'USD',$4,$5,1,'Active');
            """;
            public const string Insert02 = """
            INSERT INTO portfolio_financial.financial_authority(portfolio_id,book_id,authority_epoch,operating_state,policy_source_versions,source_watermark,valuation_watermark,migration_state)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8);
            """;
            public const string Insert03 = """
                INSERT INTO portfolio_financial.ledger_account(book_id,account_id,version,category,normal_side,currency,fund_dimension_required,status,content_hash)
                VALUES($1,$2,$3,$4,$5,'USD',$6,'Active',$7);
                """;
            public const string Insert04 = """
                INSERT INTO portfolio_financial.ledger_posting_rule(book_id,rule_id,version,kind,payload,content_hash,status,effective_from)
                VALUES($1,$2,$3,$4,$5,$6,'Active',$7);
                """;
            public const string Insert05 = """
            INSERT INTO portfolio_financial.ledger_period(book_id,period_id,start_date,end_date,state,revision,source_cut,evidence)
            VALUES($1,$2,$3,$4,'Open',1,$5,'{}');
            """;
            public const string Select02 = """
            SELECT r.input_hash,e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.EventPayload,e.commandid,e.eventtimestamp::text,e.streamversion
            FROM portfolio_financial.financial_operation_receipt r JOIN event_log e ON e.eventversion=r.event_version
            JOIN event_name_id n ON n.eventnameid=e.eventnameid WHERE r.portfolio_id=$1 AND r.operation_id=$2;
            """;
            public const string Select03 = """
            SELECT policy_source_versions::text,financial_revision,operating_state FROM portfolio_financial.financial_authority
            WHERE portfolio_id=$1 FOR UPDATE;
            """;
            public const string Select04 = "SELECT currentversion FROM event_stream_id WHERE eventstream=$1 FOR SHARE;";
            public const string Select05 = "SELECT currentversion FROM event_stream_id WHERE eventstream=$1;";
            public const string Insert06 = """
            INSERT INTO portfolio_financial.financial_operation_receipt(portfolio_id,operation_id,actor_name,input_hash,event_version,financial_revision)
            VALUES($1,$2,$3,$4,$5,$6);
            """;
            public const string Update01 = "UPDATE portfolio_financial.financial_authority SET financial_revision=$2 WHERE portfolio_id=$1;";
        }
    }

    public static class Schema
    {
        public const string Create="""
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
            """ + Financial.FinancialHistoryProjection.Create01;
        public const string Drop="DROP SCHEMA IF EXISTS portfolio CASCADE;";
    }

    public static class Portfolio
    {
        public const string Get="SELECT payload_json::text FROM portfolio.portfolio_by_id WHERE portfolio_id=$1 ORDER BY portfolio_version DESC LIMIT 1;";
        public const string Revision="SELECT aggregate_version,source_event_id FROM portfolio.portfolio_by_id WHERE portfolio_id=$1 ORDER BY portfolio_version DESC LIMIT 1;";
        public const string ByState="SELECT payload_json::text FROM portfolio.portfolio_by_state WHERE operating_state=$1 AND state_bucket=$2 AND portfolio_id>$3 ORDER BY portfolio_id LIMIT $4;";
        public const string Upsert="""
            WITH saved_portfolio AS (
              INSERT INTO portfolio.portfolio_by_id(portfolio_id,portfolio_version,operating_state,schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash)
              SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9 WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio' AND entity_key=$1::text AND source_event_id>=$6)
              ON CONFLICT(portfolio_id,portfolio_version) DO UPDATE SET operating_state=EXCLUDED.operating_state,schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash WHERE portfolio.portfolio_by_id.source_event_id<=EXCLUDED.source_event_id RETURNING 1),
            removed_state AS (
              DELETE FROM portfolio.portfolio_by_state WHERE portfolio_id=$1 AND (operating_state<>$3 OR state_bucket<>$10) AND source_event_id<=$6 RETURNING 1)
            INSERT INTO portfolio.portfolio_by_state(operating_state,state_bucket,portfolio_id,portfolio_version,schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash)
            SELECT $3,$10,$1,$2,$4,$5,$6,$7,$8,$9 WHERE EXISTS(SELECT 1 FROM saved_portfolio)
            ON CONFLICT(operating_state,state_bucket,portfolio_id) DO UPDATE SET portfolio_version=EXCLUDED.portfolio_version,schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash WHERE portfolio.portfolio_by_state.source_event_id<=EXCLUDED.source_event_id
            """;
        public const string DeleteDraft="""
            WITH tombstone AS (
              INSERT INTO portfolio.projection_tombstone(entity_kind,entity_key,source_event_id) VALUES('portfolio',$1::text,$2)
              ON CONFLICT(entity_kind,entity_key) DO UPDATE SET source_event_id=GREATEST(portfolio.projection_tombstone.source_event_id,EXCLUDED.source_event_id) RETURNING 1),
            deleted_active_funds AS (DELETE FROM portfolio.active_fund_by_portfolio_horizon WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1),
            deleted_assignments AS (DELETE FROM portfolio.fund_template_assignment WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1),
            deleted_allocations AS (DELETE FROM portfolio.fund_allocation WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1),
            deleted_envelopes AS (DELETE FROM portfolio.fund_risk_envelope WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1),
            deleted_trades AS (DELETE FROM portfolio.fund_order_trade WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1),
            deleted_compositions AS (DELETE FROM portfolio.fund_composition WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1),
            deleted_orders AS (DELETE FROM portfolio.fund_order WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1),
            deleted_funds AS (DELETE FROM portfolio.fund_by_portfolio WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1),
            deleted_states AS (DELETE FROM portfolio.portfolio_by_state WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1)
            DELETE FROM portfolio.portfolio_by_id WHERE portfolio_id=$1 AND source_event_id<=$2
            """;
    }

    public static class Fund
    {
        public const string ByPortfolio="SELECT payload_json::text FROM portfolio.fund_by_portfolio WHERE portfolio_id=$1 AND fund_id>$2 ORDER BY fund_id,fund_mandate_version DESC LIMIT $3;";
        public const string Get="SELECT payload_json::text FROM portfolio.fund_by_portfolio WHERE fund_id=$1 ORDER BY fund_mandate_version DESC LIMIT 1;";
        public const string Revision="SELECT portfolio_id,aggregate_version,source_event_id FROM portfolio.fund_by_portfolio WHERE fund_id=$1 ORDER BY fund_mandate_version DESC LIMIT 1;";
        public const string Active="SELECT payload_json::text FROM portfolio.active_fund_by_portfolio_horizon WHERE portfolio_id=$1 AND trading_year=$2 AND decision_horizon=$3 AND effective_from_utc<=$4 ORDER BY effective_from_utc DESC,fund_id LIMIT $5;";
        public const string Assignments="SELECT payload_json::text FROM portfolio.fund_template_assignment WHERE portfolio_id=$1 AND fund_id=$2 AND fund_mandate_version=$3 ORDER BY trade_template_id,trade_template_version LIMIT $4;";
        public const string Allocation="SELECT payload_json::text FROM portfolio.fund_allocation WHERE portfolio_id=$1 AND fund_id=$2 ORDER BY allocation_version DESC LIMIT 1;";
        public const string Envelope="SELECT payload_json::text FROM portfolio.fund_risk_envelope WHERE portfolio_id=$1 AND fund_id=$2 ORDER BY envelope_version DESC LIMIT 1;";
        public const string Upsert="""
            WITH saved_fund AS (
              INSERT INTO portfolio.fund_by_portfolio(portfolio_id,fund_id,fund_mandate_version,operating_state,schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash)
              SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,$10 WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio' AND entity_key=$1::text AND source_event_id>=$7)
              ON CONFLICT(portfolio_id,fund_id,fund_mandate_version) DO UPDATE SET operating_state=EXCLUDED.operating_state,schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash WHERE portfolio.fund_by_portfolio.source_event_id<=EXCLUDED.source_event_id RETURNING 1),
            removed_active AS (DELETE FROM portfolio.active_fund_by_portfolio_horizon WHERE fund_id=$2 AND source_event_id<=$7 RETURNING 1)
            INSERT INTO portfolio.active_fund_by_portfolio_horizon(portfolio_id,trading_year,decision_horizon,effective_from_utc,fund_id,fund_mandate_version,schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash)
            SELECT $1,$11,$12,$13,$2,$3,$5,$6,$7,$8,$9,$10 WHERE $4='Active' AND EXISTS(SELECT 1 FROM saved_fund)
            ON CONFLICT(portfolio_id,trading_year,decision_horizon,effective_from_utc,fund_id)
            DO UPDATE SET fund_mandate_version=EXCLUDED.fund_mandate_version,schema_version=EXCLUDED.schema_version,
              aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,
              updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash
            WHERE portfolio.active_fund_by_portfolio_horizon.source_event_id<=EXCLUDED.source_event_id
            """;
        public const string UpsertAssignment="INSERT INTO portfolio.fund_template_assignment(portfolio_id,fund_id,fund_mandate_version,trade_template_id,trade_template_version,schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash) SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11 WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio' AND entity_key=$1::text AND source_event_id>=$8) ON CONFLICT(portfolio_id,fund_id,fund_mandate_version,trade_template_id,trade_template_version) DO UPDATE SET schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash WHERE portfolio.fund_template_assignment.source_event_id<=EXCLUDED.source_event_id;";
        public const string UpsertAllocation="INSERT INTO portfolio.fund_allocation(portfolio_id,fund_id,allocation_version,schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash) SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9 WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio' AND entity_key=$1::text AND source_event_id>=$6) ON CONFLICT(portfolio_id,fund_id,allocation_version) DO UPDATE SET schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash WHERE portfolio.fund_allocation.source_event_id<=EXCLUDED.source_event_id;";
        public const string UpsertEnvelope="INSERT INTO portfolio.fund_risk_envelope(portfolio_id,fund_id,envelope_version,schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash) SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9 WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio' AND entity_key=$1::text AND source_event_id>=$6) ON CONFLICT(portfolio_id,fund_id,envelope_version) DO UPDATE SET schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash WHERE portfolio.fund_risk_envelope.source_event_id<=EXCLUDED.source_event_id;";
    }

    public static class Orders
    {
        public const string Timeline="SELECT payload_json::text FROM portfolio.fund_order WHERE portfolio_id=$1 AND fund_id=$2 AND order_month=$3 AND created_on_utc<$4 ORDER BY created_on_utc DESC,order_id DESC LIMIT $5;";
        public const string Get="SELECT payload_json::text FROM portfolio.fund_order WHERE order_id=$1;";
        public const string Trades="SELECT payload_json::text FROM portfolio.fund_order_trade WHERE order_id=$1 ORDER BY trade_id LIMIT $2;";
        public const string Trade="SELECT payload_json::text FROM portfolio.fund_order_trade WHERE trade_id=$1;";
        public const string Compositions="SELECT payload_json::text FROM portfolio.fund_composition WHERE workflow_id=$1 ORDER BY order_id LIMIT $2;";
        public const string UpsertOrder="INSERT INTO portfolio.fund_order(portfolio_id,fund_id,order_month,created_on_utc,order_id,status,schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash) SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12 WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio' AND entity_key=$1::text AND source_event_id>=$9) ON CONFLICT(order_id) DO UPDATE SET status=EXCLUDED.status,schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash WHERE portfolio.fund_order.source_event_id<=EXCLUDED.source_event_id;";
        public const string UpsertTrade="INSERT INTO portfolio.fund_order_trade(order_id,trade_id,portfolio_id,fund_id,schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash) SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,$10 WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio' AND entity_key=$3::text AND source_event_id>=$7) ON CONFLICT(trade_id) DO UPDATE SET order_id=EXCLUDED.order_id,portfolio_id=EXCLUDED.portfolio_id,fund_id=EXCLUDED.fund_id,schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash WHERE portfolio.fund_order_trade.source_event_id<=EXCLUDED.source_event_id;";
        public const string UpsertComposition="INSERT INTO portfolio.fund_composition(workflow_id,order_id,portfolio_id,fund_id,status,schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash) SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11 WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio' AND entity_key=$3::text AND source_event_id>=$8) ON CONFLICT(workflow_id,order_id) DO UPDATE SET status=EXCLUDED.status,schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash WHERE portfolio.fund_composition.source_event_id<=EXCLUDED.source_event_id;";
        public const string DeleteOrder="DELETE FROM portfolio.fund_order WHERE order_id=$1 AND source_event_id<=$2;";
        public const string DeleteTrade="DELETE FROM portfolio.fund_order_trade WHERE trade_id=$1 AND source_event_id<=$2;";
    }

    public static class Policy
    {
        public const string GetCurrent="SELECT payload_json::text FROM portfolio.portfolio_policy WHERE policy_id=$1 ORDER BY policy_version DESC LIMIT 1;";
        public const string GetVersion="SELECT payload_json::text FROM portfolio.portfolio_policy WHERE policy_id=$1 AND policy_version=$2;";
        public const string ByPortfolio="SELECT payload_json::text FROM portfolio.portfolio_policy WHERE portfolio_id=$1 ORDER BY policy_id DESC,policy_version DESC LIMIT $2;";
        public const string Active="SELECT payload_json::text FROM portfolio.active_portfolio_policy WHERE portfolio_id=$1;";
        public const string Upsert="""
            WITH saved_policy AS (
              INSERT INTO portfolio.portfolio_policy(policy_id,policy_version,portfolio_id,operating_state,schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash)
              SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,$10 WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='policy' AND entity_key=$3::text||':'||$1::text AND source_event_id>=$7)
              ON CONFLICT(policy_id,policy_version) DO UPDATE SET portfolio_id=EXCLUDED.portfolio_id,operating_state=EXCLUDED.operating_state,schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash WHERE portfolio.portfolio_policy.source_event_id<=EXCLUDED.source_event_id RETURNING 1),
            removed_active AS (DELETE FROM portfolio.active_portfolio_policy WHERE portfolio_id=$3 AND source_event_id<=$7 RETURNING 1)
            INSERT INTO portfolio.active_portfolio_policy(portfolio_id,policy_id,policy_version,schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash)
            SELECT $3,$1,$2,$5,$6,$7,$8,$9,$10 WHERE $4='Active' AND EXISTS(SELECT 1 FROM saved_policy)
            ON CONFLICT(portfolio_id) DO UPDATE SET policy_id=EXCLUDED.policy_id,policy_version=EXCLUDED.policy_version,
              schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,
              source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,
              payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash
            WHERE portfolio.active_portfolio_policy.source_event_id<=EXCLUDED.source_event_id
            """;
        public const string DeleteDraft="""
            WITH tombstone AS (
              INSERT INTO portfolio.projection_tombstone(entity_kind,entity_key,source_event_id) VALUES('policy',$1::text||':'||$2::text,$3)
              ON CONFLICT(entity_kind,entity_key) DO UPDATE SET source_event_id=GREATEST(portfolio.projection_tombstone.source_event_id,EXCLUDED.source_event_id) RETURNING 1),
            deleted_active AS (DELETE FROM portfolio.active_portfolio_policy WHERE portfolio_id=$1 AND policy_id=$2 AND source_event_id<=$3 RETURNING 1)
            DELETE FROM portfolio.portfolio_policy WHERE portfolio_id=$1 AND policy_id=$2 AND source_event_id<=$3
            """;
    }

    public static class OrderComposition
    {
        public const string InsertDecision = "INSERT INTO portfolio.order_composition_decision(operation_id,composition_id,workflow_id,portfolio_id,status,financial_revision,input_hash,receipt,committed_at_utc) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9);";
        public const string InsertFundDecision = "INSERT INTO portfolio.order_composition_fund_decision(operation_id,fund_id,accepted,reason_code,order_id) VALUES($1,$2,$3,$4,$5);";
        public const string InsertOrder = "INSERT INTO portfolio.accepted_trade_order(order_id,operation_id,portfolio_id,fund_id,status,definition_hash,definition,valid_until_utc) VALUES($1,$2,$3,$4,$5,$6,$7,$8);";
        public const string InsertLeg = "INSERT INTO portfolio.accepted_trade_order_leg(order_id,component_id,trade_leg_id,ordinal,definition) VALUES($1,$2,$3,$4,$5);";
        public const string InsertCapacity = "INSERT INTO portfolio.accepted_trade_order_capacity(order_id,portfolio_id,fund_id,scope_kind,scope_key,measure,unit,amount,method_version,financial_revision) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10);";
        public const string SelectOpeningOrder = "SELECT definition::text FROM portfolio.accepted_trade_order WHERE order_id=$1 AND portfolio_id=$2 AND fund_id=$3 FOR SHARE;";
        public const string SelectAcceptedClose = "SELECT operation_id FROM portfolio.accepted_position_close WHERE target_position_id=$1;";
        public const string InsertAcceptedClose = "INSERT INTO portfolio.accepted_position_close(operation_id,target_position_id,target_order_id,target_trade_id,close_order_id,accepted_at_utc) VALUES($1,$2,$3,$4,$5,$6);";
    }
}
