//! Working-order display and input eligibility. Backend actors remain the
//! authority for accepting a cancellation or price change. Decimal price/tick
//! checks never pass through display floats.
use super::{broker_reads, option_selection::compare, outlook::decimal_fixed};
use crate::backend::contracts::ExactDecimal;
use crate::models::{
    broker_presentation::{BrokerMutation, BrokerSummary, Properties, Property},
    cells::{Cell, Cells},
    labels::Label,
    trade_contracts::*,
};
fn dec(value: Cell<'_>) -> Result<ExactDecimal, String> {
    ExactDecimal::parse(&value.text())
}
pub fn component(order: &BrokerOrderDefinition) -> Result<&TradeOrderComponentDefinition, String> {
    let id = order.id.as_ref().and_then(|id| id.component_id.as_ref());
    let found = order
        .order
        .as_ref()
        .and_then(|o| o.components.as_ref())
        .ok_or("Missing broker components")?
        .iter()
        .filter(|c| c.component_id.as_ref() == id)
        .collect::<Vec<_>>();
    if found.len() != 1 {
        return Err("Broker order component is unavailable or ambiguous".into());
    }
    Ok(found[0])
}
fn coefficient(value: &ExactDecimal) -> (u128, usize) {
    let text = value.as_str().trim_start_matches('-');
    let scale = text.split_once('.').map(|(_, f)| f.len()).unwrap_or(0);
    (
        text.bytes()
            .filter(|b| *b != b'.')
            .fold(0u128, |n, b| n * 10 + u128::from(b - b'0')),
        scale,
    )
}
pub fn is_tick_multiple(value: &ExactDecimal, tick: &ExactDecimal) -> bool {
    if compare(tick, &ExactDecimal::parse("0").unwrap()).is_le() {
        return false;
    }
    let (value, vs) = coefficient(value);
    let (tick, ts) = coefficient(tick);
    if vs >= ts {
        tick.checked_mul(10u128.pow((vs - ts) as u32))
            .map(|divisor| value % divisor == 0)
            .unwrap_or(value == 0)
    } else {
        let mut remainder = value % tick;
        for _ in 0..ts - vs {
            remainder = (remainder * 10) % tick;
        }
        remainder == 0
    }
}
pub fn can_mutate(
    order: &BrokerOrderDefinition,
    execution: Option<&OrderExecutionDefinition>,
    pending: bool,
) -> Result<bool, String> {
    if pending
        || !["Working", "PartiallyFilled"].contains(&order.cell("Status").text().as_str())
        || execution.is_some_and(|e| {
            ["Filled", "Cancelled", "Rejected"].contains(&e.cell("Status").text().as_str())
        })
    {
        return Ok(false);
    }
    let c = component(order)?;
    if [
        "TickIncrement",
        "MinimumSignedNetDebitLimit",
        "MaximumSignedNetDebitLimit",
    ]
    .iter()
    .any(|k| c.cell(k).is_missing())
    {
        return Ok(false);
    }
    Ok(
        compare(&dec(c.cell("TickIncrement"))?, &ExactDecimal::parse("0")?).is_gt()
            && compare(
                &dec(c.cell("MinimumSignedNetDebitLimit"))?,
                &dec(c.cell("MaximumSignedNetDebitLimit"))?,
            )
            .is_le(),
    )
}
pub fn validate_limit(order: &BrokerOrderDefinition, price: &ExactDecimal) -> Result<(), String> {
    let c = component(order)?;
    let tick = dec(c.cell("TickIncrement"))?;
    if compare(price, &dec(c.cell("MinimumSignedNetDebitLimit"))?).is_lt()
        || compare(price, &dec(c.cell("MaximumSignedNetDebitLimit"))?).is_gt()
        || !is_tick_multiple(price, &tick)
    {
        return Err(
            "Limit price must be inside the component bounds and match its tick increment".into(),
        );
    }
    Ok(())
}
fn compact(value: Cell<'_>) -> Result<String, String> {
    let mut text = decimal_fixed(&value.text(), 8, false)?;
    if text.contains('.') {
        text = text.trim_end_matches('0').trim_end_matches('.').into();
    }
    Ok(text)
}
pub fn caption(order: &BrokerOrderDefinition) -> Result<String, String> {
    let c = component(order)?;
    let legs = c.legs.as_ref().ok_or("Missing broker caption legs")?;
    let leg = |l: &TradeLegDefinition| -> Result<String, String> {
        if l.asset_family == Some(TradeAssetFamily::FuturesOption) {
            if l.cell("Strike").is_missing() {
                Ok(String::new())
            } else {
                compact(l.cell("Strike"))
            }
        } else {
            Ok(format!(
                "{} {}",
                if l.cell("SignedQuantity").as_i64().unwrap_or_default() > 0 {
                    "buy"
                } else {
                    "sell"
                },
                l.cell("ContractId").text().as_str()
            ))
        }
    };
    let mut calls: Vec<_> = legs.iter().filter(|l| l.put_call == Some(1)).collect();
    calls.sort_by_key(|l| std::cmp::Reverse(l.cell("SignedQuantity").as_i64().unwrap_or_default()));
    let mut puts: Vec<_> = legs.iter().filter(|l| l.put_call == Some(2)).collect();
    puts.sort_by_key(|l| l.cell("SignedQuantity").as_i64().unwrap_or_default());
    let mut groups = vec![];
    for side in [&calls, &puts] {
        let value = side
            .iter()
            .map(|l| leg(l))
            .collect::<Result<Vec<_>, _>>()?
            .join("-");
        if !value.is_empty() {
            groups.push(value);
        }
    }
    let description = if legs
        .iter()
        .all(|l| l.asset_family == Some(TradeAssetFamily::FuturesOption))
    {
        groups.join(":")
    } else {
        legs.iter()
            .map(leg)
            .collect::<Result<Vec<_>, _>>()?
            .join("-")
    };
    let strategy = c
        .strategy_kind
        .as_ref()
        .filter(|kind| !matches!(kind, TradeStrategyKind::Unrecognized(_)))
        .map(|kind| kind.label().into_owned())
        .unwrap_or_default();
    let kind = match strategy.as_str() {
        "IronCondor" => {
            let mut highest: Option<ExactDecimal> = None;
            for call in &calls {
                if !call.cell("Strike").is_missing() {
                    let value = dec(call.cell("Strike"))?;
                    if highest
                        .as_ref()
                        .is_none_or(|old| compare(&value, old).is_gt())
                    {
                        highest = Some(value);
                    }
                }
            }
            let short = calls.iter().any(|l| {
                l.cell("SignedQuantity").as_i64().unwrap_or_default() > 0
                    && highest.as_ref().is_some_and(|v| {
                        dec(l.cell("Strike")).is_ok_and(|s| compare(&s, v).is_eq())
                    })
            });
            if short {
                "ShortIronCondor"
            } else {
                "LongIronCondor"
            }
        }
        "VerticalSpread" => "Vertical Spread",
        "FuturesOutright" => "Futures",
        other => other,
    };
    Ok(format!(
        "{kind} = {description} @ {}",
        compact(order.cell("CurrentSignedNetDebitLimit"))?
    ))
}
fn label(value: Cell<'_>) -> Option<Label> {
    match value {
        Cell::Missing => None,
        Cell::Signed(n) => Some(Label::Number(n)),
        Cell::Unsigned(n) => Some(Label::Number(n as i64)),
        other => Some(Label::Text(other.text())),
    }
}
pub fn summary(
    order: &BrokerOrderDefinition,
    execution: Option<&OrderExecutionDefinition>,
) -> Result<BrokerSummary, String> {
    let terminal = execution.filter(|e| {
        matches!(
            e.status,
            Some(
                OrderExecutionStatus::Filled
                    | OrderExecutionStatus::Cancelled
                    | OrderExecutionStatus::Rejected
            )
        )
    });
    let details = order.order.as_ref();
    Ok(BrokerSummary {
        order_id: broker_reads::typed_identity(
            order.id.as_ref().ok_or("Missing broker order identity")?,
        )?,
        status: label(
            terminal
                .map(|e| e.cell("Status"))
                .unwrap_or_else(|| order.cell("Status")),
        ),
        broker_status: label(order.cell("Status")),
        execution_status: label(execution.cell("Status")),
        price: order.current_signed_net_debit_limit.clone(),
        broker_revision: order.broker_revision,
        time_in_force: details.and_then(|d| d.time_in_force.clone()),
        broker_algorithm: label(details.cell("BrokerAlgorithm")),
        algorithm_pace: details.and_then(|d| d.algorithm_pace.clone()),
        requested: execution.and_then(|e| e.order_quantity),
        filled: execution.and_then(|e| e.cumulative_filled_quantity),
        remaining: execution.map(|e| {
            (i64::from(e.order_quantity.unwrap_or_default())
                - i64::from(e.cumulative_filled_quantity.unwrap_or_default()))
            .max(0)
        }),
        dispatch_category: order.dispatch_category.clone(),
        dispatch_detail: order.dispatch_detail.clone(),
        changed_at_utc: order.changed_at_utc.clone(),
    })
}
pub fn mutation(
    order: &BrokerOrderDefinition,
    execution: Option<&OrderExecutionDefinition>,
    pending: bool,
) -> Result<BrokerMutation, String> {
    let c = component(order)?;
    Ok(BrokerMutation {
        can_mutate: can_mutate(order, execution, pending)?,
        minimum: c.minimum_signed_net_debit_limit.clone(),
        maximum: c.maximum_signed_net_debit_limit.clone(),
        tick: c.tick_increment.clone(),
        pending,
    })
}
pub fn properties(value: &impl Cells, names: &[&str]) -> Result<Properties, String> {
    Ok(Properties {
        properties: names
            .iter()
            .map(|name| {
                let value = value.cell(name);
                let display = if name.ends_with("AtUtc") && !value.is_missing() {
                    crate::backend::date_display::general(&value.text())?
                } else {
                    match value {
                        Cell::Boolean(v) => if v { "True" } else { "False" }.into(),
                        _ => value.text(),
                    }
                };
                Ok(Property {
                    name: (*name).into(),
                    display,
                })
            })
            .collect::<Result<Vec<_>, String>>()?,
    })
}
pub const SUMMARY_NAMES: &[&str] = &[
    "OrderId",
    "Status",
    "BrokerStatus",
    "ExecutionStatus",
    "Price",
    "BrokerRevision",
    "TimeInForce",
    "BrokerAlgorithm",
    "AlgorithmPace",
    "Requested",
    "Filled",
    "Remaining",
    "DispatchCategory",
    "DispatchDetail",
    "ChangedAtUtc",
];
pub const FILL_NAMES: &[&str] = &[
    "ExecutionFillId",
    "ExecutionAttemptId",
    "ComponentId",
    "TradeLegId",
    "LegacyMarketInstrumentId",
    "SignedQuantity",
    "Price",
    "Commission",
    "FilledAtUtc",
    "ExternalExecutionId",
    "ContractId",
];
