//! Closed, scope-checked established-trade reads. Historical bounds and live
//! dates come from the API; no workstation business clock or financial model.
use super::business_context::BusinessContext;
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use crate::models::{trade_contracts::*, wire_types::FromBackend};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
pub const ROOT: &str = "TomasAI.IFM.Domain.Trade.Shared";
#[derive(Clone, Copy, Debug, Eq, PartialEq, Ord, PartialOrd)]
pub struct Scope {
    pub portfolio: i32,
    pub fund: i32,
    pub order: i32,
    pub trade: i32,
}
impl Scope {
    pub fn key(self) -> Result<String, String> {
        if [self.portfolio, self.fund, self.order, self.trade]
            .iter()
            .any(|n| *n <= 0)
        {
            return Err("Invalid established trade scope".into());
        }
        Ok(format!(
            "{}.{}.{}.{}",
            self.portfolio, self.fund, self.order, self.trade
        ))
    }
    pub fn document(self) -> Json {
        json!({"PortfolioId":self.portfolio,"FundId":self.fund,"OrderId":self.order,"TradeId":self.trade})
    }
    pub fn wire(self) -> Result<Value, String> {
        self.key()?;
        Ok(Value::Array(vec![
            self.portfolio.into(),
            self.fund.into(),
            self.order.into(),
            self.trade.into(),
        ]))
    }
    pub fn position(self, strategy: &str) -> Result<(String, Json), String> {
        let purpose = match strategy {
            "IronCondor" | "VerticalSpread" => "strategy-position",
            "FuturesOutright" => "futures-position",
            _ => return Err("Unsupported established strategy".into()),
        };
        let key = self.key()?;
        let id = super::order_submission::stable(&[purpose, &key]);
        Ok((
            format!("{key}.{}", id.simple()),
            json!({"Trade":self.document(),"PositionId":id.to_string()}),
        ))
    }
    pub fn matches(self, id: &Json) -> bool {
        id == &self.document()
    }
    pub fn typed_id(self) -> TradeEntityId {
        TradeEntityId {
            portfolio_id: Some(self.portfolio),
            fund_id: Some(self.fund),
            order_id: Some(self.order),
            trade_id: Some(self.trade),
        }
    }
    pub fn typed_position(self, strategy: &str) -> Result<(String, StrategyPositionId), String> {
        let purpose = match strategy {
            "IronCondor" | "VerticalSpread" => "strategy-position",
            "FuturesOutright" => "futures-position",
            _ => return Err("Unsupported established strategy".into()),
        };
        let key = self.key()?;
        let id = super::order_submission::stable(&[purpose, &key]);
        Ok((
            format!("{key}.{}", id.simple()),
            StrategyPositionId {
                trade: Some(self.typed_id()),
                position_id: Some(id.to_string()),
            },
        ))
    }
    pub fn position_wire(self, strategy: &str) -> Result<Value, String> {
        let (_, id) = self.typed_position(strategy)?;
        Ok(Value::Array(vec![
            self.wire()?,
            id.position_id.ok_or("Missing position identity")?.into(),
        ]))
    }
}
async fn query_response(
    backend: &Backend,
    registry: &Registry,
    kind: &str,
    key: &str,
    mut fields: BTreeMap<String, Value>,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    let allowed = [
        format!("{ROOT}.Futures.Option.GetIronCondorOptionTradeQuery"),
        format!("{ROOT}.Futures.Option.GetVerticalSpreadOptionTradeQuery"),
        format!("{ROOT}.Futures.GetFuturesTradeQuery"),
        format!("{ROOT}.Futures.Option.Position.GetIronCondorOptionTradePositionHistoryQuery"),
        format!("{ROOT}.Trade.Realtime.GetTradeRealtimeSnapshotQuery"),
        format!("{ROOT}.Queries.GetTradeLimitQuery"),
        format!("{ROOT}.Trade.Position.Plan.GetCurrentIronCondorTradePlanQuery"),
        format!("{ROOT}.Trade.Position.Plan.GetIronCondorTradePlanHistoryQuery"),
        format!("{ROOT}.Trade.Position.Plan.GetIronCondorTradePlanChartQuery"),
        "TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetLastFuturesOptionTickDataQuery"
            .into(),
    ];
    if !allowed.iter().any(|value| value == kind) {
        return Err("Unsupported monitor query".into());
    }
    let route = if kind.ends_with(".GetIronCondorOptionTradePositionHistoryQuery") {
        (
            "FuturesOptionTradePositionQuery",
            "GetIronCondorOptionTradePositionHistory",
        )
    } else {
        registry.route(kind)?
    };
    let subject = crate::backend::contracts::ActorSubject::query(route.0, route.1, key);
    fields.insert("Subject".into(), subject.wire());
    fields
        .entry("EntityId".into())
        .or_insert_with(|| Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]));
    let response = backend
        .request_until(&subject, &registry.encode(kind, &fields)?, deadline, None)
        .await?;
    Ok(response)
}

async fn query<T: FromBackend>(
    backend: &Backend,
    registry: &Registry,
    kind: &str,
    key: &str,
    fields: BTreeMap<String, Value>,
    deadline: tokio::time::Instant,
) -> Result<T, String> {
    let response = query_response(backend, registry, kind, key, fields, deadline).await?;
    T::from_backend(service_result(&response)?)
}
pub async fn established(
    backend: &Backend,
    registry: &Registry,
    scope: Scope,
    strategy: &str,
    deadline: tokio::time::Instant,
) -> Result<EstablishedTradeDefinition, String> {
    let name = match strategy {
        "IronCondor" => "Futures.Option.GetIronCondorOptionTradeQuery",
        "VerticalSpread" => "Futures.Option.GetVerticalSpreadOptionTradeQuery",
        "FuturesOutright" => "Futures.GetFuturesTradeQuery",
        _ => return Err("Unsupported established strategy".into()),
    };
    let value: EstablishedTradeDefinition = query(
        backend,
        registry,
        &format!("{ROOT}.{name}"),
        &scope.key()?,
        BTreeMap::from([("TradeId".into(), scope.wire()?)]),
        deadline,
    )
    .await?;
    if value.id.as_ref() != Some(&scope.typed_id())
        || value.strategy_kind.as_ref().map(|v| v.label()).as_deref() != Some(strategy)
    {
        return Err("Established trade response crossed the selected financial identity".into());
    }
    Ok(value)
}
pub async fn positions(
    backend: &Backend,
    registry: &Registry,
    scope: Scope,
    context: &BusinessContext,
    deadline: tokio::time::Instant,
) -> Result<Vec<StrategyPositionSnapshot>, String> {
    let (key, id) = scope.typed_position("IronCondor")?;
    let rows: Vec<StrategyPositionSnapshot> = query(
        backend,
        registry,
        &format!("{ROOT}.Futures.Option.Position.GetIronCondorOptionTradePositionHistoryQuery"),
        &key,
        BTreeMap::from([
            ("PositionId".into(), scope.position_wire("IronCondor")?),
            (
                "FromUtc".into(),
                timestamp_wire(chrono::DateTime::UNIX_EPOCH),
            ),
            ("ToUtc".into(), timestamp_wire(context.as_of_utc)),
            ("PageSize".into(), 1000.into()),
            ("LoadAll".into(), true.into()),
        ]),
        deadline,
    )
    .await?;
    if rows.iter().any(|position| {
        position.id.as_ref() != Some(&id)
            || position.strategy_kind != Some(TradeStrategyKind::IronCondor)
    }) {
        return Err("Position history crossed its requested strategy scope".into());
    }
    Ok(rows)
}
pub async fn realtime(
    backend: &Backend,
    registry: &Registry,
    scope: Scope,
    date: chrono::NaiveDate,
    deadline: tokio::time::Instant,
) -> Result<Option<TradeRealtimeSnapshot>, String> {
    let result: Option<TradeRealtimeSnapshot> = query(
        backend,
        registry,
        &format!("{ROOT}.Trade.Realtime.GetTradeRealtimeSnapshotQuery"),
        &scope.key()?,
        BTreeMap::from([
            ("TradeId".into(), scope.wire()?),
            ("ValueDate".into(), date_only_wire(date)),
        ]),
        deadline,
    )
    .await?;
    if result.as_ref().is_some_and(|value| {
        value.trade_id.as_ref() != Some(&scope.typed_id())
            || value.value_date.as_ref().map(|v| v.as_str()) != Some(date.to_string().as_str())
    }) {
        return Err("Realtime snapshot crossed its selected trade or API ValueDate".into());
    }
    Ok(result)
}
fn plan_scope(
    scope: Scope,
    date: chrono::NaiveDate,
) -> Result<(String, Value, StrategyPositionId), String> {
    let (key, id) = scope.typed_position("IronCondor")?;
    Ok((
        format!("{key}.{}.IronCondor", date.format("%Y%m%d")),
        Value::Array(vec![
            scope.position_wire("IronCondor")?,
            date_only_wire(date),
        ]),
        id,
    ))
}
pub async fn current_plan(
    backend: &Backend,
    registry: &Registry,
    scope: Scope,
    date: chrono::NaiveDate,
    deadline: tokio::time::Instant,
) -> Result<Option<StrategyTradePlanSnapshot>, String> {
    let (key, plan, id) = plan_scope(scope, date)?;
    let result: Option<StrategyTradePlanSnapshot> = query(
        backend,
        registry,
        &format!("{ROOT}.Trade.Position.Plan.GetCurrentIronCondorTradePlanQuery"),
        &key,
        BTreeMap::from([("PlanId".into(), plan)]),
        deadline,
    )
    .await?;
    if result.as_ref().is_some_and(|value| {
        value.position.as_ref().and_then(|p| p.id.as_ref()) != Some(&id)
            || value.value_date.as_ref().map(|v| v.as_str()) != Some(date.to_string().as_str())
    }) {
        return Err("Trade Plan crossed the requested trade/date".into());
    }
    Ok(result)
}
pub async fn plan_page(
    backend: &Backend,
    registry: &Registry,
    scope: Scope,
    date: chrono::NaiveDate,
    offset: i32,
    size: i32,
    maximum: Option<i64>,
    deadline: tokio::time::Instant,
) -> Result<StrategyTradePlanHistoryPage, String> {
    if offset < 0 || !(1..=100).contains(&size) || maximum.is_some_and(|n| n < 0) {
        return Err("Invalid Trade Plan page".into());
    }
    let (key, plan, id) = plan_scope(scope, date)?;
    let result: StrategyTradePlanHistoryPage = query(
        backend,
        registry,
        &format!("{ROOT}.Trade.Position.Plan.GetIronCondorTradePlanHistoryQuery"),
        &key,
        BTreeMap::from([
            ("PlanId".into(), plan),
            ("Offset".into(), offset.into()),
            ("PageSize".into(), size.into()),
            (
                "MaximumRevision".into(),
                maximum.map(Value::from).unwrap_or(Value::Nil),
            ),
        ]),
        deadline,
    )
    .await?;
    let rows = result
        .items
        .as_ref()
        .filter(|rows| rows.len() <= size as usize)
        .ok_or("Invalid Trade Plan history page")?;
    if rows.iter().any(|row| {
        row.position.as_ref().and_then(|p| p.id.as_ref()) != Some(&id)
            || row.value_date.as_ref().map(|v| v.as_str()) != Some(date.to_string().as_str())
    }) || maximum.is_some_and(|n| result.maximum_revision != Some(n))
        || result.offset != Some(offset)
    {
        return Err("Trade Plan page crossed its requested scope or revision cut".into());
    }
    Ok(result)
}
pub async fn chart(
    backend: &Backend,
    registry: &Registry,
    scope: Scope,
    date: chrono::NaiveDate,
    deadline: tokio::time::Instant,
) -> Result<crate::models::trade_chart::TradeChartSeries, String> {
    let (key, plan, _) = plan_scope(scope, date)?;
    let result = query_response(
        backend,
        registry,
        &format!("{ROOT}.Trade.Position.Plan.GetIronCondorTradePlanChartQuery"),
        &key,
        BTreeMap::from([("PlanId".into(), plan)]),
        deadline,
    )
    .await?;
    crate::models::trade_chart::TradeChartSeries::from_backend(service_result(&result)?)
}
pub async fn limits(
    backend: &Backend,
    registry: &Registry,
    scope: Scope,
    deadline: tokio::time::Instant,
) -> Result<TradeLimitReadModel, String> {
    let result: TradeLimitReadModel = query(
        backend,
        registry,
        &format!("{ROOT}.Queries.GetTradeLimitQuery"),
        &scope.trade.to_string(),
        BTreeMap::from([
            ("TradeId".into(), scope.trade.into()),
            ("EntityId".into(), Value::Nil),
        ]),
        deadline,
    )
    .await?;
    if result.trade_id != Some(scope.trade) {
        return Err("Persisted trade limits crossed the selected trade".into());
    }
    Ok(result)
}
pub async fn saved_leg(
    backend: &Backend,
    registry: &Registry,
    contract: &str,
    date: chrono::NaiveDate,
    deadline: tokio::time::Instant,
) -> Result<Option<FuturesOptionTickDataV2ReadModel>, String> {
    if contract.trim().is_empty() {
        return Err("Missing saved option contract".into());
    }
    let result: Option<FuturesOptionTickDataV2ReadModel> = query(
        backend,
        registry,
        "TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetLastFuturesOptionTickDataQuery",
        &format!("{contract}.{date}"),
        BTreeMap::from([
            ("ContractId".into(), contract.into()),
            ("ValueDate".into(), date_only_wire(date)),
            ("EntityId".into(), Value::Nil),
        ]),
        deadline,
    )
    .await?;
    if result.as_ref().is_some_and(|value| {
        value.contract_id.as_deref() != Some(contract)
            || value.value_date.as_ref().map(|v| v.as_str()) != Some(date.to_string().as_str())
    }) {
        return Err("Saved option observation crossed the requested contract/date".into());
    }
    Ok(result)
}
