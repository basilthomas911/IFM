namespace TomasAI.IFM.Application.Storage.TradeDb.Schema;
/// <summary>Native PostgreSQL trade tables and explicit history-order indexes. No runtime CQL translation.</summary>
public static class TradeSchemaSql
{
    public const string Drop = "DROP SCHEMA IF EXISTS trade CASCADE;";
    public const string Create = """
        CREATE SCHEMA IF NOT EXISTS trade;
        
        CREATE TABLE IF NOT EXISTS trade.broker_account_read_model (
        account_alias text, revision bigint, account_definition bytea, PRIMARY KEY (account_alias, revision)
        );
        
        CREATE INDEX IF NOT EXISTS broker_account_read_model_ordered ON trade.broker_account_read_model (account_alias, revision DESC);
        
        CREATE TABLE IF NOT EXISTS trade.trade_order (
        portfolioId int, fundId int, orderId int, revision int,
                    status text, valueDate date, updatedAtUtc timestamptz,
                    definitionHash text, payload bytea,
                    PRIMARY KEY (portfolioId, fundId, orderId, revision)
        );
        
        CREATE INDEX IF NOT EXISTS trade_order_ordered ON trade.trade_order (portfolioId, fundId, orderId DESC, revision DESC);
        
        CREATE TABLE IF NOT EXISTS trade.order_execution (
        portfolioId int, fundId int, orderId int, executionAttemptId uuid,
                    status text, startedAtUtc timestamptz, completedAtUtc timestamptz, payload bytea,
                    PRIMARY KEY (portfolioId, fundId, orderId, executionAttemptId)
        );
        
        CREATE TABLE IF NOT EXISTS trade.order_execution_fill (
        executionAttemptId uuid, executionFillId uuid, componentId uuid,
                    tradeLegId uuid, contractId text, filledAtUtc timestamptz, payload bytea,
                    PRIMARY KEY (executionAttemptId, filledAtUtc, executionFillId)
        );
        
        CREATE INDEX IF NOT EXISTS order_execution_fill_ordered ON trade.order_execution_fill (executionAttemptId, filledAtUtc ASC, executionFillId ASC);
        
        CREATE TABLE IF NOT EXISTS trade.established_trade (
        portfolioId int, fundId int, orderId int, tradeId int,
                    assetFamily text, strategyKind text, establishedAtUtc timestamptz,
                    evidenceRevision int, payload bytea,
                    PRIMARY KEY (portfolioId, fundId, orderId, tradeId)
        );
        
        CREATE TABLE IF NOT EXISTS trade.established_trade_history (
        portfolioId int, fundId int, strategyKind text, establishedAtUtc timestamptz,
                    orderId int, tradeId int, evidenceRevision int, payload bytea,
                    PRIMARY KEY (portfolioId, fundId, strategyKind, establishedAtUtc, orderId, tradeId, evidenceRevision)
        );
        
        CREATE INDEX IF NOT EXISTS established_trade_history_ordered ON trade.established_trade_history (portfolioId, fundId, strategyKind, establishedAtUtc DESC, orderId ASC, tradeId ASC, evidenceRevision DESC);
        
        CREATE TABLE IF NOT EXISTS trade.strategy_position_current (
        portfolioId int, fundId int, orderId int, tradeId int, positionId uuid,
                    strategyKind text, positionSequence bigint, routeGeneration bigint,
                    isOpen boolean, asOfUtc timestamptz, payload bytea,
                    PRIMARY KEY (portfolioId, fundId, orderId, tradeId, positionId)
        );
        
        CREATE TABLE IF NOT EXISTS trade.strategy_position_history (
        positionId uuid, asOfUtc timestamptz, positionSequence bigint, phase text, payload bytea,
                    PRIMARY KEY (positionId, asOfUtc, positionSequence)
        );
        
        CREATE INDEX IF NOT EXISTS strategy_position_history_ordered ON trade.strategy_position_history (positionId, asOfUtc DESC, positionSequence DESC);
        
        CREATE TABLE IF NOT EXISTS trade.open_position_route (
        contractId text, portfolioId int, fundId int, orderId int, tradeId int,
                    positionId uuid, tradeLegId uuid, tradeType text, generation bigint,
                    PRIMARY KEY (contractId, positionId, tradeLegId)
        );
        
        CREATE TABLE IF NOT EXISTS trade.open_position_route_recovery (
        shard smallint, positionId uuid, tradeLegId uuid, contractId text,
                    portfolioId int, fundId int, orderId int, tradeId int, tradeType text,
                    generation bigint,
                    PRIMARY KEY (shard, positionId, tradeLegId)
        );
        
        CREATE TABLE IF NOT EXISTS trade.risk_management_invocation (
        workflow_id uuid,invocation_id uuid,revision bigint,payload bytea,content_hash text,PRIMARY KEY (workflow_id,invocation_id,revision)
        );
        
        CREATE INDEX IF NOT EXISTS risk_management_invocation_ordered ON trade.risk_management_invocation (workflow_id, invocation_id, revision DESC);
        
        CREATE TABLE IF NOT EXISTS trade.risk_management_history (
        portfolio_id int,fund_id int,value_date date,evaluated_at_utc timestamptz,invocation_id uuid,revision bigint,payload bytea,PRIMARY KEY (portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id)
        );
        
        CREATE INDEX IF NOT EXISTS risk_management_history_ordered ON trade.risk_management_history (portfolio_id, fund_id, value_date, evaluated_at_utc DESC, invocation_id ASC);
        
        CREATE TABLE IF NOT EXISTS trade.order_composition_invocation (
        workflow_id uuid,invocation_id uuid,result_hash text,input_hash text,payload bytea,PRIMARY KEY (workflow_id,invocation_id)
        );
        
        CREATE TABLE IF NOT EXISTS trade.order_composition_history (
        portfolio_id int,fund_id int,value_date date,evaluated_at_utc timestamptz,invocation_id uuid,workflow_id uuid,event_id uuid,target_horizon smallint,outcome smallint,reason_code text,result_id uuid,result_hash text,PRIMARY KEY (portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id)
        );
        
        CREATE INDEX IF NOT EXISTS order_composition_history_ordered ON trade.order_composition_history (portfolio_id, fund_id, value_date, evaluated_at_utc DESC, invocation_id ASC);
        
        CREATE TABLE IF NOT EXISTS trade.option_leg (
        orderId int,
            tradeId int,
            contractId text,
            quantity int,
            strikePrice numeric,
            optionLegType text,
            optionLegAction text,
            createdOn timestamptz,
            createdBy text,
            updatedOn timestamptz,
            updatedBy text,
            PRIMARY KEY (tradeId, orderId, contractId)
        );
        
        CREATE TABLE IF NOT EXISTS trade.option_leg_data (
        orderId int,
            tradeId int,
            valueDate date,
            tradeType text,
            daysToExpiry int,
            tradeStatus text,
            optionLegId text,
            bidPrice numeric,
            askPrice numeric,
            impliedVolatility double precision,
            delta double precision,
            gamma double precision,
            theta double precision,
            vega double precision,
            rho double precision,
            createdOn timestamptz,
            createdBy text,
            updatedOn timestamptz,
            updatedBy text,
            PRIMARY KEY (orderId, tradeId, valueDate, tradeType, daysToExpiry, tradeStatus, optionLegId)
        );
        
        CREATE INDEX IF NOT EXISTS option_leg_data_ordered ON trade.option_leg_data (orderId, tradeId desc, valueDate desc, tradeType desc, daysToExpiry desc, tradeStatus asc, optionLegId asc);
        
        CREATE TABLE IF NOT EXISTS trade.option_trade (
        orderId int,
            tradeId int,
            tradeStrategy text,
            tradeDate date,
            maturityDate date,
            tradeType text,
            tradeState text,
            tradeAction text,
            underlyingContractId text,
            underlyingAssetType text,
            isPrimaryTrade boolean,
            isHedgeTrade boolean,
            createdOn timestamptz,
            createdBy text,
            updatedOn timestamptz,
            updatedBy text,
            PRIMARY KEY (orderId, tradeId)
        );
        
        CREATE INDEX IF NOT EXISTS option_trade_ordered ON trade.option_trade (orderId, tradeId desc);
        
        CREATE TABLE IF NOT EXISTS trade.option_trade_spread_bar_data (
        orderId int,
            tradeId int,
            valueDate date,
            tradeType text,
            barDate timestamptz,
            lossLimit numeric,
            winLimit numeric,
            forwardSpread numeric,
            netSpread numeric,
            PRIMARY KEY (orderId, tradeId, valueDate, tradeType, barDate)
        );
        
        CREATE INDEX IF NOT EXISTS option_trade_spread_bar_data_ordered ON trade.option_trade_spread_bar_data (orderId, tradeid desc, valueDate desc, tradeType asc, barDate desc);
        
        CREATE TABLE IF NOT EXISTS trade.option_trade_spread_data (
        orderId int,
            tradeId int,
            valueDate date,
            tradeType text,
            sequenceId bigint,
            lossLimit numeric,
            winLimit numeric,
            forwardSpread numeric,
            netSpread numeric,
            createdOn timestamptz,
            createdBy text,
            PRIMARY KEY (orderId, tradeId, valueDate, tradeType, sequenceId)
        );
        
        CREATE INDEX IF NOT EXISTS option_trade_spread_data_ordered ON trade.option_trade_spread_data (orderId, tradeId DESC, valueDate DESC, tradeType ASC, sequenceId DESC);
        
        CREATE INDEX IF NOT EXISTS order_composition_history_keyset ON trade.order_composition_history(portfolio_id,fund_id,value_date,evaluated_at_utc DESC,workflow_id DESC,invocation_id DESC);
        CREATE TABLE IF NOT EXISTS trade.regime_discovery_invocation (
            workflow_id uuid, command_id uuid, source_event_id uuid NOT NULL, source_payload bytea NOT NULL,
            PRIMARY KEY (workflow_id, command_id));
        CREATE TABLE IF NOT EXISTS trade.regime_discovery (
        workflowId uuid PRIMARY KEY,
                workflowEntityId text,
                inputWorkflowRevision bigint,
                commandId uuid,
                sourceEventId uuid,
                sourceEventSequence bigint,
                status text,
                parameterPayloadSha256 text,
                signalSnapshotId uuid,
                resultPayload bytea,
                resultPayloadSha256 text,
                failureCode int,
                failureMessage text,
                reasonsPayload bytea,
                schemaVersion int,
                terminalAtUtc timestamptz,
                updatedAtUtc timestamptz
        );
        
        CREATE TABLE IF NOT EXISTS trade.market_condition (
        workflowId uuid PRIMARY KEY, workflowEntityId text, inputWorkflowRevision bigint,
                commandId uuid, sourceEventId uuid, fundId int, instrumentRoot text, targetHorizon text,
                parameterSetId uuid, parameterSetVersion int, parameterPayloadSha256 text,
                snapshotId uuid, snapshotSha256 text, tradeability text, conditionType text,
                direction text, phase text, strength numeric, confidence numeric, primaryReasonCode text,
                resultPayload bytea, resultPayloadSha256 text, evaluatedAtUtc timestamptz, validUntilUtc timestamptz,
                marketDataAsOfUtc timestamptz, completedAtUtc timestamptz, updatedAtUtc timestamptz,
                volatilityBehavior text, liquidityQuality text, dataQuality text, upstreamAlignment text,
                evidencePayload bytea, conflictingEvidencePayload bytea, blockingReasonsPayload bytea,
                reasonsPayload bytea, summaryText text
        );
        
        CREATE TABLE IF NOT EXISTS trade.trade_selection_invocation_event (
        workflow_id uuid,
            invocation_id uuid,
            source_sequence bigint,
            event_id uuid,
            portfolio_id int,
            fund_id int,
            target_horizon smallint,
            lifecycle_status smallint,
            outcome smallint,
            occurred_at_utc timestamptz,
            reason_code text,
            selected_deployment_id uuid,
            selected_deployment_version int,
            selected_strategy_id uuid,
            selected_strategy_version int,
            selected_structure_id uuid,
            selected_structure_version int,
            selected_variant_id uuid,
            selected_variant_version int,
            candidate_set_sha256 text,
            binding_sha256 text,
            parameter_set_id uuid,
            parameter_version int,
            parameter_sha256 text,
            result_id uuid,
            result_sha256 text,
            result_payload bytea,
            event_payload bytea,
            PRIMARY KEY (workflow_id, invocation_id, source_sequence)
        );
        
        CREATE INDEX IF NOT EXISTS trade_selection_invocation_event_ordered ON trade.trade_selection_invocation_event (workflow_id, invocation_id, source_sequence DESC);
        
        CREATE TABLE IF NOT EXISTS trade.trade_selection_history_by_fund_date (
        portfolio_id int,
            fund_id int,
            value_date date,
            occurred_at_utc timestamptz,
            workflow_id uuid,
            invocation_id uuid,
            event_id uuid,
            target_horizon smallint,
            outcome smallint,
            reason_code text,
            result_id uuid,
            result_sha256 text,
            PRIMARY KEY (portfolio_id, fund_id, value_date,
                         occurred_at_utc, workflow_id, invocation_id)
        );
        
        CREATE INDEX IF NOT EXISTS trade_selection_history_by_fund_date_ordered ON trade.trade_selection_history_by_fund_date (portfolio_id, fund_id, value_date, occurred_at_utc DESC, workflow_id ASC, invocation_id ASC);
        
        CREATE TABLE IF NOT EXISTS trade.market_condition_assessment (
        workflow_id uuid PRIMARY KEY,payload bytea,payload_sha256 text
        );
        
        CREATE TABLE IF NOT EXISTS trade.market_condition_assessment_by_profile (
        market_profile_id text,instrument_root text,target_horizon text,evaluated_at_utc timestamptz,workflow_id uuid,payload bytea,payload_sha256 text,PRIMARY KEY (market_profile_id,instrument_root,target_horizon,evaluated_at_utc,workflow_id)
        );
        
        CREATE INDEX IF NOT EXISTS market_condition_assessment_by_profile_ordered ON trade.market_condition_assessment_by_profile (market_profile_id, instrument_root, target_horizon, evaluated_at_utc DESC, workflow_id ASC);
        
        CREATE TABLE IF NOT EXISTS trade.market_condition_by_fund (
        fundId int, instrumentRoot text, targetHorizon text, evaluatedAtUtc timestamptz, workflowId uuid,
                workflowEntityId text, inputWorkflowRevision bigint, commandId uuid, sourceEventId uuid,
                parameterSetId uuid, parameterSetVersion int, parameterPayloadSha256 text,
                snapshotId uuid, snapshotSha256 text, tradeability text, conditionType text,
                direction text, phase text, strength numeric, confidence numeric, primaryReasonCode text,
                resultPayload bytea, resultPayloadSha256 text, validUntilUtc timestamptz,
                marketDataAsOfUtc timestamptz, completedAtUtc timestamptz, updatedAtUtc timestamptz,
                volatilityBehavior text, liquidityQuality text, dataQuality text, upstreamAlignment text,
                evidencePayload bytea, conflictingEvidencePayload bytea, blockingReasonsPayload bytea,
                reasonsPayload bytea, summaryText text,
                PRIMARY KEY (fundId, instrumentRoot, targetHorizon, evaluatedAtUtc, workflowId)
        );
        
        CREATE INDEX IF NOT EXISTS market_condition_by_fund_ordered ON trade.market_condition_by_fund (fundId, instrumentRoot, targetHorizon, evaluatedAtUtc DESC, workflowId DESC);
        
        CREATE TABLE IF NOT EXISTS trade.intrinsic_time_strategy_workflow (
        workflowId uuid PRIMARY KEY,
                workflowEntityId text,
                workflowDefinitionId text,
                workflowDefinitionVersion int,
                contractId text,
                timeFrameStartValueDate date,
                timePeriod text,
                triggerEventId uuid,
                correlationId uuid,
                status text,
                outcome text,
                currentStage text,
                workflowRevision bigint,
                lastEventId bigint,
                stateSchemaVersion int,
                statePayload bytea,
                stopReasonCode text,
                startedAtUtc timestamptz,
                terminalAtUtc timestamptz,
                updatedAtUtc timestamptz
        );
        
        CREATE TABLE IF NOT EXISTS trade.intrinsic_time_strategy_workflow_active (
        workflowEntityId text PRIMARY KEY,
                workflowId uuid,
                contractId text,
                timeFrameStartValueDate date,
                timePeriod text,
                currentStage text,
                workflowRevision bigint,
                lastEventId bigint,
                stateSchemaVersion int,
                statePayload bytea,
                startedAtUtc timestamptz,
                updatedAtUtc timestamptz
        );
        
        CREATE TABLE IF NOT EXISTS trade.intrinsic_time_strategy_workflow_start_attempt (
        workflowEntityId text,
                requestedAtUtc timestamptz,
                requestedWorkflowId uuid,
                decision text,
                activeWorkflowId uuid,
                startCommandId uuid,
                triggerEventId uuid,
                activeStage text,
                reasonCode text,
                sourceEventId bigint,
                PRIMARY KEY (workflowEntityId, requestedAtUtc, requestedWorkflowId)
        );
        
        CREATE INDEX IF NOT EXISTS intrinsic_time_strategy_workflow_start_attempt_ordered ON trade.intrinsic_time_strategy_workflow_start_attempt (workflowEntityId, requestedAtUtc DESC, requestedWorkflowId DESC);
        
        CREATE TABLE IF NOT EXISTS trade.intrinsic_time_strategy_workflow_timeline (
        workflowId uuid,
                eventId bigint,
                workflowEntityId text,
                workflowRevision bigint,
                stage text,
                eventName text,
                eventSchemaVersion int,
                eventPayload bytea,
                occurredAtUtc timestamptz,
                PRIMARY KEY (workflowId, eventId)
        );
        
        CREATE INDEX IF NOT EXISTS intrinsic_time_strategy_workflow_timeline_ordered ON trade.intrinsic_time_strategy_workflow_timeline (workflowId, eventId ASC);
        
        CREATE TABLE IF NOT EXISTS trade.intrinsic_time_strategy_workflow_by_entity (
        workflowEntityId text,
                startedAtUtc timestamptz,
                workflowId uuid,
                status text,
                outcome text,
                currentStage text,
                workflowRevision bigint,
                terminalAtUtc timestamptz,
                stopReasonCode text,
                PRIMARY KEY (workflowEntityId, startedAtUtc, workflowId)
        );
        
        CREATE INDEX IF NOT EXISTS intrinsic_time_strategy_workflow_by_entity_ordered ON trade.intrinsic_time_strategy_workflow_by_entity (workflowEntityId, startedAtUtc DESC, workflowId DESC);
        
        CREATE TABLE IF NOT EXISTS trade.intrinsic_time_strategy_workflow_by_status_day (
        status text,
                startedDate date,
                startedAtUtc timestamptz,
                workflowId uuid,
                workflowEntityId text,
                outcome text,
                currentStage text,
                workflowRevision bigint,
                terminalAtUtc timestamptz,
                stopReasonCode text,
                PRIMARY KEY (status, startedDate, startedAtUtc, workflowId)
        );
        
        CREATE INDEX IF NOT EXISTS intrinsic_time_strategy_workflow_by_status_day_ordered ON trade.intrinsic_time_strategy_workflow_by_status_day (status, startedDate, startedAtUtc DESC, workflowId DESC);
        
        CREATE TABLE IF NOT EXISTS trade.trade_fill (
        orderId int,
            tradeId int,
            fillDate timestamptz,
            fillQuantity int,
            createdOn timestamptz,
            createdBy text,
            PRIMARY KEY (orderId, tradeId, fillDate)
        );
        
        CREATE INDEX IF NOT EXISTS trade_fill_ordered ON trade.trade_fill (orderId, tradeId desc, fillDate desc);
        
        CREATE TABLE IF NOT EXISTS trade.trade_fill_data (
        orderId int,
            tradeId int,
            contractId text,
            fillDate timestamptz,
            bidPrice numeric,
            askPrice numeric,
            commission numeric,
            optionLegAction text,
            createdOn timestamptz,
            createdBy text,
            PRIMARY KEY (orderId, tradeId, fillDate, contractId)
        );
        
        CREATE INDEX IF NOT EXISTS trade_fill_data_ordered ON trade.trade_fill_data (orderId, tradeId desc, fillDate desc, contractId asc);
        
        CREATE TABLE IF NOT EXISTS trade.trade_limit (
        tradeId int,
            tradeType text,
            riskMargin numeric,
            maxProfit numeric,
            maxLoss numeric,
            maxReturn numeric,
            maxLossLimit numeric,
            minProfitLimit numeric,
            maxProfitLimit numeric,
            minProfitTarget numeric,
            dailyProfitTarget numeric,
            createdOn timestamptz,
            createdBy text,
            updatedOn timestamptz,
            updatedBy text,
            PRIMARY KEY (tradeId, tradeType)
        );
        
        CREATE INDEX IF NOT EXISTS trade_limit_ordered ON trade.trade_limit (tradeId, tradeType ASC);
        
        CREATE TABLE IF NOT EXISTS trade.trade_live_feed (
        orderId int,
            tradeId int,
            liveFeed boolean,
            PRIMARY KEY (orderId, tradeId)
        );
        
        CREATE INDEX IF NOT EXISTS trade_live_feed_ordered ON trade.trade_live_feed (orderId, tradeId DESC);
        
        CREATE TABLE IF NOT EXISTS trade.trade_placement_signal (
        sequenceId bigint,
            contractId text,
            valueDate date,
            tradePlacementSignal text,
            tradePrice numeric,
            createdOn timestamptz,
            createdBy text,
            PRIMARY KEY (contractId, valueDate, sequenceId)
        );
        
        CREATE INDEX IF NOT EXISTS trade_placement_signal_ordered ON trade.trade_placement_signal (contractId, valueDate DESC, sequenceId DESC);
        
        CREATE TABLE IF NOT EXISTS trade.trade_plan (
        orderId int,
            tradeId int,
            valueDate date,
            sequenceId bigint,
            actionDate timestamptz,
            tradeDate date,
            maturityDate date,
            tradeType text,
            actionType text,
            actionSubType text,
            actionState text,
            actionReason text,
            tradePnl numeric,
            forwardLossRatio double precision,
            lossProbability double precision,
            mScore double precision,
            maxProfit numeric,
            maxLoss numeric,
            minProfitTarget numeric,
            dailyProfitTarget numeric,
            assetPrice numeric,
            assetStdDev double precision,
            assetMean double precision,
            assetPriceChange double precision,
            marketTrend text,
            marketVolatility text,
            marketDirection text,
            vixVolatility text,
            tradeRisk text,
            fiftyDayMA double precision,
            fiveDayXMA double precision,
            putOTMProbability double precision,
            callOTMProbability double precision,
            shortPutGamma double precision,
            shortCallGamma double precision,
            gammaRisk text,
            netPrice numeric,
            forwardPrice numeric,
            forwardDelta double precision,
            stopLossLimit double precision,
            trendType text,
            trendStrength text,
            rsi double precision,
            rsiSlope double precision,
            tdi text,
            tdiStrength text,
            createdOn timestamptz,
            createdBy text,
            PRIMARY KEY (orderId, tradeId, valueDate, sequenceId)
        );
        
        CREATE INDEX IF NOT EXISTS trade_plan_ordered ON trade.trade_plan (orderId, tradeId DESC, valueDate DESC, sequenceId DESC);
        
        CREATE TABLE IF NOT EXISTS trade.trade_plan_forward_loss_limit (
        orderId int,
            tradeId int,
            valueDate date,
            tradeType text,
            limitType text,
            PRIMARY KEY (orderId, tradeId, valueDate, tradeType)
        );
        
        CREATE INDEX IF NOT EXISTS trade_plan_forward_loss_limit_ordered ON trade.trade_plan_forward_loss_limit (orderId, tradeId DESC, valueDate DESC, tradeType ASC);
        
        CREATE TABLE IF NOT EXISTS trade.trade_plan_forward_loss_ratio (
        partitionId int,
            valueDate date,
            forwardLossRatio double precision,
            sequenceId bigint,
            PRIMARY KEY (partitionId, valueDate, forwardLossRatio, sequenceId)
        );
        
        CREATE INDEX IF NOT EXISTS trade_plan_forward_loss_ratio_ordered ON trade.trade_plan_forward_loss_ratio (partitionId, valueDate desc, forwardLossRatio asc, sequenceId desc);
        
        CREATE TABLE IF NOT EXISTS trade.trade_position (
        orderId int,
            tradeId int,
            valueDate date,
            tradeType text,
            tradeStatus text,
            daysToExpiry int,
            commission numeric,
            deltaHedge int,
            netSpread numeric,
            tradeValue numeric,
            tradePnl numeric,
            assetPrice numeric,
            otmProbability double precision,
            forwardPrice numeric,
            forwardLossRatio double precision,
            lossProbability double precision,
            riskFreeRate double precision,
            createdOn timestamptz,
            createdBy text,
            updatedOn timestamptz,
            updatedBy text,
            PRIMARY KEY (orderId, tradeId, valueDate, tradeStatus, daysToExpiry, tradeType)
        );
        
        CREATE TABLE IF NOT EXISTS trade.trade_position_state (
        orderId int,
            tradeId int,
            tradePositionState text,
            openedOn timestamptz,
            openedBy text,
            PRIMARY KEY (orderId, tradeId)
        );
        
        CREATE INDEX IF NOT EXISTS trade_position_state_ordered ON trade.trade_position_state (orderId, tradeId desc);
        
        CREATE TABLE IF NOT EXISTS trade.trade_type_limit (
        tradeId int,
            tradeType text,
            maxLossLimit numeric,
            minProfitLimit numeric,
            maxProfitLimit numeric,
            PRIMARY KEY (tradeId, tradeType)
        );
        
        CREATE INDEX IF NOT EXISTS trade_type_limit_ordered ON trade.trade_type_limit (tradeId, tradeType ASC);
        
        ALTER TABLE trade.market_condition ADD COLUMN IF NOT EXISTS volatilityBehavior text;
        
        ALTER TABLE trade.market_condition ADD COLUMN IF NOT EXISTS liquidityQuality text;
        
        ALTER TABLE trade.market_condition ADD COLUMN IF NOT EXISTS dataQuality text;
        
        ALTER TABLE trade.market_condition ADD COLUMN IF NOT EXISTS upstreamAlignment text;
        
        ALTER TABLE trade.market_condition ADD COLUMN IF NOT EXISTS evidencePayload bytea;
        
        ALTER TABLE trade.market_condition ADD COLUMN IF NOT EXISTS conflictingEvidencePayload bytea;
        
        ALTER TABLE trade.market_condition ADD COLUMN IF NOT EXISTS blockingReasonsPayload bytea;
        
        ALTER TABLE trade.market_condition ADD COLUMN IF NOT EXISTS reasonsPayload bytea;
        
        ALTER TABLE trade.market_condition ADD COLUMN IF NOT EXISTS summaryText text;
        
        ALTER TABLE trade.market_condition_by_fund ADD COLUMN IF NOT EXISTS volatilityBehavior text;
        
        ALTER TABLE trade.market_condition_by_fund ADD COLUMN IF NOT EXISTS liquidityQuality text;
        
        ALTER TABLE trade.market_condition_by_fund ADD COLUMN IF NOT EXISTS dataQuality text;
        
        ALTER TABLE trade.market_condition_by_fund ADD COLUMN IF NOT EXISTS upstreamAlignment text;
        
        ALTER TABLE trade.market_condition_by_fund ADD COLUMN IF NOT EXISTS evidencePayload bytea;
        
        ALTER TABLE trade.market_condition_by_fund ADD COLUMN IF NOT EXISTS conflictingEvidencePayload bytea;
        
        ALTER TABLE trade.market_condition_by_fund ADD COLUMN IF NOT EXISTS blockingReasonsPayload bytea;
        
        ALTER TABLE trade.market_condition_by_fund ADD COLUMN IF NOT EXISTS reasonsPayload bytea;
        
        ALTER TABLE trade.market_condition_by_fund ADD COLUMN IF NOT EXISTS summaryText text;
        ALTER TABLE trade.strategy_position_history ADD COLUMN IF NOT EXISTS projection_sequence bigint NOT NULL DEFAULT 0;
        CREATE INDEX IF NOT EXISTS open_position_route_by_position ON trade.open_position_route(positionId);
        CREATE INDEX IF NOT EXISTS open_position_route_recovery_by_position ON trade.open_position_route_recovery(positionId);
        CREATE INDEX IF NOT EXISTS trade_selection_history_keyset ON trade.trade_selection_history_by_fund_date(portfolio_id,fund_id,value_date,occurred_at_utc DESC,workflow_id DESC,invocation_id DESC);
        """;
}