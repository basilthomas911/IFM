//! Rehydrate a retained managed financial request without regenerating its
//! operation identity, financial revision, timestamps, amount scale or hash.
use crate::backend::{contracts::ExactDecimal, schema::Registry};
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::BTreeMap;
fn wire(registry: &Registry, kind: &str, value: &Json, depth: usize) -> Result<Value, String> {
    if depth > 64 {
        return Err("Financial request nesting exceeds its bound".into());
    }
    if let Some(inner) = kind
        .strip_prefix("System.Nullable`1[")
        .and_then(|v| v.strip_suffix(']'))
    {
        return if value.is_null() {
            Ok(Value::Nil)
        } else {
            wire(registry, inner, value, depth + 1)
        };
    }
    if kind == "System.Decimal" {
        let exact = if value.is_number() {
            value.to_string()
        } else {
            value.as_str().ok_or("Invalid persisted amount")?.into()
        };
        return Ok(ExactDecimal::parse(&exact)?.wire());
    }
    if kind == "System.DateTime" {
        let text = value.as_str().ok_or("Invalid persisted timestamp")?;
        // System.Text.Json writes default/Unspecified timestamps without a zone.
        // MessagePack's DateTime formatter represents the same clock fields as UTC.
        let date = chrono::DateTime::parse_from_rfc3339(text)
            .map(|v| v.to_utc())
            .or_else(|_| {
                chrono::NaiveDateTime::parse_from_str(text, "%Y-%m-%dT%H:%M:%S%.f")
                    .map(|v| v.and_utc())
            })
            .map_err(|_| "Invalid persisted timestamp")?;
        return Ok(crate::backend::contracts::timestamp_wire(date));
    }
    if value.is_null() {
        return registry.value_from_json(kind, value);
    }
    if let Some(inner) = kind.strip_suffix("[]") {
        return value
            .as_array()
            .filter(|v| v.len() <= 100_000)
            .ok_or("Invalid persisted request array")?
            .iter()
            .map(|v| wire(registry, inner, v, depth + 1))
            .collect::<Result<Vec<_>, _>>()
            .map(Value::Array);
    }
    if let Ok(shape) = registry.describe(kind) {
        if shape.is_enum {
            if let Some(number) = value.as_i64() {
                if !registry
                    .enum_names(kind)?
                    .iter()
                    .any(|name| registry.enum_value(kind, name) == Ok(number))
                {
                    return Err(format!("Unknown persisted enum value for {kind}"));
                }
                return Ok(number.into());
            }
            return registry.value_from_json(kind, value);
        }
        let fields = value.as_object().ok_or("Invalid persisted request model")?;
        // Ignore CLR display/computed properties as the managed JSON reader does;
        // only published numeric wire members enter the command envelope.
        let fields = shape
            .members
            .iter()
            .map(|(name, _, kind)| {
                let value = fields
                    .get(name)
                    .ok_or_else(|| format!("Persisted request is missing {name}"))?;
                Ok((name.clone(), wire(registry, kind, value, depth + 1)?))
            })
            .collect::<Result<BTreeMap<_, _>, String>>()?;
        return registry.encode(kind, &fields);
    }
    registry.value_from_json(kind, value)
}
pub fn rehydrate(registry: &Registry, command: &str, pending: &Json) -> Result<Value, String> {
    super::financial_journal::validate(registry, command, pending)?;
    wire(
        registry,
        &format!("TomasAI.IFM.Domain.Portfolio.Shared.Financial.{command}"),
        &pending["Request"],
        0,
    )
}
