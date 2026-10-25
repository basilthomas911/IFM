//! Dispatch acknowledgements are not broker acceptance. Observe every exact
//! accepted component, and retain uncertainty if the caller budget expires.
use super::broker_reads::{self, Scope};
use crate::backend::{schema::Registry, transport::Backend};
use serde_json::Value as Json;
pub async fn wait_for_broker(
    backend: &Backend,
    registry: &Registry,
    orders: &[Json],
    deadline: tokio::time::Instant,
) -> Result<(), String> {
    if orders.is_empty() {
        return Err("No accepted orders to acknowledge".into());
    }
    tokio::time::timeout_at(deadline, async {
        loop {
            let mut all = true;
            for order in orders {
                let number = |key: &str| {
                    order["Id"][key]
                        .as_i64()
                        .and_then(|n| i32::try_from(n).ok())
                        .filter(|n| *n > 0)
                        .ok_or("Invalid accepted order scope")
                };
                let scope = Scope {
                    portfolio: number("PortfolioId")?,
                    fund: number("FundId")?,
                    order: number("OrderId")?,
                };
                let evidence = broker_reads::orders(backend, registry, scope, deadline)
                    .await
                    .map_err(|e| {
                        format!("OutcomeUnknown: Broker acknowledgement unavailable: {e}")
                    })?;
                if let Some(rejected) = evidence.iter().find(|row| {
                    row["Id"]["Execution"]["TradeOrder"] == order["Id"]
                        && order["Components"].as_array().is_some_and(|components| {
                            components
                                .iter()
                                .any(|c| c["ComponentId"] == row["Id"]["ComponentId"])
                        })
                        && ["Rejected", "Cancelled"]
                            .contains(&row["Status"].as_str().unwrap_or_default())
                }) {
                    return Err(format!(
                        "Broker rejected the accepted order: {}: {}",
                        rejected["DispatchCategory"].as_str().unwrap_or_default(),
                        rejected["DispatchDetail"].as_str().unwrap_or_default()
                    ));
                }
                let components = order["Components"]
                    .as_array()
                    .filter(|c| !c.is_empty())
                    .ok_or("Accepted order has no components")?;
                all &= components.iter().all(|component| {
                    evidence.iter().any(|row| {
                        row["Id"]["Execution"]["TradeOrder"] == order["Id"]
                            && row["Id"]["ComponentId"] == component["ComponentId"]
                            && ["Working", "PartiallyFilled", "Filled"]
                                .contains(&row["Status"].as_str().unwrap_or_default())
                    })
                });
            }
            if all {
                return Ok(());
            }
            tokio::time::sleep(std::time::Duration::from_millis(200)).await;
        }
    })
    .await
    .map_err(|_| {
        "OutcomeUnknown: Broker acceptance deadline expired; reconcile this original order"
            .to_string()
    })?
}
