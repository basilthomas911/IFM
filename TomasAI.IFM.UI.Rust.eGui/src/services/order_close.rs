//! Reduce-only intent is bound to the backend's remaining opening position.
//! Every contract, remaining signed quantity and original leg identity must agree.
use super::{parameters, trade_monitor_queries::Scope};
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use crate::models::{trade_contracts::StrategyPositionSnapshot, wire_types::FromBackend};
use rmpv::Value;

/// Validated concrete position together with its immutable original wire record.
pub struct CurrentPosition {
    /// Snapshot associated with this current position.
    pub snapshot: StrategyPositionSnapshot,
    /// Wire associated with this current position.
    pub wire: Value,
}
use serde_json::json;
use std::collections::BTreeMap;
const TRADE: &str = "TomasAI.IFM.Domain.Trade.Shared";
const ROOT: &str = "TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition";

/// Resolve a closing setup against its single opening trade, including legacy
/// closing rows whose requested maturity was never populated.
pub fn inherit_opening_trade(
    trade: &mut crate::models::trade_contracts::FundOrderTradeProjectionReadModel,
    trades: &[crate::models::trade_contracts::FundOrderTradeProjectionReadModel],
) -> Result<Option<crate::models::trade_contracts::FundOrderTradeProjectionReadModel>, String> {
    if trade.primary_trade != Some(false) {
        return Ok(None);
    }
    let primaries: Vec<_> = trades
        .iter()
        .filter(|row| row.primary_trade == Some(true))
        .collect();
    if primaries.len() != 1 {
        return Err("A closing trade requires exactly one opening trade in this order".into());
    }
    let primary = primaries[0];
    if primary.order_id != trade.order_id
        || primary.portfolio_id != trade.portfolio_id
        || primary.fund_id != trade.fund_id
        || primary.trade_id == trade.trade_id
    {
        return Err("Opening trade does not belong to the closing order".into());
    }
    let maturity = primary
        .maturity_date
        .clone()
        .or_else(|| primary.requested_maturity_date.clone());
    if maturity.is_none() && trade.trade_type.as_deref() != Some("FuturesOutright") {
        return Err(
            "The opening trade has no maturity date; reload its established position".into(),
        );
    }
    trade.requested_maturity_date = maturity;
    Ok(Some(primary.clone()))
}

pub fn primary_scope(
    primary: &crate::models::trade_contracts::FundOrderTradeProjectionReadModel,
) -> Result<Scope, String> {
    let positive = |value: Option<i32>| {
        value
            .filter(|id| *id > 0)
            .ok_or("The opening trade has no established execution identity".to_string())
    };
    Ok(Scope {
        portfolio: positive(primary.portfolio_id)?,
        fund: positive(primary.fund_id)?,
        order: positive(primary.execution_order_id)?,
        trade: positive(primary.execution_trade_id)?,
    })
}

/// Stage the exact inverse of the remaining position, never a new delta-selected
/// spread. The backend checks the position again when the close is submitted.
pub fn reversed_legs(
    position: &StrategyPositionSnapshot,
    multiplier: &ExactDecimal,
    expiry: Option<chrono::NaiveDate>,
) -> Result<(i32, Vec<super::option_draft::Selected>), String> {
    use crate::models::cells::Cells;
    if position.is_open != Some(true) {
        return Err("The opening position is already closed".into());
    }
    let rows = position
        .legs
        .as_ref()
        .filter(|rows| !rows.is_empty() && rows.len() <= 4)
        .ok_or("Opening position has no remaining legs")?;
    let mut result = Vec::new();
    let mut quantity = None;
    let mut seen = std::collections::BTreeSet::new();
    for row in rows {
        let signed = row
            .signed_quantity
            .and_then(i32::checked_neg)
            .filter(|q| *q != 0)
            .ok_or("Invalid remaining opening quantity")?;
        let units = signed.checked_abs().ok_or("Opening quantity overflow")?;
        if quantity.is_some_and(|q| q != units) {
            return Err(
                "Remaining opening legs are not balanced; reconcile the position before closing"
                    .into(),
            );
        }
        quantity = Some(units);
        let contract = row
            .contract_id
            .clone()
            .filter(|id| !id.is_empty())
            .ok_or("Missing opening contract")?;
        if !seen.insert(contract.clone()) {
            return Err("Duplicate opening contract".into());
        }
        let future = row.cell("AssetFamily").text() == "Futures";
        if !future
            && expiry.is_some_and(|date| {
                row.expiry.as_ref().map(|v| v.as_str()) != Some(date.to_string().as_str())
            })
        {
            return Err("Opening position legs do not match its maturity date".into());
        }
        let call = row.put_call == Some(1);
        if !future && !matches!(row.put_call, Some(1 | 2)) {
            return Err("Missing opening option right".into());
        }
        let side = if signed < 0 { -1 } else { 1 };
        let role = if future {
            format!("{}Future", if side < 0 { "-" } else { "+" })
        } else {
            format!(
                "{}{}",
                if side < 0 { "-S" } else { "+L" },
                if call { "C" } else { "P" }
            )
        };
        result.push(super::option_draft::Selected {
            quote: super::broker_preview::Leg {
                contract,
                side,
                strike: if future {
                    None
                } else {
                    Some(ExactDecimal::parse(
                        row.strike
                            .as_ref()
                            .ok_or("Missing opening strike")?
                            .as_str(),
                    )?)
                },
                is_call: call,
                is_future: future,
                bid: None,
                ask: None,
                delta: None,
                vega: None,
                multiplier: Some(
                    row.cash_multiplier
                        .as_ref()
                        .map(|v| ExactDecimal::parse(v.as_str()))
                        .transpose()?
                        .unwrap_or_else(|| multiplier.clone()),
                ),
            },
            role,
            quote_at: None,
            frozen: false,
        });
    }
    Ok((quantity.ok_or("Missing opening quantity")?, result))
}

/// Provides current for the order close implementation.
pub async fn current(
    backend: &Backend,
    registry: &Registry,
    primary: Scope,
    strategy: &str,
    deadline: tokio::time::Instant,
) -> Result<CurrentPosition, String> {
    let (key, id) = primary.typed_position(strategy)?;
    let (actor, verb, kind) = match strategy {
        "IronCondor" => (
            "FuturesOptionTradePositionQuery",
            "GetIronCondorOptionTradePosition",
            "Futures.Option.Position.GetIronCondorOptionTradePositionQuery",
        ),
        "VerticalSpread" => (
            "FuturesOptionTradePositionQuery",
            "GetVerticalSpreadOptionTradePosition",
            "Futures.Option.Position.GetVerticalSpreadOptionTradePositionQuery",
        ),
        "FuturesOutright" => (
            "FuturesTradePositionQuery",
            "GetFuturesTradePosition",
            "Futures.Position.GetFuturesTradePositionQuery",
        ),
        _ => return Err("Unsupported closing strategy".into()),
    };
    let subject = ActorSubject::query(actor, verb, &key);
    let body = registry.encode(
        &format!("{TRADE}.{kind}"),
        &BTreeMap::from([
            ("Subject".into(), subject.wire()),
            (
                "EntityId".into(),
                Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]),
            ),
            ("PositionId".into(), primary.position_wire(strategy)?),
        ]),
    )?;
    let reply = backend
        .request_until(&subject, &body, deadline, None)
        .await?;
    let wire = into_service_result(reply)?;
    let snapshot = StrategyPositionSnapshot::from_backend(&wire)?;
    if snapshot.id.as_ref() != Some(&id)
        || snapshot
            .strategy_kind
            .as_ref()
            .map(|value| value.label())
            .as_deref()
            != Some(strategy)
    {
        return Err("The target opening position is unavailable, changed or invalid".into());
    }
    Ok(CurrentPosition { snapshot, wire })
}

/// Provides candidate for the order close implementation.
pub async fn candidate(
    backend: &Backend,
    registry: &Registry,
    primary: Scope,
    strategy: &str,
    prepared: &Value,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    let read = current(backend, registry, primary, strategy, deadline).await?;
    let position = parameters::document(
        registry,
        &format!("{TRADE}.StrategyPositionSnapshot"),
        &read.wire,
        true,
    )?;
    let (_, id) = primary.position(strategy)?;
    if position["IsOpen"] != true {
        return Err("The target opening position is already closed".into());
    }
    let opening = parameters::document(
        registry,
        &format!("{ROOT}.PortfolioOrderCandidate"),
        prepared,
        true,
    )?;
    let mut component = opening["Components"]
        .as_array()
        .filter(|v| v.len() == 1)
        .ok_or("A closing order requires one prepared component")?[0]
        .clone();
    let remaining = position["Legs"]
        .as_array()
        .filter(|v| !v.is_empty() && v.len() <= 4)
        .ok_or("No remaining position legs")?;
    let requested = component["Legs"]
        .as_array_mut()
        .filter(|v| v.len() == remaining.len())
        .ok_or("The closing trade must reverse every remaining position leg")?;
    let mut seen = std::collections::BTreeSet::new();
    for leg in requested {
        let contract = leg["ContractId"]
            .as_str()
            .ok_or("Missing closing contract")?
            .to_string();
        if !seen.insert(contract.clone()) {
            return Err("Duplicate closing contract".into());
        }
        let matching = remaining
            .iter()
            .filter(|row| row["ContractId"] == contract)
            .collect::<Vec<_>>();
        if matching.len() != 1 {
            return Err("Closing contract differs from the opening position".into());
        }
        let target = matching[0];
        let quantity = target["SignedQuantity"]
            .as_i64()
            .and_then(|n| i32::try_from(n).ok())
            .and_then(i32::checked_neg)
            .filter(|n| *n != 0)
            .ok_or("Invalid remaining position quantity")?;
        if leg["SignedQuantity"] != quantity {
            return Err(format!(
                "Closing leg {contract} must reverse its remaining signed quantity"
            ));
        }
        for field in ["Expiry", "Strike", "PutCall"] {
            if !target[field].is_null() && leg[field] != target[field] {
                return Err(format!(
                    "Closing leg {contract} must retain its opening {field}"
                ));
            }
        }
        leg["TradeLegId"] = target["TradeLegId"].clone();
    }
    component["ReservedTradeId"] = primary.trade.into();
    component["PermitBalancedPartialAcceptance"] = false.into();
    let reference = json!({"PortfolioId":primary.portfolio,"FundId":primary.fund,"OrderId":primary.order,"TradeId":primary.trade,"PositionId":id["PositionId"]});
    let position = json!({"Id":reference,"StrategyKind":strategy,"IsOpen":true,"Legs":remaining.iter().map(|leg|json!({"TradeLegId":leg["TradeLegId"],"SignedQuantity":leg["SignedQuantity"],"ContractId":leg["ContractId"],"AssetFamily":leg["AssetFamily"]})).collect::<Vec<_>>()});
    let candidate = json!({"CompositionId":opening["CompositionId"],"WorkflowId":{"Position":reference,"ValueDate":opening["ValueDate"],"ExitDecisionId":uuid::Uuid::new_v4().to_string()},"Position":position,"StrategyKind":strategy,"ValueDate":opening["ValueDate"],"ValidUntilUtc":opening["ValidUntilUtc"],"Origin":opening["Origin"],"Component":component,"EvidenceHash":opening["EvidenceHash"],"PositionType":"Closing","SetupTrade":opening["SetupTrade"]});
    parameters::from_document(
        registry,
        &format!("{ROOT}.PortfolioCloseOrderCandidate"),
        &candidate,
    )
}
