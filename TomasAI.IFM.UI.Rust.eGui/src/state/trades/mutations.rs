//! Retained writes stay in their owner across awaits. Cancelling a read or
//! closing an editor cannot discard an uncertain command identity or payload.
use super::*;
use crate::services::{actor_requests, manual_orders};
fn positive(row: &Json, key: &str) -> Result<i32, String> {
    row[key]
        .as_i64()
        .and_then(|v| i32::try_from(v).ok())
        .filter(|v| *v > 0)
        .ok_or(format!("Missing {key}"))
}
impl TradeController {
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
            self.order_draft = Some(json!({"orderId":0,"orderDate":display,"orderStatus":"Open"}));
            self.data.context = Some(context);
            return Ok("New order editor opened".into());
        }
        let order = selected(
            registry,
            "FundOrderProjectionReadModel",
            &self.data.orders,
            "OrderId",
            self.data.order,
        )?;
        if action == "prepare-trade" {
            if order.is_none() {
                return Err("Select an order before adding a trade".into());
            }
            // This legacy query reconstructs its entity from SeedType on the
            // backend. Allocation is never automatically retried after failure.
            let wire = actor_requests::query(
                backend,
                registry,
                "TomasAI.IFM.Domain.Reference.Shared.Queries.GetNextSeedIdQuery",
                "TradeId",
                BTreeMap::from([
                    ("EntityId".into(), Value::Nil),
                    ("SeedType".into(), "TradeId".into()),
                ]),
            )
            .await
            .map_err(|error| {
                format!("Trade identity allocation failed and was not retried: {error}")
            })?;
            self.prepared_trade = Some(
                wire.as_array()
                    .and_then(|v| v.first())
                    .and_then(Value::as_i64)
                    .and_then(|v| i32::try_from(v).ok())
                    .filter(|v| *v > 0)
                    .ok_or("Invalid allocated trade identity")?,
            );
            self.data.context = Some(context);
            return Ok("Trade identity allocated".into());
        }
        let operation = match action {
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
                    let contract=self.data.contracts.iter().map(|wire|registry.project("TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel",wire)).collect::<Result<Vec<_>,_>>()?.into_iter().find(|row|row["ContractId"]==contract_id).ok_or("Select a current base futures contract")?;
                    let effective =
                        crate::backend::date_input::parse(field(intent, "effectiveDate"))?;
                    manual_orders::add(
                        registry,
                        &context,
                        &order,
                        self.prepared_trade.ok_or("Open Add Trade first")?,
                        self.data.trades.is_empty(),
                        trade_type,
                        &contract,
                        effective,
                        correlation,
                    )?
                } else {
                    let trade = if action == "remove-trade" {
                        Some(positive(
                            &selected(
                                registry,
                                "FundOrderTradeProjectionReadModel",
                                &self.data.trades,
                                "TradeId",
                                self.data.trade,
                            )?
                            .ok_or("Select a trade")?,
                            "TradeId",
                        )?)
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
        self.data.context = Some(context);
        self.message =
            success_message.unwrap_or_else(|| format!("Manual Portfolio order {order} created."));
        self.data.load_orders(backend, registry, deadline).await?;
        if matches!(action, "create-order" | "create-trade") {
            let selected = if action == "create-order" {
                self.data.order
            } else {
                Some(order.into())
            };
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
