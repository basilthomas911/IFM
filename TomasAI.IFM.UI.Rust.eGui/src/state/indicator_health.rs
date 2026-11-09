//! Typed API indicator audits projected into the existing Operations list and detail tree.
use crate::contract_editor::{ContractResult, ContractRow};
use chrono::{DateTime, Utc};
use serde::Deserialize;
use std::collections::BTreeMap;

/// Reads only the indicator check from the API readiness envelope.
#[derive(Deserialize)]
pub struct IndicatorReadiness {
    /// Named API checks; unrelated readiness fields are ignored by the decoder.
    pub entries: IndicatorChecks,
}

/// Typed indicator entry in the readiness response.
#[derive(Deserialize)]
pub struct IndicatorChecks {
    /// Absent checks are unavailable, never implicitly healthy.
    pub market_data_indicators: Option<IndicatorCheck>,
}

/// The health check carries a bounded immutable audit snapshot.
#[derive(Deserialize)]
pub struct IndicatorCheck {
    /// Server-qualified audit state and observations.
    pub data: IndicatorAudit,
}

/// The audit is periodic; it need not contain a new four-hour observation every five minutes.
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IndicatorAudit {
    /// Server-owned lifecycle state.
    pub state: String,
    /// Completion time of the most recent full audit.
    pub audited_at_utc: Option<String>,
    /// Active producer outputs, including consumer snapshot qualification.
    pub entries: Vec<IndicatorEntry>,
}

/// One indicator and interval with original source provenance.
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IndicatorEntry {
    /// Exact active contract or source role.
    pub contract_id: String,
    /// Producer interval or workflow horizon.
    pub time_frame: String,
    /// Configured calculation and periods.
    pub indicator: String,
    /// Server-calculated warm-up status.
    pub warm: bool,
    /// Server-calculated value validity.
    pub valid: bool,
    /// Original market observation time.
    pub source_as_of_utc: Option<String>,
    /// Minimum expected completed-interval source time.
    pub expected_as_of_utc: Option<String>,
    /// Precise qualification or acquisition failure.
    pub reason: String,
    /// Combined server qualification; the UI does not recalculate indicator values.
    pub ready: bool,
}


/// Appends bounded indicator rows to the current Operations result without altering dataset counters.
pub fn append(result: &mut ContractResult, audit: IndicatorAudit, server_now: DateTime<Utc>) -> Result<(), String> {
    if audit.entries.len() > 512 || !matches!(audit.state.as_str(), "Warm" | "Initializing" | "Recovering" | "Failed" | "ScheduledStopped") {
        return Err("Invalid indicator audit state or capacity".into());
    }
    let stopped = audit.state == "ScheduledStopped";
    let audited_at = audit.audited_at_utc.as_deref().filter(|value| !value.is_empty())
        .map(|value| DateTime::parse_from_rfc3339(value).map(|at| at.to_utc()))
        .transpose().map_err(|_| "Invalid indicator audit timestamp")?;
    if audit.entries.iter().any(|entry| entry.contract_id.len() > 128 || entry.indicator.len() > 128
        || entry.reason.len() > 16_384 || entry.ready && (!entry.warm || !entry.valid || !entry.reason.is_empty() || entry.source_as_of_utc.is_none())) {
        return Err("Invalid indicator observation or display capacity".into());
    }
    if !stopped && audited_at.is_some_and(|at| at > server_now + chrono::Duration::seconds(2)
        || server_now - at > chrono::Duration::seconds(345)) {
        return Err("Indicator audit is stale or its server clock is invalid".into());
    }
    let ready = audit.entries.iter().filter(|entry| entry.ready).count();
    if audit.state == "Warm" && (audit.entries.is_empty() || ready != audit.entries.len() || audited_at.is_none()) {
        return Err("Warm indicator audit is missing qualified observations".into());
    }
    let overall = if stopped { "Inactive" } else if audit.state == "Failed" { "Red" }
        else if audit.state == "Warm" { "Green" } else { "Yellow" };
    if overall == "Red" || overall == "Yellow" && result.context.get("status").is_none_or(|status| status != "Red") {
        result.context.insert("status".into(), overall.into());
    }
    let summary = result.context.entry("summary".into()).or_default();
    summary.push_str(&format!(" · Indicators {ready}/{} ({})", audit.entries.len(), audit.state));
    for entry in audit.entries {
        let name = format!("Indicator · {} {} {}", entry.contract_id, entry.time_frame, entry.indicator);
        let status = if stopped { "Inactive" } else if entry.ready { "Green" } else if audit.state == "Failed" { "Red" } else { "Yellow" };
        result.rows.push(ContractRow {
            id: format!("indicator:{}:{}:{}", entry.contract_id, entry.time_frame, entry.indicator),
            fields: BTreeMap::from([
                ("kind".into(), "stage".into()), ("Stage".into(), name), ("Status".into(), status.into()),
                ("Contract".into(), entry.contract_id), ("TimeFrame".into(), entry.time_frame), ("Indicator".into(), entry.indicator),
                ("Warm".into(), entry.warm.to_string()), ("Valid".into(), entry.valid.to_string()),
                ("Reason".into(), entry.reason), ("MarketDataAsOfUtc".into(), entry.source_as_of_utc.unwrap_or_else(|| "Unavailable".into())),
                ("ExpectedSourceAsOfUtc".into(), entry.expected_as_of_utc.unwrap_or_else(|| "Event-driven source".into())),
                ("LastAuditUtc".into(), audited_at.map(|at| at.to_rfc3339()).unwrap_or_else(|| "Pending".into())),
            ]),
            ..Default::default()
        });
    }
    Ok(())
}


/// Keeps monitor failures visible without hiding independently available dataset health.
pub fn unavailable(result: &mut ContractResult, reason: String) {
    result.context.insert("status".into(), "Red".into());
    result.rows.push(ContractRow { id: "indicator:unavailable".into(), fields: BTreeMap::from([
        ("kind".into(), "stage".into()), ("Stage".into(), "Indicator audit".into()),
        ("Status".into(), "Red".into()), ("Reason".into(), reason),
    ]), ..Default::default() });
}
