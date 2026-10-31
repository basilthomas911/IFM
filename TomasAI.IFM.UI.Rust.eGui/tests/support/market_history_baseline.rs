//! Frozen pre-refactor market-history projection, for parity and timings only.
use chrono::{DateTime, NaiveDate, Utc};
use ifm_ui_egui::market_charts::{ChartSeries, ChartSnapshot};
use serde_json::Value as Json;
use std::collections::BTreeMap;
pub fn market_seconds(time: DateTime<Utc>) -> f64 {
    time.with_timezone(&chrono_tz::America::New_York)
        .naive_local()
        .and_utc()
        .timestamp_millis() as f64
        / 1000.0
}
fn time(value: &Json) -> Result<DateTime<Utc>, String> {
    let text = value.as_str().ok_or("Missing history timestamp")?;
    DateTime::parse_from_rfc3339(text)
        .map(|t| t.with_timezone(&Utc))
        .map_err(|_| "Invalid history timestamp".into())
}
fn day(value: &Json) -> Result<NaiveDate, String> {
    NaiveDate::parse_from_str(value.as_str().ok_or("Missing history date")?, "%Y-%m-%d")
        .map_err(|_| "Invalid history date".into())
}
fn number(value: &Json) -> Result<f64, String> {
    value
        .as_f64()
        .or_else(|| value.as_str().and_then(|s| s.parse().ok()))
        .filter(|v| v.is_finite())
        .ok_or("Invalid plotted price".into())
}
fn rows(value: &Json) -> Result<&Vec<Json>, String> {
    let values = value.as_array().ok_or("History reply is not an array")?;
    if values.len() > 100_000 {
        return Err("History exceeds the display bound".into());
    }
    Ok(values)
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
    source: &Json,
) -> Result<ChartSnapshot, String> {
    if start > end {
        return Err("Invalid history window".into());
    }
    let mut bars = Vec::new();
    for row in rows(source)? {
        if row["BarRateType"] != "FifteenSeconds" {
            continue;
        }
        let stamp = time(&row["BarDate"])?;
        if stamp < start || stamp > end {
            continue;
        }
        bars.push((stamp, number(&row["BarValue"])?, row));
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
            let up = number(&last.2["UpTrendTrigger"])?;
            let down = number(&last.2["DownTrendTrigger"])?;
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
pub fn bands(date: NaiveDate, source: &Json) -> Result<ChartSnapshot, String> {
    let mut selected: BTreeMap<NaiveDate, (DateTime<Utc>, &Json)> = BTreeMap::new();
    for row in rows(source)? {
        let metadata = &row["Metadata"];
        // TimeFrame is an ignored C# convenience property; its serialized owner
        // is SignalKey, not Metadata itself.
        if metadata["SignalKey"]["TimeFrame"] != "Daily" || metadata["IsValid"] != true {
            continue;
        }
        let value_date = day(&metadata["ValueDate"])?;
        if value_date > date {
            continue;
        }
        let observed = time(&metadata["MarketDataAsOfUtc"])?;
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
            if row[key].is_null() && key != "Price" {
                continue;
            }
            let price = number(&row[key])?;
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
