//! Read-only market history adapters. The API owns valuation, indicators and the
//! clock. Floating point conversion here is only for plotting published prices.
use crate::models::{
    cells::{Cell, Cells},
    trade_contracts::{FuturesBarDataReadModel, FuturesBbSignalReadModel, MarketOutlookReadModel},
    wire_types::FromBackend,
};
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    market_charts::{ChartSeries, ChartSnapshot},
    services::{actor_requests, business_context::BusinessContext},
};
use chrono::{DateTime, NaiveDate, Utc};
use std::collections::BTreeMap;

/// The copied charts label Unix coordinates directly. Match the hybrid's Eastern
/// wall-clock coordinate, including the offset at each observation (DST aware).
pub fn market_seconds(time: DateTime<Utc>) -> f64 {
    time.with_timezone(&chrono_tz::America::New_York)
        .naive_local()
        .and_utc()
        .timestamp_millis() as f64
        / 1000.0
}
fn time(value: &Cell<'_>) -> Result<DateTime<Utc>, String> {
    let text = value.as_str().ok_or("Missing history timestamp")?;
    DateTime::parse_from_rfc3339(text)
        .map(|t| t.with_timezone(&Utc))
        .map_err(|_| "Invalid history timestamp".into())
}
fn day(value: &Cell<'_>) -> Result<NaiveDate, String> {
    NaiveDate::parse_from_str(value.as_str().ok_or("Missing history date")?, "%Y-%m-%d")
        .map_err(|_| "Invalid history date".into())
}
fn number(value: &Cell<'_>) -> Result<f64, String> {
    value.number().ok_or("Invalid plotted price".into())
}
fn rows<T>(source: &[T]) -> Result<&[T], String> {
    if source.len() > 100_000 {
        Err("History exceeds the display bound".into())
    } else {
        Ok(source)
    }
}
fn series(name: &str, color: &str, points: Vec<[f64; 2]>) -> ChartSeries {
    ChartSeries {
        name: name.into(),
        color: color.into(),
        points,
    }
}

/// Reproduce the existing display projection without deriving bars or triggers.
pub fn bars(
    symbol: &str,
    start: DateTime<Utc>,
    end: DateTime<Utc>,
    source: &[FuturesBarDataReadModel],
) -> Result<ChartSnapshot, String> {
    if start > end {
        return Err("Invalid history window".into());
    }
    let mut bars = Vec::new();
    for row in rows(source)? {
        if row
            .bar_rate_type
            .as_ref()
            .map(|value| value.label())
            .as_deref()
            != Some("FifteenSeconds")
        {
            continue;
        }
        let stamp = time(&row.cell("BarDate"))?;
        if stamp < start || stamp > end {
            continue;
        }
        bars.push((stamp, number(&row.cell("BarValue"))?, row));
    }
    bars.sort_by_key(|b| b.0);
    let mut points: Vec<_> = bars.iter().map(|b| [market_seconds(b.0), b.1]).collect();
    if bars.len() == 1 && bars[0].0 < end {
        points.push([market_seconds(end), bars[0].1]);
    }
    let mut minimum = bars.iter().map(|b| b.1).reduce(f64::min);
    let mut maximum = bars.iter().map(|b| b.1).reduce(f64::max);
    let mut output = vec![series(
        if symbol == "ES" { "ES Close" } else { "VX" },
        if symbol == "ES" { "Yellow" } else { "Fuchsia" },
        points.clone(),
    )];
    if symbol == "ES" {
        if let Some(last) = bars.last() {
            let up = number(&last.2.cell("UpTrendTrigger"))?;
            let down = number(&last.2.cell("DownTrendTrigger"))?;
            output.push(series(
                "Upper Trigger",
                "Lime",
                points.iter().map(|p| [p[0], up]).collect(),
            ));
            output.push(series(
                "Lower Trigger",
                "Red",
                points.iter().map(|p| [p[0], down]).collect(),
            ));
            if up > 0.0 {
                maximum = maximum.map(|v| v.max(up));
            }
            if down > 0.0 {
                minimum = minimum.map(|v| v.min(down));
            }
        }
    }
    let padding = if symbol == "ES" { 5.0 } else { 0.025 };
    Ok(ChartSnapshot {
        status: if bars.is_empty() {
            "No saved bars in the last six hours".into()
        } else {
            String::new()
        },
        start: market_seconds(start),
        end: market_seconds(end),
        minimum: minimum.map(|v| v - padding),
        maximum: maximum.map(|v| v + padding),
        series: output,
    })
}

/// Select the newest valid Daily observation per date. Nullable band values stay
/// absent; a warming indicator must never be rendered as an invented zero.
pub fn bands(
    date: NaiveDate,
    source: &[FuturesBbSignalReadModel],
) -> Result<ChartSnapshot, String> {
    let mut selected: BTreeMap<NaiveDate, (DateTime<Utc>, &FuturesBbSignalReadModel)> =
        BTreeMap::new();
    for row in rows(source)? {
        let metadata = row.metadata.as_ref();
        // TimeFrame is an ignored C# convenience property; its serialized owner
        // is SignalKey, not Metadata itself.
        if metadata
            .and_then(|value| value.signal_key.as_ref())
            .and_then(|value| value.time_frame.as_ref())
            .map(|value| value.label())
            .as_deref()
            != Some("Daily")
            || metadata.and_then(|value| value.is_valid) != Some(true)
        {
            continue;
        }
        let value_date = day(&metadata.cell("ValueDate"))?;
        if value_date > date {
            continue;
        }
        let observed = time(&metadata.cell("MarketDataAsOfUtc"))?;
        if selected.get(&value_date).is_none_or(|old| observed > old.0) {
            selected.insert(value_date, (observed, row));
        }
    }
    let selected: Vec<_> = selected
        .iter()
        .skip(selected.len().saturating_sub(40))
        .collect();
    let coordinate = |d: NaiveDate| d.and_hms_opt(0, 0, 0).unwrap().and_utc().timestamp() as f64;
    let mut output = Vec::new();
    let mut values = Vec::new();
    for (key, name, color) in [
        ("Price", "ES Close", "Yellow"),
        ("Ema20Center", "20 EMA", "Blue"),
        ("Upper20", "Upper Band", "Green"),
        ("Lower20", "Lower Band", "Red"),
    ] {
        let mut points = Vec::new();
        for (day, (_, row)) in &selected {
            if row.cell(key).is_missing() && key != "Price" {
                continue;
            }
            let price = number(&row.cell(key))?;
            values.push(price);
            points.push([coordinate(**day), price]);
        }
        output.push(series(name, color, points));
    }
    let minimum = values.iter().copied().reduce(f64::min);
    let maximum = values.iter().copied().reduce(f64::max);
    let padding = minimum
        .zip(maximum)
        .map(|(lo, hi)| ((hi - lo) * 0.05).max(1.0))
        .unwrap_or(1.0);
    let first = selected.first().map(|(d, _)| **d).unwrap_or(
        date.checked_sub_signed(chrono::Duration::days(39))
            .ok_or("History date underflow")?,
    );
    let start = coordinate(first);
    Ok(ChartSnapshot {
        status: if selected.is_empty() {
            "No daily Bollinger observations".into()
        } else {
            String::new()
        },
        start,
        end: (start + 86400.0).max(coordinate(date)),
        minimum: minimum.map(|v| v - padding),
        maximum: maximum.map(|v| v + padding),
        series: output,
    })
}

/// Failed refreshes retain the last useful series and visibly mark them stale.
/// This is display continuity, never a substitute for feed health or trade data.
pub fn retain_previous(
    previous: &BTreeMap<String, ChartSnapshot>,
    next: &mut BTreeMap<String, ChartSnapshot>,
) {
    for (key, current) in next.iter_mut() {
        if !current.status.is_empty() && !current.series.iter().any(|s| !s.points.is_empty()) {
            if let Some(saved) = previous
                .get(key)
                .filter(|s| s.series.iter().any(|s| !s.points.is_empty()))
            {
                let status = format!(
                    "Reconnecting: {}",
                    current.status.trim_start_matches("Reconnecting: ")
                );
                *current = saved.clone();
                current.status = status;
            }
        }
    }
}

pub async fn current_contract(
    backend: &Backend,
    registry: &Registry,
    symbol: &str,
) -> Result<String, String> {
    let wire = actor_requests::query(
        backend,
        registry,
        "TomasAI.IFM.Domain.MarketData.Shared.Queries.GetOnTheRunFuturesContractQuery",
        symbol,
        BTreeMap::from([("Symbol".into(), symbol.into())]),
    )
    .await?;
    let value = super::strategy::member(
        registry,
        "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel",
        &wire,
        "ContractId",
    )?;
    value
        .as_str()
        .filter(|value| !value.is_empty())
        .map(str::to_owned)
        .ok_or("Current futures contract unavailable".into())
}
async fn load_bars(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    symbol: &str,
) -> Result<ChartSnapshot, String> {
    let contract = current_contract(backend, registry, symbol).await?;
    let end = context.as_of_utc;
    let start = end - chrono::Duration::hours(6);
    let entity = format!(
        "{contract}.{symbol}.{}.{}.{}",
        context.value_date,
        start.format("%Y-%m-%dT%H:%M:%S%.3f"),
        end.format("%Y-%m-%dT%H:%M:%S%.3f")
    );
    let wire = actor_requests::query(
        backend,
        registry,
        "TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetFuturesBarDataQuery",
        &entity,
        BTreeMap::from([
            ("ContractId".into(), contract.into()),
            ("Symbol".into(), symbol.into()),
            ("ValueDate".into(), date_only_wire(context.value_date)),
            ("StartDate".into(), timestamp_wire(start)),
            ("EndDate".into(), timestamp_wire(end)),
            ("AcrossValueDates".into(), true.into()),
        ]),
    )
    .await?;
    bars(
        symbol,
        start,
        end,
        &Vec::<FuturesBarDataReadModel>::from_backend(&wire)?,
    )
}
async fn load_bands(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
) -> Result<ChartSnapshot, String> {
    let root = "TomasAI.IFM.Domain.MarketData.Analytics.Shared.";
    let wire = actor_requests::query(
        backend,
        registry,
        &format!("{root}Queries.GetFuturesBollingerBandHistoryQuery"),
        &format!("ES.{}.40", context.value_date.format("%Y%m%d")),
        BTreeMap::from([
            ("RootSymbol".into(), "ES".into()),
            ("ValueDate".into(), date_only_wire(context.value_date)),
            ("MaxDays".into(), 40.into()),
        ]),
    )
    .await?;
    let mut source = Vec::<FuturesBbSignalReadModel>::from_backend(&wire)?;
    // The current provisional Daily point is provided by Analytics, never
    // recomputed in the frontend. Failure to fetch it keeps committed history.
    if let Ok(contract) = current_contract(backend, registry, "ES").await {
        if let Ok(outlook) = actor_requests::query(
            backend,
            registry,
            &format!("{root}Queries.GetMarketOutlookSnapshotQuery"),
            &format!("{contract}.{}", context.value_date.format("%Y%m%d")),
            BTreeMap::from([
                ("ContractId".into(), contract.into()),
                ("ValueDate".into(), date_only_wire(context.value_date)),
            ]),
        )
        .await
        {
            let value = MarketOutlookReadModel::from_backend(&outlook)?;
            if let Some(value) = value.futures_bb_signal {
                source.push(value);
            }
        }
    }
    bands(context.value_date, &source)
}
/// One absolute API-derived refresh window, with independent ES/VX/band results.
/// A single unavailable dataset cannot suppress the other two charts.
pub async fn load(
    backend: &Backend,
    registry: &Registry,
) -> Result<BTreeMap<String, ChartSnapshot>, String> {
    let deadline = tokio::time::Instant::now() + backend.default_timeout();
    let context = tokio::time::timeout_at(deadline, BusinessContext::load(backend))
        .await
        .map_err(|_| "Market history session deadline expired")??;
    // Bound each result by the same deadline while retaining siblings that have
    // already completed. A hung VX read must not discard a successful ES read.
    let results = tokio::join!(
        tokio::time::timeout_at(deadline, load_bars(backend, registry, &context, "ES")),
        tokio::time::timeout_at(deadline, load_bars(backend, registry, &context, "VX")),
        tokio::time::timeout_at(deadline, load_bands(backend, registry, &context))
    );
    let bounded = |result: Result<Result<ChartSnapshot, String>, tokio::time::error::Elapsed>| {
        result.unwrap_or_else(|_| Err("Market history refresh deadline expired".into()))
    };
    let results = (bounded(results.0), bounded(results.1), bounded(results.2));
    Ok([("ES", results.0), ("VX", results.1), ("ES-BB", results.2)]
        .into_iter()
        .map(|(key, value)| {
            (
                key.into(),
                value.unwrap_or_else(|error| ChartSnapshot {
                    status: error,
                    ..Default::default()
                }),
            )
        })
        .collect())
}
