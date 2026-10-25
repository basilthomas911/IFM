//! Atomic trade-workspace selection. A failed read leaves the prior complete
//! Portfolio/Fund/order selection intact rather than mixing scope revisions.
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    services::{
        actor_requests, business_context::BusinessContext, portfolio as portfolio_api,
        trade_orders as api,
    },
};
use chrono::{Datelike, Months, NaiveDate};
use rmpv::Value;
use std::collections::BTreeMap;
#[derive(Clone, Default)]
pub struct TradeData {
    pub context: Option<BusinessContext>,
    pub from: Option<NaiveDate>,
    pub through: Option<NaiveDate>,
    pub contracts: Vec<Value>,
    pub portfolios: Vec<Value>,
    pub funds: Vec<Value>,
    pub orders: Vec<Value>,
    pub trades: Vec<Value>,
    pub portfolio: Option<i64>,
    pub fund: Option<i64>,
    pub order: Option<i64>,
    pub trade: Option<i64>,
}
fn id(registry: &Registry, item: &str, wire: &Value, name: &str) -> Result<i64, String> {
    registry.project(&api::kind(item), wire)?[name]
        .as_i64()
        .filter(|v| *v > 0)
        .ok_or(format!("Missing {name}"))
}
impl TradeData {
    pub async fn load(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let mut next = self.clone();
        let context = BusinessContext::load(backend).await?;
        if next.from.is_none() {
            let month =
                NaiveDate::from_ymd_opt(context.value_date.year(), context.value_date.month(), 1)
                    .ok_or("Invalid API month")?;
            next.from = Some(month);
            next.through = Some(
                month
                    .checked_add_months(Months::new(1))
                    .and_then(|v| v.pred_opt())
                    .ok_or("Trade range exceeds the backend calendar")?,
            );
        }
        let contract_rows = actor_requests::query(
            backend,
            registry,
            "TomasAI.IFM.Domain.MarketData.Shared.Queries.GetFuturesContractsQuery",
            "none",
            BTreeMap::new(),
        )
        .await?;
        next.contracts = contract_rows
            .as_array()
            .filter(|v| v.len() <= 100_000)
            .cloned()
            .ok_or("Invalid futures contract list")?;
        next.portfolios = api::page(
            backend,
            registry,
            &context,
            "GetPortfoliosQuery",
            "page",
            BTreeMap::from([(
                "State".into(),
                registry
                    .enum_value(
                        &format!("{}.Contracts.PortfolioOperatingState", portfolio_api::ROOT),
                        "Active",
                    )?
                    .into(),
            )]),
            deadline,
        )
        .await?;
        let mut selected = None;
        for row in &next.portfolios {
            let projected = registry.project(&api::kind("PortfolioReadModel"), row)?;
            if projected["OperatingState"] != "Active" {
                return Err("Trade workspace received an inactive Portfolio".into());
            }
            let value = id(registry, "PortfolioReadModel", row, "PortfolioId")?;
            if selected.is_none() || Some(value) == self.portfolio {
                selected = Some(value);
            }
            if Some(value) == self.portfolio {
                break;
            }
        }
        next.context = Some(context);
        if let Some(selected) = selected {
            next.select_portfolio(backend, registry, selected, deadline)
                .await?;
        } else {
            next.portfolio = None;
            next.fund = None;
            next.order = None;
            next.trade = None;
            next.funds.clear();
            next.orders.clear();
            next.trades.clear();
        }
        if let Some(order) = next.order {
            next.select_order(backend, registry, order, deadline)
                .await?;
        }
        *self = next;
        Ok(())
    }
    pub async fn select_portfolio(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        portfolio: i64,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        if !self
            .portfolios
            .iter()
            .any(|row| id(registry, "PortfolioReadModel", row, "PortfolioId") == Ok(portfolio))
        {
            return Err("Select a current Portfolio".into());
        }
        let context = self.context.as_ref().ok_or("Load API time first")?;
        let rows = api::page(
            backend,
            registry,
            context,
            "GetFundsQuery",
            &portfolio.to_string(),
            BTreeMap::from([
                ("PortfolioId".into(), portfolio.into()),
                ("State".into(), Value::Nil),
            ]),
            deadline,
        )
        .await?;
        let mut latest = BTreeMap::<i64, (i64, Value)>::new();
        for row in rows {
            let projected = registry.project(&api::kind("FundMandateReadModel"), &row)?;
            let fund = id(registry, "FundMandateReadModel", &row, "FundId")?;
            let version = projected["FundMandateVersion"]
                .as_i64()
                .filter(|v| *v > 0)
                .ok_or("Invalid Fund mandate version")?;
            if latest.get(&fund).is_none_or(|(old, _)| version > *old) {
                latest.insert(fund, (version, row));
            }
        }
        let mut next = self.clone();
        next.funds = latest
            .into_values()
            .filter_map(|(_, row)| {
                match registry.project(&api::kind("FundMandateReadModel"), &row) {
                    Ok(v) if v["OperatingState"] == "Active" => Some(Ok(row)),
                    Ok(_) => None,
                    Err(e) => Some(Err(e)),
                }
            })
            .collect::<Result<Vec<_>, _>>()?;
        next.portfolio = Some(portfolio);
        let selected = next
            .funds
            .iter()
            .find(|row| id(registry, "FundMandateReadModel", row, "FundId").ok() == self.fund)
            .or(next.funds.first())
            .map(|row| id(registry, "FundMandateReadModel", row, "FundId"))
            .transpose()?;
        if let Some(fund) = selected {
            next.select_fund(backend, registry, fund, deadline).await?;
        } else {
            next.fund = None;
            next.order = None;
            next.trade = None;
            next.orders.clear();
            next.trades.clear();
        }
        *self = next;
        Ok(())
    }
    pub async fn select_fund(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        fund: i64,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        if !self
            .funds
            .iter()
            .any(|row| id(registry, "FundMandateReadModel", row, "FundId") == Ok(fund))
        {
            return Err("Select a current Active Fund".into());
        }
        let mut next = self.clone();
        next.fund = Some(fund);
        next.load_orders(backend, registry, deadline).await?;
        *self = next;
        Ok(())
    }
    pub async fn load_orders(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let context = self.context.as_ref().ok_or("Load API time first")?;
        let portfolio = self.portfolio.ok_or("Select a Portfolio")?;
        let fund = self.fund.ok_or("Select a Fund")?;
        let from = self
            .from
            .ok_or("Load the API date range")?
            .checked_sub_months(Months::new(1))
            .ok_or("Trade range starts before the backend calendar")?;
        let through = self.through.ok_or("Load the API date range")?;
        if self.from.is_none_or(|from| from > through) {
            return Err("Trade range ends before it starts".into());
        }
        let mut month =
            NaiveDate::from_ymd_opt(from.year(), from.month(), 1).ok_or("Invalid trade range")?;
        let mut rows = vec![];
        let mut months = 0;
        while month <= through {
            months += 1;
            if months > 120 {
                return Err("Select a trade range of at most 120 months".into());
            }
            rows.extend(
                api::page(
                    backend,
                    registry,
                    context,
                    "GetFundOrdersPageQuery",
                    &format!("{portfolio}.{fund}"),
                    BTreeMap::from([
                        ("PortfolioId".into(), portfolio.into()),
                        ("FundId".into(), fund.into()),
                        ("OrderMonth".into(), date_only_wire(month)),
                    ]),
                    deadline,
                )
                .await?,
            );
            month = month
                .checked_add_months(Months::new(1))
                .ok_or("Trade range exceeds the backend calendar")?;
        }
        let mut keyed = vec![];
        let mut identities = std::collections::BTreeSet::new();
        for row in rows {
            let value = registry.project(&api::kind("FundOrderProjectionReadModel"), &row)?;
            let at = chrono::DateTime::parse_from_rfc3339(
                value["CreatedOnUtc"]
                    .as_str()
                    .ok_or("Missing order timestamp")?,
            )
            .map_err(|_| "Invalid order timestamp")?;
            let identity = id(registry, "FundOrderProjectionReadModel", &row, "OrderId")?;
            if !identities.insert(identity) {
                return Err("Duplicate order identity across month partitions".into());
            }
            keyed.push((at, identity, row));
        }
        keyed.sort_by(|a, b| b.0.cmp(&a.0).then(b.1.cmp(&a.1)));
        let selected = keyed
            .iter()
            .find(|(_, id, _)| Some(*id) == self.order)
            .or(keyed.first())
            .map(|(_, id, _)| *id);
        self.orders = keyed.into_iter().map(|(_, _, row)| row).collect();
        self.order = selected;
        self.trade = None;
        self.trades.clear();
        Ok(())
    }
    pub async fn select_order(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        order: i64,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        if !self
            .orders
            .iter()
            .any(|row| id(registry, "FundOrderProjectionReadModel", row, "OrderId") == Ok(order))
        {
            return Err("Select a current Fund order".into());
        }
        let context = self.context.as_ref().ok_or("Load API time first")?;
        let rows = api::page(
            backend,
            registry,
            context,
            "GetFundOrderTradesPageQuery",
            &order.to_string(),
            BTreeMap::from([("OrderId".into(), order.into())]),
            deadline,
        )
        .await?;
        for row in &rows {
            let projected =
                registry.project(&api::kind("FundOrderTradeProjectionReadModel"), row)?;
            if projected["PortfolioId"] != self.portfolio.unwrap_or(0)
                || projected["FundId"] != self.fund.unwrap_or(0)
            {
                return Err("Order trades crossed the selected Fund".into());
            }
        }
        let selected = rows
            .first()
            .map(|row| {
                id(
                    registry,
                    "FundOrderTradeProjectionReadModel",
                    row,
                    "TradeId",
                )
            })
            .transpose()?;
        self.trades = rows;
        self.order = Some(order);
        self.trade = selected;
        Ok(())
    }
}
