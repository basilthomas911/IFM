namespace TomasAI.IFM.Application.Storage.PortfolioDb;

/// <summary>SQL statements and query templates for Portfolio storage.</summary>
public static class PortfolioDbSql
{
    /// <summary>SQL for disable fund projection.</summary>
    internal const string DisableFundProjection =
        """
        UPDATE portfolio.fund_by_portfolio SET operating_state='Disabled',schema_version=$4,aggregate_version=$5,
        source_event_id=$6,updated_on_utc=$7,payload_json=$8,payload_hash=$9
        WHERE portfolio_id=$1 AND fund_id=$2 AND fund_mandate_version=$3 AND source_event_id<=$6;
        """;

    /// <summary>Inserts accounting export rows.</summary>
    internal const string Financial_AccountingExportStore_Insert01 =
        """
        INSERT INTO portfolio_financial.accounting_export(destination_company,export_id,source_set,source_cut,
            payload_hash,mapping_version,delivery_status,metadata)
        VALUES($1,$2,$3,$4,$5,$6,'Pending',$7);
        """;

    /// <summary>Inserts accounting export source rows.</summary>
    internal const string Financial_AccountingExportStore_Insert02 =
        """
        INSERT INTO portfolio_financial.accounting_export_source(destination_company,journal_id,export_id)
        VALUES($1,$2,$3);
        """;

    /// <summary>Inserts accounting export attempt rows.</summary>
    internal const string Financial_AccountingExportStore_Insert03 =
        """
        INSERT INTO portfolio_financial.accounting_export_attempt(destination_company,export_id,attempt_id,outcome,
            external_receipt)
        VALUES($1,$2,$3,$4,$5);
        """;

    /// <summary>Reads ledger journal rows.</summary>
    internal const string Financial_AccountingExportStore_Select01 =
        """
        SELECT journal_id,transaction_id,book_id,fund_id,accounting_date,journal_hash,financial_revision,reversal_journal_id
        FROM portfolio_financial.ledger_journal
        WHERE portfolio_id=$1 AND book_id=$2 AND journal_id=$3 AND financial_revision<=$4;
        """;

    /// <summary>Reads ledger entry rows.</summary>
    internal const string Financial_AccountingExportStore_Select02 =
        """
        SELECT ordinal,account_id,account_version,fund_id,debit,credit,source_line_reference
        FROM portfolio_financial.ledger_entry WHERE journal_id=$1 ORDER BY ordinal LIMIT 257;
        """;

    /// <summary>Reads accounting export attempt rows.</summary>
    internal const string Financial_AccountingExportStore_Select03 =
        """
        SELECT outcome,external_receipt FROM portfolio_financial.accounting_export_attempt
        WHERE destination_company=$1 AND export_id=$2 AND attempt_id=$3;
        """;

    /// <summary>Reads the current accounting export status.</summary>
    internal const string Financial_AccountingExportStore_Select04 =
        """
        SELECT metadata::text,delivery_status,retry_count,external_receipt,payload_hash
        FROM portfolio_financial.accounting_export WHERE destination_company=$1 AND export_id=$2;
        """;

    /// <summary>Reads and locks the current accounting export status.</summary>
    internal const string Financial_AccountingExportStore_Select04ForUpdate =
        """
        SELECT metadata::text,delivery_status,retry_count,external_receipt,payload_hash
        FROM portfolio_financial.accounting_export
        WHERE destination_company=$1 AND export_id=$2
        FOR UPDATE;
        """;

    /// <summary>Updates accounting export rows.</summary>
    internal const string Financial_AccountingExportStore_Update01 =
        """
        UPDATE portfolio_financial.accounting_export SET retry_count=$3,delivery_status=$4,external_receipt=$5
        WHERE destination_company=$1 AND export_id=$2;
        """;

    /// <summary>Inserts capacity expiry dispatch rows.</summary>
    internal const string Financial_CapacityExpiryDispatchStore_Insert01 =
        """
        INSERT INTO portfolio_financial.capacity_expiry_dispatch(operation_id,reservation_id,portfolio_id,fund_id,request,
            input_hash,status,created_at_utc)
        VALUES($1,$2,$3,$4,$5,$6,'Pending',$7) ON CONFLICT DO NOTHING;
        """;

    /// <summary>Reads capacity reservation rows.</summary>
    internal const string Financial_CapacityExpiryDispatchStore_Select01 =
        """
        SELECT r.portfolio_id,r.fund_id,r.reservation_id,r.version,r.strategy_units,r.requirements->>'ContentHash',
          a.financial_revision,r.expires_at_utc
        FROM portfolio_financial.capacity_reservation r
        JOIN portfolio_financial.financial_authority a USING(portfolio_id)
        WHERE r.status=1 AND r.execution_id IS NULL AND r.expires_at_utc<=$1 AND ($2::int IS NULL OR r.portfolio_id=$2)
          AND NOT EXISTS(SELECT 1 FROM portfolio_financial.capacity_expiry_dispatch d WHERE d.reservation_id=r.reservation_id AND d.status='Pending')
        ORDER BY r.expires_at_utc,r.reservation_id LIMIT 32;
        """;

    /// <summary>Reads capacity expiry dispatch rows.</summary>
    internal const string Financial_CapacityExpiryDispatchStore_Select02 =
        """
        SELECT fund_id,request::text
        FROM portfolio_financial.capacity_expiry_dispatch
        WHERE status='Pending' AND ($1::int IS NULL OR portfolio_id=$1)
        ORDER BY created_at_utc,operation_id LIMIT 32;
        """;

    /// <summary>Updates capacity expiry dispatch rows.</summary>
    internal const string Financial_CapacityExpiryDispatchStore_Update01 =
        """
        UPDATE portfolio_financial.capacity_expiry_dispatch SET status=$3,reconciled_at_utc=$4
        WHERE operation_id=$1 AND input_hash=$2 AND status='Pending';
        """;

    /// <summary>Inserts capacity reservation rows.</summary>
    internal const string Financial_CapacityReservationStore_Insert01 =
        """
        INSERT INTO portfolio_financial.capacity_reservation(reservation_id,portfolio_id,operation_id,fund_id,book_id,order_id,
          candidate_hash,risk_hash,sized_order_hash,strategy_units,requirements,environment,expires_at_utc,status,version,
          completion_event_id,request,receipt,input_hash,remaining_units)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,1,1,$14,$15,$16,$17,$10);
        """;

    /// <summary>Inserts capacity lifecycle rows.</summary>
    internal const string Financial_CapacityReservationStore_Insert02 =
        """
        INSERT INTO portfolio_financial.capacity_lifecycle(reservation_id,version,source_identity,operation_id,
            prior_status,new_status,
          usage_delta,filled_units,cancelled_units,remaining_units,execution_id,execution_revision,committed_at_utc,
            completion_event_id,input_hash,receipt)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16);
        """;

    /// <summary>Inserts capacity usage rows.</summary>
    internal const string Financial_CapacityReservationStore_Insert03 =
        """
        INSERT INTO portfolio_financial.capacity_usage(portfolio_id,scope_kind,scope_key,measure,unit,held,working,
            position,revision)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9) ON CONFLICT(portfolio_id,scope_kind,scope_key,measure,unit)
        DO UPDATE SET held=capacity_usage.held+EXCLUDED.held,working=capacity_usage.working+EXCLUDED.working,
          position=capacity_usage.position+EXCLUDED.position,revision=EXCLUDED.revision;
        """;

    /// <summary>Reads capacity usage rows.</summary>
    internal const string Financial_CapacityReservationStore_Select01 =
        """
        SELECT u.scope_kind,u.scope_key,u.measure,u.unit,u.held,u.working,u.position
        FROM portfolio_financial.capacity_usage u
        WHERE u.portfolio_id=$1 AND EXISTS (
          SELECT 1 FROM jsonb_to_recordset($2::jsonb) AS requested(scope_kind int,scope_key text,measure int,unit int)
          WHERE requested.scope_kind=u.scope_kind AND requested.scope_key=u.scope_key AND requested.measure=u.measure
            AND requested.unit=u.unit)
        LIMIT 257;
        """;

    /// <summary>Reads capacity reservation rows.</summary>
    internal const string Financial_CapacityReservationStore_Select02 =
        """
        SELECT reservation_id FROM portfolio_financial.capacity_reservation
        WHERE reservation_id=$1 OR (portfolio_id=$2 AND order_id=$3 AND status NOT IN (8,9));
        """;

    /// <summary>Reads capacity lifecycle rows.</summary>
    internal const string Financial_CapacityReservationStore_Select03 =
        """
        SELECT operation_id
        FROM portfolio_financial.capacity_lifecycle WHERE reservation_id=$1 AND source_identity=$2;
        """;

    /// <summary>Reads capacity lifecycle rows.</summary>
    internal const string Financial_CapacityReservationStore_Select04 =
        """
        SELECT r.version,r.status,r.strategy_units,r.filled_units,r.cancelled_units,r.remaining_units,
          r.requirements->>'ContentHash',r.expires_at_utc,r.execution_id,
          COALESCE((SELECT MAX(l.execution_revision) FROM portfolio_financial.capacity_lifecycle l WHERE l.reservation_id=r.reservation_id),0),r.request::text,r.closed_units
        FROM portfolio_financial.capacity_reservation r WHERE portfolio_id=$1 AND reservation_id=$2;
        """;

    /// <summary>Reads event log rows.</summary>
    internal const string Financial_CapacityReservationStore_Select05 =
        """
        SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,e.commandid,
            e.eventtimestamp::text,e.streamversion
        FROM event_log e
        JOIN event_name_id n ON n.eventnameid=e.eventnameid
        WHERE e.commandid=$1 ORDER BY e.eventversion LIMIT 65;
        """;

    /// <summary>SQL for select reservation snapshot events.</summary>
    internal const string Financial_CapacityReservationStore_SelectReservationSnapshotEvents =
        """
        SELECT r.version,
          c.eventstreamid, cn.eventname, cn.eventtypename, c.eventversion, c.eventpayload, c.commandid,
            c.eventtimestamp::text, c.streamversion,
          e.eventstreamid, en.eventname, en.eventtypename, e.eventversion, e.eventpayload, e.commandid,
            e.eventtimestamp::text, e.streamversion
        FROM portfolio_financial.capacity_reservation r
        LEFT JOIN portfolio_financial.capacity_lifecycle l
          ON l.reservation_id=r.reservation_id AND l.version=r.version
        LEFT JOIN portfolio_financial.financial_operation_receipt co
          ON co.portfolio_id=r.portfolio_id AND co.operation_id=r.operation_id
        LEFT JOIN event_log c ON c.eventversion=co.event_version
        LEFT JOIN event_name_id cn ON cn.eventnameid=c.eventnameid
        LEFT JOIN portfolio_financial.financial_operation_receipt lo
          ON lo.portfolio_id=r.portfolio_id AND lo.operation_id=l.operation_id
        LEFT JOIN event_log e ON e.eventversion=lo.event_version
        LEFT JOIN event_name_id en ON en.eventnameid=e.eventnameid
        WHERE r.portfolio_id=$1 AND r.reservation_id=$2;
        """;

    /// <summary>Updates capacity reservation rows.</summary>
    internal const string Financial_CapacityReservationStore_Update01 =
        """
        UPDATE portfolio_financial.capacity_reservation SET status=$3,version=$4,execution_id=$5,
          filled_units=$6,cancelled_units=$7,remaining_units=$8,closed_units=$9
          WHERE reservation_id=$1 AND portfolio_id=$2;
        """;

    /// <summary>SQL for check draft portfolio deletion.</summary>
    internal const string Financial_CheckDraftPortfolioDeletion =
        """
        SELECT NOT EXISTS (SELECT 1 FROM portfolio.fund_order WHERE portfolio_id=$1)
           AND NOT EXISTS (
               SELECT 1 FROM unnest($2::integer[]) f(fund_id)
               JOIN event_stream_id s ON s.eventstream='PortfolioFund.' || $1::text || '.' || f.fund_id::text
               LEFT JOIN LATERAL (
                   SELECT aggregate_version FROM portfolio.fund_by_portfolio
                   WHERE portfolio_id=$1 AND fund_id=f.fund_id
                   ORDER BY fund_mandate_version DESC LIMIT 1
               ) projection ON TRUE
               WHERE s.currentversion>0 AND projection.aggregate_version IS DISTINCT FROM s.currentversion
           );
        """;

    /// <summary>SQL for check policy unreferenced.</summary>
    internal const string Financial_CheckPolicyUnreferenced =
        """
        SELECT CASE WHEN s.currentversion IS NULL THEN TRUE
            WHEN current_projection.aggregate_version=s.currentversion
                THEN COALESCE((current_projection.payload_json->>'ActivePolicyId')::integer,0)<>$3
            WHEN tombstone.source_event_id=head.eventversion THEN TRUE
            ELSE FALSE END
        FROM (SELECT $1::integer AS portfolio_id) p
        LEFT JOIN event_stream_id s ON s.eventstream=$2
        LEFT JOIN event_log head ON head.eventstreamid=s.eventstreamid AND head.streamversion=s.currentversion
        LEFT JOIN portfolio.projection_tombstone tombstone ON tombstone.entity_kind='portfolio'
            AND tombstone.entity_key=p.portfolio_id::text
        LEFT JOIN LATERAL (
            SELECT aggregate_version,payload_json
            FROM portfolio.portfolio_by_id
            WHERE portfolio_id=p.portfolio_id ORDER BY portfolio_version DESC LIMIT 1
        ) current_projection ON TRUE;
        """;

    /// <summary>Inserts emulator order rows.</summary>
    internal const string Financial_EmulatorExecutionStore_Insert01 =
        """
        INSERT INTO portfolio_financial.emulator_order(execution_id,portfolio_id,fund_id,order_id,reservation_id,operation_id,
          request,receipt,input_hash,submitted_at_utc) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10);
        """;

    /// <summary>Reads emulator order rows.</summary>
    internal const string Financial_EmulatorExecutionStore_Select01 =
        """
        SELECT operation_id FROM portfolio_financial.emulator_order WHERE execution_id=$1;
        """;

    /// <summary>Reads financial authority rows.</summary>
    internal const string Financial_FinancialAuthorityPreparationStore_Select01 =
        """
        SELECT policy_source_versions::text,financial_revision,authority_epoch,operating_state
        FROM portfolio_financial.financial_authority WHERE portfolio_id=$1 FOR SHARE;
        """;

    /// <summary>Inserts financial history receipt rows.</summary>
    internal const string Financial_FinancialHistoryJournal_Insert01 =
        """
        INSERT INTO portfolio_financial.financial_history_receipt(event_version,projected_at_utc)
        VALUES($1,$2) ON CONFLICT DO NOTHING;
        """;

    /// <summary>SQL for read pending fund authorizations.</summary>
    internal const string Financial_FinancialHistoryJournal_ReadPendingFundAuthorizations =
        """
        SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,e.commandid,
            e.eventtimestamp::text,e.streamversion
        FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
        WHERE n.eventname=$1 AND ($2::uuid IS NULL OR e.commandid=$2)
          AND NOT EXISTS(SELECT 1 FROM portfolio_financial.fund_risk_authorization_projection_receipt r WHERE r.event_version=e.eventversion)
        ORDER BY e.eventversion LIMIT 32;
        """;

    /// <summary>Reads event log rows.</summary>
    internal const string Financial_FinancialHistoryJournal_Select01 =
        """
        SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,e.commandid,
            e.eventtimestamp::text,e.streamversion
        FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
        JOIN portfolio_financial.financial_operation_receipt o ON o.event_version=e.eventversion
        WHERE n.eventname=ANY($1) AND ($2::int IS NULL OR o.portfolio_id=$2)
          AND (NOT EXISTS(SELECT 1 FROM portfolio_financial.financial_history_receipt r WHERE r.event_version=e.eventversion)
               OR NOT EXISTS(SELECT 1 FROM portfolio.financial_operation_by_portfolio_month h
                             WHERE h.portfolio_id=o.portfolio_id AND h.operation_id=o.operation_id))
        ORDER BY e.eventversion LIMIT 32;
        """;

    /// <summary>Inserts financial operation by portfolio month rows.</summary>
    internal const string Financial_FinancialHistoryProjection_Insert01 =
        """
        INSERT INTO portfolio.financial_operation_by_portfolio_month(
          portfolio_id,month,financial_revision,operation_id,source_event_id,event_type,committed_at_utc,payload_json)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8)
        ON CONFLICT(portfolio_id,month,financial_revision,operation_id) DO NOTHING;
        """;

    /// <summary>SQL for match fund sources.</summary>
    internal const string Financial_FinancialQueryStore_MatchFundSources =
        """
        SELECT $4>0 AND $5>0 AND $6>0
          AND COALESCE((SELECT aggregate_version FROM portfolio.portfolio_by_id
                       WHERE portfolio_id=$1 ORDER BY portfolio_version DESC LIMIT 1)=$4,FALSE)
          AND COALESCE((SELECT aggregate_version FROM portfolio.fund_by_portfolio
                       WHERE portfolio_id=$1 AND fund_id=$2 ORDER BY fund_mandate_version DESC LIMIT 1)=$5,FALSE)
          AND COALESCE((SELECT aggregate_version FROM portfolio.portfolio_policy
                       WHERE portfolio_id=$1 AND policy_id=$3 ORDER BY policy_version DESC LIMIT 1)=$6,FALSE);
        """;

    /// <summary>SQL for read fund risk authorization.</summary>
    internal const string Financial_FinancialQueryStore_ReadFundRiskAuthorization =
        """
        SELECT source_event_id,"authorization"::text FROM portfolio_financial.fund_risk_authorization_read
        WHERE command_id=$1 LIMIT 2;
        """;

    /// <summary>SQL for read outcome.</summary>
    internal const string Financial_FinancialQueryStore_ReadOutcome =
        """
        SELECT event_type,payload_json::text FROM portfolio.financial_operation_by_portfolio_month
        WHERE portfolio_id=$1 AND operation_id=$2 LIMIT 2;
        """;

    /// <summary>Reads financial authority rows.</summary>
    internal const string Financial_FinancialQueryStore_Select01 =
        """
        SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;
        """;

    /// <summary>Reads ledger period rows.</summary>
    internal const string Financial_FinancialQueryStore_Select02 =
        """
        SELECT period_id,start_date,end_date,revision,state FROM portfolio_financial.ledger_period
        WHERE book_id=$1 ORDER BY start_date DESC LIMIT 257;
        """;

    /// <summary>Reads ledger account rows.</summary>
    internal const string Financial_FinancialQueryStore_Select03 =
        """
        SELECT DISTINCT ON (account_id) account_id,version,category,normal_side,fund_dimension_required,content_hash,status
        FROM portfolio_financial.ledger_account WHERE book_id=$1 ORDER BY account_id,version DESC LIMIT 257;
        """;

    /// <summary>Reads ledger posting rule rows.</summary>
    internal const string Financial_FinancialQueryStore_Select04 =
        """
        SELECT DISTINCT ON (rule_id) payload::text,status,effective_from,effective_to
        FROM portfolio_financial.ledger_posting_rule
        WHERE book_id=$1 ORDER BY rule_id,version DESC LIMIT 257;
        """;

    /// <summary>Reads ledger reconciliation rows.</summary>
    internal const string Financial_FinancialQueryStore_Select05 =
        """
        SELECT reconciliation_id,(counts->>'FinancialRevision')::bigint,(counts->>'JournalCount')::bigint,
            (counts->>'EntryCount')::bigint,(totals->>'Debits')::numeric,(totals->>'Credits')::numeric,
            differences::text,source_cut,content_hash
        FROM portfolio_financial.ledger_reconciliation WHERE book_id=$1 AND fund_id IS NULL
        ORDER BY (counts->>'FinancialRevision')::bigint DESC,reconciliation_id DESC LIMIT 1;
        """;

    /// <summary>Reads ledger posting rule rows.</summary>
    internal const string Financial_FinancialQueryStore_Select06 =
        """
        SELECT payload::text FROM portfolio_financial.ledger_posting_rule WHERE book_id=$1 AND status='Active'
          AND effective_from<=$2 AND (effective_to IS NULL OR effective_to>=$2)
          ORDER BY kind,rule_id,version LIMIT 257;
        """;

    /// <summary>Reads ledger period rows.</summary>
    internal const string Financial_FinancialQueryStore_Select07 =
        """
        SELECT state='Open'
        FROM portfolio_financial.ledger_period WHERE book_id=$1 AND start_date<=$2 AND end_date>=$2;
        """;

    /// <summary>Reads financial authority rows.</summary>
    internal const string Financial_FinancialQueryStore_Select08 =
        """
        SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;
        """;

    /// <summary>Reads financial authority rows.</summary>
    internal const string Financial_FinancialQueryStore_Select09 =
        """
        SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;
        """;

    /// <summary>Reads capacity usage rows.</summary>
    internal const string Financial_FinancialQueryStore_Select10 =
        """
        SELECT scope_kind,scope_key,measure,unit,held,working,position FROM portfolio_financial.capacity_usage
        WHERE portfolio_id=$1 AND ((scope_kind=1 AND scope_key=$2) OR (scope_kind=3 AND scope_key=$3)
          OR (scope_kind=2 AND scope_key=$4) OR (scope_kind=4 AND scope_key=$5))
        ORDER BY scope_kind,scope_key,measure,unit LIMIT 257;
        """;

    /// <summary>Reads ledger transaction rows.</summary>
    internal const string Financial_FinancialQueryStore_Select11 =
        """
        SELECT COUNT(*)>0 AND bool_and(fund_id=$3)
        FROM portfolio_financial.ledger_transaction WHERE portfolio_id=$1 AND operation_id=$2;
        """;

    /// <summary>Reads ledger journal rows.</summary>
    internal const string Financial_FinancialQueryStore_Select12 =
        """
        SELECT journal_id,transaction_id,book_id,fund_id,accounting_date,journal_hash
        FROM portfolio_financial.ledger_journal
        WHERE portfolio_id=$1 AND journal_id=$2 AND ($3::int IS NULL OR fund_id=$3);
        """;

    /// <summary>Reads ledger entry rows.</summary>
    internal const string Financial_FinancialQueryStore_Select13 =
        """
        SELECT ordinal,account_id,account_version,fund_id,debit,credit,source_line_reference
        FROM portfolio_financial.ledger_entry WHERE journal_id=$1 ORDER BY ordinal LIMIT 257;
        """;

    /// <summary>Reads financial encumbrance rows.</summary>
    internal const string Financial_FinancialQueryStore_Select14 =
        """
        SELECT COALESCE(SUM(amount),0) FROM portfolio_financial.financial_encumbrance
        WHERE book_id=$1 AND ($2::int IS NULL OR fund_id=$2) AND status='Pending';
        """;

    /// <summary>Reads financial authority rows.</summary>
    internal const string Financial_FinancialQueryStore_Select15 =
        """
        SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;
        """;

    /// <summary>Reads capacity usage rows.</summary>
    internal const string Financial_FinancialQueryStore_Select16 =
        """
        SELECT scope_kind,scope_key,measure,unit,held,working,position FROM portfolio_financial.capacity_usage
        WHERE portfolio_id=$1 AND ($2::text IS NULL OR (scope_kind=3 AND scope_key=$2))
        ORDER BY scope_kind,scope_key,measure,unit LIMIT 4097;
        """;

    /// <summary>Reads ledger transaction rows.</summary>
    internal const string Financial_FinancialQueryStore_Select17 =
        """
        SELECT t.transaction_id,t.operation_id,o.financial_revision,t.item_ordinal,t.payload::text,j.journal_id
        FROM portfolio_financial.ledger_transaction t JOIN portfolio_financial.financial_operation_receipt o
          ON o.portfolio_id=t.portfolio_id AND o.operation_id=t.operation_id
        LEFT JOIN portfolio_financial.ledger_journal j ON j.transaction_id=t.transaction_id
        WHERE t.portfolio_id=$1 AND t.fund_id=$2 AND o.financial_revision<=$3
          AND (o.financial_revision,t.item_ordinal)>($4,$5)
        ORDER BY o.financial_revision,t.item_ordinal LIMIT $6;
        """;

    /// <summary>Reads ledger reconciliation rows.</summary>
    internal const string Financial_FinancialQueryStore_Select18 =
        """
        SELECT reconciliation_id,source_cut,status,content_hash,differences::text
        FROM portfolio_financial.ledger_reconciliation
        WHERE portfolio_id=$1 AND reconciliation_id=$2 AND ($3::int IS NULL OR fund_id=$3);
        """;

    /// <summary>Reads financial authority rows.</summary>
    internal const string Financial_FinancialQueryStore_Select19 =
        """
        SELECT policy_source_versions::text,financial_revision
        FROM portfolio_financial.financial_authority WHERE portfolio_id=$1 FOR SHARE;
        """;

    /// <summary>Reads ledger account balance rows.</summary>
    internal const string Financial_FinancialQueryStore_Select20 =
        """
        SELECT account_id,fund_id,currency,debit_total,credit_total,balance
        FROM portfolio_financial.ledger_account_balance
        WHERE book_id=$1 AND ($2::int IS NULL OR fund_id=$2) ORDER BY account_id,fund_id LIMIT 4097;
        """;

    /// <summary>Reads capacity reservation rows.</summary>
    internal const string Financial_FinancialQueryStore_Select21 =
        """
        SELECT fund_id FROM portfolio_financial.capacity_reservation WHERE portfolio_id=$1 AND reservation_id=$2;
        """;

    /// <summary>Reads capacity lifecycle rows.</summary>
    internal const string Financial_FinancialQueryStore_Select22 =
        """
        SELECT r.receipt::text,r.reservation_id,r.version,r.status,r.strategy_units,r.filled_units,r.cancelled_units,
            r.remaining_units,
          r.requirements->>'ContentHash',r.expires_at_utc,r.execution_id,
          COALESCE((SELECT MAX(l.execution_revision) FROM portfolio_financial.capacity_lifecycle l WHERE l.reservation_id=r.reservation_id),0),r.closed_units
        FROM portfolio_financial.capacity_reservation r JOIN portfolio_financial.financial_operation_receipt o
          ON o.portfolio_id=r.portfolio_id AND o.operation_id=r.operation_id
        WHERE r.portfolio_id=$1 AND ($2::int IS NULL OR r.fund_id=$2) AND ($3::uuid IS NULL OR r.reservation_id=$3)
          AND o.financial_revision>$4 AND o.financial_revision<=$5 ORDER BY o.financial_revision LIMIT $6;
        """;

    /// <summary>SQL for select fund performance.</summary>
    internal const string Financial_FinancialQueryStore_SelectFundPerformance =
        """
        SELECT t.transaction_id,t.operation_id,o.financial_revision,t.item_ordinal,t.payload::text
        FROM portfolio_financial.ledger_transaction t JOIN portfolio_financial.financial_operation_receipt o
          ON o.portfolio_id=t.portfolio_id AND o.operation_id=t.operation_id
        WHERE t.portfolio_id=$1 AND t.fund_id=$2 AND o.financial_revision<=$3
          AND t.accounting_date >= $4 AND t.accounting_date <= $5
        ORDER BY o.financial_revision,t.item_ordinal;
        """;

    /// <summary>Reads event stream id rows.</summary>
    internal const string Financial_FinancialWorkflowRecoveryJournal_With01 =
        """
        WITH page AS (
            SELECT eventstreamid FROM event_stream_id
            WHERE eventstreamid>$1 AND eventstream LIKE 'Command.IntrinsicTimeStrategyWorkflowCommand.%'
              AND ($2::text IS NULL OR eventstream=$2)
            ORDER BY eventstreamid LIMIT 32
        )
        SELECT p.eventstreamid::bigint,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,
               e.commandid,e.eventtimestamp::text,e.streamversion
        FROM page p
        LEFT JOIN LATERAL (
            SELECT * FROM event_log l WHERE l.eventstreamid=p.eventstreamid
            ORDER BY streamversion DESC LIMIT 1
        ) e ON TRUE
        LEFT JOIN event_name_id n ON n.eventnameid=e.eventnameid
        ORDER BY p.eventstreamid;
        """;

    /// <summary>SQL for acknowledge projection.</summary>
    internal const string Financial_FundRiskAuthorizationStore_AcknowledgeProjection =
        """
        INSERT INTO portfolio_financial.fund_risk_authorization_projection_receipt(event_version)
        VALUES($1) ON CONFLICT DO NOTHING;
        """;

    /// <summary>SQL for project.</summary>
    internal const string Financial_FundRiskAuthorizationStore_Project =
        """
        INSERT INTO portfolio_financial.fund_risk_authorization_read(portfolio_id,fund_id,command_id,source_event_id,
            "authorization")
        VALUES($1,$2,$3,$4,$5) ON CONFLICT DO NOTHING;
        """;

    /// <summary>Reads capacity reservation rows.</summary>
    internal const string Financial_FundRiskAuthorizationStore_Select01 =
        """
        SELECT status,version,request::text,receipt::text FROM portfolio_financial.capacity_reservation
        WHERE portfolio_id=$1 AND reservation_id=$2;
        """;

    /// <summary>Reads cash, withdrawals and funding holds at one statement snapshot and round trip.</summary>
    internal const string Financial_GeneralLedgerStore_AvailableCash =
        """
        SELECT (SELECT COALESCE(SUM(b.balance),0) FROM portfolio_financial.ledger_account_balance b
        WHERE b.book_id=$1 AND b.fund_id=$2 AND EXISTS(SELECT 1 FROM portfolio_financial.ledger_account a
          WHERE a.book_id=b.book_id AND a.account_id=b.account_id AND a.category='Cash')) - (SELECT COALESCE(SUM(amount),
            0) FROM portfolio_financial.financial_encumbrance WHERE book_id=$1 AND fund_id=$2
            AND status='Pending') - (WITH paid AS (
          SELECT f.reservation_id,f.component,SUM(GREATEST(0,f.cash_paid-COALESCE((
            SELECT SUM(e.debit-e.credit) FROM portfolio_financial.ledger_journal reversal
              JOIN portfolio_financial.ledger_entry e ON e.journal_id=reversal.journal_id
            WHERE reversal.reversal_journal_id=j.journal_id AND e.fund_id=$2 AND EXISTS(
              SELECT 1 FROM portfolio_financial.ledger_account a WHERE a.book_id=e.book_id AND a.account_id=e.account_id
              AND a.category='Cash')),0))) amount
          FROM portfolio_financial.capacity_funding_receipt f JOIN portfolio_financial.ledger_journal j ON j.transaction_id=f.transaction_id
          WHERE j.book_id=$1 AND j.fund_id=$2 GROUP BY f.reservation_id,f.component),
        holds AS (
          SELECT r.reservation_id,(r.remaining_units+r.filled_units-r.closed_units)::numeric/r.strategy_units fraction,
            COALESCE((r.requirements->>'SettlementCash')::numeric,0) settlement,
            COALESCE((r.requirements->>'MarginFunding')::numeric,0) margin,
            COALESCE((r.requirements->>'FeeReserve')::numeric,0) fee,
            COALESCE((r.requirements->>'VariationReserve')::numeric,0) variation
          FROM portfolio_financial.capacity_reservation r WHERE r.book_id=$1 AND r.fund_id=$2 AND r.status NOT IN (8,9))
        SELECT COALESCE(SUM(ceil(100*(GREATEST(0,h.settlement*h.fraction-COALESCE(s.amount,0))+
          GREATEST(0,h.margin*h.fraction-COALESCE(m.amount,0))+GREATEST(0,h.fee*h.fraction-COALESCE(f.amount,
            0))+h.variation*h.fraction))/100),0)
        FROM holds h LEFT JOIN paid s ON s.reservation_id=h.reservation_id AND s.component=1
          LEFT JOIN paid f ON f.reservation_id=h.reservation_id AND f.component=2
          LEFT JOIN paid m ON m.reservation_id=h.reservation_id AND m.component=3);
        """;

    /// <summary>Inserts ledger transaction rows.</summary>
    internal const string Financial_GeneralLedgerStore_Insert01 =
        """
        INSERT INTO portfolio_financial.ledger_transaction(transaction_id,book_id,portfolio_id,fund_id,operation_id,
            item_ordinal,kind,
        source_system,source_event_key,source_hash,amount,currency,accounting_date,value_date,settlement_date,
            obligation_id,related_journal_id,payload)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'USD',$12,$13,$14,$15,$16,$17);
        """;

    /// <summary>Inserts ledger journal rows.</summary>
    internal const string Financial_GeneralLedgerStore_Insert02 =
        """
        INSERT INTO portfolio_financial.ledger_journal(journal_id,transaction_id,book_id,portfolio_id,fund_id,
            operation_id,accounting_date,value_date,settlement_date,
        kind,source_hash,rule_id,rule_version,reversal_journal_id,committed_at_utc,financial_revision,journal_hash)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17);
        """;

    /// <summary>Inserts financial source receipt rows.</summary>
    internal const string Financial_GeneralLedgerStore_Insert03 =
        """
        INSERT INTO portfolio_financial.financial_source_receipt(book_id,source_system,source_event_key,posting_purpose,
            source_content_hash,operation_id,journal_id)
        VALUES($1,$2,$3,$4,$5,$6,$7);
        """;

    /// <summary>Inserts ledger valuation rows.</summary>
    internal const string Financial_GeneralLedgerStore_Insert04 =
        """
        INSERT INTO portfolio_financial.ledger_valuation(book_id,fund_id,position_key,amount,source_sequence,revision)
        VALUES($1,$2,$3,$4,$5,$6)
        ON CONFLICT(book_id,fund_id,position_key) DO UPDATE SET amount=EXCLUDED.amount,
            source_sequence=EXCLUDED.source_sequence,revision=EXCLUDED.revision;
        """;

    /// <summary>Inserts ledger posting receipt rows.</summary>
    internal const string Financial_GeneralLedgerStore_Insert05 =
        """
        INSERT INTO portfolio_financial.ledger_posting_receipt(portfolio_id,operation_id,execution_id,input_hash,
            receipt_type,manifest,completion_event_id,committed_at_utc,financial_revision,payload)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10);
        """;

    /// <summary>Inserts capacity funding receipt rows.</summary>
    internal const string Financial_GeneralLedgerStore_Insert06 =
        """
        INSERT INTO portfolio_financial.capacity_funding_receipt(transaction_id,reservation_id,component,cash_paid)
        VALUES($1,$2,$3,$4);
        """;

    /// <summary>Inserts financial encumbrance rows.</summary>
    internal const string Financial_GeneralLedgerStore_Insert07 =
        """
        INSERT INTO portfolio_financial.financial_encumbrance(obligation_id,portfolio_id,book_id,fund_id,source_identity,
            kind,amount,currency,status,revision,evidence)
        VALUES($1,$2,$3,$4,$5,'Withdrawal',$6,'USD','Pending',$7,$8);
        """;

    /// <summary>Inserts ledger entry rows.</summary>
    internal const string Financial_GeneralLedgerStore_Insert08 =
        """
        INSERT INTO portfolio_financial.ledger_entry(journal_id,ordinal,book_id,account_id,account_version,fund_id,debit,
            credit,currency,source_line_reference,order_id,trade_id)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,'USD',$9,$10,$11);
        """;

    /// <summary>Inserts ledger account balance rows.</summary>
    internal const string Financial_GeneralLedgerStore_Insert09 =
        """
        INSERT INTO portfolio_financial.ledger_account_balance(book_id,account_id,fund_id,currency,debit_total,
            credit_total,balance,revision)
        VALUES($1,$2,$3,'USD',$4,$5,$4-$5,$6)
        ON CONFLICT(book_id,account_id,fund_id,currency) DO UPDATE SET debit_total=portfolio_financial.ledger_account_balance.debit_total+EXCLUDED.debit_total,
        credit_total=portfolio_financial.ledger_account_balance.credit_total+EXCLUDED.credit_total,
        balance=portfolio_financial.ledger_account_balance.balance+EXCLUDED.balance,revision=EXCLUDED.revision;
        """;

    /// <summary>Writes a journal line and its balance delta together on the caller's existing transaction.</summary>
    internal const string Financial_GeneralLedgerStore_InsertLineAndBalance =
        """
        WITH entry AS (
            INSERT INTO portfolio_financial.ledger_entry(journal_id,ordinal,book_id,account_id,account_version,fund_id,
            debit,credit,currency,source_line_reference,order_id,trade_id)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,'USD',$9,$10,$11)
            RETURNING book_id,account_id,fund_id,currency,debit,credit
        )
        INSERT INTO portfolio_financial.ledger_account_balance(book_id,account_id,fund_id,currency,debit_total,
            credit_total,balance,revision)
        SELECT book_id,account_id,fund_id,currency,debit,credit,debit-credit,$12 FROM entry
        ON CONFLICT(book_id,account_id,fund_id,currency) DO UPDATE SET debit_total=portfolio_financial.ledger_account_balance.debit_total+EXCLUDED.debit_total,
            credit_total=portfolio_financial.ledger_account_balance.credit_total+EXCLUDED.credit_total,
            balance=portfolio_financial.ledger_account_balance.balance+EXCLUDED.balance,revision=EXCLUDED.revision;
        """;

    /// <summary>Reads unnest rows.</summary>
    internal const string Financial_GeneralLedgerStore_Select01 =
        """
        SELECT input.ordinality,receipt.source_content_hash,receipt.operation_id
        FROM unnest($2::text[],$3::text[],$4::text[]) WITH ordinality
            AS input(source_system,source_event_key,posting_purpose,ordinality)
        JOIN portfolio_financial.financial_source_receipt receipt
            ON receipt.book_id=$1 AND receipt.source_system=input.source_system
            AND receipt.source_event_key=input.source_event_key AND receipt.posting_purpose=input.posting_purpose;
        """;

    /// <summary>Reads ledger period rows.</summary>
    internal const string Financial_GeneralLedgerStore_Select02 =
        """
        SELECT state FROM portfolio_financial.ledger_period WHERE book_id=$1 AND start_date<=$2 AND end_date>=$2;
        """;

    /// <summary>Reads ledger posting rule rows.</summary>
    internal const string Financial_GeneralLedgerStore_Select03 =
        """
        SELECT payload::text
        FROM portfolio_financial.ledger_posting_rule WHERE book_id=$1 AND rule_id=$2 AND version=$3
        AND status='Active' AND effective_from<=$4 AND (effective_to IS NULL OR effective_to>=$4);
        """;

    /// <summary>Reads ledger valuation rows.</summary>
    internal const string Financial_GeneralLedgerStore_Select04 =
        """
        SELECT amount,source_sequence
        FROM portfolio_financial.ledger_valuation WHERE book_id=$1 AND fund_id=$2 AND position_key=$3;
        """;

    /// <summary>Reads ledger journal rows.</summary>
    internal const string Financial_GeneralLedgerStore_Select05 =
        """
        SELECT fund_id FROM portfolio_financial.ledger_journal WHERE book_id=$1 AND journal_id=$2;
        """;

    /// <summary>Reads ledger journal rows.</summary>
    internal const string Financial_GeneralLedgerStore_Select06 =
        """
        SELECT COALESCE(SUM(t.amount),0)
        FROM portfolio_financial.ledger_journal j
        JOIN portfolio_financial.ledger_transaction t ON t.transaction_id=j.transaction_id
        WHERE j.reversal_journal_id=$1;
        """;

    /// <summary>Reads capacity reservation rows.</summary>
    internal const string Financial_GeneralLedgerStore_Select09 =
        """
        SELECT order_id,execution_id,request::text FROM portfolio_financial.capacity_reservation
        WHERE reservation_id=$1 AND book_id=$2 AND fund_id=$3;
        """;

    /// <summary>Reads ledger entry rows.</summary>
    internal const string Financial_GeneralLedgerStore_Select10 =
        """
        SELECT COALESCE(SUM(e.credit-e.debit),0)
        FROM portfolio_financial.ledger_entry e WHERE e.journal_id=$1 AND e.fund_id=$2
        AND EXISTS(SELECT 1 FROM portfolio_financial.ledger_account a WHERE a.book_id=e.book_id
        AND a.account_id=e.account_id AND a.category='Cash');
        """;

    /// <summary>Reads ledger account rows.</summary>
    internal const string Financial_GeneralLedgerStore_Select11 =
        """
        SELECT fund_dimension_required
        FROM portfolio_financial.ledger_account WHERE book_id=$1 AND account_id=$2 AND version=$3
        AND (status='Active' OR ($4 AND status='Retired'));
        """;

    /// <summary>Reads ledger entry rows.</summary>
    internal const string Financial_GeneralLedgerStore_Select12 =
        """
        SELECT ordinal,account_id,account_version,fund_id,debit,credit,order_id,trade_id,source_line_reference
        FROM portfolio_financial.ledger_entry WHERE journal_id=$1 ORDER BY ordinal;
        """;

    /// <summary>Updates financial authority rows.</summary>
    internal const string Financial_GeneralLedgerStore_Update01 =
        """
        UPDATE portfolio_financial.financial_authority SET operating_state='Overdrawn' WHERE portfolio_id=$1;
        """;

    /// <summary>Updates financial encumbrance rows.</summary>
    internal const string Financial_GeneralLedgerStore_Update02 =
        """
        UPDATE portfolio_financial.financial_encumbrance
        SET amount=amount-$1,status=CASE WHEN amount=$1 THEN $2 ELSE 'Pending' END,revision=$3,evidence=$4
        WHERE obligation_id=$5 AND portfolio_id=$6 AND book_id=$7 AND fund_id=$8 AND status='Pending' AND amount>=$1;
        """;

    /// <summary>SQL for read migration state.</summary>
    internal const string Financial_LedgerConfigurationProjection_ReadMigrationState =
        """
        SELECT migration_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;
        """;

    /// <summary>SQL for set authority.</summary>
    internal const string Financial_LedgerConfigurationProjection_SetAuthority =
        """
        UPDATE portfolio_financial.financial_authority SET policy_source_versions=$2,migration_state=$3,
        operating_state=$4,source_watermark=$5,valuation_watermark=$6,authority_epoch=$7 WHERE portfolio_id=$1;
        """;

    /// <summary>Inserts ledger account rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Insert02 =
        """
        INSERT INTO portfolio_financial.ledger_account(book_id,account_id,version,category,normal_side,currency,
            fund_dimension_required,status,content_hash)
        VALUES($1,$2,$3,$4,$5,'USD',$6,'Active',$7);
        """;

    /// <summary>Inserts ledger posting rule rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Insert03 =
        """
        INSERT INTO portfolio_financial.ledger_posting_rule(book_id,rule_id,version,kind,payload,content_hash,status,
            effective_from)
        VALUES($1,$2,$3,$4,$5,$6,'Active',$7);
        """;

    /// <summary>Inserts ledger period rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Insert04 =
        """
        INSERT INTO portfolio_financial.ledger_period(book_id,period_id,start_date,end_date,state,revision,source_cut,evidence)
        VALUES($1,$2,$3,$4,'Open',1,$5,$6);
        """;

    /// <summary>Inserts ledger reconciliation rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Insert05 =
        """
        INSERT INTO portfolio_financial.ledger_reconciliation(reconciliation_id,book_id,portfolio_id,fund_id,source_cut,
            counts,totals,content_hash,differences,resolution_links,status)
        VALUES($1,$2,$3,NULL,$4,$5,$6,$7,$8,'[]',$9);
        """;

    /// <summary>SQL for select01.</summary>
    internal const string Financial_LedgerConfigurationStore_Select01 =
        """
        SELECT pg_advisory_xact_lock(34100,$1);
        """;

    /// <summary>Reads ledger account rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Select03 =
        """
        SELECT COALESCE(MAX(version),0)
        FROM portfolio_financial.ledger_account WHERE book_id=$1 AND account_id=$2;
        """;

    /// <summary>Reads ledger account rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Select04 =
        """
        SELECT category
        FROM portfolio_financial.ledger_account WHERE book_id=$1 AND account_id=$2 ORDER BY version DESC LIMIT 1;
        """;

    /// <summary>Reads ledger posting rule rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Select05 =
        """
        SELECT COALESCE(MAX(version),0)
        FROM portfolio_financial.ledger_posting_rule WHERE book_id=$1 AND rule_id=$2;
        """;

    /// <summary>Reads ledger account rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Select06 =
        """
        SELECT status FROM portfolio_financial.ledger_account WHERE book_id=$1 AND account_id=$2 AND version=$3;
        """;

    /// <summary>Reads ledger posting rule rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Select07 =
        """
        SELECT COUNT(*) FROM portfolio_financial.ledger_posting_rule WHERE book_id=$1 AND status='Active'
        AND (payload->'Debit'->>'AccountId'=$2 OR payload->'Credit'->>'AccountId'=$2
        OR payload->'ValuationAsset'->>'AccountId'=$2 OR payload->'UnrealizedPnl'->>'AccountId'=$2);
        """;

    /// <summary>Reads capacity usage rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Select08 =
        """
        SELECT COUNT(*) FROM portfolio_financial.capacity_usage WHERE portfolio_id=$1 AND scope_kind=$2
          AND scope_key NOT LIKE 'U1:%' AND (held<>0 OR working<>0 OR position<>0);
        """;

    /// <summary>Reads financial authority rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Select09 =
        """
        SELECT authority_epoch FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;
        """;

    /// <summary>Reads event stream id rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Select10 =
        """
        SELECT currentversion FROM event_stream_id WHERE eventstream=$1 FOR SHARE;
        """;

    /// <summary>Reads ledger journal rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Select11 =
        """
        SELECT r.status='Matched' AND r.source_cut=$3 AND NOT EXISTS(
          SELECT 1 FROM portfolio_financial.ledger_journal j WHERE j.book_id=r.book_id
            AND j.financial_revision>(r.counts->>'FinancialRevision')::bigint)
        FROM portfolio_financial.ledger_reconciliation r WHERE r.book_id=$1 AND r.reconciliation_id=$2;
        """;

    /// <summary>Reads ledger journal rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Select12 =
        """
        SELECT COUNT(DISTINCT j.journal_id),COUNT(e.ordinal),COALESCE(SUM(e.debit),0),COALESCE(SUM(e.credit),0)
        FROM portfolio_financial.ledger_journal j
        LEFT JOIN portfolio_financial.ledger_entry e ON e.journal_id=j.journal_id WHERE j.book_id=$1;
        """;

    /// <summary>Updates financial authority rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Update01 =
        """
        UPDATE portfolio_financial.financial_authority
        SET policy_source_versions=$2,migration_state='QualifiedDevelopmentFresh',operating_state=$3
        WHERE portfolio_id=$1;
        """;

    /// <summary>Updates ledger account rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Update02 =
        """
        UPDATE portfolio_financial.ledger_account
        SET status='Retired' WHERE book_id=$1 AND account_id=$2 AND version=$3 AND status='Active';
        """;

    /// <summary>Updates ledger posting rule rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Update03 =
        """
        UPDATE portfolio_financial.ledger_posting_rule
        SET status='Retired' WHERE book_id=$1 AND rule_id=$2 AND version=$3 AND status='Active';
        """;

    /// <summary>Updates ledger period rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Update04 =
        """
        UPDATE portfolio_financial.ledger_period SET state=$4,revision=revision+1,source_cut=$5,evidence=$6
        WHERE book_id=$1 AND period_id=$2 AND revision=$3 AND state=$7;
        """;

    /// <summary>Updates financial authority rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Update05 =
        """
        UPDATE portfolio_financial.financial_authority
        SET policy_source_versions=$2,source_watermark=$3,valuation_watermark=$4,operating_state=$5
        WHERE portfolio_id=$1;
        """;

    /// <summary>Updates financial authority rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Update06 =
        """
        UPDATE portfolio_financial.financial_authority SET operating_state=$2 WHERE portfolio_id=$1;
        """;

    /// <summary>Updates financial authority rows.</summary>
    internal const string Financial_LedgerConfigurationStore_Update07 =
        """
        UPDATE portfolio_financial.financial_authority SET operating_state=$2 WHERE portfolio_id=$1;
        """;

    /// <summary>Reads ledger entry rows.</summary>
    internal const string Financial_LedgerConfigurationStore_With01 =
        """
        WITH actual AS (SELECT account_id,fund_id,SUM(debit) d,SUM(credit) c FROM portfolio_financial.ledger_entry WHERE book_id=$1 GROUP BY account_id,fund_id),
        recorded AS (SELECT account_id,fund_id,debit_total d,credit_total c FROM portfolio_financial.ledger_account_balance WHERE book_id=$1),
        keys AS (SELECT account_id,fund_id FROM actual UNION SELECT account_id,fund_id FROM recorded)
        SELECT k.account_id,k.fund_id,COALESCE(a.d,0),COALESCE(a.c,0),COALESCE(r.d,0),COALESCE(r.c,0)
        FROM keys k LEFT JOIN actual a ON a.account_id=k.account_id AND a.fund_id IS NOT DISTINCT FROM k.fund_id
        LEFT JOIN recorded r ON r.account_id=k.account_id AND r.fund_id IS NOT DISTINCT FROM k.fund_id
        WHERE COALESCE(a.d,0)<>COALESCE(r.d,0) OR COALESCE(a.c,0)<>COALESCE(r.c,0)
        ORDER BY k.account_id,k.fund_id;
        """;

    /// <summary>SQL for insert checkpoint.</summary>
    internal const string Financial_LedgerFinancialProjection_InsertCheckpoint =
        """
        INSERT INTO portfolio_financial.financial_projection_checkpoint(source_event_id,portfolio_id,event_version,
            financial_revision)
        VALUES($1,$2,$3,$4) ON CONFLICT(source_event_id) DO NOTHING;
        """;

    /// <summary>SQL for match prepared portfolio.</summary>
    internal const string Financial_PortfolioAuthorityFence_MatchPreparedPortfolio =
        """
        SELECT EXISTS (
            SELECT FROM portfolio.portfolio_by_id
            WHERE portfolio_id=$1 AND aggregate_version=$2 AND portfolio_version=$3 AND operating_state=$4
        );
        """;

    /// <summary>SQL for select01.</summary>
    internal const string Financial_PortfolioAuthorityFence_Select01 =
        """
        SELECT pg_advisory_xact_lock(34100,$1);
        """;

    /// <summary>Reads and locks financial authority rows.</summary>
    internal const string Financial_PortfolioAuthorityFence_Select02 =
        """
        SELECT policy_source_versions::text,authority_epoch,financial_revision,operating_state
        FROM portfolio_financial.financial_authority
        WHERE portfolio_id=$1 FOR UPDATE;
        """;

    /// <summary>Reads capacity reservation rows.</summary>
    internal const string Financial_PortfolioAuthorityFence_Select03 =
        """
        SELECT reservation_id
        FROM portfolio_financial.capacity_reservation
        WHERE portfolio_id=$1 AND order_id=$2 AND status NOT IN (8,9) LIMIT 1;
        """;

    /// <summary>Updates financial authority rows.</summary>
    internal const string Financial_PortfolioAuthorityFence_Update01 =
        """
        UPDATE portfolio_financial.financial_authority
        SET financial_revision=financial_revision+1 WHERE portfolio_id=$1;
        """;

    /// <summary>Updates financial authority rows.</summary>
    internal const string Financial_PortfolioAuthorityFence_Update02 =
        """
        UPDATE portfolio_financial.financial_authority
        SET authority_epoch=$2,financial_revision=$3,operating_state='NeedsRefresh'
        WHERE portfolio_id=$1;
        """;

    /// <summary>Updates financial authority rows.</summary>
    internal const string Financial_PortfolioAuthorityFence_Update03 =
        """
        UPDATE portfolio_financial.financial_authority SET policy_source_versions=$2 WHERE portfolio_id=$1;
        """;

    /// <summary>Inserts ledger book rows.</summary>
    internal const string Financial_PortfolioDbFinancialSupport_Insert01 =
        """
        INSERT INTO portfolio_financial.ledger_book(book_id,accounting_entity_id,portfolio_id,base_currency,
            execution_account_ref,environment,version,status)
        VALUES($1,$2,$3,'USD',$4,$5,1,'Active');
        """;

    /// <summary>Inserts financial authority rows.</summary>
    internal const string Financial_PortfolioDbFinancialSupport_Insert02 =
        """
        INSERT INTO portfolio_financial.financial_authority(portfolio_id,book_id,authority_epoch,operating_state,
            policy_source_versions,source_watermark,valuation_watermark,migration_state)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8);
        """;

    /// <summary>Inserts ledger account rows.</summary>
    internal const string Financial_PortfolioDbFinancialSupport_Insert03 =
        """
        INSERT INTO portfolio_financial.ledger_account(book_id,account_id,version,category,normal_side,currency,
            fund_dimension_required,status,content_hash)
        VALUES($1,$2,$3,$4,$5,'USD',$6,'Active',$7);
        """;

    /// <summary>Inserts ledger posting rule rows.</summary>
    internal const string Financial_PortfolioDbFinancialSupport_Insert04 =
        """
        INSERT INTO portfolio_financial.ledger_posting_rule(book_id,rule_id,version,kind,payload,content_hash,status,
            effective_from)
        VALUES($1,$2,$3,$4,$5,$6,'Active',$7);
        """;

    /// <summary>Inserts ledger period rows.</summary>
    internal const string Financial_PortfolioDbFinancialSupport_Insert05 =
        """
        INSERT INTO portfolio_financial.ledger_period(book_id,period_id,start_date,end_date,state,revision,source_cut,evidence)
        VALUES($1,$2,$3,$4,'Open',1,$5,'{}');
        """;

    /// <summary>Inserts financial operation receipt rows.</summary>
    internal const string Financial_PortfolioDbFinancialSupport_Insert06 =
        """
        INSERT INTO portfolio_financial.financial_operation_receipt(portfolio_id,operation_id,actor_name,input_hash,
            event_version,financial_revision)
        VALUES($1,$2,$3,$4,$5,$6);
        """;

    /// <summary>SQL for select01.</summary>
    internal const string Financial_PortfolioDbFinancialSupport_Select01 =
        """
        SELECT pg_advisory_xact_lock(34100,$1);
        """;

    /// <summary>Reads financial operation receipt rows.</summary>
    internal const string Financial_PortfolioDbFinancialSupport_Select02 =
        """
        SELECT r.input_hash,e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,e.commandid,
            e.eventtimestamp::text,e.streamversion
        FROM portfolio_financial.financial_operation_receipt r JOIN event_log e ON e.eventversion=r.event_version
        JOIN event_name_id n ON n.eventnameid=e.eventnameid WHERE r.portfolio_id=$1 AND r.operation_id=$2;
        """;

    /// <summary>Reads and locks financial authority rows.</summary>
    internal const string Financial_PortfolioDbFinancialSupport_Select03 =
        """
        SELECT policy_source_versions::text,financial_revision,operating_state
        FROM portfolio_financial.financial_authority
        WHERE portfolio_id=$1 FOR UPDATE;
        """;

    /// <summary>Reads event stream id rows.</summary>
    internal const string Financial_PortfolioDbFinancialSupport_Select04 =
        """
        SELECT currentversion FROM event_stream_id WHERE eventstream=$1 FOR SHARE;
        """;

    /// <summary>Reads event stream id rows.</summary>
    internal const string Financial_PortfolioDbFinancialSupport_Select05 =
        """
        SELECT currentversion FROM event_stream_id WHERE eventstream=$1;
        """;

    /// <summary>Updates financial authority rows.</summary>
    internal const string Financial_PortfolioDbFinancialSupport_Update01 =
        """
        UPDATE portfolio_financial.financial_authority SET financial_revision=$2 WHERE portfolio_id=$1;
        """;

    /// <summary>Reads schema version rows.</summary>
    internal const string Financial_PortfolioFinancialSchema_Select01 =
        """
        SELECT version FROM portfolio_financial.schema_version WHERE singleton=TRUE;
        """;

    /// <summary>SQL for read active book by execution account.</summary>
    internal const string Financial_ReadActiveBookByExecutionAccount =
        """
        SELECT a.policy_source_versions::text
        FROM portfolio_financial.ledger_book b
        JOIN portfolio_financial.financial_authority a
          ON a.portfolio_id=b.portfolio_id AND a.book_id=b.book_id
        WHERE b.environment=$1 AND b.execution_account_ref=$2 AND b.status='Active';
        """;

    /// <summary>SQL for read book.</summary>
    internal const string Financial_ReadBook =
        """
        SELECT policy_source_versions::text FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;
        """;

    /// <summary>SQL for read prepared identifier receipts.</summary>
    internal const string Financial_ReadPreparedIdentifierReceipts =
        """
        SELECT s.eventstream,e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,
               e.eventpayload,e.commandid,e.eventtimestamp::text,e.streamversion
        FROM event_stream_id s
        JOIN event_log e ON e.eventstreamid=s.eventstreamid AND e.streamversion=1
        JOIN event_name_id n ON n.eventnameid=e.eventnameid
        WHERE s.eventstream=ANY($1);
        """;

    /// <summary>SQL for active.</summary>
    internal const string Fund_Active =
        """
        SELECT payload_json::text
        FROM portfolio.active_fund_by_portfolio_horizon
        WHERE portfolio_id=$1 AND trading_year=$2 AND decision_horizon=$3 AND effective_from_utc<=$4
        ORDER BY effective_from_utc DESC,fund_id LIMIT $5;
        """;

    /// <summary>SQL for allocation.</summary>
    internal const string Fund_Allocation =
        """
        SELECT payload_json::text
        FROM portfolio.fund_allocation
        WHERE portfolio_id=$1 AND fund_id=$2 ORDER BY allocation_version DESC LIMIT 1;
        """;

    /// <summary>SQL for assignments.</summary>
    internal const string Fund_Assignments =
        """
        SELECT payload_json::text
        FROM portfolio.fund_template_assignment
        WHERE portfolio_id=$1 AND fund_id=$2 AND fund_mandate_version=$3
        ORDER BY trade_template_id,trade_template_version LIMIT $4;
        """;

    /// <summary>SQL for by portfolio.</summary>
    internal const string Fund_ByPortfolio =
        """
        SELECT payload_json::text
        FROM portfolio.fund_by_portfolio
        WHERE portfolio_id=$1 AND fund_id>$2 ORDER BY fund_id,fund_mandate_version DESC LIMIT $3;
        """;

    /// <summary>SQL for envelope.</summary>
    internal const string Fund_Envelope =
        """
        SELECT payload_json::text
        FROM portfolio.fund_risk_envelope
        WHERE portfolio_id=$1 AND fund_id=$2 ORDER BY envelope_version DESC LIMIT 1;
        """;

    /// <summary>SQL for get.</summary>
    internal const string Fund_Get =
        """
        SELECT payload_json::text
        FROM portfolio.fund_by_portfolio WHERE fund_id=$1 ORDER BY fund_mandate_version DESC LIMIT 1;
        """;

    /// <summary>SQL for revision.</summary>
    internal const string Fund_Revision =
        """
        SELECT portfolio_id,aggregate_version,source_event_id
        FROM portfolio.fund_by_portfolio WHERE fund_id=$1 ORDER BY fund_mandate_version DESC LIMIT 1;
        """;

    /// <summary>SQL for upsert.</summary>
    internal const string Fund_Upsert =
        """
        WITH saved_fund AS (
          INSERT INTO portfolio.fund_by_portfolio(portfolio_id,fund_id,fund_mandate_version,operating_state,
            schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash)
          SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,$10 WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio' AND entity_key=$1::text AND source_event_id>=$7)
          ON CONFLICT(portfolio_id,fund_id,fund_mandate_version) DO UPDATE SET operating_state=EXCLUDED.operating_state,
            schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,
            source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,
            payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash WHERE portfolio.fund_by_portfolio.source_event_id<=EXCLUDED.source_event_id RETURNING 1),
        removed_active AS (DELETE FROM portfolio.active_fund_by_portfolio_horizon WHERE fund_id=$2
            AND source_event_id<=$7 RETURNING 1)
        INSERT INTO portfolio.active_fund_by_portfolio_horizon(portfolio_id,trading_year,decision_horizon,
            effective_from_utc,fund_id,fund_mandate_version,schema_version,aggregate_version,source_event_id,
            updated_on_utc,payload_json,payload_hash)
        SELECT $1,$11,$12,$13,$2,$3,$5,$6,$7,$8,$9,$10 WHERE $4='Active' AND EXISTS(SELECT 1 FROM saved_fund)
        ON CONFLICT(portfolio_id,trading_year,decision_horizon,effective_from_utc,fund_id)
        DO UPDATE SET fund_mandate_version=EXCLUDED.fund_mandate_version,schema_version=EXCLUDED.schema_version,
          aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,
          updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash
        WHERE portfolio.active_fund_by_portfolio_horizon.source_event_id<=EXCLUDED.source_event_id;
        """;

    /// <summary>SQL for upsert allocation.</summary>
    internal const string Fund_UpsertAllocation =
        """
        INSERT INTO portfolio.fund_allocation(portfolio_id,fund_id,allocation_version,schema_version,aggregate_version,
            source_event_id,updated_on_utc,payload_json,payload_hash) SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9
        WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio'
            AND entity_key=$1::text AND source_event_id>=$6)
        ON CONFLICT(portfolio_id,fund_id,allocation_version) DO UPDATE SET schema_version=EXCLUDED.schema_version,
            aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,
            updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash
        WHERE portfolio.fund_allocation.source_event_id<=EXCLUDED.source_event_id;
        """;

    /// <summary>SQL for upsert assignment.</summary>
    internal const string Fund_UpsertAssignment =
        """
        INSERT INTO portfolio.fund_template_assignment(portfolio_id,fund_id,fund_mandate_version,trade_template_id,
            trade_template_version,schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,
            payload_hash) SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11
        WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio'
            AND entity_key=$1::text AND source_event_id>=$8)
        ON CONFLICT(portfolio_id,fund_id,fund_mandate_version,trade_template_id,
            trade_template_version) DO UPDATE SET schema_version=EXCLUDED.schema_version,
            aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,
            updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash
        WHERE portfolio.fund_template_assignment.source_event_id<=EXCLUDED.source_event_id;
        """;

    /// <summary>SQL for upsert envelope.</summary>
    internal const string Fund_UpsertEnvelope =
        """
        INSERT INTO portfolio.fund_risk_envelope(portfolio_id,fund_id,envelope_version,schema_version,aggregate_version,
            source_event_id,updated_on_utc,payload_json,payload_hash) SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9
        WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio'
            AND entity_key=$1::text AND source_event_id>=$6)
        ON CONFLICT(portfolio_id,fund_id,envelope_version) DO UPDATE SET schema_version=EXCLUDED.schema_version,
            aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,
            updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash
        WHERE portfolio.fund_risk_envelope.source_event_id<=EXCLUDED.source_event_id;
        """;

    /// <summary>SQL for lock accepted execution capacity.</summary>
    internal const string LockAcceptedExecutionCapacity =
        """
        SELECT payload FROM portfolio_financial.accepted_execution_capacity WHERE order_id=$1 FOR UPDATE;
        """;

    /// <summary>SQL for lock fund projection.</summary>
    internal const string LockFundProjection =
        """
        SELECT pg_advisory_xact_lock(1701,$1);
        """;

    /// <summary>SQL for insert accepted close.</summary>
    internal const string OrderComposition_InsertAcceptedClose =
        """
        INSERT INTO portfolio.accepted_position_close(operation_id,target_position_id,target_order_id,target_trade_id,
            close_order_id,accepted_at_utc)
        VALUES($1,$2,$3,$4,$5,$6);
        """;

    /// <summary>SQL for insert capacity.</summary>
    internal const string OrderComposition_InsertCapacity =
        """
        INSERT INTO portfolio.accepted_trade_order_capacity(order_id,portfolio_id,fund_id,scope_kind,scope_key,measure,
            unit,amount,method_version,financial_revision)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10);
        """;

    /// <summary>SQL for insert decision.</summary>
    internal const string OrderComposition_InsertDecision =
        """
        INSERT INTO portfolio.order_composition_decision(operation_id,composition_id,workflow_id,portfolio_id,status,
            financial_revision,input_hash,receipt,committed_at_utc)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9);
        """;

    /// <summary>SQL for insert fund decision.</summary>
    internal const string OrderComposition_InsertFundDecision =
        """
        INSERT INTO portfolio.order_composition_fund_decision(operation_id,fund_id,accepted,reason_code,order_id)
        VALUES($1,$2,$3,$4,$5);
        """;

    /// <summary>SQL for insert leg.</summary>
    internal const string OrderComposition_InsertLeg =
        """
        INSERT INTO portfolio.accepted_trade_order_leg(order_id,component_id,trade_leg_id,ordinal,definition)
        VALUES($1,$2,$3,$4,$5);
        """;

    /// <summary>SQL for insert order.</summary>
    internal const string OrderComposition_InsertOrder =
        """
        INSERT INTO portfolio.accepted_trade_order(order_id,operation_id,portfolio_id,fund_id,status,definition_hash,
            definition,valid_until_utc)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8);
        """;

    /// <summary>SQL for select accepted close.</summary>
    internal const string OrderComposition_SelectAcceptedClose =
        """
        SELECT c.close_order_id
        FROM portfolio.accepted_position_close c
        JOIN portfolio.order_composition_decision d USING (operation_id)
        WHERE c.target_position_id=$1
        ORDER BY d.financial_revision DESC
        LIMIT 1;
        """;

    /// <summary>SQL for select current close position.</summary>
    internal const string OrderComposition_SelectCurrentClosePosition =
        """
        SELECT payload FROM trade.strategy_position_current
        WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 AND positionid=$5 AND isopen;
        """;

    /// <summary>SQL for select opening order.</summary>
    internal const string OrderComposition_SelectOpeningOrder =
        """
        SELECT definition::text
        FROM portfolio.accepted_trade_order WHERE order_id=$1 AND portfolio_id=$2 AND fund_id=$3 FOR SHARE;
        """;

    /// <summary>SQL for select prior close execution.</summary>
    internal const string OrderComposition_SelectPriorCloseExecution =
        """
        SELECT CASE WHEN COUNT(*)=1 THEN (array_agg(payload))[1] END
        FROM trade.order_execution WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3;
        """;

    /// <summary>SQL for compositions.</summary>
    internal const string Orders_Compositions =
        """
        SELECT payload_json::text
        FROM portfolio.fund_composition WHERE workflow_id=$1 ORDER BY order_id LIMIT $2;
        """;

    /// <summary>SQL for delete order.</summary>
    internal const string Orders_DeleteOrder =
        """
        DELETE FROM portfolio.fund_order WHERE order_id=$1 AND source_event_id<=$2;
        """;

    /// <summary>SQL for delete trade.</summary>
    internal const string Orders_DeleteTrade =
        """
        DELETE FROM portfolio.fund_order_trade WHERE trade_id=$1 AND source_event_id<=$2;
        """;

    /// <summary>SQL for get.</summary>
    internal const string Orders_Get =
        """
        SELECT payload_json::text FROM portfolio.fund_order WHERE order_id=$1;
        """;

    /// <summary>SQL for timeline.</summary>
    internal const string Orders_Timeline =
        """
        SELECT payload_json::text
        FROM portfolio.fund_order
        WHERE portfolio_id=$1 AND fund_id=$2 AND order_month=$3 AND created_on_utc<$4
        ORDER BY created_on_utc DESC,order_id DESC LIMIT $5;
        """;

    /// <summary>SQL for trade.</summary>
    internal const string Orders_Trade =
        """
        SELECT payload_json::text FROM portfolio.fund_order_trade WHERE trade_id=$1;
        """;

    /// <summary>SQL for trades.</summary>
    internal const string Orders_Trades =
        """
        SELECT payload_json::text FROM portfolio.fund_order_trade WHERE order_id=$1 ORDER BY trade_id LIMIT $2;
        """;

    /// <summary>SQL for upsert composition.</summary>
    internal const string Orders_UpsertComposition =
        """
        INSERT INTO portfolio.fund_composition(workflow_id,order_id,portfolio_id,fund_id,status,schema_version,
            aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash) SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11
        WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio'
            AND entity_key=$3::text AND source_event_id>=$8)
        ON CONFLICT(workflow_id,order_id) DO UPDATE SET status=EXCLUDED.status,schema_version=EXCLUDED.schema_version,
            aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,
            updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash
        WHERE portfolio.fund_composition.source_event_id<=EXCLUDED.source_event_id;
        """;

    /// <summary>SQL for upsert order.</summary>
    internal const string Orders_UpsertOrder =
        """
        INSERT INTO portfolio.fund_order(portfolio_id,fund_id,order_month,created_on_utc,order_id,status,schema_version,
            aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash) SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,
            $10,$11,$12
        WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio'
            AND entity_key=$1::text AND source_event_id>=$9)
        ON CONFLICT(order_id) DO UPDATE SET status=EXCLUDED.status,schema_version=EXCLUDED.schema_version,
            aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,
            updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash
        WHERE portfolio.fund_order.source_event_id<=EXCLUDED.source_event_id;
        """;

    /// <summary>SQL for upsert trade.</summary>
    internal const string Orders_UpsertTrade =
        """
        INSERT INTO portfolio.fund_order_trade(order_id,trade_id,portfolio_id,fund_id,schema_version,aggregate_version,
            source_event_id,updated_on_utc,payload_json,payload_hash) SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,$10
        WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio'
            AND entity_key=$3::text AND source_event_id>=$7)
        ON CONFLICT(trade_id) DO UPDATE SET order_id=EXCLUDED.order_id,portfolio_id=EXCLUDED.portfolio_id,
            fund_id=EXCLUDED.fund_id,schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,
            source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,
            payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash
        WHERE portfolio.fund_order_trade.source_event_id<=EXCLUDED.source_event_id;
        """;

    /// <summary>SQL for active.</summary>
    internal const string Policy_Active =
        """
        SELECT payload_json::text FROM portfolio.active_portfolio_policy WHERE portfolio_id=$1;
        """;

    /// <summary>SQL for by portfolio.</summary>
    internal const string Policy_ByPortfolio =
        """
        SELECT payload_json::text
        FROM portfolio.portfolio_policy
        WHERE portfolio_id=$1 ORDER BY policy_id DESC,policy_version DESC LIMIT $2;
        """;

    /// <summary>SQL for delete draft.</summary>
    internal const string Policy_DeleteDraft =
        """
        WITH tombstone AS (
          INSERT INTO portfolio.projection_tombstone(entity_kind,entity_key,source_event_id) VALUES('policy',
            $1::text||':'||$2::text,$3)
          ON CONFLICT(entity_kind,entity_key) DO UPDATE SET source_event_id=GREATEST(portfolio.projection_tombstone.source_event_id,EXCLUDED.source_event_id) RETURNING 1),
        deleted_active AS (DELETE FROM portfolio.active_portfolio_policy WHERE portfolio_id=$1 AND policy_id=$2
            AND source_event_id<=$3 RETURNING 1)
        DELETE FROM portfolio.portfolio_policy WHERE portfolio_id=$1 AND policy_id=$2 AND source_event_id<=$3;
        """;

    /// <summary>SQL for get current.</summary>
    internal const string Policy_GetCurrent =
        """
        SELECT payload_json::text
        FROM portfolio.portfolio_policy WHERE policy_id=$1 ORDER BY policy_version DESC LIMIT 1;
        """;

    /// <summary>SQL for get version.</summary>
    internal const string Policy_GetVersion =
        """
        SELECT payload_json::text FROM portfolio.portfolio_policy WHERE policy_id=$1 AND policy_version=$2;
        """;

    /// <summary>SQL for upsert.</summary>
    internal const string Policy_Upsert =
        """
        WITH saved_policy AS (
          INSERT INTO portfolio.portfolio_policy(policy_id,policy_version,portfolio_id,operating_state,schema_version,
            aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash)
          SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,$10 WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='policy' AND entity_key=$3::text||':'||$1::text AND source_event_id>=$7)
          ON CONFLICT(policy_id,policy_version) DO UPDATE SET portfolio_id=EXCLUDED.portfolio_id,
            operating_state=EXCLUDED.operating_state,schema_version=EXCLUDED.schema_version,
            aggregate_version=EXCLUDED.aggregate_version,source_event_id=EXCLUDED.source_event_id,
            updated_on_utc=EXCLUDED.updated_on_utc,payload_json=EXCLUDED.payload_json,
            payload_hash=EXCLUDED.payload_hash WHERE portfolio.portfolio_policy.source_event_id<=EXCLUDED.source_event_id RETURNING 1),
        removed_active AS (DELETE FROM portfolio.active_portfolio_policy WHERE portfolio_id=$3
            AND source_event_id<=$7 RETURNING 1)
        INSERT INTO portfolio.active_portfolio_policy(portfolio_id,policy_id,policy_version,schema_version,
            aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash)
        SELECT $3,$1,$2,$5,$6,$7,$8,$9,$10 WHERE $4='Active' AND EXISTS(SELECT 1 FROM saved_policy)
        ON CONFLICT(portfolio_id) DO UPDATE SET policy_id=EXCLUDED.policy_id,policy_version=EXCLUDED.policy_version,
          schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,
          source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,
          payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash
        WHERE portfolio.active_portfolio_policy.source_event_id<=EXCLUDED.source_event_id;
        """;

    /// <summary>SQL for by operating state.</summary>
    internal const string Portfolio_ByOperatingState =
        """
        SELECT payload_json::text
        FROM portfolio.portfolio_by_state
        WHERE operating_state=$1 AND portfolio_id>$2 ORDER BY portfolio_id LIMIT $3;
        """;

    /// <summary>SQL for by state.</summary>
    internal const string Portfolio_ByState =
        """
        SELECT payload_json::text
        FROM portfolio.portfolio_by_state
        WHERE operating_state=$1 AND state_bucket=$2 AND portfolio_id>$3 ORDER BY portfolio_id LIMIT $4;
        """;

    /// <summary>SQL for delete draft.</summary>
    internal const string Portfolio_DeleteDraft =
        """
        WITH tombstone AS (
          INSERT INTO portfolio.projection_tombstone(entity_kind,entity_key,source_event_id) VALUES('portfolio',$1::text,$2)
          ON CONFLICT(entity_kind,entity_key) DO UPDATE SET source_event_id=GREATEST(portfolio.projection_tombstone.source_event_id,EXCLUDED.source_event_id) RETURNING 1),
        deleted_active_funds AS (DELETE FROM portfolio.active_fund_by_portfolio_horizon WHERE portfolio_id=$1
            AND source_event_id<=$2 RETURNING 1),
        deleted_assignments AS (DELETE FROM portfolio.fund_template_assignment WHERE portfolio_id=$1
            AND source_event_id<=$2 RETURNING 1),
        deleted_allocations AS (DELETE FROM portfolio.fund_allocation WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1),
        deleted_envelopes AS (DELETE FROM portfolio.fund_risk_envelope WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1),
        deleted_trades AS (DELETE FROM portfolio.fund_order_trade WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1),
        deleted_compositions AS (DELETE FROM portfolio.fund_composition WHERE portfolio_id=$1
            AND source_event_id<=$2 RETURNING 1),
        deleted_orders AS (DELETE FROM portfolio.fund_order WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1),
        deleted_funds AS (DELETE FROM portfolio.fund_by_portfolio WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1),
        deleted_states AS (DELETE FROM portfolio.portfolio_by_state WHERE portfolio_id=$1 AND source_event_id<=$2 RETURNING 1)
        DELETE FROM portfolio.portfolio_by_id WHERE portfolio_id=$1 AND source_event_id<=$2;
        """;

    /// <summary>SQL for get.</summary>
    internal const string Portfolio_Get =
        """
        SELECT payload_json::text
        FROM portfolio.portfolio_by_id WHERE portfolio_id=$1 ORDER BY portfolio_version DESC LIMIT 1;
        """;

    /// <summary>SQL for revision.</summary>
    internal const string Portfolio_Revision =
        """
        SELECT aggregate_version,source_event_id
        FROM portfolio.portfolio_by_id WHERE portfolio_id=$1 ORDER BY portfolio_version DESC LIMIT 1;
        """;

    /// <summary>SQL for upsert.</summary>
    internal const string Portfolio_Upsert =
        """
        WITH saved_portfolio AS (
          INSERT INTO portfolio.portfolio_by_id(portfolio_id,portfolio_version,operating_state,schema_version,
            aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash)
          SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9 WHERE NOT EXISTS(SELECT 1 FROM portfolio.projection_tombstone WHERE entity_kind='portfolio' AND entity_key=$1::text AND source_event_id>=$6)
          ON CONFLICT(portfolio_id,portfolio_version) DO UPDATE SET operating_state=EXCLUDED.operating_state,
            schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,
            source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,
            payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash WHERE portfolio.portfolio_by_id.source_event_id<=EXCLUDED.source_event_id RETURNING 1),
        removed_state AS (
          DELETE FROM portfolio.portfolio_by_state WHERE portfolio_id=$1 AND (operating_state<>$3 OR state_bucket<>$10)
            AND source_event_id<=$6 RETURNING 1)
        INSERT INTO portfolio.portfolio_by_state(operating_state,state_bucket,portfolio_id,portfolio_version,
            schema_version,aggregate_version,source_event_id,updated_on_utc,payload_json,payload_hash)
        SELECT $3,$10,$1,$2,$4,$5,$6,$7,$8,$9 WHERE EXISTS(SELECT 1 FROM saved_portfolio)
        ON CONFLICT(operating_state,state_bucket,portfolio_id) DO UPDATE SET portfolio_version=EXCLUDED.portfolio_version,
            schema_version=EXCLUDED.schema_version,aggregate_version=EXCLUDED.aggregate_version,
            source_event_id=EXCLUDED.source_event_id,updated_on_utc=EXCLUDED.updated_on_utc,
            payload_json=EXCLUDED.payload_json,payload_hash=EXCLUDED.payload_hash
        WHERE portfolio.portfolio_by_state.source_event_id<=EXCLUDED.source_event_id;
        """;

    /// <summary>SQL for read execution capacity approval.</summary>
    internal const string ReadExecutionCapacityApproval =
        """
        SELECT definition_hash
        FROM portfolio.accepted_trade_order
        WHERE order_id=$1 AND portfolio_id=$2 AND fund_id=$3 AND operation_id=$4;
        """;

    /// <summary>SQL for read execution capacity exposures.</summary>
    internal const string ReadExecutionCapacityExposures =
        """
        SELECT scope_kind,scope_key,measure,unit,amount,method_version
        FROM portfolio.accepted_trade_order_capacity WHERE order_id=$1 AND portfolio_id=$2 AND fund_id=$3;
        """;

    /// <summary>SQL for read execution capacity source.</summary>
    internal const string ReadExecutionCapacitySource =
        """
        SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,e.commandid,
            e.eventtimestamp::text,e.streamversion
        FROM event_log e
        JOIN event_name_id n ON n.eventnameid=e.eventnameid
        WHERE e.commandid=$1 ORDER BY e.eventversion LIMIT 65;
        """;

    /// <summary>SQL for read locked fund projection.</summary>
    internal const string ReadLockedFundProjection =
        """
        SELECT payload_json::text
        FROM portfolio.fund_by_portfolio WHERE portfolio_id=$1 AND fund_id=$2 FOR UPDATE;
        """;

    /// <summary>SQL for read posted execution capacity sources.</summary>
    internal const string ReadPostedExecutionCapacitySources =
        """
        SELECT source_event_key,posting_purpose,source_content_hash
        FROM portfolio_financial.financial_source_receipt
        WHERE book_id=$1 AND source_system=$2 AND source_event_key=ANY($3);
        """;

    /// <summary>SQL for write accepted execution capacity.</summary>
    internal const string WriteAcceptedExecutionCapacity =
        """
        INSERT INTO portfolio_financial.accepted_execution_capacity(order_id,portfolio_id,fund_id,execution_id,
            source_event_version,financial_revision,payload)
        VALUES($1,$2,$3,$4,$5,$6,$7) ON CONFLICT(order_id) DO UPDATE SET
        source_event_version=EXCLUDED.source_event_version,financial_revision=EXCLUDED.financial_revision,payload=EXCLUDED.payload
        WHERE accepted_execution_capacity.source_event_version < EXCLUDED.source_event_version;
        """;
}
