//! Native counterpart of the hybrid MarketOutlookPresentation. This module
//! formats backend values and maps semantic colors; it computes no indicators.
use crate::models::{
    cells::{Cell, Cells},
    trade_contracts::MarketOutlookReadModel,
    wire_types::FromBackend,
};
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    market_outlook::OutlookCell,
    services::{actor_requests, business_context::BusinessContext, market_history},
};
use chrono::{DateTime, Utc};
use std::collections::BTreeMap;

pub struct Outlook {
    pub cells: BTreeMap<String, OutlookCell>,
    pub status: String,
    pub summary: String,
}
fn text(value: &Cell<'_>) -> String {
    value.text()
}
fn numeric(value: &Cell<'_>) -> Option<f64> {
    value.number()
}
/// Decimal display rounding works on the preserved 96-bit coefficient. Even a
/// very large price retains its cents; conversion to f64 is only used for color
/// comparisons against small thresholds, never to format the decimal itself.
pub fn decimal_fixed(text: &str, places: u32, percent: bool) -> Result<String, String> {
    ExactDecimal::parse(text)?;
    if places > 8 {
        return Err("Unsupported display precision".into());
    }
    let negative = text.starts_with('-');
    let digits = text.trim_start_matches('-');
    let scale = digits
        .split_once('.')
        .map(|(_, s)| s.len() as u32)
        .unwrap_or(0);
    let mut coefficient = digits
        .bytes()
        .filter(|b| *b != b'.')
        .fold(0u128, |n, b| n * 10 + (b - b'0') as u128);
    let target = places + if percent { 2 } else { 0 };
    if scale > target {
        let divisor = 10u128.pow(scale - target);
        coefficient = coefficient / divisor + u128::from(coefficient % divisor >= divisor / 2);
    } else {
        coefficient = coefficient
            .checked_mul(10u128.pow(target - scale))
            .ok_or("Display overflow")?;
    }
    let divisor = 10u128.pow(places);
    let sign = if negative && coefficient != 0 {
        "-"
    } else {
        ""
    };
    let value = if places == 0 {
        format!("{sign}{coefficient}")
    } else {
        format!(
            "{sign}{}.{:0width$}",
            coefficient / divisor,
            coefficient % divisor,
            width = places as usize
        )
    };
    if !percent {
        return Ok(value);
    }
    let (whole, fraction) = value.split_once('.').unwrap_or((&value, ""));
    let digits = whole.trim_start_matches('-');
    let mut grouped = String::new();
    if whole.starts_with('-') {
        grouped.push('-');
    }
    for (index, ch) in digits.chars().enumerate() {
        if index > 0 && (digits.len() - index) % 3 == 0 {
            grouped.push(',');
        }
        grouped.push(ch);
    }
    if places > 0 {
        grouped.push('.');
        grouped.push_str(fraction);
    }
    Ok(format!("{grouped}%"))
}
fn fixed(value: &Cell<'_>, places: u32) -> String {
    if let Some(s) = value.as_str() {
        return decimal_fixed(s, places, false).unwrap_or_else(|_| "N/A".into());
    }
    numeric(value)
        .map(|n| format!("{n:.precision$}", precision = places as usize))
        .unwrap_or_else(|| "N/A".into())
}
fn cell(
    cells: &mut BTreeMap<String, OutlookCell>,
    key: &str,
    value: String,
    foreground: &str,
    background: &str,
) {
    cells.insert(
        key.into(),
        OutlookCell {
            value,
            foreground: foreground.into(),
            background: background.into(),
        },
    );
}
fn simple(cells: &mut BTreeMap<String, OutlookCell>, key: &str, value: String) {
    cell(cells, key, value, "LightText", "DarkSurface")
}
fn mood(value: Option<f64>, up: f64, down: f64) -> &'static str {
    match value {
        Some(n) if n > up => "Positive",
        Some(n) if n < down => "Negative",
        _ => "Caution",
    }
}
pub fn project(
    snapshot: &MarketOutlookReadModel,
    live: bool,
    now: DateTime<Utc>,
) -> Result<Outlook, String> {
    let mut cells = BTreeMap::new();
    let e = snapshot.futures_eod_data.as_ref();
    let trade = snapshot.futures_trade_signal.as_ref();
    if e.is_none() {
        return Err("Market outlook lacks EOD display state".into());
    }
    for (key, field) in [
        ("openPrice", "OpenPrice"),
        ("highPrice", "HighPrice"),
        ("lowPrice", "LowPrice"),
        ("closePrice", "ClosePrice"),
    ] {
        simple(&mut cells, key, fixed(&e.cell(field), 2));
    }
    cells.get_mut("closePrice").unwrap().foreground = "Caution".into();
    simple(&mut cells, "volume", text(&e.cell("Volume")));
    for (key, field, colors) in [
        (
            "marketDirection",
            "MarketDirection",
            &[
                ("Up", "Caution"),
                ("NeutralDown", "Warning"),
                ("Down", "Negative"),
            ][..],
        ),
        (
            "marketVolatility",
            "MarketVolatility",
            &[
                ("High", "Negative"),
                ("Low", "Caution"),
                ("Rising", "Warning"),
            ][..],
        ),
        (
            "priceDirection",
            "PriceDirection",
            &[
                ("Rising", "Positive"),
                ("RisingSlowly", "PositiveMuted"),
                ("Flat", "Caution"),
                ("FallingSlowly", "NegativeMuted"),
                ("Falling", "Negative"),
            ][..],
        ),
        (
            "priceVolatility",
            "PriceVolatility",
            &[("Rising", "Negative"), ("Flat", "Caution")][..],
        ),
    ] {
        let value = text(&e.cell(field));
        let color = colors
            .iter()
            .find(|(name, _)| *name == value)
            .map(|(_, color)| *color)
            .unwrap_or("Positive");
        cell(&mut cells, key, value, "DarkText", color);
    }
    let change = &e.cell("DailyPercentChange");
    let percent = change
        .as_str()
        .map(|s| decimal_fixed(s, 2, true).unwrap_or_else(|_| "N/A".into()))
        .unwrap_or_else(|| {
            numeric(change)
                .map(|n| format!("{:.2}%", n * 100.0))
                .unwrap_or_else(|| "N/A".into())
        });
    cell(
        &mut cells,
        "dailyPercentChange",
        percent,
        "DarkText",
        mood(numeric(change), 0.0, 0.0),
    );
    let bb = snapshot.futures_bb_signal.as_ref();
    for (key, field) in [
        ("dailyStdDev", "StandardDeviation20"),
        ("upperBand", "Upper20"),
        ("mean", "Ema20Center"),
        ("lowerBand", "Lower20"),
    ] {
        simple(&mut cells, key, fixed(&bb.cell(field), 2));
    }
    let mdi = &snapshot.cell("MdiPercent");
    let m = numeric(mdi);
    cell(
        &mut cells,
        "mdi",
        fixed(mdi, 2),
        "DarkText",
        match m {
            None => "Default",
            Some(n) if n >= 60.0 => "Positive",
            Some(n) if n >= 30.0 => "Caution",
            _ => "Negative",
        },
    );
    for key in ["vwap", "adx", "atr", "macd"] {
        cell(&mut cells, key, "N/A".into(), "LightText", "Default");
    }
    let vwap = snapshot.futures_vwap_signal.as_ref();
    if vwap.cell("IsWarm") == Cell::Boolean(true)
        && numeric(&vwap.cell("Vwap")).is_some_and(|n| n > 0.0)
    {
        let exact = vwap.cell("IsValid") == Cell::Boolean(true)
            && vwap.cell("IsTickExact") == Cell::Boolean(true);
        let value = format!(
            "{}{}",
            if exact { "" } else { "~" },
            fixed(&vwap.cell("Vwap"), 2)
        );
        let comparison = numeric(&vwap.cell("Vwap"))
            .zip(numeric(&e.cell("ClosePrice")))
            .map(|(a, b)| a - b);
        cell(
            &mut cells,
            "vwap",
            value,
            "DarkText",
            if !exact {
                "Caution"
            } else {
                match comparison {
                    Some(n) if n > 0.0 => "Negative",
                    Some(n) if n < 0.0 => "Positive",
                    _ => "Caution",
                }
            },
        );
    }
    let adx = snapshot.futures_adx_signal.as_ref();
    if adx.cell("IsWarm") == Cell::Boolean(true) {
        let plus = numeric(&adx.cell("PlusDI"));
        let minus = numeric(&adx.cell("MinusDI"));
        let color = if numeric(&adx.cell("AdxValue")).is_some_and(|v| v < 25.0) {
            "Caution"
        } else if plus > minus {
            "Positive"
        } else if minus > plus {
            "Negative"
        } else {
            "Caution"
        };
        cell(
            &mut cells,
            "adx",
            fixed(&adx.cell("AdxValue"), 2),
            "DarkText",
            color,
        );
    }
    let atr = snapshot.futures_atr_signal.as_ref();
    if atr.cell("IsWarm") == Cell::Boolean(true) {
        if let Some(ratio) = numeric(&atr.cell("AtrRatio")) {
            cell(
                &mut cells,
                "atr",
                fixed(&atr.cell("AtrValue"), 2),
                "DarkText",
                if !(0.60..=1.50).contains(&ratio) {
                    "Negative"
                } else if !(0.80..=1.25).contains(&ratio) {
                    "Caution"
                } else {
                    "Positive"
                },
            );
        }
    }
    let macd = snapshot.futures_macd_signal.as_ref();
    if macd.cell("IsWarm") == Cell::Boolean(true) {
        let value = numeric(&macd.cell("Histogram")).unwrap_or(0.0);
        cell(
            &mut cells,
            "macd",
            fixed(&macd.cell("Histogram"), 2),
            "DarkText",
            if value >= 2.0 {
                "Positive"
            } else if value <= -2.0 {
                "Negative"
            } else {
                "Caution"
            },
        );
    }
    simple(
        &mut cells,
        "contractId",
        if trade.is_none() {
            text(&snapshot.cell("ContractId"))
        } else {
            text(&trade.cell("ContractId"))
        },
    );
    let trend_color = match trade.cell("TrendType").as_str().unwrap_or("") {
        "UpTrend" | "UpTrending" => "Positive",
        "DownTrend" | "DownTrending" => "Negative",
        _ => "Caution",
    };
    cell(&mut cells, "trend", "N/A".into(), "DarkText", trend_color);
    // These colors intentionally follow the hybrid composite's base trade model;
    // replacing the displayed ITI trend does not silently alter its color rules.
    for (key, field, color) in [
        ("mdiDownLimit", "MDIDownTrendLimit", "Negative"),
        ("mdiUpLimit", "MDIUpTrendLimit", "Positive"),
    ] {
        cell(
            &mut cells,
            key,
            if trade.is_none() {
                "0.00".into()
            } else {
                fixed(&trade.cell(field), 2)
            },
            "DarkText",
            color,
        );
    }
    let rsi = snapshot.futures_rsi_signal.as_ref();
    cell(
        &mut cells,
        "rsi",
        fixed(&rsi.cell("RSI"), 2),
        "DarkText",
        if rsi.is_none() {
            "Default"
        } else {
            mood(numeric(&rsi.cell("RSI")), 60.0, 40.0)
        },
    );
    let classification = &snapshot.cell("MdiClassification");
    let mdi_color = match classification.as_str() {
        Some("UpTrending") => "Positive",
        Some("DownTrending") => "Negative",
        None => "Default",
        _ => "Caution",
    };
    cell(
        &mut cells,
        "mdiTrend",
        if classification.is_missing() {
            "N/A".into()
        } else {
            text(classification)
        },
        "DarkText",
        mdi_color,
    );
    if !classification.is_missing() {
        cells.get_mut("mdiUpLimit").unwrap().value = "60.00".into();
        cells.get_mut("mdiDownLimit").unwrap().value = "30.00".into();
    }
    let valid_eod = e.cell("ContractId").as_str().is_some_and(|v| !v.is_empty())
        && e.cell("ValueDate")
            .as_str()
            .is_some_and(|v| v > "0001-01-01");
    let risk = text(&trade.cell("TrendStrength"));
    let risk_color = match risk.as_str() {
        "High" => "Positive",
        "Medium" => "Caution",
        _ => "Negative",
    };
    cell(
        &mut cells,
        "riskPosition",
        if valid_eod {
            if trade.is_none() { "None".into() } else { risk }
        } else {
            "N/A".into()
        },
        "DarkText",
        risk_color,
    );
    let direction = snapshot.latest_iti_trend_signal.as_ref();
    if !direction.is_none() {
        cells.get_mut("trend").unwrap().value = match direction.cell("IntrinsicTimeTrend").as_str()
        {
            Some("UpTrend") => "UpTrending",
            Some("DownTrend") => "DownTrending",
            _ => "RangeBound",
        }
        .into();
    }
    for (key, field) in [
        ("upTrendLimit", "UpTrendTrigger"),
        ("downLimitTrigger", "DownTrendTrigger"),
        ("tradeEntry", "IntrinsicPrice"),
        ("trendDelta", "TrendDelta"),
        ("trendExtreme", "TrendExtreme"),
        ("trendReversal", "TrendReversal"),
    ] {
        simple(&mut cells, key, fixed(&direction.cell(field), 2));
    }
    simple(
        &mut cells,
        "tradeExit",
        if direction.is_none() {
            "N/A".into()
        } else if trade.is_none() {
            "0.00".into()
        } else {
            fixed(&trade.cell("ExitTrigger"), 2)
        },
    );
    let ema = snapshot.futures_ema_signal.as_ref();
    simple(&mut cells, "fiftyDMA", fixed(&ema.cell("Ema50"), 2));
    simple(
        &mut cells,
        "twoHundredDMA",
        if ema.cell("IsWarm") == Cell::Boolean(true) {
            fixed(&ema.cell("Ema200"), 2)
        } else {
            "N/A".into()
        },
    );
    let tdi = snapshot.futures_tdi_signal.as_ref();
    for (key, field) in [
        ("tdiDirection", "TDI"),
        ("tdiStrength", "TDIStrength"),
        ("tdiMarketState", "MarketState"),
        ("tdiCross", "Cross"),
    ] {
        simple(
            &mut cells,
            key,
            if tdi.is_none() {
                if key == "tdiDirection" {
                    "Warming".into()
                } else {
                    "N/A".into()
                }
            } else {
                text(&tdi.cell(field))
            },
        );
    }
    simple(
        &mut cells,
        "tdiDivergence",
        fixed(&tdi.cell("PriceSignalDivergence"), 2),
    );
    let updated = DateTime::parse_from_rfc3339(
        snapshot
            .cell("UpdatedAtUtc")
            .as_str()
            .ok_or("Missing outlook observation time")?,
    )
    .map_err(|_| "Invalid outlook observation time")?
    .with_timezone(&Utc);
    let freshness = if live && now - updated > chrono::Duration::minutes(5) {
        "STALE"
    } else if live {
        "live"
    } else {
        "current"
    };
    let missing = text(&snapshot.cell("MissingInputs"));
    let completeness = if missing.trim().is_empty() {
        "complete".into()
    } else {
        format!("warming/degraded: {missing}")
    };
    let baseline = ema
        .and_then(|value| value.baseline_value_date.as_ref())
        .or(bb.and_then(|value| value.baseline_value_date.as_ref()))
        .or_else(|| {
            ema.filter(|value| value.is_warm == Some(true))
                .and_then(|value| value.metadata.as_ref())
                .and_then(|value| value.value_date.as_ref())
        })
        .map(|date| date.as_str());
    let baseline = baseline
        .map(|d| format!("EMA/BB baseline {d}"))
        .unwrap_or("EMA/BB baseline unavailable".into());
    Ok(Outlook {
        status: format!(
            "Market Outlook {} | {} | {freshness} | {completeness} | {baseline}",
            text(&snapshot.cell("RefreshTrigger")),
            text(&snapshot.cell("ValueDate"))
        ),
        summary: String::new(),
        cells,
    })
}
pub async fn load(backend: &Backend, registry: &Registry) -> Result<Outlook, String> {
    tokio::time::timeout(backend.default_timeout(), async {
        let context = BusinessContext::load(backend).await?;
        let contract = market_history::current_contract(backend, registry, "ES").await?;
        let root = "TomasAI.IFM.Domain.MarketData.Analytics.Shared.";
        let wire = actor_requests::query(
            backend,
            registry,
            &format!("{root}Queries.GetMarketOutlookSnapshotQuery"),
            &format!("{contract}.{}", context.value_date.format("%Y%m%d")),
            BTreeMap::from([
                ("ContractId".into(), contract.clone().into()),
                ("ValueDate".into(), date_only_wire(context.value_date)),
            ]),
        )
        .await?;
        let snapshot = MarketOutlookReadModel::from_backend(&wire)?;
        let mut outlook = project(&snapshot, context.market_state == 2, context.as_of_utc)?;
        if snapshot.cell("ValueDate").as_str() != Some(context.value_date.to_string().as_str())
            || snapshot.contract_id.as_deref() != Some(contract.as_str())
        {
            return Err(
                "Market outlook does not match the API session and current contract".into(),
            );
        }
        outlook.summary = format!(
            "{contract} | {} | {}",
            context.value_date,
            match context.market_state {
                0 => "Closed",
                1 => "OffTrading",
                2 => "LiveTrading",
                _ => "Unknown",
            }
        );
        Ok(outlook)
    })
    .await
    .map_err(|_| "Market outlook refresh deadline expired".to_string())?
}
