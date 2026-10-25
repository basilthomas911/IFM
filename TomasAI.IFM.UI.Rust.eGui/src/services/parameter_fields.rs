//! Lossless parameter working-copy preparation. Scalar paths resolve against
//! the actual typed wire shape, never against a JSON roundtrip of the payload.
//! Identity, metrics and horizon arrays have separate editor actions.
use crate::backend::{contracts::*, schema::Registry};
use rmpv::Value;
use serde::{Deserialize, Serialize};
use serde_json::json;
use std::collections::{BTreeMap, BTreeSet};
const HIDDEN: &[&str] = &[
    "ParameterSetId",
    "Version",
    "SchemaVersion",
    "StrategyParameterSetId",
    "StrategyParameterSetVersion",
    "SignalRequirements",
    "SignalMetrics",
    "ObservationMetrics",
    "Horizon",
    "TargetHorizon",
];
#[derive(Clone, Serialize, Deserialize, Debug, PartialEq)]
#[serde(rename_all = "PascalCase")]
pub struct ParameterField {
    pub path: String,
    pub value: String,
    pub kind: u8,
    pub group: String,
    pub name: String,
}
#[derive(Clone)]
struct Leaf {
    field: ParameterField,
    kind: String,
    slots: Vec<usize>,
}
fn humanize(value: &str) -> String {
    let mut out = String::new();
    let mut previous = ' ';
    for ch in value.replace('_', " ").chars() {
        if ch.is_ascii_uppercase() && (previous.is_ascii_lowercase() || previous.is_ascii_digit()) {
            out.push(' ');
        }
        out.push(ch);
        previous = ch;
    }
    out
}
fn segments(path: &str) -> Vec<String> {
    path.split('/')
        .filter(|v| !v.is_empty())
        .map(|v| v.replace("~1", "/").replace("~0", "~"))
        .collect()
}
fn labels(path: &str) -> (String, String) {
    let parts = segments(path);
    let group = if parts.len() == 1 {
        "General".into()
    } else {
        parts.first().map(|s| humanize(s)).unwrap_or_default()
    };
    let names = if parts.len() > 1 {
        &parts[1..]
    } else {
        &parts[..]
    };
    let mut name = String::new();
    for part in names {
        if !part.is_empty() && part.chars().all(|c| c.is_ascii_digit()) {
            name.push_str(&format!(" [{part}]"));
        } else {
            if !name.is_empty() {
                name.push_str(" / ");
            }
            name.push_str(&humanize(part));
        }
    }
    (group, name)
}
pub fn timespan(ticks: i64) -> String {
    let n = ticks.unsigned_abs();
    let days = n / 864_000_000_000;
    let sec = n / 10_000_000;
    format!(
        "{}{}{:02}:{:02}:{:02}{}",
        if ticks < 0 { "-" } else { "" },
        if days > 0 {
            format!("{days}.")
        } else {
            String::new()
        },
        sec / 3600 % 24,
        sec / 60 % 60,
        sec % 60,
        if n % 10_000_000 == 0 {
            String::new()
        } else {
            format!(".{:07}", n % 10_000_000)
        }
    )
}
fn parse_timespan(text: &str) -> Result<i64, String> {
    let (negative, text) = if let Some(v) = text.strip_prefix('-') {
        (true, v)
    } else {
        (false, text)
    };
    let parts: Vec<_> = text.split(':').collect();
    if parts.len() != 3 {
        return Err("Expected TimeSpan c format".into());
    }
    let (days, hours) = if let Some((d, h)) = parts[0].split_once('.') {
        (d.parse::<u64>().map_err(|_| "Invalid days")?, h)
    } else {
        (0, parts[0])
    };
    let hours = hours.parse::<u64>().map_err(|_| "Invalid hours")?;
    let minutes = parts[1].parse::<u64>().map_err(|_| "Invalid minutes")?;
    let (seconds, fraction) = parts[2].split_once('.').unwrap_or((parts[2], ""));
    let seconds = seconds.parse::<u64>().map_err(|_| "Invalid seconds")?;
    if hours >= 24
        || minutes >= 60
        || seconds >= 60
        || fraction.len() > 7
        || !fraction.chars().all(|v| v.is_ascii_digit())
    {
        return Err("Invalid TimeSpan component".into());
    }
    let nanos = if fraction.is_empty() {
        0
    } else {
        fraction.parse::<u64>().map_err(|_| "Invalid fraction")?
            * 10u64.pow(7 - fraction.len() as u32)
    };
    let ticks =
        (days as u128 * 86400 + hours as u128 * 3600 + minutes as u128 * 60 + seconds as u128)
            * 10_000_000
            + nanos as u128;
    let signed = if negative {
        -(ticks as i128)
    } else {
        ticks as i128
    };
    i64::try_from(signed).map_err(|_| "TimeSpan overflow".into())
}
fn leaves(
    registry: &Registry,
    kind: &str,
    wire: &Value,
    include_identity: bool,
) -> Result<Vec<Leaf>, String> {
    fn visit(
        registry: &Registry,
        kind: &str,
        wire: &Value,
        path: &str,
        slots: Vec<usize>,
        all: bool,
        result: &mut Vec<Leaf>,
        depth: usize,
    ) -> Result<(), String> {
        if depth > 64 || result.len() > 100_000 {
            return Err("Parameter shape exceeds editor bounds".into());
        }
        let kind = kind
            .strip_prefix("System.Nullable`1[")
            .and_then(|v| v.strip_suffix(']'))
            .unwrap_or(kind);
        if let Some(inner) = kind.strip_suffix("[]").or_else(|| {
            kind.strip_prefix("System.Collections.Immutable.ImmutableArray`1[")
                .and_then(|v| v.strip_suffix(']'))
        }) {
            if wire.is_nil() {
                if kind.ends_with("[]") {
                    let (group, name) = labels(path);
                    result.push(Leaf {
                        field: ParameterField {
                            path: path.into(),
                            value: "null".into(),
                            kind: 7,
                            group,
                            name,
                        },
                        kind: kind.into(),
                        slots,
                    });
                }
                return Ok(());
            }
            let items = wire.as_array().ok_or("Invalid parameter array")?;
            for (index, value) in items.iter().enumerate() {
                let mut next = slots.clone();
                next.push(index);
                visit(
                    registry,
                    inner,
                    value,
                    &format!("{path}/{index}"),
                    next,
                    all,
                    result,
                    depth + 1,
                )?;
            }
            return Ok(());
        }
        let shape = registry.describe(kind);
        if !wire.is_nil() {
            if let Ok(shape) = &shape {
                if !shape.unions.is_empty() {
                    let parts = wire
                        .as_array()
                        .filter(|v| v.len() == 2)
                        .ok_or("Invalid typed parameter union")?;
                    let tag = parts[0].as_i64().ok_or("Missing parameter type tag")?;
                    let target = shape
                        .unions
                        .iter()
                        .find(|(t, _)| *t == tag)
                        .ok_or("Unknown parameter type")?;
                    let mut next = slots;
                    next.push(1);
                    return visit(
                        registry,
                        &target.1,
                        &parts[1],
                        path,
                        next,
                        all,
                        result,
                        depth + 1,
                    );
                }
                if !shape.is_enum {
                    let members = &shape.members;
                    for (name, index, inner) in members {
                        if (!all && path.is_empty() && HIDDEN.contains(&name.as_str()))
                            || (!all && name == "IsRequired")
                        {
                            continue;
                        }
                        let item = wire
                            .as_array()
                            .and_then(|v| v.get(*index))
                            .ok_or("Incomplete typed parameter model")?;
                        let mut next = slots.clone();
                        next.push(*index);
                        let escaped = name.replace('~', "~0").replace('/', "~1");
                        visit(
                            registry,
                            inner,
                            item,
                            &format!("{path}/{escaped}"),
                            next,
                            all,
                            result,
                            depth + 1,
                        )?;
                    }
                    return Ok(());
                }
            }
        }
        let (value, json_kind) = if wire.is_nil() {
            ("null".into(), 7)
        } else if shape.as_ref().is_ok_and(|v| v.is_enum) {
            (
                wire.as_i64().ok_or("Invalid parameter enum")?.to_string(),
                4,
            )
        } else {
            match kind {
                "System.Boolean" => {
                    if wire.as_bool().ok_or("Invalid parameter boolean")? {
                        ("true".into(), 5)
                    } else {
                        ("false".into(), 6)
                    }
                }
                "System.String" | "System.Guid" => {
                    (wire.as_str().ok_or("Invalid parameter string")?.into(), 3)
                }
                "System.DateTime" => {
                    let dt = timestamp(wire)?;
                    (utc_roundtrip(dt), 3)
                }
                "System.DateTimeOffset" => (reference_offset(wire)?, 3),
                "System.DateOnly" => (date_only(wire)?.to_string(), 3),
                "System.TimeSpan" => (
                    timespan(wire.as_i64().ok_or("Invalid parameter TimeSpan")?),
                    3,
                ),
                "System.Decimal" => (
                    ExactDecimal::parse(wire.as_str().ok_or("Invalid decimal field")?)?
                        .as_str()
                        .into(),
                    4,
                ),
                "System.Double" | "System.Single" => {
                    let n = wire.as_f64().ok_or("Invalid parameter number")?;
                    if !n.is_finite() {
                        return Err("Nonfinite parameter field".into());
                    }
                    (n.to_string(), 4)
                }
                _ => {
                    let v = registry.project(kind, wire)?;
                    if !v.is_number() {
                        return Err(format!("Unsupported parameter scalar {kind}"));
                    }
                    (v.to_string(), 4)
                }
            }
        };
        let (group, name) = labels(path);
        result.push(Leaf {
            field: ParameterField {
                path: path.into(),
                value,
                kind: json_kind,
                group,
                name,
            },
            kind: kind.into(),
            slots,
        });
        Ok(())
    }
    let mut result = vec![];
    visit(
        registry,
        kind,
        wire,
        "",
        vec![],
        include_identity,
        &mut result,
        0,
    )?;
    Ok(result)
}
/// ParameterValue wrappers contain Value at slot zero; its name is deliberately
/// not exposed as an extra /Value prefix in the existing editor contract.
fn payload<'a>(registry: &Registry, wire: &'a Value) -> Result<(String, &'a Value), String> {
    let shape =
        registry.describe("TomasAI.IFM.Domain.Reference.Shared.ParameterSets.ParameterSetValue")?;
    let parts = wire
        .as_array()
        .filter(|v| v.len() == 2)
        .ok_or("Invalid parameter union")?;
    let tag = parts[0].as_i64().ok_or("Invalid parameter tag")?;
    let wrapper = shape
        .unions
        .iter()
        .find(|(n, _)| *n == tag)
        .ok_or("Unknown parameter union")?;
    let wrapper = registry.describe(&wrapper.1)?;
    let member = wrapper
        .members
        .iter()
        .find(|(name, _, _)| name == "Value")
        .ok_or("No typed parameter payload")?;
    Ok((
        member.2.clone(),
        parts[1]
            .as_array()
            .and_then(|v| v.get(member.1))
            .ok_or("Missing typed parameter payload")?,
    ))
}
pub fn read(
    registry: &Registry,
    wire: &Value,
    include_identity: bool,
) -> Result<Vec<ParameterField>, String> {
    let (kind, value) = payload(registry, wire)?;
    Ok(leaves(registry, &kind, value, include_identity)?
        .into_iter()
        .map(|v| v.field)
        .collect())
}
pub fn display(registry: &Registry, wire: &Value) -> Result<String, String> {
    Ok(read(registry, wire, true)?
        .into_iter()
        .map(|v| format!("{}: {}", v.path, v.value))
        .collect::<Vec<_>>()
        .join("\r\n"))
}
pub fn apply(
    registry: &Registry,
    wire: &Value,
    changes: &[ParameterField],
) -> Result<Value, String> {
    let (kind, value) = payload(registry, wire)?;
    let allowed = leaves(registry, &kind, value, false)?
        .into_iter()
        .map(|v| (v.field.path.clone(), v))
        .collect::<BTreeMap<_, _>>();
    let mut seen = BTreeSet::new();
    let mut copy = wire.clone();
    for change in changes {
        let leaf = allowed
            .get(&change.path)
            .filter(|v| v.field.kind == change.kind && seen.insert(change.path.clone()))
            .ok_or("Invalid or duplicate parameter field")?;
        let replacement = if leaf.field.kind == 7 {
            if change.value != "null" {
                return Err("Null fields cannot change type".into());
            }
            Value::Nil
        } else {
            if registry.describe(&leaf.kind).is_ok_and(|v| v.is_enum) {
                let n = change
                    .value
                    .parse::<i32>()
                    .map_err(|_| "Invalid enum integer")?;
                Value::from(n)
            } else {
                let input = match leaf.kind.as_str() {
                    "System.Decimal"
                    | "System.String"
                    | "System.Guid"
                    | "System.DateTime"
                    | "System.DateTimeOffset"
                    | "System.DateOnly" => json!(change.value),
                    "System.TimeSpan" => json!(parse_timespan(&change.value)?),
                    _ => serde_json::from_str(&change.value)
                        .map_err(|_| "Invalid scalar field value")?,
                };
                registry.value_from_json(&leaf.kind, &input)?
            }
        };
        fn slot(value: &mut Value, index: usize) -> Result<&mut Value, String> {
            match value {
                Value::Array(values) => {
                    values.get_mut(index).ok_or("Missing parameter slot".into())
                }
                _ => Err("Invalid parameter array".into()),
            }
        }
        let mut target = slot(slot(&mut copy, 1)?, 0)?;
        for slot in &leaf.slots {
            target = match target {
                Value::Array(values) => values
                    .get_mut(*slot)
                    .ok_or("Missing parameter field slot")?,
                _ => return Err("Invalid parameter field path".into()),
            };
        }
        *target = replacement;
    }
    Ok(copy)
}
