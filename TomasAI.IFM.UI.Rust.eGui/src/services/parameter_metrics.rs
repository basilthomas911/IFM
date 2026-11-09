//! Presentation preparation for the Regime Discovery editor. This mirrors its
//! compatibility selection mapping; signal calculation and validation stay on API.
use crate::backend::schema::Registry;
use serde_json::{Value as Json, json};
use sha2::{Digest, Sha256};
use std::collections::BTreeMap;
const FRAME: &str = "TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType";
const SIGNAL: &str = "TomasAI.IFM.Domain.MarketData.Shared.SignalMetricsType";
const OBSERVATION: &str = "TomasAI.IFM.Domain.MarketData.Shared.ObservationMetricsType";
const LEGACY: &str =
    "TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery.RegimeDiscoverySignalMetric";
pub const ROOT: &str = "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery";


/// Returns the display text for the requested value or field.
fn text<'a>(row: &'a Json, key: &str) -> &'a str {
    row[key].as_str().unwrap_or_default()
}


/// Provides stable for the parameter metrics implementation.
fn stable(value: &str) -> String {
    let hash = Sha256::digest(value.as_bytes());
    uuid::Uuid::from_bytes_le(hash[..16].try_into().unwrap()).to_string()
}


/// Sets n for parameter metrics.
fn set_n(set: &str) -> Result<String, String> {
    uuid::Uuid::parse_str(set)
        .map(|v| v.simple().to_string())
        .map_err(|_| "Invalid metric parameter set identity".into())
}


/// Provides duration for the parameter metrics implementation.
fn duration(frame: &str) -> Option<u32> {
    Some(match frame {
        "FifteenSeconds" => 15,
        "OneMinute" => 60,
        "FiveMinutes" => 300,
        "FifteenMinutes" => 900,
        "ThirtyMinutes" => 1800,
        "OneHour" => 3600,
        "FourHours" => 14400,
        "Daily" => 86400,
        _ => return None,
    })
}


/// Provides legacy for the parameter metrics implementation.
pub fn legacy(
    registry: &Registry,
    set: &str,
    signals: &[Json],
    observations: &[Json],
    horizon: &Json,
) -> Result<Vec<Json>, String> {
    let required = horizon["TimeFrames"]
        .as_array()
        .ok_or("Missing horizon intervals")?
        .iter()
        .filter(|v| v["IsRequired"] == true)
        .map(|v| text(v, "TimeFrame"))
        .collect::<Vec<_>>();
    if required.is_empty() || required.iter().any(|v| duration(v).is_none()) {
        return Err("At least one supported required timeframe is needed".into());
    }
    let target = required.into_iter().max_by_key(|v| duration(v)).unwrap();
    let set = set_n(set)?;
    let mut rows = BTreeMap::new();
    let mut add = |metric: &str, frame: &str, row: &Json| -> Result<(), String> {
        let order = (
            registry.enum_value(FRAME, frame)?,
            registry.enum_value(LEGACY, metric)?,
        );
        rows.entry(order).or_insert_with(||json!({"RequirementId":stable(&format!("{set}:{metric}:{frame}")),"Metric":metric,"TimeFrame":frame,"Enabled":row["Enabled"],"IsRequired":true,"MaximumAgeSeconds":row["MaximumAgeSeconds"],"CalculationConfigurationId":format!("{metric}.v1"),"PrepareAtStartup":row["PrepareAtStartup"],"Monitor":row["Monitor"]}));
        Ok(())
    };
    for row in signals {
        let frame = text(row, "TimeFrame");
        let metric = text(row, "Metric");
        let period = row["PeriodLength"]
            .as_i64()
            .ok_or("Invalid metric period")?;
        let mappings: &[&str] = match (metric, period) {
            ("Ema", 20) => &["Ema20", "Ema20Slope"],
            ("Ema", 50) => &["Ema50", "Ema50Slope"],
            ("Ema", 200) => &["Ema200", "Ema200Slope"],
            ("Rsi", 14) => &["Rsi14", "Rsi14Slope"],
            ("Adx", 14) => &["Adx14", "PlusDi14", "MinusDi14"],
            ("Macd", 26) => &["MacdHistogram"],
            ("Atr", 14) if frame == target => &["Atr14", "AtrBaselineRatio"],
            ("Atr", 14) => &["Atr14"],
            ("BollingerBand", 20) => {
                &["BollingerWidth", "BollingerWidthRatio", "BollingerPosition"]
            }
            ("MarketStructure", 20) => &[
                "Ema20Interaction",
                "AtrNormalizedRange",
                "RollingHigh20",
                "RollingLow20",
                "BreakoutDistanceAtr",
            ],
            ("Tdi", 13) => &["Tdi"],
            _ => {
                return Err(format!(
                    "{metric} period {period} is not supported by the current evaluator"
                ));
            }
        };
        for metric in mappings {
            add(metric, frame, row)?;
        }
    }
    for row in observations {
        if row["Metric"] == "VxTermStructure" {
            add("VxFrontSecondRatio", "Daily", row)?;
        } else if row["Enabled"] == true {
            return Err(format!(
                "{} is not consumed by Regime Discovery",
                text(row, "Metric")
            ));
        }
    }
    let mut normalized = normalize_ema_baselines(&rows.into_values().collect::<Vec<_>>());
    normalized.sort_by_key(|row| (
        registry.enum_value(FRAME, text(row, "TimeFrame")).unwrap_or_default(),
        registry.enum_value(LEGACY, text(row, "Metric")).unwrap_or_default(),
    ));
    Ok(normalized)
}


/// Mirrors the server's Daily EMA baseline mapping and duplicate merge policy.
fn normalize_ema_baselines(rows: &[Json]) -> Vec<Json> {
    let mut normalized: Vec<Json> = Vec::new();
    for source in rows {
        let mut row = source.clone();
        if matches!(text(&row, "Metric"), "Ema20" | "Ema50" | "Ema200" | "Ema20Slope" | "Ema50Slope" | "Ema200Slope")
            && row["TimeFrame"] != "Daily"
        {
            row["TimeFrame"] = json!("Daily");
            row["MaximumAgeSeconds"] = json!(345600);
        }
        if let Some(existing) = normalized.iter_mut().find(|value|
            value["Metric"] == row["Metric"] && value["TimeFrame"] == row["TimeFrame"])
        {
            existing["Enabled"] = json!(existing["Enabled"] == true || row["Enabled"] == true);
            existing["IsRequired"] = json!(existing["IsRequired"] == true || row["IsRequired"] == true);
            existing["MaximumAgeSeconds"] = json!(existing["MaximumAgeSeconds"].as_i64().unwrap_or_default()
                .max(row["MaximumAgeSeconds"].as_i64().unwrap_or_default()));
        } else {
            normalized.push(row);
        }
    }
    normalized
}


/// Provides definition for the parameter metrics implementation.
fn definition(metric: &str) -> Option<(&'static str, i64)> {
    Some(match metric {
        "Ema20" | "Ema20Slope" => ("Ema", 20),
        "Ema50" | "Ema50Slope" => ("Ema", 50),
        "Ema200" | "Ema200Slope" => ("Ema", 200),
        "Rsi14" | "Rsi14Slope" => ("Rsi", 14),
        "Adx14" | "PlusDi14" | "MinusDi14" => ("Adx", 14),
        "MacdHistogram" => ("Macd", 26),
        "Atr14" | "AtrBaselineRatio" => ("Atr", 14),
        "BollingerWidth" | "BollingerWidthRatio" | "BollingerPosition" => ("BollingerBand", 20),
        "Ema20Interaction"
        | "AtrNormalizedRange"
        | "RollingHigh20"
        | "RollingLow20"
        | "BreakoutDistanceAtr" => ("MarketStructure", 20),
        "Tdi" => ("Tdi", 13),
        _ => return None,
    })
}


/// Provides from legacy for the parameter metrics implementation.
pub fn from_legacy(
    registry: &Registry,
    set: &str,
    rows: &[Json],
) -> Result<(Vec<Json>, Vec<Json>), String> {
    let set = set_n(set)?;
    let mut groups: BTreeMap<(i64, i64, i64), Vec<&Json>> = BTreeMap::new();
    let normalized = normalize_ema_baselines(rows);
    for row in &normalized {
        if let Some((metric, period)) = definition(text(row, "Metric")) {
            groups
                .entry((
                    registry.enum_value(FRAME, text(row, "TimeFrame"))?,
                    registry.enum_value(SIGNAL, metric)?,
                    period,
                ))
                .or_default()
                .push(row);
        }
    }
    let names = |kind: &str, value: i64| -> Result<String, String> {
        registry
            .enum_names(kind)?
            .into_iter()
            .find(|name| registry.enum_value(kind, name) == Ok(value))
            .ok_or("Unknown metric enumeration".into())
    };
    let mut signals = vec![];
    for ((frame, metric, period), rows) in groups {
        let frame = names(FRAME, frame)?;
        let metric = names(SIGNAL, metric)?;
        signals.push(json!({"RequirementId":stable(&format!("{set}:signal:{metric}:{frame}:{period}")),"Metric":metric,"TimeFrame":frame,"PeriodLength":period,"Enabled":rows.iter().any(|v|v["Enabled"]==true),"MaximumAgeSeconds":rows.iter().filter_map(|v|v["MaximumAgeSeconds"].as_i64()).max().unwrap_or_default(),"PrepareAtStartup":rows.iter().any(|v|v["PrepareAtStartup"]==true),"Monitor":rows.iter().any(|v|v["Monitor"]==true)}));
    }
    let vx = rows
        .iter()
        .filter(|v| matches!(text(v, "Metric"), "VxFrontSecondRatio" | "VxFrontLevel"))
        .collect::<Vec<_>>();
    registry.enum_value(OBSERVATION, "VxTermStructure")?;
    let observations = vec![
        json!({"RequirementId":stable(&format!("{set}:observation:VxTermStructure")),"Metric":"VxTermStructure","Enabled":vx.iter().any(|v|v["Enabled"]==true),"MaximumAgeSeconds":vx.iter().filter_map(|v|v["MaximumAgeSeconds"].as_i64()).max().unwrap_or(345600),"PrepareAtStartup":vx.is_empty()||vx.iter().any(|v|v["PrepareAtStartup"]==true),"Monitor":vx.is_empty()||vx.iter().any(|v|v["Monitor"]==true)}),
    ];
    Ok((signals, observations))
}
