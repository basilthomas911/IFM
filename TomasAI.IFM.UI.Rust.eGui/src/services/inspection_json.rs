//! Embedded risk evidence uses System.Text.Json's declared property order and
//! numeric enums. Its generated metadata is independent of display-only cells.
use crate::backend::{identity::canonical_json, schema::Registry};
use rmpv::Value;
use serde::Deserialize;
use std::collections::BTreeMap;
#[derive(Deserialize)]
struct Property {
    name: String,
    member: String,
    #[serde(rename = "type")]
    kind: String,
    #[serde(default, rename = "ignoreNull")]
    ignore_null: bool,
    #[serde(default, rename = "ignoreDefault")]
    ignore_default: bool,
}
fn quoted(text: &str) -> Result<String, String> {
    canonical_json(&serde_json::Value::String(text.into()))
}
fn base64(bytes: &[u8]) -> String {
    const TABLE: &[u8] = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    let mut output = String::with_capacity(bytes.len().div_ceil(3) * 4);
    for chunk in bytes.chunks(3) {
        let a = chunk[0] as usize;
        let b = chunk.get(1).copied().unwrap_or(0) as usize;
        let c = chunk.get(2).copied().unwrap_or(0) as usize;
        for index in [a >> 2, ((a & 3) << 4) | (b >> 4)] {
            output.push(TABLE[index] as char);
        }
        output.push(if chunk.len() > 1 {
            TABLE[((b & 15) << 2) | (c >> 6)] as char
        } else {
            '='
        });
        output.push(if chunk.len() > 2 {
            TABLE[c & 63] as char
        } else {
            '='
        });
    }
    output
}
pub fn render(registry: &Registry, kind: &str, wire: &Value) -> Result<String, String> {
    let shapes: BTreeMap<String, Vec<Property>> = serde_json::from_str(include_str!(
        "../../tests/fixtures/backend/inspection-json-shapes.json"
    ))
    .map_err(|e| e.to_string())?;
    fn node(
        registry: &Registry,
        shapes: &BTreeMap<String, Vec<Property>>,
        kind: &str,
        wire: &Value,
        depth: usize,
        budget: &mut usize,
    ) -> Result<String, String> {
        if depth > 64 || *budget == 0 {
            return Err("Embedded evidence exceeds its display bound".into());
        }
        *budget -= 1;
        if wire.is_nil() {
            return Ok("null".into());
        }
        if kind == "System.TimeSpan" {
            return quoted(&crate::services::parameter_fields::timespan(
                wire.as_i64().ok_or("Invalid TimeSpan evidence")?,
            ));
        }
        let kind = kind
            .strip_prefix("System.Nullable`1[")
            .and_then(|v| v.strip_suffix(']'))
            .unwrap_or(kind);
        if kind == "System.Byte[]" || kind == "System.ReadOnlyMemory`1[System.Byte]" {
            return quoted(&base64(wire.as_slice().ok_or("Invalid binary evidence")?));
        }
        let indent = "  ".repeat(depth);
        let child_indent = "  ".repeat(depth + 1);
        if let Some(inner) = kind.strip_suffix("[]").or_else(|| {
            kind.strip_prefix("System.Collections.Immutable.ImmutableArray`1[")
                .and_then(|s| s.strip_suffix(']'))
        }) {
            let rows = wire
                .as_array()
                .filter(|v| v.len() <= 100_000)
                .ok_or("Invalid embedded evidence collection")?;
            if rows.is_empty() {
                return Ok("[]".into());
            }
            let values = rows
                .iter()
                .map(|v| {
                    node(registry, shapes, inner, v, depth + 1, budget)
                        .map(|s| format!("{child_indent}{s}"))
                })
                .collect::<Result<Vec<_>, String>>()?;
            return Ok(format!("[\n{}\n{indent}]", values.join(",\n")));
        }
        let concrete = kind;
        let actual = wire;
        if let Ok(shape) = registry.describe(kind) {
            if shape.is_enum {
                return wire
                    .as_i64()
                    .map(|v| v.to_string())
                    .ok_or("Invalid JSON evidence enum".into());
            }
            if !shape.unions.is_empty() {
                let parts = wire
                    .as_array()
                    .filter(|v| v.len() == 2)
                    .ok_or("Invalid JSON evidence union")?;
                let tag = parts[0].as_i64().ok_or("Invalid JSON evidence tag")?;
                let entry = shape
                    .unions
                    .iter()
                    .find(|(v, _)| *v == tag)
                    .ok_or("Unknown JSON evidence variant")?;
                // The concrete name must outlive recursive property rendering.
                return object(registry, shapes, kind, &entry.1, &parts[1], depth, budget);
            }
        }
        if shapes.contains_key(kind) {
            return object(registry, shapes, kind, concrete, actual, depth, budget);
        }
        let value = crate::services::inspection_display::semantic_document(registry, kind, wire)?;
        match value {
            serde_json::Value::String(text) => quoted(&text),
            serde_json::Value::Number(number) => {
                let raw = number.to_string();
                Ok(if kind == "System.Decimal" {
                    raw
                } else {
                    raw.strip_suffix(".0").unwrap_or(&raw).into()
                })
            }
            serde_json::Value::Bool(value) => Ok(value.to_string()),
            serde_json::Value::Null => Ok("null".into()),
            _ => Err(format!("Missing embedded JSON metadata for {kind}")),
        }
    }
    fn object(
        registry: &Registry,
        shapes: &BTreeMap<String, Vec<Property>>,
        declared: &str,
        concrete: &str,
        wire: &Value,
        depth: usize,
        budget: &mut usize,
    ) -> Result<String, String> {
        let properties = shapes
            .get(declared)
            .ok_or(format!("Missing embedded JSON metadata for {declared}"))?;
        if properties.is_empty() {
            return Ok("{}".into());
        }
        let indent = "  ".repeat(depth);
        let child_indent = "  ".repeat(depth + 1);
        let mut values = vec![];
        for p in properties {
            let value = super::strategy_display::raw_property(registry, concrete, wire, &p.member)?;
            if (p.ignore_null && value.is_nil())
                || (p.ignore_default
                    && (value.is_nil()
                        || value.as_i64() == Some(0)
                        || value.as_bool() == Some(false)))
            {
                continue;
            }
            values.push(format!(
                "{child_indent}{}: {}",
                quoted(&p.name)?,
                node(registry, shapes, &p.kind, &value, depth + 1, budget)?
            ));
        }
        if values.is_empty() {
            return Ok("{}".into());
        }
        Ok(format!("{{\n{}\n{indent}}}", values.join(",\n")))
    }
    let output = node(registry, &shapes, kind, wire, 0, &mut 100_000)?;
    if output.len() > 4 * 1024 * 1024 {
        return Err("Embedded evidence exceeds 4 MiB".into());
    }
    Ok(if cfg!(windows) {
        output.replace('\n', "\r\n")
    } else {
        output
    })
}
