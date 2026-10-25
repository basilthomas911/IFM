//! Canonical trade-workspace reads. Selection identities and date partitions
//! come from API models; display cells never become command authority.
use crate::{
    backend::{schema::Registry, transport::Backend},
    services::{business_context::BusinessContext, portfolio as api},
};
use rmpv::Value;
use std::collections::{BTreeMap, BTreeSet};
pub fn kind(name: &str) -> String {
    format!("{}.ViewModels.{name}", api::ROOT)
}
/// Read a bounded complete collection without accepting a repeated cursor or
/// a duplicate business identity across pages. One deadline covers every page.
pub async fn page(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    name: &str,
    key: &str,
    mut fields: BTreeMap<String, Value>,
    deadline: tokio::time::Instant,
) -> Result<Vec<Value>, String> {
    let (item, identity) = match name {
        "GetPortfoliosQuery" => ("PortfolioReadModel", "PortfolioId"),
        "GetFundsQuery" => ("FundMandateReadModel", ""),
        "GetFundOrdersPageQuery" => ("FundOrderProjectionReadModel", "OrderId"),
        "GetFundOrderTradesPageQuery" => ("FundOrderTradeProjectionReadModel", "TradeId"),
        _ => return Err("Unsupported trade workspace collection".into()),
    };
    fields.insert("PageSize".into(), 200.into());
    fields.insert("PageToken".into(), Value::Nil);
    let mut result = vec![];
    let mut cursors = BTreeSet::new();
    let mut identities = BTreeSet::new();
    for _ in 0..100 {
        let wire = api::query(
            backend,
            registry,
            context,
            name,
            key,
            fields.clone(),
            deadline,
        )
        .await?;
        let page = registry.project(
            &format!("{}.ServiceApi.PortfolioPage`1[{}]", api::ROOT, kind(item)),
            &wire,
        )?;
        if page["PageSize"] != 200 {
            return Err("Trade workspace page size differs from the request".into());
        }
        let rows = wire
            .as_array()
            .and_then(|v| v.first())
            .and_then(Value::as_array)
            .filter(|v| v.len() <= 200)
            .ok_or("Invalid trade workspace page")?;
        for row in rows {
            let projected = registry.project(&kind(item), row)?;
            let id = if identity.is_empty() {
                format!(
                    "{}@{}",
                    projected["FundId"], projected["FundMandateVersion"]
                )
            } else {
                projected[identity]
                    .as_i64()
                    .filter(|v| *v > 0)
                    .ok_or("Invalid trade workspace identity")?
                    .to_string()
            };
            if !identities.insert(id) {
                return Err("Duplicate trade workspace identity across pages".into());
            }
            if fields
                .get("PortfolioId")
                .and_then(Value::as_i64)
                .is_some_and(|id| projected["PortfolioId"] != id)
                || fields
                    .get("FundId")
                    .and_then(Value::as_i64)
                    .is_some_and(|id| projected["FundId"] != id)
                || fields
                    .get("OrderId")
                    .and_then(Value::as_i64)
                    .is_some_and(|id| projected["OrderId"] != id)
            {
                return Err("Trade workspace page crossed the requested scope".into());
            }
            result.push(row.clone());
        }
        match page["NextPageToken"].as_str().filter(|v| !v.is_empty()) {
            None => return Ok(result),
            Some(cursor) if cursor.len() <= 2048 && cursors.insert(cursor.to_string()) => {
                fields.insert("PageToken".into(), cursor.into());
            }
            _ => return Err("Trade workspace cursor repeated or exceeded its bound".into()),
        }
    }
    Err("Trade workspace collection exceeded 100 pages".into())
}
