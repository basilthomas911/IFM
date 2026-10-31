//! Format backend market metrics; no prices, volatility or DTE are recalculated here.
use super::{option_selection::compare, outlook::decimal_fixed};
use crate::{
    backend::contracts::ExactDecimal,
    models::{
        broker_presentation::MarketMetrics,
        cells::{Cell, Cells},
        trade_contracts::{EvaluatedOptionChainReadModel, FuturesContractV3ReadModel},
    },
};
use std::borrow::Cow;
fn raw(value: Cell<'_>) -> Result<Cow<'_, str>, String> {
    if let Cell::Text(value) = value {
        Ok(value)
    } else {
        Err("Missing exact market metric".into())
    }
}
fn fixed(value: Cell<'_>, places: u32) -> Result<String, String> {
    if value.is_missing() {
        Ok("N/A".into())
    } else {
        decimal_fixed(&raw(value)?, places, false)
    }
}
fn signed(value: Cell<'_>, percent: bool) -> Result<String, String> {
    let text = raw(value)?;
    let mut formatted = decimal_fixed(&text, 2, percent)?;
    if compare(&ExactDecimal::parse(&text)?, &ExactDecimal::parse("0")?).is_gt() {
        formatted.insert(0, '+');
    }
    Ok(formatted)
}
pub fn document(
    chain: &EvaluatedOptionChainReadModel,
    underlying: &FuturesContractV3ReadModel,
    tick: Option<&str>,
    live: bool,
) -> Result<MarketMetrics, String> {
    let latest = chain
        .contracts
        .as_ref()
        .ok_or("Missing market metric contracts")?
        .iter()
        .filter_map(|row| row.quote_at_utc.as_ref().map(|value| value.as_str()))
        .map(chrono::DateTime::parse_from_rfc3339)
        .collect::<Result<Vec<_>, _>>()
        .map_err(|_| "Invalid market quote timestamp")?
        .into_iter()
        .max();
    let age = if let Some(latest) = latest {
        let at = chrono::DateTime::parse_from_rfc3339(
            chain
                .as_of_utc
                .as_ref()
                .ok_or("Missing API market as-of")?
                .as_str(),
        )
        .map_err(|_| "Invalid API market as-of")?;
        let delta = at - latest;
        let milliseconds = delta
            .num_nanoseconds()
            .map(|n| n as f64 / 1_000_000.0)
            .unwrap_or(delta.num_milliseconds() as f64);
        format!("{:.0} ms", milliseconds.max(0.0))
    } else {
        if live { "N/A" } else { "Frozen preview" }.into()
    };
    let change =
        if chain.daily_price_change.is_none() || chain.daily_price_change_fraction.is_none() {
            "N/A".into()
        } else {
            format!(
                "{} ({})",
                signed(chain.cell("DailyPriceChange"), false)?,
                signed(chain.cell("DailyPriceChangeFraction"), true)?
            )
        };
    let iv = if chain.atm_implied_volatility.is_none() {
        "N/A".into()
    } else {
        decimal_fixed(&raw(chain.cell("AtmImpliedVolatility"))?, 1, true)?.replace('%', " %")
    };
    let rank = if chain.iv_rank.is_some() {
        fixed(chain.cell("IvRank"), 1)?
    } else if chain
        .iv_rank_status
        .as_deref()
        .unwrap_or_default()
        .starts_with("InsufficientHistory")
    {
        "History needed".into()
    } else {
        "N/A".into()
    };
    Ok(MarketMetrics {
        dte: chain
            .days_to_expiry
            .map(|value| value.to_string())
            .unwrap_or("N/A".into()),
        underlying: underlying.contract_id.clone(),
        multiplier: underlying.multiplier.clone(),
        tick: tick.unwrap_or("N/A").into(),
        settlement: "Unavailable".into(),
        daily_std_dev: fixed(chain.cell("BollingerStandardDeviation20"), 2)?,
        last: fixed(chain.cell("LastUnderlyingPrice"), 2)?,
        daily_price_change: change,
        iv,
        iv_rank: rank,
        quote_age: age,
    })
}
