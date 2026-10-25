//! Reduce-only intent is bound to the backend's remaining opening position.
//! Every contract, remaining signed quantity and original leg identity must agree.
use super::{parameters, trade_monitor_queries::Scope};
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use rmpv::Value;
use serde_json::json;
use std::collections::BTreeMap;
const TRADE: &str = "TomasAI.IFM.Domain.Trade.Shared";
const ROOT: &str = "TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition";
pub async fn current(
    backend: &Backend,
    registry: &Registry,
    primary: Scope,
    strategy: &str,
    deadline: tokio::time::Instant,
) -> Result<serde_json::Value, String> {
    let (key, id) = primary.position(strategy)?;
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
            (
                "PositionId".into(),
                parameters::from_document(registry, &format!("{TRADE}.StrategyPositionId"), &id)?,
            ),
        ]),
    )?;
    let reply = backend
        .request_until(&subject, &body, deadline, None)
        .await?;
    let position = parameters::document(
        registry,
        &format!("{TRADE}.StrategyPositionSnapshot"),
        service_result(&reply)?,
        true,
    )?;
    if position["Id"] != id || position["StrategyKind"] != strategy {
        return Err("The target opening position is unavailable, changed or invalid".into());
    }
    Ok(position)
}
pub async fn candidate(
    backend: &Backend,
    registry: &Registry,
    primary: Scope,
    strategy: &str,
    prepared: &Value,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    let position = current(backend, registry, primary, strategy, deadline).await?;
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
