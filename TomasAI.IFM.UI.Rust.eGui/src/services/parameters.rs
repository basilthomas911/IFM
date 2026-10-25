//! Native typed parameter service. Initializer templates are generated from the
//! compiled public C# contracts; they contain no environment data or business
//! rules. Read pagination and writes share the caller's absolute deadline.
use crate::backend::{codec, contracts::*, schema::Registry, transport::Backend};
use rmpv::Value;
use std::collections::BTreeMap;
pub const ROOT: &str = "TomasAI.IFM.Domain.Reference.Shared.ParameterSets";
pub fn kind(name: &str) -> String {
    format!("{ROOT}.{name}")
}
/// Convert named UI documents back into the typed backend model, accepting the
/// numeric enum representation emitted by the unchanged hybrid bridge.
pub fn from_document(
    registry: &Registry,
    kind: &str,
    json: &serde_json::Value,
) -> Result<Value, String> {
    if json.is_null() {
        return Ok(Value::Nil);
    }
    let kind = kind
        .strip_prefix("System.Nullable`1[")
        .and_then(|v| v.strip_suffix(']'))
        .unwrap_or(kind);
    if let Some(inner) = kind.strip_suffix("[]").or_else(|| {
        kind.strip_prefix("System.Collections.Immutable.ImmutableArray`1[")
            .and_then(|v| v.strip_suffix(']'))
    }) {
        return json
            .as_array()
            .filter(|v| v.len() <= 100_000)
            .ok_or("Invalid parameter document array")?
            .iter()
            .map(|v| from_document(registry, inner, v))
            .collect::<Result<Vec<_>, _>>()
            .map(Value::Array);
    }
    if let Ok(shape) = registry.describe(kind) {
        if shape.is_enum {
            return if let Some(n) = json.as_i64() {
                i32::try_from(n)
                    .map(Value::from)
                    .map_err(|_| "Parameter enum overflow".into())
            } else {
                registry.value_from_json(kind, json)
            };
        }
        if !shape.unions.is_empty() {
            return Err("Select a concrete parameter document type".into());
        }
        let fields = json
            .as_object()
            .ok_or("Expected parameter document object")?
            .iter()
            .map(|(name, value)| {
                let member = shape
                    .members
                    .iter()
                    .find(|(n, _, _)| n == name)
                    .ok_or_else(|| format!("Unrecognized parameter property {name}"))?;
                Ok((name.clone(), from_document(registry, &member.2, value)?))
            })
            .collect::<Result<BTreeMap<_, _>, String>>()?;
        return registry.encode(kind, &fields);
    }
    registry.value_from_json(kind, json)
}
pub fn document(
    registry: &Registry,
    kind: &str,
    wire: &Value,
    enums: bool,
) -> Result<serde_json::Value, String> {
    use serde_json::{Value as Json, json};
    if wire.is_nil() {
        return Ok(Json::Null);
    }
    let kind = kind
        .strip_prefix("System.Nullable`1[")
        .and_then(|v| v.strip_suffix(']'))
        .unwrap_or(kind);
    if let Some(inner) = kind.strip_suffix("[]").or_else(|| {
        kind.strip_prefix("System.Collections.Immutable.ImmutableArray`1[")
            .and_then(|v| v.strip_suffix(']'))
    }) {
        return wire
            .as_array()
            .ok_or("Invalid parameter document array")?
            .iter()
            .map(|v| document(registry, inner, v, enums))
            .collect::<Result<Vec<_>, _>>()
            .map(Json::Array);
    }
    if let Ok(shape) = registry.describe(kind) {
        if shape.is_enum {
            return if enums {
                registry.project(kind, wire)
            } else {
                Ok(json!(wire.as_i64().ok_or("Invalid parameter enum")?))
            };
        }
        if !shape.unions.is_empty() {
            let parts = wire
                .as_array()
                .filter(|v| v.len() == 2)
                .ok_or("Invalid parameter document union")?;
            let tag = parts[0].as_i64().ok_or("Invalid parameter document tag")?;
            let target = shape
                .unions
                .iter()
                .find(|(t, _)| *t == tag)
                .ok_or("Unknown parameter document tag")?;
            return document(registry, &target.1, &parts[1], enums);
        }
        let slots = wire.as_array().ok_or("Invalid parameter document model")?;
        let mut json = serde_json::Map::new();
        for (name, index, inner) in shape.members {
            json.insert(
                name,
                document(
                    registry,
                    &inner,
                    slots.get(index).ok_or("Missing parameter document slot")?,
                    enums,
                )?,
            );
        }
        if kind == self::kind("ParameterValidationReport") {
            let issues = wire
                .as_array()
                .and_then(|v| v.get(1))
                .and_then(Value::as_array)
                .ok_or("Invalid validation issues")?;
            let valid = issues.iter().all(|v| {
                registry
                    .field(&self::kind("ParameterValidationIssue"), "Severity", v)
                    .is_ok_and(|v| v.as_str() != Some("Error"))
            });
            json.insert("IsValid".into(), Json::Bool(valid));
        }
        return Ok(Json::Object(json));
    }
    match kind {
        "System.DateTimeOffset" => Ok(json!(reference_offset(wire)?)),
        "System.DateTime" => Ok(json!(utc_roundtrip(timestamp(wire)?))),
        "System.TimeSpan" => Ok(json!(crate::services::parameter_fields::timespan(
            wire.as_i64().ok_or("Invalid duration")?
        ))),
        _ => registry.project(kind, wire),
    }
}
pub fn overlay(
    registry: &Registry,
    name: &str,
    original: &Value,
    fields: &BTreeMap<String, Value>,
) -> Result<Value, String> {
    overlay_type(registry, &kind(name), original, fields)
}
pub fn overlay_type(
    registry: &Registry,
    kind: &str,
    original: &Value,
    fields: &BTreeMap<String, Value>,
) -> Result<Value, String> {
    let shape = registry.describe(kind)?;
    let mut copy = original.clone();
    let Value::Array(ref mut slots) = copy else {
        return Err("Invalid parameter request template".into());
    };
    for (key, value) in fields {
        let (_, index, _) = shape
            .members
            .iter()
            .find(|(n, _, _)| n == key)
            .ok_or_else(|| format!("Unknown parameter request field {key}"))?;
        let slot = slots
            .get_mut(*index)
            .ok_or("Incomplete parameter request template")?;
        *slot = value.clone();
    }
    Ok(copy)
}
pub fn template(name: &str) -> Result<Value, String> {
    // Generated data is compiled into the executable, never read from an external
    // script/configuration at runtime. Unknown requests cannot gain a route.
    let values: BTreeMap<String, Vec<u8>> = serde_json::from_str(include_str!(
        "../../tests/fixtures/backend/parameter-request-defaults.json"
    ))
    .map_err(|e| e.to_string())?;
    codec::decode(values.get(name).ok_or("Unsupported parameter request")?)
}
pub async fn query(
    backend: &Backend,
    registry: &Registry,
    name: &str,
    mut fields: BTreeMap<String, Value>,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    let (actor, verb) = registry.route(&kind(name))?;
    if actor != "ParameterSetQuery" {
        return Err("Not a parameter query".into());
    }
    let subject = ActorSubject::query(actor, verb, "none");
    fields.insert("Subject".into(), subject.wire());
    let body = overlay(registry, name, &template(name)?, &fields)?;
    let reply = backend
        .request_until(&subject, &body, deadline, None)
        .await?;
    Ok(service_result(&reply)?.clone())
}
pub async fn versions(
    backend: &Backend,
    registry: &Registry,
    component: &str,
    set: Option<uuid::Uuid>,
    deadline: tokio::time::Instant,
) -> Result<Vec<Value>, String> {
    let mut fields = BTreeMap::from([
        ("ComponentCode".into(), component.into()),
        ("SetId".into(), set.unwrap_or_default().to_string().into()),
    ]);
    let mut all = vec![];
    let mut cursor = None;
    let mut identities = std::collections::BTreeSet::new();
    for _ in 0..100 {
        let wire = query(
            backend,
            registry,
            "ListParameterVersionsQuery",
            fields.clone(),
            deadline,
        )
        .await?;
        let rows = wire
            .as_array()
            .filter(|v| v.len() <= 100)
            .ok_or("Invalid parameter version page")?;
        for wire in rows {
            let row = registry.project(&kind("ParameterSetVersion"), wire)?;
            if row["Reference"]["ComponentCode"] != component {
                return Err("Parameter page crossed selected component".into());
            }
            let id = row["Reference"]["SetId"]
                .as_str()
                .ok_or("Missing parameter set identity")?;
            if set.is_some_and(|v| v.to_string() != id) {
                return Err("Parameter page crossed set identity".into());
            }
            let version = row["Reference"]["Version"]
                .as_i64()
                .filter(|v| *v > 0)
                .ok_or("Invalid parameter version")?;
            if !identities.insert((id.to_string(), version)) {
                return Err("Duplicate parameter version or nonadvancing page".into());
            }
            all.push(wire.clone());
        }
        if rows.len() < 100 {
            return Ok(all);
        }
        let last = registry.project(&kind("ParameterSetVersion"), rows.last().unwrap())?;
        let name = last["Name"].as_str().ok_or("Missing parameter name")?;
        let id = last["Reference"]["SetId"]
            .as_str()
            .ok_or("Missing parameter identity")?;
        let version = last["Reference"]["Version"]
            .as_i64()
            .ok_or("Missing parameter version")?;
        let next = (name.to_string(), id.to_string(), version);
        if cursor.as_ref() == Some(&next) {
            return Err("Parameter page cursor did not advance".into());
        }
        cursor = Some(next);
        fields.insert("AfterName".into(), name.into());
        fields.insert("AfterSetId".into(), id.into());
        fields.insert("AfterVersion".into(), version.into());
    }
    Err("Parameter listing exceeds 10000 versions; select a specific set".into())
}
pub async fn command(
    backend: &Backend,
    registry: &Registry,
    name: &str,
    entity: uuid::Uuid,
    request: uuid::Uuid,
    mut fields: BTreeMap<String, Value>,
    deadline: tokio::time::Instant,
) -> Result<(), String> {
    let (actor, verb) = registry.route(&kind(name))?;
    if !matches!(actor, "ParameterSetCommand" | "ParameterAssignmentCommand") {
        return Err("Unsupported parameter mutation actor".into());
    }
    let subject = ActorSubject {
        kind: ActorKind::Command,
        name: actor.into(),
        verb: verb.into(),
        entity: entity.simple().to_string(),
    };
    if entity.is_nil() || request.is_nil() {
        return Err("Missing parameter command identity".into());
    }
    fields.insert("Subject".into(), subject.wire());
    fields.insert(
        "EntityId".into(),
        Value::Array(vec![entity.to_string().into()]),
    );
    fields.insert("CommandId".into(), request.to_string().into());
    let body = overlay(registry, name, &template(name)?, &fields)?;
    let reply = backend
        .request_until(&subject, &body, deadline, None)
        .await
        .map_err(|e| format!("OutcomeUnknown: {e}"))?;
    match service_result(&reply) {
        Ok(v)
            if v.as_array()
                .and_then(|v| v.first())
                .and_then(Value::as_str)
                .is_some_and(|s| uuid::Uuid::parse_str(s).is_ok_and(|v| !v.is_nil())) =>
        {
            Ok(())
        }
        Ok(_) => Err("OutcomeUnknown: malformed parameter acknowledgement".into()),
        Err(error)
            if reply
                .as_array()
                .and_then(|v| v.first())
                .and_then(Value::as_bool)
                == Some(false) =>
        {
            Err(error)
        }
        Err(error) => Err(format!("OutcomeUnknown: {error}")),
    }
}
