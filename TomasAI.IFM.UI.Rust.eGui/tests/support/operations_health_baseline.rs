//! Frozen pre-refactor projection, used only for timing and parity.
use chrono::{DateTime, Utc};
use ifm_ui_egui::contract_editor::{ContractResult, ContractRow};
use serde_json::Value;
use std::collections::BTreeMap;
fn scalar(value: &Value) -> String {
    match value {
        Value::Null => String::new(),
        Value::String(s) => s.clone(),
        Value::Bool(b) => if *b { "True" } else { "False" }.into(),
        _ => value.to_string(),
    }
}
fn utc(value: &Value) -> String {
    value
        .as_str()
        .and_then(|s| DateTime::parse_from_rfc3339(s).ok())
        .map(|d| d.to_utc().format("%Y-%m-%d %H:%M:%S UTC").to_string())
        .unwrap_or_else(|| "Unknown".into())
}
fn span(value: &Value) -> String {
    let Some(text) = value.as_str() else {
        return "Unknown".into();
    };
    let negative = text.starts_with('-');
    let text = text.strip_prefix('-').unwrap_or(text);
    let parts: Vec<_> = text.split(':').collect();
    if parts.len() != 3 {
        return "Unknown".into();
    }
    let (days, hours) = parts[0]
        .split_once('.')
        .map(|(d, h)| (d.parse::<f64>().ok(), h.parse::<f64>().ok()))
        .unwrap_or((Some(0.), parts[0].parse::<f64>().ok()));
    let Some(seconds) = days
        .zip(hours)
        .zip(parts[1].parse::<f64>().ok())
        .zip(parts[2].parse::<f64>().ok())
        .map(|(((d, h), m), s)| d * 86400. + h * 3600. + m * 60. + s)
    else {
        return "Unknown".into();
    };
    let seconds = if negative { -seconds } else { seconds };
    if !seconds.is_finite() {
        return "Unknown".into();
    }
    if seconds >= 60. {
        format!("{:.1} min", seconds / 60.)
    } else if seconds >= 1. {
        format!("{seconds:.1} s")
    } else {
        format!("{:.1} ms", seconds * 1000.)
    }
}
pub fn project(value: &Value, as_of: DateTime<Utc>) -> Result<ContractResult, String> {
    let valid_status = |v: &Value| {
        matches!(
            v.as_str(),
            Some("Inactive" | "Green" | "Yellow" | "Orange" | "Red")
        )
    };
    let stages = value["stages"].as_array().ok_or("Missing health stages")?;
    let datasets = value["datasets"]
        .as_array()
        .ok_or("Missing dataset health")?;
    let observed = value["observedOnUtc"]
        .as_str()
        .and_then(|s| DateTime::parse_from_rfc3339(s).ok())
        .ok_or("Missing central observation time")?
        .to_utc();
    let bounded =
        |v: &Value, max: usize| v.as_str().is_some_and(|s| s.encode_utf16().count() <= max);
    if value["schemaVersion"].as_i64().unwrap_or(1) != 1
        || stages.len() > 512
        || datasets.len() > 16
        || !valid_status(&value["overallStatus"])
        || stages.iter().any(|s| {
            !valid_status(&s["status"])
                || !bounded(&s["stage"], 128)
                || !bounded(&s["reasonCode"], 128)
                || !bounded(&s["reason"], 4096)
        })
        || datasets.iter().any(|d| {
            !valid_status(&d["status"])
                || !bounded(&d["dataset"], 64)
                || !bounded(&d["reason"], 4096)
        })
    {
        return Err("Operations health is incomplete or outside its bounds".into());
    }
    if as_of
        .signed_duration_since(observed)
        .num_milliseconds()
        .unsigned_abs()
        > 30000
    {
        return Err("Central operations observation is stale or its clock is invalid; current health is unknown".into());
    }
    let mut rows = Vec::new();
    for stage in stages {
        let mut fields = BTreeMap::from([
            ("kind".into(), "stage".into()),
            (
                "Requirement".into(),
                if stage["required"] == true {
                    "Required"
                } else {
                    "Optional"
                }
                .into(),
            ),
        ]);
        for (source, target) in [
            ("stage", "Stage"),
            ("status", "Status"),
            ("pending", "Pending"),
            ("capacity", "Capacity"),
            ("highWater", "HighWater"),
            ("received", "Received"),
            ("completed", "Completed"),
            ("failed", "Failed"),
            ("coalesced", "Coalesced"),
            ("saturated", "Saturated"),
            ("reasonCode", "ReasonCode"),
            ("reason", "Reason"),
        ] {
            fields.insert(target.into(), scalar(&stage[source]));
        }
        for (source, target) in [
            ("marketDataAsOfUtc", "MarketDataAsOfUtc"),
            ("lastSucceededUtc", "LastSucceededUtc"),
        ] {
            fields.insert(target.into(), utc(&stage[source]));
        }
        for (source, target) in [
            ("oldestPendingAge", "OldestPending"),
            ("p50Latency", "P50Latency"),
            ("p95Latency", "P95Latency"),
            ("p99Latency", "P99Latency"),
        ] {
            fields.insert(target.into(), span(&stage[source]));
        }
        let age = stage["marketDataAsOfUtc"]
            .as_str()
            .and_then(|s| DateTime::parse_from_rfc3339(s).ok())
            .map(|d| {
                let delta = observed.signed_duration_since(d.to_utc());
                let ms = delta.num_milliseconds();
                if delta < chrono::Duration::zero() {
                    "Clock ahead".into()
                } else if ms >= 60000 {
                    format!("{:.1} min", ms as f64 / 60000.)
                } else if ms >= 1000 {
                    format!("{:.1} s", ms as f64 / 1000.)
                } else {
                    format!("{:.1} ms", ms as f64)
                }
            })
            .unwrap_or_else(|| "Unknown".into());
        fields.insert("SourceAge".into(), age);
        rows.push(ContractRow {
            id: format!("stage:{}", scalar(&stage["stage"])),
            fields,
            ..Default::default()
        });
    }
    for dataset in datasets {
        let mut fields = BTreeMap::from([("kind".into(), "dataset".into())]);
        for (source, target) in [
            ("dataset", "Dataset"),
            ("status", "Status"),
            ("sessionState", "Session"),
            ("processId", "ProcessId"),
            ("generationId", "Generation"),
            ("running", "Running"),
            ("healthy", "Healthy"),
            ("cooperativeAttempts", "CooperativeAttempts"),
            ("processReplacementCount", "ProcessReplacements"),
            ("processReplacementLatched", "ReplacementLatched"),
            ("reason", "Reason"),
            ("workerInstanceId", "WorkerInstance"),
            ("gracefulStopSucceeded", "GracefulStopSucceeded"),
            ("forcedTermination", "ForcedTermination"),
        ] {
            fields.insert(target.into(), scalar(&dataset[source]));
        }
        for (a, b, target) in [
            (
                "recordsConsumed",
                "recordsProduced",
                "NativeConsumedProduced",
            ),
            ("ringUsed", "ringCapacity", "RingUsedCapacity"),
            (
                "channelBatchCount",
                "channelBatchCapacity",
                "ChannelUsedCapacity",
            ),
            (
                "recordsCompleted",
                "recordsStarted",
                "AggregationCompletedStarted",
            ),
        ] {
            fields.insert(
                target.into(),
                format!("{}/{}", scalar(&dataset[a]), scalar(&dataset[b])),
            );
        }
        for (source, target) in [
            ("lastHealthyUtc", "LastHealthyUtc"),
            ("nextProbeUtc", "NextProbeUtc"),
            ("startedOnUtc", "StartedOnUtc"),
        ] {
            fields.insert(target.into(), utc(&dataset[source]));
        }
        fields.insert("IncidentAge".into(), span(&dataset["incidentAge"]));
        rows.push(ContractRow {
            id: format!("dataset:{}", scalar(&dataset["dataset"])),
            fields,
            ..Default::default()
        });
    }
    let context = BTreeMap::from([
        ("status".into(), scalar(&value["overallStatus"])),
        (
            "summary".into(),
            format!(
                "Operations health: {} | Session: {} | Value date: {} | Revision: {}",
                scalar(&value["overallStatus"]),
                scalar(&value["sessionState"]),
                value["valueDate"].as_str().unwrap_or("none"),
                scalar(&value["revision"])
            ),
        ),
        (
            "observation".into(),
            format!(
                "Central observation: {} | Last probe: {} | Next probe: {} | Stale-generation publications rejected: {}",
                utc(&value["observedOnUtc"]),
                utc(&value["lastProbeUtc"]),
                utc(&value["nextProbeUtc"]),
                scalar(&value["rejectedStaleGenerationPublications"])
            ),
        ),
    ]);
    Ok(ContractResult {
        view: "operations-health".into(),
        rows,
        context,
        ..Default::default()
    })
}
