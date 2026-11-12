//! Retained writes stay in their owner across awaits. Cancelling a read or
//! closing an editor cannot discard an uncertain command identity or payload.
use super::*;
use crate::backend::contracts::{ActorKind, ActorSubject, service_result};
use crate::services::manual_orders;

/// Checks the supplied value against the required positive-value constraint.
fn positive(row: &Json, key: &str) -> Result<i32, String> {
    row[key]
        .as_i64()
        .and_then(|v| i32::try_from(v).ok())
        .filter(|v| *v > 0)
        .ok_or(format!("Missing {key}"))
}
impl TradeController {
    /// Reconciles manual for trade controller.
    pub(super) async fn reconcile_manual(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let Some(operation) = &self.pending_manual else {
            return Ok(());
        };
        let context = self.data.context.as_ref().ok_or("Load API time")?;
        let observed = operation
            .observe(backend, registry, context, deadline)
            .await
            .map_err(|error| {
                format!("OutcomeUnknown: manual order outcome could not be checked: {error}")
            })?;
        let Some(order) = observed else {
            return Err("OutcomeUnknown: the retained manual order outcome is not projected yet; no command was resubmitted".into());
        };
        self.pending_manual = None;
        self.order_draft = None;
        if let Some(request) = self.manual_request.take() {
            if let Some((_, receipt)) = self.receipts.get_mut(&request) {
                receipt.state = "Completed".into();
                receipt.message = format!("Manual operation confirmed for order {order}");
                self.reconciled.push(receipt.clone());
            }
        }
        Ok(())
    }

    /// Provides manual action for the trade controller implementation.
    pub(super) async fn manual_action(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        if self.pending_manual.is_some() {
            return Err("Reload to reconcile the retained manual operation before submitting another mutation; this new request was not submitted".into());
        }
        let context = BusinessContext::load(backend).await?;
        let action = intent["intent"]
            .as_str()
            .ok_or("Missing manual trade action")?;
        let correlation = uuid::Uuid::parse_str(intent["requestId"].as_str().unwrap_or_default())
            .map_err(|_| "Invalid manual request identity")?;
        let portfolio = selected(
            registry,
            "PortfolioReadModel",
            &self.data.portfolios,
            "PortfolioId",
            self.data.portfolio,
        )?
        .ok_or("Select a Portfolio")?;
        let fund = selected(
            registry,
            "FundMandateReadModel",
            &self.data.funds,
            "FundId",
            self.data.fund,
        )?
        .ok_or("Select a Fund")?;
        if action == "prepare-order" {
            // Display conversion only; the API instant remains the request clock.
            let local = context
                .as_of_utc
                .with_timezone(&chrono_tz::America::New_York)
                .naive_local();
            let display = utc_roundtrip(local.and_utc())
                .trim_end_matches('Z')
                .to_string();
            self.order_draft = Some(crate::models::broker_presentation::OrderDraftDisplay {
                order_id: 0,
                order_date: display,
                order_status: "Open".into(),
            });
            self.data.context = Some(context);
            return Ok("New order editor opened".into());
        }
        // Add Trade is a new intent, not a retry of an uncertain write. Read its
        // current order and trades before inferring opening versus closing.
        if matches!(action, "prepare-trade" | "create-trade") {
            let original = self.data.order.ok_or("Select an order before adding a trade")?;
            self.data.context = Some(context.clone());
            self.data.load_orders(backend, registry, deadline).await?;
            if self.data.order != Some(original) {
                return Err("The selected order is no longer available; reload before adding a trade".into());
            }
            let rows = self.data.trades.iter()
                .map(|wire| model(registry, "FundOrderTradeProjectionReadModel", wire))
                .collect::<Result<Vec<_>, _>>()?;
            let scope = (original, rows.first().and_then(|row| row["TradeId"].as_i64()));
            if action == "create-trade" && self.prepared_trade_scope != Some(scope) {
                return Err("The opening trade changed while Add Trade was open; reopen the dialog and review it before submitting".into());
            }
            if !rows.is_empty() && (rows.len() != 1 || rows[0]["PrimaryTrade"] != true
                || !matches!(rows[0]["TradeState"].as_str(), Some("TradeToOpen" | "Open"))) {
                return Err("This order already has an opening trade. Select and place that trade before adding a close; remove an inactive rejected or draft trade before replacing it.".into());
            }
            if rows.is_empty() && self.data.base_contract_choices()?.is_empty() {
                return Err("Configure the Fund Base Contracts in Fund Details, and ensure current Databento futures contracts are available before adding an opening trade".into());
            }
        }
        let order = selected(
            registry,
            "FundOrderProjectionReadModel",
            &self.data.orders,
            "OrderId",
            self.data.order,
        )?;
        let mut edit = None;
        if matches!(action, "prepare-change-trade" | "change-trade") {
            let order = order.as_ref().ok_or("Select an order")?;
            let trade = selected(registry, "FundOrderTradeProjectionReadModel",
                &self.data.trades, "TradeId", self.data.trade)?.ok_or("Select a new trade")?;
            let id = positive(&trade, "TradeId")?;
            if intent["originalId"].as_str() != Some(text(&order["OrderId"]).as_str())
                || field(intent, "tradeId").parse::<i32>().ok() != Some(id)
            {
                return Err("The selected trade changed; reopen Change Trade.".into());
            }
            if trade["TradeState"] != "NewTrade" || trade["ExecutionOrderId"].as_i64().unwrap_or_default() != 0
                || trade["ExecutionTradeId"].as_i64().unwrap_or_default() != 0 || order["Origin"] != "ManualUi"
            {
                return Err("Only a manual NewTrade can be changed before execution.".into());
            }
            let revision = |row: &Json| row["AggregateVersion"].as_i64().filter(|v| *v > 0)
                .ok_or("The selected draft has no valid version");
            let versions = (id, revision(order)?, revision(&trade)?);
            if action == "prepare-change-trade" {
                self.prepared_trade_change = Some(versions);
                self.data.context = Some(context);
                self.data.refresh_contracts(backend, registry).await?;
                return Ok("Trade editor opened".into());
            }
            if self.prepared_trade_change != Some(versions) {
                return Err("The trade changed while the editor was open; reload before saving.".into());
            }
            self.load_submissions(registry).await?;
            if self.submission_operations.values().any(|operation| self.matches_submission(registry, operation)) {
                return Err("This trade has a retained submission. Reconcile the execution before continuing.".into());
            }
            let contract = field(intent, "baseContract");
            let symbol = if trade["PrimaryTrade"] == true {
                self.data.context = Some(context.clone());
                self.data.refresh_contracts(backend, registry).await?;
                self.data.contracts.iter()
                    .map(|wire| registry.project("TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel", wire))
                    .collect::<Result<Vec<_>, _>>()?
                    .into_iter().find(|row| row["ContractId"] == contract)
                    .map(|row| text(&row["Symbol"]))
                    .ok_or("Select a current or future base contract configured for this Fund.")?
            } else {
                text(&trade["BaseContractSymbol"])
            };
            let reference = field(intent, "tradeReference");
            if reference.trim().is_empty() || reference.encode_utf16().count() > 2048 {
                return Err("Enter a trade reference of at most 2048 characters.".into());
            }
            edit = Some(manual_orders::change(registry, &context, order, &trade, json!({
                "TradeType": field(intent, "tradeType"),
                "RequestedTradeDate": crate::backend::date_input::parse(field(intent, "effectiveDate"))?.to_string(),
                "TradeReference": reference.trim(),
                "BaseContractId": contract,
                "BaseContractSymbol": symbol,
            }), correlation)?);
            self.release_preview(backend, registry, deadline).await?;
        }
        if action == "prepare-trade" {
            if order.is_none() {
                return Err("Select an order before adding a trade".into());
            }
            let operation = uuid::Uuid::new_v4();
            let subject = ActorSubject {
                kind: ActorKind::Function,
                name: "SeedAllocationFunction".into(),
                verb: "AllocateSeedId".into(),
                entity: operation.simple().to_string(),
            };
            const LOOKUP_TYPE_BOUNDED_CONTEXT: u8 = 15;
            let request = Value::Array(vec![
                operation.to_string().into(),
                subject.wire(),
                false.into(),
                Value::Array(vec![subject.entity.clone().into()]),
                1023.into(),
                LOOKUP_TYPE_BOUNDED_CONTEXT.into(),
                "TradeId".into(),
            ]);
            let reply = backend.request(&subject, &request, None, None).await.map_err(|error| {
                format!("Trade identity allocation failed and was not retried: {error}")
            })?;
            let completed = service_result(&reply)?
                .as_array()
                .and_then(|terminal| terminal.first())
                .and_then(Value::as_array)
                .ok_or("Missing seed allocation completion")?;
            let returned_operation = completed.get(4).and_then(Value::as_str)
                .and_then(|id| uuid::Uuid::parse_str(id).ok());
            if returned_operation != Some(operation) {
                return Err("Seed allocation reply belongs to another operation".into());
            }
            self.prepared_trade = Some(
                completed.get(9)
                    .and_then(Value::as_i64)
                    .and_then(|v| i32::try_from(v).ok())
                    .filter(|v| *v > 0)
                    .ok_or("Invalid allocated trade identity")?,
            );
            self.prepared_trade_label = None;
            use crate::models::{trade_contracts::FundOrderTradeProjectionReadModel, wire_types::FromBackend};
            let primary = self.data.trades.iter()
                .map(FundOrderTradeProjectionReadModel::from_backend)
                .collect::<Result<Vec<_>, _>>()?
                .into_iter().find(|trade| trade.primary_trade == Some(true));
            self.prepared_trade_scope = Some((self.data.order.ok_or("Select an order")?,
                primary.as_ref().and_then(|trade| trade.trade_id).map(i64::from)));
            if let Some(primary) = primary
                && let Ok(scope) = crate::services::order_close::primary_scope(&primary)
            {
                let trade_type = primary.trade_type.as_deref().unwrap_or_default();
                let strategy = if trade_type.contains("IronCondor") {
                    "IronCondor"
                } else if trade_type == "FuturesOutright" {
                    "FuturesOutright"
                } else {
                    "VerticalSpread"
                };
                let trade = crate::services::trade_monitor_queries::established(
                    backend, registry, scope, strategy, deadline,
                ).await?;
                self.prepared_trade_label = Some(crate::models::trade_presentation::loaded_trade_tab_title(
                    scope.order, scope.trade, trade_type, &trade,
                ));
            }
            self.data.context = Some(context);
            return Ok("Trade identity allocated".into());
        }
        let operation = match action {
            "change-trade" => edit.ok_or("Open Change Trade first")?,
            "create-order" => {
                if self.order_draft.is_none() {
                    return Err("Open Create Order first".into());
                }
                manual_orders::create(
                    registry,
                    &context,
                    &portfolio,
                    &fund,
                    correlation,
                    field(intent, "reference"),
                )?
            }
            "create-trade" | "remove-order" | "remove-trade" => {
                let order = order.ok_or("Select an order")?;
                if intent["originalId"].as_str() != Some(text(&order["OrderId"]).as_str()) {
                    return Err("The selected order changed; reload before continuing".into());
                }
                if action == "create-trade" {
                    let primary = self
                        .data
                        .trades
                        .iter()
                        .map(|wire| model(registry, "FundOrderTradeProjectionReadModel", wire))
                        .collect::<Result<Vec<_>, _>>()?
                        .into_iter()
                        .find(|row| row["PrimaryTrade"] == true);
                    let trade_type = primary
                        .as_ref()
                        .map(|row| closing(row["TradeType"].as_str().unwrap_or_default()))
                        .unwrap_or_else(|| field(intent, "tradeType"));
                    let contract_id = primary
                        .as_ref()
                        .map(|row| text(&row["BaseContractId"]))
                        .unwrap_or_else(|| field(intent, "baseContract").into());
                    if primary.is_none() && !self.data.base_contract_choices()?.contains(&contract_id) {
                        return Err("Select a current or future base contract for this Fund; the contract list may have rolled since the dialog opened".into());
                    }
                    let known = self.data.contracts.iter().map(|wire|registry.project("TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel",wire))
                        .collect::<Result<Vec<_>,_>>()?.into_iter().find(|row|row["ContractId"]==contract_id);
                    let contract = if let Some(contract) = known { contract } else if primary.is_some() {
                        // Closing retains the original instrument even after Fund defaults or the current cycle change.
                        let wire = crate::services::actor_requests::query(backend, registry,
                            "TomasAI.IFM.Domain.MarketData.Shared.Queries.GetFuturesContractQuery", &contract_id,
                            BTreeMap::from([("ContractId".into(),contract_id.clone().into())])).await?;
                        let original = registry.project("TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel",&wire)?;
                        if original["ContractId"] != contract_id { return Err("The original closing contract lookup returned a different instrument".into()); }
                        original
                    } else { return Err("Select a current base futures contract".into()); };

                    let effective =
                        crate::backend::date_input::parse(field(intent, "effectiveDate"))?;
                    let maturity = primary
                        .as_ref()
                        .map(|row| {
                            let value = row["MaturityDate"]
                                .as_str()
                                .or_else(|| row["RequestedMaturityDate"].as_str());
                            value.map(crate::backend::date_input::parse).transpose()
                        })
                        .transpose()?
                        .flatten();
                    if primary.is_some() && maturity.is_none() && trade_type != "FuturesOutright" {
                        return Err("The opening trade has no maturity date; reload its established trade before adding a close".into());
                    }
                    manual_orders::add(
                        registry,
                        &context,
                        &order,
                        self.prepared_trade.ok_or("Open Add Trade first")?,
                        self.data.trades.is_empty(),
                        trade_type,
                        primary.as_ref().and_then(|row| row["TradeAction"].as_str()),
                        &contract,
                        effective,
                        maturity,
                        field(intent, "tradeReference"),
                        correlation,
                    )?
                } else {
                    let trade = if action == "remove-trade" {
                        let selected_trade = positive(
                            &selected(
                                registry,
                                "FundOrderTradeProjectionReadModel",
                                &self.data.trades,
                                "TradeId",
                                self.data.trade,
                            )?
                            .ok_or("Select a trade")?,
                            "TradeId",
                        )?;
                        if field(intent, "removeTradeId").parse::<i32>().ok() != Some(selected_trade) {
                            return Err("The selected trade changed; reopen Remove Trade to confirm.".into());
                        }
                        Some(selected_trade)
                    } else {
                        None
                    };
                    manual_orders::remove(
                        registry,
                        &context,
                        &order,
                        trade,
                        field(intent, "reason"),
                        correlation,
                    )?
                }
            }
            _ => return Err("Unsupported manual order action".into()),
        };
        let success_message = match operation.proof {
            manual_orders::Proof::Created { .. } => None,
            manual_orders::Proof::Added { order, trade, .. } => {
                Some(format!("Trade {trade} added to Portfolio order {order}."))
            }
            manual_orders::Proof::Changed { order, trade, .. } =>
                Some(format!("Trade {trade} updated on Portfolio order {order}.")),
            manual_orders::Proof::Removed { order, trade, .. } => Some(format!(
                "Trade {trade} removed from Portfolio order {order}."
            )),
            manual_orders::Proof::Deleted { order } => {
                Some(format!("Portfolio order {order} deleted."))
            }
        };
        self.manual_request = Some(correlation.to_string());
        self.pending_manual = Some(operation);
        // Borrow in place: a caller deadline may drop this future at any await.
        let outcome = self
            .pending_manual
            .as_mut()
            .ok_or("Missing prepared manual operation")?
            .submit(backend, registry, &context, deadline)
            .await;
        let order = match outcome {
            Ok(order) => order,
            Err(error) => {
                if !error.starts_with("OutcomeUnknown:") {
                    self.pending_manual = None;
                    self.manual_request = None;
                }
                return Err(error);
            }
        };
        self.confirmed_write = true;
        self.pending_manual = None;
        self.manual_request = None;
        self.order_draft = None;
        self.prepared_trade_change = None;
        self.data.context = Some(context);
        self.message =
            success_message.unwrap_or_else(|| format!("Manual Portfolio order {order} created."));
        self.data.load_orders(backend, registry, deadline).await?;
        if matches!(action, "create-order" | "create-trade" | "remove-trade") {
            // Continue on the newly created order, rather than leaving Add
            // Trade pointed at the previously selected opening trade.
            let selected = Some(order.into());
            if let Some(selected) = selected {
                self.data
                    .select_order(backend, registry, selected, deadline)
                    .await?;
            }
            if action == "create-trade" {
                if let Some(trade) = self.prepared_trade.filter(|id| {
                    self.data.trades.iter().any(|wire| {
                        model(registry, "FundOrderTradeProjectionReadModel", wire)
                            .is_ok_and(|row| row["TradeId"] == *id)
                    })
                }) {
                    self.data.trade = Some(trade.into());
                }
            }
        }
        Ok(self.message.clone())
    }
}
