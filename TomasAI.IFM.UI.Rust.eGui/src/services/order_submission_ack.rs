//! Dispatch acknowledgements are not broker acceptance. Observe every exact
//! accepted component, and retain uncertainty if the caller budget expires.
use super::broker_reads::{self, Scope};
use crate::{
    backend::{schema::Registry, transport::Backend},
    models::trade_contracts::{BrokerOrderStatus, TradeOrderDefinition},
};
use futures_util::StreamExt;
use serde_json::Value as Json;

const REJECTION_PREFIX: &str = "Broker rejected the accepted order:";

/// Distinguishes a terminal broker decision from an unavailable acknowledgement.
pub fn is_rejection(error: &str) -> bool {
    error.starts_with(REJECTION_PREFIX)
}
/// Waits for for broker for order submission ack.
pub async fn wait_for_broker(
    backend: &Backend,
    registry: &Registry,
    orders: &[Json],
    deadline: tokio::time::Instant,
) -> Result<(), String> {
    if orders.is_empty() {
        return Err("No accepted orders to acknowledge".into());
    }
    // Subscribe before the first read so an acknowledgement committed during that read
    // is still observed.
    let orders = orders
        .iter()
        .cloned()
        .map(serde_json::from_value::<TradeOrderDefinition>)
        .collect::<Result<Vec<_>, _>>()
        .map_err(|e| format!("Invalid accepted order receipt: {e}"))?;
    tokio::time::timeout_at(deadline, async {
        let mut notices = backend.subscribe("Event", "BrokerOrderEvent").await?;
        let mut connection = backend.watch_connection();
        loop {
            let mut all = true;
            for order in &orders {
                let id = order.id.as_ref().ok_or("Invalid accepted order scope")?;
                let valid =
                    |id: Option<i32>| id.filter(|n| *n > 0).ok_or("Invalid accepted order scope");
                let scope = Scope {
                    portfolio: valid(id.portfolio_id)?,
                    fund: valid(id.fund_id)?,
                    order: valid(id.order_id)?,
                };
                let components = order
                    .components
                    .as_ref()
                    .filter(|c| !c.is_empty())
                    .ok_or("Accepted order has no components")?;
                let evidence = broker_reads::orders(backend, registry, scope, deadline)
                    .await
                    .map_err(|e| {
                        format!("OutcomeUnknown: Broker acknowledgement unavailable: {e}")
                    })?;
                let matching = |row: &crate::models::trade_contracts::BrokerOrderDefinition| {
                    row.id
                        .as_ref()
                        .and_then(|id| id.execution.as_ref())
                        .and_then(|id| id.trade_order.as_ref())
                        == Some(id)
                };
                if let Some(rejected) = evidence.iter().find(|row| {
                    matching(row)
                        && components.iter().any(|c| {
                            c.component_id.as_ref()
                                == row.id.as_ref().and_then(|id| id.component_id.as_ref())
                        })
                        && matches!(
                            row.status,
                            Some(BrokerOrderStatus::Rejected | BrokerOrderStatus::Cancelled)
                        )
                }) {
                    return Err(format!(
                        "{REJECTION_PREFIX} {}: {}",
                        rejected.dispatch_category.as_deref().unwrap_or_default(),
                        rejected.dispatch_detail.as_deref().unwrap_or_default()
                    ));
                }
                all &= components.iter().all(|component| {
                    evidence.iter().any(|row| {
                        matching(row)
                            && row.id.as_ref().and_then(|id| id.component_id.as_ref())
                                == component.component_id.as_ref()
                            && matches!(
                                row.status,
                                Some(
                                    BrokerOrderStatus::Working
                                        | BrokerOrderStatus::PartiallyFilled
                                        | BrokerOrderStatus::Filled
                                )
                            )
                    })
                });
            }
            if all {
                return Ok(());
            }
            loop {
                let refresh = tokio::select! {
                    notice = notices.next() => {
                        let notice = notice.ok_or("OutcomeUnknown: Broker notifications are unavailable")?;
                        orders.iter().any(|order| order.id.as_ref().is_some_and(|id| {
                            let prefix = format!("Event.BrokerOrderEvent.BrokerOrderChanged.{}.{}.{}.",
                                id.portfolio_id.unwrap_or_default(), id.fund_id.unwrap_or_default(), id.order_id.unwrap_or_default());
                            notice.subject.starts_with(&prefix)
                        }))
                    }
                    changed = connection.changed() => {
                        changed.map_err(|_| "OutcomeUnknown: Broker connection closed")?;
                        connection.borrow_and_update().connected
                    }
                };
                if refresh { break; }
            }
        }
    })
    .await
    .map_err(|_| {
        "OutcomeUnknown: Broker acceptance deadline expired; reconcile this original order"
            .to_string()
    })?
}
