//! Immutable typed initialization evidence. Missing optional reads remain explicit
//! errors; no client financial totals or execution authority are manufactured.
use super::{
    broker_reads::{self, Scope},
    business_context::BusinessContext,
    financial, portfolio,
};
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    models::{trade_contracts::*, wire_types::FromBackend},
};
use rmpv::Value;
use std::collections::BTreeMap;
#[derive(Default)]
pub struct Initialization {
    pub balances: Option<FinancialBalanceSnapshot>,
    pub usage: Option<FinancialCapacityUsage>,
    pub risk: Option<FundRiskEnvelopeReadModel>,
    pub order: Option<FundOrderProjectionReadModel>,
    pub trade: Option<OptionTradeReadModel>,
    pub underlying: Option<FuturesContractV3ReadModel>,
    pub price: Option<FuturesEodDataV2ReadModel>,
    pub account: Option<BrokerAccountDefinition>,
    pub reservation: Option<FinancialReservationView>,
    pub errors: Vec<String>,
}
async fn basic<T: FromBackend>(
    backend: &Backend,
    registry: &Registry,
    kind: &str,
    key: &str,
    mut fields: BTreeMap<String, Value>,
    deadline: tokio::time::Instant,
) -> Result<Option<T>, String> {
    let allowed = [
        "TomasAI.IFM.Domain.MarketData.Shared.Queries.GetFuturesContractQuery",
        "TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetFuturesEodDataQuery",
        "TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetLastFuturesEodDataQuery",
        "TomasAI.IFM.Domain.Trade.Shared.Queries.GetOptionTradeQuery",
    ];
    if !allowed.contains(&kind) {
        return Err("Unsupported broker initialization read".into());
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
    if kind.ends_with(".GetOptionTradeQuery")
        && reply.as_array().is_some_and(|fields| {
            fields.len() >= 4
                && fields[0].as_bool() == Some(false)
                && fields[1].as_i64() == Some(404)
        })
    {
        return Ok(None);
    }
    Option::<T>::from_backend(service_result(&reply)?)
}
async fn financial_value<T: FromBackend>(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    scope: Scope,
    name: &str,
    parameters: Value,
    deadline: tokio::time::Instant,
) -> Result<Option<T>, String> {
    let read = financial::query(
        backend,
        registry,
        context,
        name,
        scope.portfolio,
        Some(scope.fund),
        parameters,
        financial::AccessScope::MetricsReader,
        deadline,
    )
    .await?;
    read.value.as_ref().map(T::from_backend).transpose()
}
async fn stored_price(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    contract: &str,
    deadline: tokio::time::Instant,
) -> Result<Option<FuturesEodDataV2ReadModel>, String> {
    let key = format!("{contract}.{}", context.value_date);
    let fields = BTreeMap::from([
        ("ContractId".into(), contract.into()),
        ("ValueDate".into(), date_only_wire(context.value_date)),
    ]);
    let current = basic::<FuturesEodDataV2ReadModel>(
        backend,
        registry,
        "TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetFuturesEodDataQuery",
        &key,
        fields.clone(),
        deadline,
    )
    .await;
    if let Ok(Some(value)) = &current {
        let positive = |value: Option<&str>| {
            value
                .and_then(|value| value.parse::<f64>().ok())
                .is_some_and(|value| value.is_finite() && value > 0.0)
        };
        if positive(value.close_price.as_ref().map(|value| value.as_str()))
            && value
                .daily_std_dev_amount
                .is_some_and(|value| value.is_finite() && value > 0.0)
        {
            return current;
        }
    }
    basic(
        backend,
        registry,
        "TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetLastFuturesEodDataQuery",
        &key,
        fields,
        deadline,
    )
    .await
}
fn retain<T>(name: &str, result: Result<Option<T>, String>, errors: &mut Vec<String>) -> Option<T> {
    match result {
        Ok(value) => value,
        Err(error) => {
            errors.push(format!("{name}: {error}"));
            None
        }
    }
}
pub async fn load(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    scope: Scope,
    trade_id: i32,
    contract: &str,
    is_future: bool,
    alias: &str,
    deadline: tokio::time::Instant,
) -> Result<Initialization, String> {
    scope.key()?;
    if trade_id <= 0 || contract.is_empty() {
        return Err("Invalid broker initialization identity".into());
    }
    let risk = async {
        let wire = portfolio::query(
            backend,
            registry,
            context,
            "GetFundRiskEnvelopeQuery",
            &format!("{}.{}", scope.portfolio, scope.fund),
            BTreeMap::from([
                ("PortfolioId".into(), scope.portfolio.into()),
                ("FundId".into(), scope.fund.into()),
                ("AsOfUtc".into(), timestamp_wire(context.as_of_utc)),
            ]),
            deadline,
        )
        .await?;
        let result = Option::<FundRiskEnvelopeReadModel>::from_backend(&wire)?;
        if result.as_ref().is_some_and(|row| {
            row.portfolio_id != Some(scope.portfolio) || row.fund_id != Some(scope.fund)
        }) {
            return Err("Risk envelope crossed the selected scope".into());
        }
        Ok::<_, String>(result)
    };
    let order = async {
        let wire = portfolio::query(
            backend,
            registry,
            context,
            "GetFundOrderByOrderIdQuery",
            &scope.order.to_string(),
            BTreeMap::from([("OrderId".into(), scope.order.into())]),
            deadline,
        )
        .await?;
        let result = Option::<FundOrderProjectionReadModel>::from_backend(&wire)?;
        if result.as_ref().is_some_and(|row| {
            row.portfolio_id != Some(scope.portfolio)
                || row.fund_id != Some(scope.fund)
                || row.order_id != Some(scope.order)
        }) {
            return Err("Order initialization crossed the selected scope".into());
        }
        Ok::<_, String>(result)
    };
    let underlying = async {
        let result = basic::<FuturesContractV3ReadModel>(
            backend,
            registry,
            "TomasAI.IFM.Domain.MarketData.Shared.Queries.GetFuturesContractQuery",
            contract,
            BTreeMap::from([("ContractId".into(), contract.into())]),
            deadline,
        )
        .await?;
        if result
            .as_ref()
            .is_some_and(|row| row.contract_id.as_deref() != Some(contract))
        {
            return Err("Underlying definition crossed the selected contract".into());
        }
        Ok(result)
    };
    let trade = async {
        if is_future {
            return Ok(None);
        }
        let result = basic::<OptionTradeReadModel>(
            backend,
            registry,
            "TomasAI.IFM.Domain.Trade.Shared.Queries.GetOptionTradeQuery",
            &format!("{}.{}", scope.order, trade_id),
            BTreeMap::from([
                ("OrderId".into(), scope.order.into()),
                ("TradeId".into(), trade_id.into()),
            ]),
            deadline,
        )
        .await?;
        if result
            .as_ref()
            .is_some_and(|row| row.order_id != Some(scope.order) || row.trade_id != Some(trade_id))
        {
            return Err("Stored trade crossed the selected identity".into());
        }
        Ok(result)
    };
    let balances = financial_value::<FinancialBalanceSnapshot>(
        backend,
        registry,
        context,
        scope,
        "GetAccountBalancesQuery",
        Value::Array(vec![]),
        deadline,
    );
    let usage = financial_value::<FinancialCapacityUsage>(
        backend,
        registry,
        context,
        scope,
        "GetCapacityUsageQuery",
        Value::Array(vec![]),
        deadline,
    );
    let account = broker_reads::account(backend, registry, alias, deadline);
    let price = stored_price(backend, registry, context, contract, deadline);
    let (balances, usage, risk, order, underlying, trade, account, price) = tokio::join!(
        balances, usage, risk, order, underlying, trade, account, price
    );
    let mut errors = vec![];
    let mut result = Initialization {
        balances: retain("Fund balances", balances, &mut errors),
        usage: retain("Fund capacity usage", usage, &mut errors),
        risk: retain("Fund risk envelope", risk, &mut errors),
        order: retain("Order", order, &mut errors),
        underlying: retain("Underlying definition", underlying, &mut errors),
        trade: retain("Stored trade", trade, &mut errors),
        account: retain("Broker account", account, &mut errors),
        price: retain("Stored underlying price", price, &mut errors),
        ..Default::default()
    };
    if let Some(id) = result
        .order
        .as_ref()
        .and_then(|order| order.risk_authorization.as_ref())
        .and_then(|authorization| authorization.reservation_id.as_deref())
    {
        let id = uuid::Uuid::parse_str(id).map_err(|_| "Invalid stored reservation identity")?;
        let read = financial_value::<FinancialReservationView>(
            backend,
            registry,
            context,
            scope,
            "GetCapacityReservationQuery",
            Value::Array(vec![id.to_string().into()]),
            deadline,
        )
        .await;
        result.reservation = retain("Order reservation", read, &mut errors);
    }
    result.errors = errors;
    Ok(result)
}
