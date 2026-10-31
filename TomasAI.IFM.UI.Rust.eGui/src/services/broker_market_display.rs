//! Concrete market/leg display documents. Decimal text and timestamp precision
//! retain the existing managed publication contract; these records are never order inputs.
use super::option_draft::Selected;
use crate::models::{
    broker_presentation::{LegDisplay, MarketDisplay},
    trade_contracts::*,
    wire_types::{DecimalText, OffsetText, UtcText},
};

fn stamp(value: Option<&OffsetText>) -> Result<Option<OffsetText>, String> {
    let Some(value) = value else { return Ok(None) };
    let at = chrono::DateTime::parse_from_rfc3339(value.as_str())
        .map_err(|_| "Invalid display timestamp")?;
    Ok(Some(OffsetText(format!(
        "{}.{:07}{}",
        at.format("%Y-%m-%dT%H:%M:%S"),
        at.timestamp_subsec_nanos() / 100,
        at.format("%:z")
    ))))
}
pub fn market(row: &EvaluatedOptionChainReadModel) -> Result<MarketDisplay, String> {
    row.contracts
        .as_ref()
        .ok_or("Missing market display rows")?;
    let mut result = MarketDisplay {
        underlying_contract_id: row.underlying_contract_id.clone(),
        expiry_date: row.expiry_date.clone(),
        underlying_price: row.underlying_price.clone(),
        lower_strike_bound: row.lower_strike_bound.clone(),
        upper_strike_bound: row.upper_strike_bound.clone(),
        window_method: row.window_method.clone(),
        as_of_utc: row.as_of_utc.clone(),
        bollinger_standard_deviation20: row.bollinger_standard_deviation20.clone(),
        previous_session_close: row.previous_session_close.clone(),
        iv_rank: row.iv_rank.clone(),
        iv_rank_status: row.iv_rank_status.clone(),
        bollinger_as_of_utc: row.bollinger_as_of_utc.clone(),
        last_underlying_price: row.last_underlying_price.clone(),
        last_underlying_trade_at_utc: row.last_underlying_trade_at_utc.clone(),
        contracts: row.contracts.clone(),
    };
    result.as_of_utc = stamp(result.as_of_utc.as_ref())?;
    result.bollinger_as_of_utc = stamp(result.bollinger_as_of_utc.as_ref())?;
    result.last_underlying_trade_at_utc = stamp(result.last_underlying_trade_at_utc.as_ref())?;
    for row in result.contracts.as_mut().unwrap() {
        row.quote_at_utc = stamp(row.quote_at_utc.as_ref())?;
        row.trade_at_utc = stamp(row.trade_at_utc.as_ref())?;
        row.evaluated_at_utc = stamp(row.evaluated_at_utc.as_ref())?;
        row.last_quote_event_at_utc = stamp(row.last_quote_event_at_utc.as_ref())?;
        row.last_quote_received_at_utc = stamp(row.last_quote_received_at_utc.as_ref())?;
    }
    Ok(result)
}
pub fn leg(value: &Selected) -> Result<LegDisplay, String> {
    let decimal = |value: &Option<crate::backend::contracts::ExactDecimal>| {
        value
            .as_ref()
            .map(|value| DecimalText(value.as_str().into()))
    };
    Ok(LegDisplay {
        contract_id: Some(value.quote.contract.clone()),
        role: Some(value.role.clone()),
        strike: decimal(&value.quote.strike),
        bid: decimal(&value.quote.bid),
        ask: decimal(&value.quote.ask),
        delta: value.quote.delta,
        is_call: Some(value.quote.is_call),
        vega: value.quote.vega,
        multiplier: decimal(&value.quote.multiplier),
        quote_at_utc: stamp(value.quote_at.as_ref())?,
        frozen: Some(value.frozen),
        is_future: Some(value.quote.is_future),
        sign: Some(value.quote.side),
    })
}
#[derive(serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AccountDisplay<'a> {
    pub schema_version: &'a Option<u16>,
    pub id: AccountIdentity<'a>,
    pub environment: &'a Option<AccountBrokerEnvironment>,
    pub snapshot: &'a Option<BrokerAccountSnapshotEvidence>,
    pub qualification_status: &'a Option<BrokerAccountQualificationStatus>,
    pub gate: &'a Option<BrokerAccountOperationalGate>,
    pub manual_hold: &'a Option<bool>,
    pub manifest_hash: &'a Option<String>,
    pub evidence_reference: &'a Option<String>,
    pub approval_id: &'a Option<String>,
    pub authorized_by: &'a Option<String>,
    pub changed_at_utc: &'a Option<UtcText>,
    pub revision: &'a Option<i32>,
    pub reason: &'a Option<String>,
    pub development_qualifications_exempt: &'a Option<bool>,
}
#[derive(serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AccountIdentity<'a> {
    pub account_alias: Option<&'a str>,
    pub is_valid: bool,
}
pub fn account(
    value: &crate::models::trade_contracts::BrokerAccountDefinition,
) -> Result<AccountDisplay<'_>, String> {
    let alias = value.id.as_ref().and_then(|id| id.account_alias.as_deref());
    Ok(AccountDisplay {
        schema_version: &value.schema_version,
        id: AccountIdentity {
            account_alias: alias,
            is_valid: alias.is_some_and(|s| !s.trim().is_empty()),
        },
        environment: &value.environment,
        snapshot: &value.snapshot,
        qualification_status: &value.qualification_status,
        gate: &value.gate,
        manual_hold: &value.manual_hold,
        manifest_hash: &value.manifest_hash,
        evidence_reference: &value.evidence_reference,
        approval_id: &value.approval_id,
        authorized_by: &value.authorized_by,
        changed_at_utc: &value.changed_at_utc,
        revision: &value.revision,
        reason: &value.reason,
        development_qualifications_exempt: &value.development_qualifications_exempt,
    })
}
/// Quote age advances only with API publications. A disconnected workstation
/// clock cannot change a backtest's age or manufacture a fresh timestamp.
pub fn quote_age(as_of: &str, quote: &str) -> String {
    let parsed = chrono::DateTime::parse_from_rfc3339(as_of)
        .ok()
        .zip(chrono::DateTime::parse_from_rfc3339(quote).ok());
    parsed
        .map(|(now, quote)| {
            format!(
                "{:.0}s",
                ((now - quote).num_milliseconds() as f64 / 1000.0).max(0.0)
            )
        })
        .unwrap_or("Unavailable".into())
}

/// Preserve the original sorted JSON text at the sealed command boundary.
/// Native quote state and publications remain concrete records.
pub fn quote_inputs(legs: &[Selected]) -> Result<String, String> {
    let rows = legs.iter().map(leg).collect::<Result<Vec<_>, _>>()?;
    let canonical = serde_json::to_value(rows).map_err(|error| error.to_string())?;
    serde_json::to_string(&canonical).map_err(|error| error.to_string())
}
