//! Inspection formatting of backend evidence. Amounts stay exact decimal text;
//! no sizing, authorization, return or risk calculation is performed here.
use crate::backend::{contracts::utc_roundtrip, schema::Registry};
use rmpv::Value;
use serde_json::Value as Json;
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

fn at_field(value: &crate::backend::contract_view::Field<'_>) -> Result<String, String> {
    Ok(value.utc()?.map(utc_roundtrip).unwrap_or_default())
}
pub fn risk(registry: &Registry, wire: &Value) -> Result<String, String> {
    let kind = format!(
        "{}.RiskManagement.RiskObservation",
        super::inspection_queries::ROOT
    );
    let row = crate::backend::contract_view::Field::new(registry, &kind, wire);
    let view = row.path(&["Snapshot", "WorkflowDefinition"])?;
    let calculation = row.field("Calculation")?;
    let mut lines = vec![
        format!(
            "CURRENT AUTHORITY: {} (checked {})",
            row.path(&["CurrentAuthority"])?.text()?,
            at_field(&row.path(&["CheckedAtUtc"])?)?
        ),
        format!(
            "Fund outcome: {}; projection behind: {}",
            row.path(&["FundSynchronization"])?.text()?,
            row.path(&["ProjectionBehind"])?.text()?
        ),
        format!(
            "Workflow: {}; revision: {}; invocation: {}",
            view.path(&["WorkflowId", "Value"])?
                .text()?
                .replace('-', ""),
            view.path(&["WorkflowRevision"])?.text()?,
            view.path(&["RiskExecution", "CommandId"])?.text()?
        ),
        format!(
            "Workflow state: {}; financial phase: {}; stop reason: {}",
            view.path(&["Status"])?.text()?,
            view.path(&["FinancialHandoff", "Phase"])?.text()?,
            view.path(&["StopReasonCode"])?.text()?
        ),
    ];
    let input = view.field("RiskExecution")?;
    if !input.is_nil() {
        lines.push(format!(
            "Composer result: {}; policy: {} version {}",
            input.path(&["CompositionResult", "ResultId"])?.text()?,
            input.path(&["PolicyId"])?.text()?,
            input.path(&["PolicyVersion"])?.text()?
        ));
        lines.push(format!(
            "Evaluated: {}; original expiry: {}; funding environment: {}",
            at_field(&input.path(&["EvaluatedAtUtc"])?)?,
            at_field(&input.path(&["ExpiresAtUtc"])?)?,
            input.path(&["SizingAuthority", "Environment"])?.text()?
        ));
    }
    let requirement = calculation.field("Requirements")?;
    if !requirement.is_nil() {
        lines.push(format!(
            "Settlement cash {}; margin funding {}; fees {}; variation reserve {}",
            requirement.path(&["SettlementCash"])?.text()?,
            requirement.path(&["MarginFunding"])?.text()?,
            requirement.path(&["FeeReserve"])?.text()?,
            requirement.path(&["VariationReserve"])?.text()?
        ));
    }
    lines.push(format!(
        "Accepted by workflow: {}",
        row.path(&["CalculationAccepted"])?.text()?
    ));
    lines.push(format!(
        "Historical calculation: {}; units: {}",
        if calculation.is_nil() {
            "Not completed".into()
        } else {
            calculation.path(&["Outcome"])?.text()?
        },
        calculation.path(&["StrategyUnits"])?.text()?
    ));
    if !calculation.is_nil() {
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
            calculation.path(&["ResultId"])?.text()?
        ));
        lines.push(
            calculation
                .field("Reasons")?
                .items(100_000)?
                .into_iter()
                .map(|value| value.text())
                .collect::<Result<Vec<_>, _>>()?
                .join("\r\n"),
        );
    }
    let explanation = view.field("RiskExplanation")?;
    if !explanation.is_nil() {
        lines.push(format!(
            "Explanation schema {}; hash {}",
            explanation.path(&["SchemaVersion"])?.text()?,
            explanation.path(&["ContentHash"])?.text()?
        ));
        lines.push(format!(
            "Available cash {}; effective loss budget {}; market multiplier {}",
            explanation.path(&["AvailableCash"])?.text()?,
            explanation.path(&["EffectiveLossBudget"])?.text()?,
            explanation.path(&["MarketMultiplier"])?.text()?
        ));
        for condition in explanation.field("Conditions")?.items(100_000)? {
            lines.push(condition.text()?);
        }
        for quantity in explanation.field("Quantities")?.items(100_000)? {
            lines.push(format!("{} units require {} cash; available at evaluation: {}; fits={}. Loss charge {}; budget {}; fits={}.",quantity.path(&["Units"])?.text()?,quantity.path(&["Cash"])?.text()?,explanation.path(&["AvailableCash"])?.text()?,quantity.path(&["CashFits"])?.text()?,quantity.path(&["Loss"])?.text()?,explanation.path(&["EffectiveLossBudget"])?.text()?,quantity.path(&["LossFits"])?.text()?));
            for limit in quantity.field("Limits")?.items(100_000)? {
                let index = limit
                    .field("LimitIndex")?
                    .unsigned()
                    .ok_or("Invalid risk limit index")? as usize;
                let bound = explanation
                    .field("Limits")?
                    .items(100_000)?
                    .get(index)
                    .copied()
                    .ok_or("Risk evidence limit out of range")?;
                lines.push(format!(
                    "  {}: proposed={}; existing={}; fits={}",
                    bound.text()?,
                    limit.path(&["Proposed"])?.text()?,
                    limit.path(&["Existing"])?.text()?,
                    limit.path(&["Fits"])?.text()?
                ));
            }
        }
    } else {
        lines.push("Detailed explanation unavailable for this historical snapshot.".into());
    }
    lines.push(format!(
        "Resize evidence: {}",
        view.path(&["RiskResize"])?.text()?
    ));
    lines.push(
        "Open Evidence for frozen inputs, source versions, hashes, receipts and Fund state.".into(),
    );
    Ok(lines.join("\r\n") + "\r\n")
}
