//! Inspection formatting of backend evidence. Amounts stay exact decimal text;
//! no sizing, authorization, return or risk calculation is performed here.
use crate::backend::{contracts::utc_roundtrip, schema::Registry};
use rmpv::Value;
use serde_json::Value as Json;
fn text(value: &Json) -> String {
    match value {
        Json::Null => String::new(),
        Json::String(s) => s.clone(),
        Json::Bool(v) => if *v { "True" } else { "False" }.into(),
        _ => value.to_string(),
    }
}
fn at(value: &Json) -> Result<String, String> {
    if value.is_null() {
        return Ok(String::new());
    }
    chrono::DateTime::parse_from_rfc3339(value.as_str().ok_or("Missing evidence timestamp")?)
        .map(|v| utc_roundtrip(v.to_utc()))
        .map_err(|_| "Invalid evidence timestamp".into())
}
/// Canonical hash input uses the published contract slots, exact numeric
/// decimals and UTC JSON dates, rather than the display's named enum strings.
pub fn semantic_document(registry: &Registry, kind: &str, wire: &Value) -> Result<Json, String> {
    if wire.is_nil() {
        return Ok(Json::Null);
    }
    let kind = kind
        .strip_prefix("System.Nullable`1[")
        .and_then(|s| s.strip_suffix(']'))
        .unwrap_or(kind);
    if kind == "System.Decimal" {
        return serde_json::from_str(wire.as_str().ok_or("Invalid exact decimal evidence")?)
            .map_err(|e| e.to_string());
    }
    if kind == "System.DateTime" {
        let instant = crate::backend::contracts::timestamp(wire)?;
        // System.Text.Json emits at most seven fractional digits and trims zeroes.
        let roundtrip = utc_roundtrip(instant);
        let (whole, fraction) = roundtrip
            .trim_end_matches('Z')
            .split_once('.')
            .ok_or("Missing UTC precision")?;
        let fraction = fraction.trim_end_matches('0');
        return Ok(Json::String(if fraction.is_empty() {
            format!("{whole}Z")
        } else {
            format!("{whole}.{fraction}Z")
        }));
    }
    if let Some(inner) = kind.strip_suffix("[]").or_else(|| {
        kind.strip_prefix("System.Collections.Immutable.ImmutableArray`1[")
            .and_then(|s| s.strip_suffix(']'))
    }) {
        if kind == "System.Byte[]" {
            return registry.project(kind, wire);
        }
        return wire
            .as_array()
            .filter(|v| v.len() <= 100_000)
            .ok_or("Invalid evidence collection")?
            .iter()
            .map(|v| semantic_document(registry, inner, v))
            .collect::<Result<Vec<_>, _>>()
            .map(Json::Array);
    }
    if let Ok(shape) = registry.describe(kind) {
        if shape.is_enum {
            return Ok(Json::from(wire.as_i64().ok_or("Invalid evidence enum")?));
        }
        if !shape.unions.is_empty() {
            let parts = wire
                .as_array()
                .filter(|v| v.len() == 2)
                .ok_or("Invalid evidence union")?;
            let tag = parts[0].as_i64().ok_or("Missing evidence union tag")?;
            let concrete = &shape
                .unions
                .iter()
                .find(|(v, _)| *v == tag)
                .ok_or("Unknown evidence variant")?
                .1;
            return semantic_document(registry, concrete, &parts[1]);
        }
        let mut output = serde_json::Map::new();
        for (name, _, inner) in shape.members {
            let value = crate::services::strategy::member(registry, kind, wire, &name)?;
            output.insert(name, semantic_document(registry, &inner, &value)?);
        }
        return Ok(Json::Object(output));
    }
    registry.project(kind, wire)
}
pub fn semantic_hash(registry: &Registry, kind: &str, wire: &Value) -> Result<String, String> {
    crate::backend::identity::financial_hash(&semantic_document(registry, kind, wire)?)
        .map(|v| v.to_ascii_lowercase())
}

pub fn risk(registry: &Registry, wire: &Value) -> Result<String, String> {
    let kind = format!(
        "{}.RiskManagement.RiskObservation",
        super::inspection_queries::ROOT
    );
    let row = registry.project(&kind, wire)?;
    let view = &row["Snapshot"]["WorkflowDefinition"];
    let calculation = &row["Calculation"];
    let mut lines = vec![
        format!(
            "CURRENT AUTHORITY: {} (checked {})",
            text(&row["CurrentAuthority"]),
            at(&row["CheckedAtUtc"])?
        ),
        format!(
            "Fund outcome: {}; projection behind: {}",
            text(&row["FundSynchronization"]),
            text(&row["ProjectionBehind"])
        ),
        format!(
            "Workflow: {}; revision: {}; invocation: {}",
            text(&view["WorkflowId"]["Value"]).replace('-', ""),
            text(&view["WorkflowRevision"]),
            text(&view["RiskExecution"]["CommandId"])
        ),
        format!(
            "Workflow state: {}; financial phase: {}; stop reason: {}",
            text(&view["Status"]),
            text(&view["FinancialHandoff"]["Phase"]),
            text(&view["StopReasonCode"])
        ),
    ];
    if let Some(input) = view.get("RiskExecution").filter(|v| !v.is_null()) {
        lines.push(format!(
            "Composer result: {}; policy: {} version {}",
            text(&input["CompositionResult"]["ResultId"]),
            text(&input["PolicyId"]),
            text(&input["PolicyVersion"])
        ));
        lines.push(format!(
            "Evaluated: {}; original expiry: {}; funding environment: {}",
            at(&input["EvaluatedAtUtc"])?,
            at(&input["ExpiresAtUtc"])?,
            text(&input["SizingAuthority"]["Environment"])
        ));
    }
    if let Some(requirement) = calculation.get("Requirements").filter(|v| !v.is_null()) {
        lines.push(format!(
            "Settlement cash {}; margin funding {}; fees {}; variation reserve {}",
            text(&requirement["SettlementCash"]),
            text(&requirement["MarginFunding"]),
            text(&requirement["FeeReserve"]),
            text(&requirement["VariationReserve"])
        ));
    }
    lines.push(format!(
        "Accepted by workflow: {}",
        text(&row["CalculationAccepted"])
    ));
    lines.push(format!(
        "Historical calculation: {}; units: {}",
        if calculation.is_null() {
            "Not completed".into()
        } else {
            text(&calculation["Outcome"])
        },
        text(&calculation["StrategyUnits"])
    ));
    if !calculation.is_null() {
        let result = super::strategy::member(registry, &kind, wire, "Calculation")?;
        let hash = semantic_hash(
            registry,
            &format!(
                "{}.RiskManagement.RiskAssessmentResult",
                super::inspection_queries::ROOT
            ),
            &result,
        )?;
        lines.push(format!(
            "Result: {}; hash: {hash}",
            text(&calculation["ResultId"])
        ));
        lines.push(
            calculation["Reasons"]
                .as_array()
                .ok_or("Invalid risk reasons")?
                .iter()
                .map(text)
                .collect::<Vec<_>>()
                .join("\r\n"),
        );
    }
    if let Some(explanation) = view.get("RiskExplanation").filter(|v| !v.is_null()) {
        lines.push(format!(
            "Explanation schema {}; hash {}",
            text(&explanation["SchemaVersion"]),
            text(&explanation["ContentHash"])
        ));
        lines.push(format!(
            "Available cash {}; effective loss budget {}; market multiplier {}",
            text(&explanation["AvailableCash"]),
            text(&explanation["EffectiveLossBudget"]),
            text(&explanation["MarketMultiplier"])
        ));
        for condition in explanation["Conditions"]
            .as_array()
            .ok_or("Invalid risk explanation conditions")?
        {
            lines.push(text(condition));
        }
        for quantity in explanation["Quantities"]
            .as_array()
            .ok_or("Invalid risk explanation quantities")?
        {
            lines.push(format!("{} units require {} cash; available at evaluation: {}; fits={}. Loss charge {}; budget {}; fits={}.",text(&quantity["Units"]),text(&quantity["Cash"]),text(&explanation["AvailableCash"]),text(&quantity["CashFits"]),text(&quantity["Loss"]),text(&explanation["EffectiveLossBudget"]),text(&quantity["LossFits"])));
            for limit in quantity["Limits"]
                .as_array()
                .ok_or("Invalid risk quantity limits")?
            {
                let index = limit["LimitIndex"]
                    .as_u64()
                    .ok_or("Invalid risk limit index")? as usize;
                let bound = explanation["Limits"]
                    .as_array()
                    .and_then(|v| v.get(index))
                    .ok_or("Risk evidence limit out of range")?;
                lines.push(format!(
                    "  {}: proposed={}; existing={}; fits={}",
                    text(bound),
                    text(&limit["Proposed"]),
                    text(&limit["Existing"]),
                    text(&limit["Fits"])
                ));
            }
        }
    } else {
        lines.push("Detailed explanation unavailable for this historical snapshot.".into());
    }
    lines.push(format!("Resize evidence: {}", text(&view["RiskResize"])));
    lines.push(
        "Open Evidence for frozen inputs, source versions, hashes, receipts and Fund state.".into(),
    );
    Ok(lines.join("\r\n") + "\r\n")
}
