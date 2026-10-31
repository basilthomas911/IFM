//! Concrete broker/account evidence. Failed refreshes retain the last complete cut.
//! Display captions never supply account or execution identities.
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    models::{trade_contracts::*, wire_types::FromBackend},
};
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::{BTreeMap, BTreeSet};
const TRADE: &str = "TomasAI.IFM.Domain.Trade.Shared";
const ACCOUNT: &str = "TomasAI.IFM.Domain.BrokerAccount.Contracts";
#[derive(Clone, Copy, Debug, Eq, PartialEq, Ord, PartialOrd)]
pub struct Scope {
    pub portfolio: i32,
    pub fund: i32,
    pub order: i32,
}
impl Scope {
    pub fn key(self) -> Result<String, String> {
        if self.portfolio <= 0 || self.fund <= 0 || self.order <= 0 {
            return Err("Invalid broker Portfolio/Fund/order scope".into());
        }
        Ok(format!("{}.{}.{}", self.portfolio, self.fund, self.order))
    }
    pub fn wire(self) -> Result<Value, String> {
        self.key()?;
        Ok(Value::Array(vec![
            self.portfolio.into(),
            self.fund.into(),
            self.order.into(),
        ]))
    }
    /// Existing canonical request boundary; native evidence uses matches_id.
    pub fn matches(self, row: &Json) -> bool {
        row["PortfolioId"] == self.portfolio
            && row["FundId"] == self.fund
            && row["OrderId"] == self.order
    }
    pub fn matches_id(self, id: &TradeOrderId) -> bool {
        id.portfolio_id == Some(self.portfolio)
            && id.fund_id == Some(self.fund)
            && id.order_id == Some(self.order)
    }
}
async fn query(
    backend: &Backend,
    registry: &Registry,
    kind: &str,
    key: &str,
    mut fields: BTreeMap<String, Value>,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    if ![
        format!("{ACCOUNT}.GetBrokerAccountQuery"),
        format!("{TRADE}.Order.Broker.GetBrokerOrdersForTradeOrderQuery"),
        format!("{TRADE}.Order.Execution.GetOrderExecutionQuery"),
    ]
    .iter()
    .any(|allowed| allowed == kind)
    {
        return Err("Unsupported broker evidence query".into());
    }
    let (actor, verb) = registry.route(kind)?;
    let subject = crate::backend::contracts::ActorSubject::query(actor, verb, key);
    fields.insert("Subject".into(), subject.wire());
    fields.insert(
        "EntityId".into(),
        Value::Array(vec![0.into(), Value::Array(vec![key.into()])]),
    );
    let reply = backend
        .request_until(&subject, &registry.encode(kind, &fields)?, deadline, None)
        .await?;
    into_service_result(reply)
}
pub async fn account(
    backend: &Backend,
    registry: &Registry,
    alias: &str,
    deadline: tokio::time::Instant,
) -> Result<Option<BrokerAccountDefinition>, String> {
    if alias.trim().is_empty() || alias.len() > 256 {
        return Err("Invalid broker account alias".into());
    }
    let id = registry.encode(
        &format!("{ACCOUNT}.BrokerAccountId"),
        &BTreeMap::from([("AccountAlias".into(), alias.into())]),
    )?;
    let value = query(
        backend,
        registry,
        &format!("{ACCOUNT}.GetBrokerAccountQuery"),
        alias,
        BTreeMap::from([("BrokerAccountId".into(), id)]),
        deadline,
    )
    .await?;
    if value.is_nil() {
        return Ok(None);
    }
    let result = BrokerAccountDefinition::from_backend(&value)?;
    if result
        .id
        .as_ref()
        .and_then(|id| id.account_alias.as_deref())
        != Some(alias)
    {
        return Err("Broker account response crossed the selected alias".into());
    }
    Ok(Some(result))
}
pub async fn orders(
    backend: &Backend,
    registry: &Registry,
    scope: Scope,
    deadline: tokio::time::Instant,
) -> Result<Vec<BrokerOrderDefinition>, String> {
    let value = query(
        backend,
        registry,
        &format!("{TRADE}.Order.Broker.GetBrokerOrdersForTradeOrderQuery"),
        &scope.key()?,
        BTreeMap::from([("TradeOrderId".into(), scope.wire()?)]),
        deadline,
    )
    .await?;
    if value.as_array().is_none_or(|rows| rows.len() > 10000) {
        return Err("Invalid broker-order collection".into());
    }
    let rows = Vec::<BrokerOrderDefinition>::from_backend(&value)?;
    let mut ids = BTreeSet::new();
    for row in &rows {
        if row
            .id
            .as_ref()
            .and_then(|id| id.execution.as_ref())
            .and_then(|id| id.trade_order.as_ref())
            .is_none_or(|id| !scope.matches_id(id))
        {
            return Err("Broker order crossed the requested order scope".into());
        }
        let key = typed_identity(row.id.as_ref().ok_or("Missing broker-order identity")?)?;
        if !ids.insert(key) {
            return Err("Duplicate broker-order identity".into());
        }
    }
    Ok(rows)
}
fn required_guid(value: Option<&str>) -> Result<uuid::Uuid, String> {
    value
        .and_then(|value| uuid::Uuid::parse_str(value).ok())
        .filter(|value| !value.is_nil())
        .ok_or("Invalid execution identity".into())
}
pub fn typed_identity(id: &BrokerOrderId) -> Result<String, String> {
    let execution = id.execution.as_ref().ok_or("Missing execution identity")?;
    let scope = execution
        .trade_order
        .as_ref()
        .ok_or("Missing broker-order scope")?;
    let scope = Scope {
        portfolio: scope.portfolio_id.ok_or("Invalid broker-order scope")?,
        fund: scope.fund_id.ok_or("Invalid broker-order scope")?,
        order: scope.order_id.ok_or("Invalid broker-order scope")?,
    };
    let attempt = required_guid(execution.execution_attempt_id.as_deref())?;
    let component = required_guid(id.component_id.as_deref())?;
    Ok(format!(
        "{}.{}.{}",
        scope.key()?,
        attempt.simple(),
        component.simple()
    ))
}
// Canonical JSON identities are retained only for existing protocol adapters.
pub fn identity(id: &Json) -> Result<String, String> {
    let scope = &id["Execution"]["TradeOrder"];
    let number = |key: &str| {
        scope[key]
            .as_i64()
            .and_then(|v| i32::try_from(v).ok())
            .ok_or("Invalid broker-order scope")
    };
    let scope = Scope {
        portfolio: number("PortfolioId")?,
        fund: number("FundId")?,
        order: number("OrderId")?,
    };
    let attempt = guid(&id["Execution"]["ExecutionAttemptId"])?;
    let component = guid(&id["ComponentId"])?;
    Ok(format!(
        "{}.{}.{}",
        scope.key()?,
        attempt.simple(),
        component.simple()
    ))
}
fn guid(value: &Json) -> Result<uuid::Uuid, String> {
    uuid::Uuid::parse_str(value.as_str().ok_or("Missing execution identity")?)
        .ok()
        .filter(|v| !v.is_nil())
        .ok_or("Invalid execution identity".into())
}
pub async fn execution(
    backend: &Backend,
    registry: &Registry,
    scope: Scope,
    attempt: uuid::Uuid,
    deadline: tokio::time::Instant,
) -> Result<Option<OrderExecutionDefinition>, String> {
    if attempt.is_nil() {
        return Err("Invalid execution attempt identity".into());
    }
    let key = format!("{}.{}", scope.key()?, attempt.simple());
    let value = query(
        backend,
        registry,
        &format!("{TRADE}.Order.Execution.GetOrderExecutionQuery"),
        &key,
        BTreeMap::from([
            ("TradeOrderId".into(), scope.wire()?),
            ("ExecutionAttemptId".into(), attempt.to_string().into()),
        ]),
        deadline,
    )
    .await?;
    if value.is_nil() {
        return Ok(None);
    }
    let result = OrderExecutionDefinition::from_backend(&value)?;
    if result
        .trade_order_id
        .as_ref()
        .is_none_or(|id| !scope.matches_id(id))
        || result.execution_attempt_id.as_deref() != Some(attempt.to_string().as_str())
    {
        return Err("Execution response crossed the requested attempt".into());
    }
    Ok(Some(result))
}
#[derive(Default)]
pub struct Evidence {
    pub orders: Vec<BrokerOrderDefinition>,
    pub executions: Vec<OrderExecutionDefinition>,
}
pub async fn evidence(
    backend: &Backend,
    registry: &Registry,
    scopes: &[Scope],
    deadline: tokio::time::Instant,
) -> Result<Evidence, String> {
    let mut result = Evidence::default();
    let mut attempts = BTreeSet::new();
    let mut ids = BTreeSet::new();
    let mut seen_scopes = BTreeSet::new();
    for scope in scopes
        .iter()
        .copied()
        .filter(|scope| seen_scopes.insert(*scope))
    {
        for order in orders(backend, registry, scope, deadline).await? {
            let id = order.id.as_ref().ok_or("Missing broker-order identity")?;
            if !ids.insert(typed_identity(id)?) {
                return Err("Broker evidence repeated an order across scopes".into());
            }
            let attempt = required_guid(
                id.execution
                    .as_ref()
                    .and_then(|id| id.execution_attempt_id.as_deref()),
            )?;
            if attempts.insert((scope, attempt)) {
                if let Some(value) = execution(backend, registry, scope, attempt, deadline).await? {
                    result.executions.push(value)
                }
            }
            result.orders.push(order);
        }
    }
    Ok(result)
}
