//! System.Text.Json-compatible values for persisted typed financial receipts.
//! Decimal numbers retain their invariant coefficient/scale; no float conversion.
use crate::backend::{contracts::*, schema::Registry};
use rmpv::Value;
use serde_json::Value as Json;
use std::str::FromStr;
pub fn stored_json(registry: &Registry, kind: &str, wire: &Value) -> Result<Json, String> {
    convert(registry, kind, wire, 0)
}
fn convert(registry: &Registry, kind: &str, wire: &Value, depth: usize) -> Result<Json, String> {
    if depth > 64 {
        return Err("Financial receipt nesting exceeds its bound".into());
    }
    if wire.is_nil() {
        return Ok(Json::Null);
    }
    if let Some(inner) = kind
        .strip_prefix("System.Nullable`1[")
        .and_then(|v| v.strip_suffix(']'))
    {
        return convert(registry, inner, wire, depth + 1);
    }
    if kind == "System.Decimal" {
        let raw = wire.as_str().ok_or("Invalid financial decimal")?;
        ExactDecimal::parse(raw)?;
        return serde_json::Number::from_str(raw)
            .map(Json::Number)
            .map_err(|e| e.to_string());
    }
    if kind == "System.DateTime" {
        return Ok(timestamp(wire)?
            .to_rfc3339_opts(chrono::SecondsFormat::AutoSi, true)
            .into());
    }
    if let Some(inner) = kind.strip_suffix("[]") {
        return wire
            .as_array()
            .filter(|v| v.len() <= 100_000)
            .ok_or("Invalid financial receipt array")?
            .iter()
            .map(|v| convert(registry, inner, v, depth + 1))
            .collect::<Result<Vec<_>, _>>()
            .map(Json::Array);
    }
    if let Ok(shape) = registry.describe(kind) {
        if shape.is_enum {
            return wire
                .as_i64()
                .map(Json::from)
                .ok_or("Invalid financial receipt enum".into());
        }
        let mut json = serde_json::Map::new();
        for (name, _, ty) in shape.members {
            let value = crate::services::strategy::member(registry, kind, wire, &name)?;
            json.insert(name, convert(registry, &ty, &value, depth + 1)?);
        }
        return Ok(Json::Object(json));
    }
    registry.project(kind, wire)
}
