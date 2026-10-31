//! Formats the backend's provisional economics for copied Blotter controls.
//! No payoff, exposure, capital usage or risk approval is calculated here.
use super::{
    broker_initialization::Initialization, option_draft::Selected, outlook::decimal_fixed,
};
use crate::models::{
    broker_presentation::PreviewDisplay,
    cells::{Cell, Cells},
    trade_contracts::TradeOrderPreview,
};
use std::collections::BTreeMap;
fn amount(value: Cell<'_>) -> Result<String, String> {
    if value.is_missing() {
        return Ok("Unavailable".into());
    }
    let raw = &value.text();
    let fixed = decimal_fixed(raw, 2, false)?;
    let (whole, fraction) = fixed.split_once('.').unwrap_or((&fixed, ""));
    let negative = whole.starts_with('-');
    let whole = whole.trim_start_matches('-');
    let mut result = String::new();
    if negative {
        result.push('-')
    }
    for (i, c) in whole.chars().enumerate() {
        if i > 0 && (whole.len() - i) % 3 == 0 {
            result.push(',')
        }
        result.push(c)
    }
    if !fraction.is_empty() {
        result.push('.');
        result.push_str(fraction)
    }
    Ok(result)
}
fn exposure(value: Cell<'_>) -> Result<String, String> {
    let Some(number) = value.number() else {
        return Ok("Unavailable".into());
    };
    if !number.is_finite() {
        return Err("Invalid backend preview exposure".into());
    }
    let result = format!("{number:.4}");
    let result = result.trim_end_matches('0').trim_end_matches('.');
    Ok(if result == "-0" { "0" } else { result }.into())
}
fn nullable<'a>(value: Option<&'a impl Cells>, key: &str) -> Cell<'a> {
    value.map(|v| v.cell(key)).unwrap_or(Cell::Missing)
}
pub fn document(
    legs: &[Selected],
    preview: &TradeOrderPreview,
    data: Option<&Initialization>,
) -> Result<PreviewDisplay, String> {
    let mut fields: BTreeMap<String, String> = BTreeMap::new();
    let mut set = |key: &str, value: String| {
        fields.insert(key.into(), value);
    };
    set(
        "Value source",
        if legs.iter().any(|l| l.frozen) {
            "Frozen quotes / stored fund"
        } else {
            "Market Selection / stored fund"
        }
        .into(),
    );
    let as_of = legs
        .iter()
        .filter_map(|l| l.quote_at.as_ref().map(|value| value.as_str()))
        .map(chrono::DateTime::parse_from_rfc3339)
        .collect::<Result<Vec<_>, _>>()
        .map_err(|_| "Invalid selected quote timestamp")?;
    set(
        "As of",
        as_of
            .into_iter()
            .min()
            .map(|at| {
                at.with_timezone(&chrono_tz::America::New_York)
                    .format("%Y-%m-%d %H:%M:%S")
                    .to_string()
            })
            .unwrap_or("Quote timestamp unavailable".into()),
    );
    set(
        "Risk approval",
        if data
            .and_then(|d| d.order.as_ref())
            .is_some_and(|o| o.risk_authorization.is_some())
        {
            "Stored approval - requires revalidation"
        } else {
            "Not requested"
        }
        .into(),
    );
    set(
        "Available funds",
        amount(nullable(
            data.and_then(|d| d.balances.as_ref()),
            "AvailableCash",
        ))?,
    );
    set(
        "Fund capacity",
        amount(nullable(
            data.and_then(|d| d.risk.as_ref()),
            "AvailableCapital",
        ))?,
    );
    set(
        "Fund risk limit",
        amount(nullable(
            data.and_then(|d| d.risk.as_ref()),
            "MaximumRiskPerTrade",
        ))?,
    );
    let limit = data
        .and_then(|d| d.trade.as_ref())
        .and_then(|v| v.trade_limit.as_ref());
    set(
        "Minimum profit",
        amount(nullable(limit, "MinProfitTarget"))?,
    );
    set("Risk margin", amount(nullable(limit, "RiskMargin"))?);
    set("Buying power", "Broker margin assessment required".into());
    set("Est. fees", "Fee schedule unavailable".into());
    set(
        "Worst approved limit",
        "Not approved for this selection".into(),
    );
    set(
        "Reserved capital",
        amount(nullable(
            data.and_then(|d| d.usage.as_ref()),
            "ReservedFundLossCapital",
        ))?,
    );
    if legs.len() == 1 && legs[0].quote.is_future {
        set("Value source", "Stored EOD reference - no bid/ask".into());
        if let Some(date) = data
            .and_then(|d| d.price.as_ref())
            .and_then(|p| p.value_date.as_ref().map(|v| v.as_str()))
        {
            set("As of", format!("EOD {date}"));
        }
        set(
            "Validation",
            "Reference price only - market quote required".into(),
        );
        set("Net mid", amount(preview.cell("NetMid"))?);
        set("Net delta", exposure(preview.cell("NetDelta"))?);
        set("Net vega", exposure(preview.cell("NetVega"))?);
        set("Max profit", "Price dependent".into());
        set("Max loss", "Price dependent".into());
    } else {
        set(
            "Validation",
            if preview.cell("HasCompleteQuotes").as_bool() != Some(true) {
                "Select priced strategy legs"
            } else if preview.cell("HasBalancedQuantities").as_bool() != Some(true) {
                "Unequal leg quantities - strategy changed"
            } else if data.is_some_and(|d| !d.errors.is_empty()) {
                "Stored data incomplete"
            } else {
                "Preview - approval required"
            }
            .into(),
        );
        if preview.cell("HasCompleteQuotes").as_bool() == Some(true) {
            for (caption, key) in [
                ("Net bid", "NetBid"),
                ("Net ask", "NetAsk"),
                ("Net mid", "NetMid"),
            ] {
                set(caption, amount(preview.cell(key))?);
            }
            for (caption, key) in [("Net delta", "NetDelta"), ("Net vega", "NetVega")] {
                if !preview.cell(key).is_missing() {
                    set(caption, exposure(preview.cell(key))?);
                }
            }
            for (caption, key) in [
                ("Call width", "CallWidth"),
                ("Put width", "PutWidth"),
                ("Call spread", "CallSpreadValue"),
                ("Put spread", "PutSpreadValue"),
            ] {
                if !preview.cell(key).is_missing() {
                    set(caption, amount(preview.cell(key))?);
                }
            }
            if preview.cell("HasPayoffEstimate").as_bool() == Some(true) {
                set("Est. credit", amount(preview.cell("EstimatedCredit"))?);
                set(
                    "Max profit",
                    if preview.cell("HasUnlimitedProfit").as_bool() == Some(true) {
                        "Unlimited".into()
                    } else {
                        amount(preview.cell("MaximumProfit"))?
                    },
                );
                set(
                    "Max loss",
                    if preview.cell("HasUnlimitedLoss").as_bool() == Some(true) {
                        "Unlimited".into()
                    } else {
                        amount(preview.cell("MaximumLoss"))?
                    },
                );
                set(
                    "Max return",
                    if preview.cell("MaximumReturn").is_missing() {
                        "Unavailable".into()
                    } else {
                        decimal_fixed(&preview.cell("MaximumReturn").text(), 2, true)?
                    },
                );
            }
        }
    }
    // BridgeDocument represents decimal amounts as invariant strings. Keeping
    // that representation also prevents the generic wire adapter from rounding
    // a 28-digit order price through f64 during publication.
    let price = preview.order_price.clone();
    Ok(PreviewDisplay {
        fields,
        order_price: price,
    })
}
