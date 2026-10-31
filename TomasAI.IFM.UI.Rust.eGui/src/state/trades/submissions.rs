//! Confirmation and execution are separate finite actions. A human decision
//! never holds the controller lock. An uncertain write retains its exact scope,
//! bytes and identity independently of the Blotter that initiated it.
use super::*;
use crate::services::{order_submission::Operation, order_submission_store as store};
impl TradeController {
    pub(super) async fn submit_confirmed(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let channel = field(intent, "fillType");
        if !["Broker", "Manual"].contains(&channel) {
            return Err("Select Broker or Manual execution".into());
        }
        let id = uuid::Uuid::parse_str(
            intent["requestId"]
                .as_str()
                .ok_or("Missing submission request identity")?,
        )
        .map_err(|_| "Invalid submission request identity")?;
        let composer = self.composer.as_ref().ok_or("Open Trade Blotter first")?;
        let prepared = composer
            .preparation
            .as_ref()
            .ok_or("Prepare an order confirmation first")?;
        let portfolio = self
            .data
            .portfolio
            .and_then(|v| i32::try_from(v).ok())
            .ok_or("Missing Portfolio scope")?;
        let mut operation = if composer.trade.primary_trade == Some(false) {
            let primaries = self
                .data
                .trades
                .iter()
                .map(|row| model(registry, "FundOrderTradeProjectionReadModel", row))
                .collect::<Result<Vec<_>, _>>()?
                .into_iter()
                .filter(|row| row["PrimaryTrade"] == true)
                .collect::<Vec<_>>();
            if primaries.len() != 1 {
                return Err("A closing setup requires one established opening setup".into());
            }
            let primary = &primaries[0];
            let number = |key: &str| {
                primary[key]
                    .as_i64()
                    .and_then(|v| i32::try_from(v).ok())
                    .filter(|v| *v > 0)
                    .ok_or("The opening setup has no established execution identity")
            };
            let scope = crate::services::trade_monitor_queries::Scope {
                portfolio,
                fund: composer.scope.fund,
                order: number("ExecutionOrderId")?,
                trade: number("ExecutionTradeId")?,
            };
            let strategy = prepared.result["Candidate"]["StrategyKind"]
                .as_str()
                .ok_or("Missing prepared strategy")?;
            let candidate = crate::services::order_close::candidate(
                backend,
                registry,
                scope,
                strategy,
                &prepared.candidate_wire,
                deadline,
            )
            .await?;
            Operation::prepare_close(registry, context, portfolio, id, candidate)?
        } else {
            Operation::prepare(
                registry,
                context,
                portfolio,
                id,
                prepared.candidate_wire.clone(),
            )?
        };
        operation.channel = channel.into();
        // Disk failure occurs before publishing. A timeout after this point leaves a
        // durable operation that cannot be replaced by closing/reopening the view.
        store::save(&operation, 0, "Confirmed; not yet submitted").await?;
        self.submission_operations.insert(id.to_string(), operation);
        self.composer.as_mut().unwrap().confirmation_id = None;
        let operation = self.submission_operations.get_mut(&id.to_string()).unwrap();
        let result: Result<String, String> = async {
            operation.evaluate(backend, registry, deadline).await?;
            store::save(
                operation,
                1,
                "Portfolio accepted; lifecycle handoff pending",
            )
            .await?;
            if operation.accepted.is_empty() {
                store::save(operation, 2, "Portfolio returned no accepted orders").await?;
                return Ok("Portfolio declined the order".to_string());
            }
            operation
                .dispatch(backend, registry, context, channel, deadline)
                .await?;
            if channel == "Broker" {
                crate::services::order_submission_ack::wait_for_broker(
                    backend,
                    registry,
                    &operation.accepted,
                    deadline,
                )
                .await?;
            }
            operation.confirmed = true;
            store::save(operation, 2, "Submission confirmed").await?;
            Ok("Order submission confirmed".into())
        }
        .await;
        match result {
            Ok(message) => {
                let operation = self.submission_operations.get_mut(&id.to_string()).unwrap();
                operation.confirmed = true;
                if let Some(composer) = self.composer.as_mut() {
                    composer.accept_submission(operation.accepted.clone(), &message)?;
                }
                if self
                    .submission_operations
                    .get(&id.to_string())
                    .is_some_and(|operation| operation.accepted.is_empty())
                {
                    self.submission_operations.remove(&id.to_string());
                }
                self.confirmed_write = true;
                // A confirmed write remains confirmed if projection refresh is unavailable.
                // Stage the complete refreshed selection before replacing the visible one.
                if let Some(order) = self.data.order {
                    let desired = self.data.trade;
                    let mut refreshed = self.data.clone();
                    let loaded = async {
                        refreshed.load_orders(backend, registry, deadline).await?;
                        refreshed
                            .select_order(backend, registry, order, deadline)
                            .await?;
                        if desired.is_some_and(|id| {
                            refreshed.trades.iter().any(|row| {
                                model(registry, "FundOrderTradeProjectionReadModel", row)
                                    .is_ok_and(|v| v["TradeId"] == id)
                            })
                        }) {
                            refreshed.trade = desired;
                        }
                        Ok::<_, String>(())
                    }
                    .await;
                    match loaded {
                        Ok(()) => self.data = refreshed,
                        Err(error) => {
                            return Ok(format!(
                                "{message}; order projection refresh failed: {error}"
                            ));
                        }
                    }
                }
                Ok(message)
            }
            Err(error) => {
                let operation = self.submission_operations.get_mut(&id.to_string()).unwrap();
                operation.confirmed = false;
                let unknown = operation.attempted
                    && (error.starts_with("OutcomeUnknown:")
                        || !operation.accepted.is_empty()
                            && !error.starts_with("Broker rejected the accepted order:"));
                if unknown {
                    let message = format!(
                        "OutcomeUnknown: {error}; reconcile the original submission {}",
                        operation.id
                    );
                    store::save(operation, 1, &message)
                        .await
                        .map_err(|e| format!("{message}; journal update failed: {e}"))?;
                    if let Some(composer) = self.composer.as_mut() {
                        composer.mark_submission_unknown(operation.accepted.clone(), &message)?;
                    }
                    Err(message)
                } else {
                    store::save(operation, 3, &error).await?;
                    self.submission_operations.remove(&id.to_string());
                    if let Some(composer) = self.composer.as_mut() {
                        composer.reject_submission(&error);
                    }
                    Err(error)
                }
            }
        }
    }
    pub(super) fn matches_submission(&self, registry: &Registry, operation: &Operation) -> bool {
        let Ok(body) = crate::services::parameters::document(
            registry,
            &operation.command_kind(),
            &operation.body,
            true,
        ) else {
            return false;
        };
        self.data.portfolio == Some(operation.portfolio.into())
            && body["Body"]["SetupTrade"]["FundId"].as_i64() == self.data.fund
            && body["Body"]["SetupTrade"]["OrderId"].as_i64() == self.data.order
            && body["Body"]["SetupTrade"]["TradeId"].as_i64() == self.data.trade
    }
    pub(super) async fn load_submissions(&mut self, registry: &Registry) -> Result<(), String> {
        if self.submissions_loaded {
            return Ok(());
        }
        for value in store::pending().await? {
            let operation = store::restore(registry, &value)?;
            self.submission_operations
                .entry(operation.id.to_string())
                .or_insert(operation);
        }
        self.submissions_loaded = true;
        Ok(())
    }
    pub(super) async fn reconcile_submissions(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let ids = self
            .submission_operations
            .iter()
            .filter(|(_, operation)| {
                !operation.confirmed && self.matches_submission(registry, operation)
            })
            .map(|(id, _)| id.clone())
            .collect::<Vec<_>>();
        for id in ids {
            let operation = self.submission_operations.get_mut(&id).unwrap();
            operation.reconcile(backend, registry, deadline).await?;
            store::save(
                operation,
                1,
                "Portfolio result reconciled; checking lifecycle",
            )
            .await?;
            let channel = operation.channel.clone();
            operation
                .dispatch(backend, registry, context, &channel, deadline)
                .await?;
            if channel == "Broker" && !operation.accepted.is_empty() {
                crate::services::order_submission_ack::wait_for_broker(
                    backend,
                    registry,
                    &operation.accepted,
                    deadline,
                )
                .await?;
            }
            operation.confirmed = true;
            store::save(operation, 2, "Submission reconciled").await?;
            if let Some(composer) = self.composer.as_mut() {
                composer.accept_submission(operation.accepted.clone(), "Submission reconciled")?;
            }
            if let Some((_, outcome)) = self.receipts.get_mut(&id) {
                outcome.state = "Completed".into();
                outcome.message = "Original order submission reconciled".into();
                self.reconciled.push(outcome.clone());
            }
        }
        Ok(())
    }
}
