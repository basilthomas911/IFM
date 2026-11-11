//! Fund-scoped contract reads. New entry never queries the global futures catalog.
use crate::backend::{schema::Registry, transport::Backend};
use crate::services::{business_context::BusinessContext, portfolio as api};
use rmpv::Value;
use std::collections::BTreeMap;

/// Reads defaults and Databento-qualified current/forward contracts for one projected Fund.
pub async fn load(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    portfolio: i64,
    fund: i64,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    api::query(
        backend,
        registry,
        context,
        "GetFundBaseContractsQuery",
        &format!("{portfolio}.{fund}"),
        BTreeMap::from([
            ("PortfolioId".into(), portfolio.into()),
            ("FundId".into(), fund.into()),
            ("Version".into(), Value::Nil),
        ]),
        deadline,
    )
    .await
}
