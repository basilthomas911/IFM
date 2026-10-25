//! Metadata exported from compiled C# contracts by the development-only oracle.
//! Array positions (including retired gaps) are preserved. Unknown contracts fail
//! explicitly instead of guessing property order or converting money to floats.
use super::contracts::{date_only, timestamp};
use rmpv::Value;
use serde::Deserialize;
use std::collections::BTreeMap;

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Schema {
    #[serde(default)]
    error_id: Option<i64>,
    #[serde(rename = "type")]
    name: String,
    is_enum: bool,
    enum_values: Option<Vec<EnumValue>>,
    members: Vec<Member>,
    actor: Option<String>,
    verb: Option<String>,
    #[serde(default)]
    map_by_name: bool,
    #[serde(default)]
    generic_parameters: Vec<String>,
    #[serde(default)]
    unions: Vec<Union>,
}
#[derive(Deserialize)]
struct Union {
    tag: i64,
    #[serde(rename = "type")]
    kind: String,
}
#[derive(Deserialize)]
struct EnumValue {
    name: String,
    value: i64,
}
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Member {
    name: String,
    index: Option<usize>,
    map_key: Option<String>,
    #[serde(rename = "type")]
    kind: String,
}

pub struct Registry {
    schemas: BTreeMap<String, Schema>,
}
/// Read-only compiled wire shape used by typed configuration editors. Numeric
/// positions allow a leaf edit to retain untouched slots and unknown tails.
pub struct ContractShape {
    pub is_enum: bool,
    pub members: Vec<(String, usize, String)>,
    pub unions: Vec<(i64, String)>,
}
impl Registry {
    pub fn describe(&self, kind: &str) -> Result<ContractShape, String> {
        let schema = self
            .schemas
            .get(kind)
            .ok_or_else(|| format!("Unknown model {kind}"))?;
        if schema.map_by_name || schema.members.iter().any(|m| m.index.is_none()) {
            return Err("A numeric-key model is required".into());
        }
        Ok(ContractShape {
            is_enum: schema.is_enum,
            members: schema
                .members
                .iter()
                .map(|m| (m.name.clone(), m.index.unwrap(), m.kind.clone()))
                .collect(),
            unions: schema
                .unions
                .iter()
                .map(|v| (v.tag, v.kind.clone()))
                .collect(),
        })
    }
    pub fn enum_names(&self, kind: &str) -> Result<Vec<String>, String> {
        self.schemas
            .get(kind)
            .filter(|s| s.is_enum)
            .and_then(|s| s.enum_values.as_ref())
            .map(|v| v.iter().map(|v| v.name.clone()).collect())
            .ok_or_else(|| format!("Unknown enum {kind}"))
    }
    /// Edit only named contract members while retaining every other wire slot,
    /// including provider evidence, unknown tails and retired positions. Callers
    /// must apply their feature's editable-field whitelist before invoking this.
    /// This adapter is a serializer, not authorization or business validation.
    pub fn patch(
        &self,
        kind: &str,
        original: &Value,
        changes: &BTreeMap<String, serde_json::Value>,
    ) -> Result<Value, String> {
        let schema = self
            .schemas
            .get(kind)
            .filter(|s| !s.is_enum && s.unions.is_empty())
            .ok_or("A concrete model contract is required")?;
        if schema.map_by_name && !matches!(original, Value::Map(_))
            || !schema.map_by_name && !matches!(original, Value::Array(_))
        {
            return Err("Model payload does not match its declared shape".into());
        }
        let mut output = original.clone();
        for (name, value) in changes {
            let member = schema
                .members
                .iter()
                .find(|m| &m.name == name)
                .ok_or_else(|| format!("Unknown model field {kind}.{name}"))?;
            let next = self.value_from_json(&member.kind, value)?;
            if let Some(index) = member.index {
                let Value::Array(values) = &mut output else {
                    return Err("Expected model array".into());
                };
                *values.get_mut(index).ok_or("Incomplete model array")? = next;
            } else {
                let key = member
                    .map_key
                    .as_deref()
                    .ok_or("Unclassified model field")?;
                let Value::Map(values) = &mut output else {
                    return Err("Expected model map".into());
                };
                if let Some((_, value)) = values
                    .iter_mut()
                    .find(|(name, _)| name.as_str() == Some(key))
                {
                    *value = next;
                } else {
                    values.push((key.into(), next));
                }
            }
        }
        Ok(output)
    }
    /// Copy same-named, identically typed array members without a display JSON
    /// roundtrip. Provider timestamp offsets and exact decimal scales are data,
    /// so a projection normalized for display must not become their new source.
    pub fn copy_members(
        &self,
        target_kind: &str,
        target: &mut Value,
        source_kind: &str,
        source: &Value,
        names: &[&str],
    ) -> Result<(), String> {
        let target_schema = self
            .schemas
            .get(target_kind)
            .ok_or("Unknown target model")?;
        let source_schema = self
            .schemas
            .get(source_kind)
            .ok_or("Unknown source model")?;
        let Value::Array(target) = target else {
            return Err("Expected target model array".into());
        };
        let source = source.as_array().ok_or("Expected source model array")?;
        for name in names {
            let from = source_schema
                .members
                .iter()
                .find(|m| m.name == *name)
                .ok_or("Unknown source member")?;
            let to = target_schema
                .members
                .iter()
                .find(|m| m.name == *name)
                .ok_or("Unknown target member")?;
            if from.kind != to.kind {
                return Err(format!("Cannot copy differently typed member {name}"));
            }
            let value = source
                .get(from.index.ok_or("Expected source array member")?)
                .ok_or("Incomplete source model")?;
            *target
                .get_mut(to.index.ok_or("Expected target array member")?)
                .ok_or("Incomplete target model")? = value.clone();
        }
        Ok(())
    }
    /// Convert an explicitly typed editor value. Decimal inputs stay exact
    /// strings. Polymorphic unions require dedicated adapters; guessing a union
    /// subtype from display property names could change a persisted identity.
    pub fn value_from_json(&self, kind: &str, value: &serde_json::Value) -> Result<Value, String> {
        if matches!(
            kind,
            "System.Byte[]"
                | "System.ReadOnlyMemory`1[System.Byte]"
                | "System.Memory`1[System.Byte]"
        ) && !value.is_null()
        {
            return value
                .as_array()
                .filter(|v| v.len() <= 16 * 1024 * 1024)
                .ok_or("Invalid binary model input")?
                .iter()
                .map(|v| {
                    v.as_u64()
                        .and_then(|n| u8::try_from(n).ok())
                        .ok_or("Binary model byte outside range".to_string())
                })
                .collect::<Result<Vec<_>, _>>()
                .map(Value::Binary);
        }
        use super::contracts::*;
        use chrono::Datelike;
        if let Some(inner) = kind
            .strip_prefix("System.Nullable`1[")
            .and_then(|s| s.strip_suffix(']'))
        {
            return if value.is_null() {
                Ok(Value::Nil)
            } else {
                self.value_from_json(inner, value)
            };
        }
        if value.is_null() {
            if kind == "System.String"
                || kind.ends_with("[]")
                || self.schemas.get(kind).is_some_and(|s| !s.is_enum)
            {
                return Ok(Value::Nil);
            }
            return Err(format!("Null is not valid for {kind}"));
        }
        if let Some(inner) = kind.strip_suffix("[]") {
            return value
                .as_array()
                .filter(|v| v.len() <= 100_000)
                .ok_or("Invalid or oversized model array")?
                .iter()
                .map(|v| self.value_from_json(inner, v))
                .collect::<Result<Vec<_>, _>>()
                .map(Value::Array);
        }
        let text = || {
            value
                .as_str()
                .ok_or_else(|| format!("Expected string for {kind}"))
        };
        match kind {
            "System.String" => return Ok(text()?.into()),
            "System.Decimal" => return Ok(ExactDecimal::parse(text()?)?.wire()),
            "System.Guid" => {
                return uuid::Uuid::parse_str(text()?)
                    .map(|v| v.to_string().into())
                    .map_err(|_| "Invalid GUID".into());
            }
            "System.Boolean" => {
                return value
                    .as_bool()
                    .map(Into::into)
                    .ok_or("Expected Boolean".into());
            }
            "System.DateOnly" => {
                let date = chrono::NaiveDate::parse_from_str(text()?, "%Y-%m-%d")
                    .map_err(|_| "Invalid DateOnly")?;
                if !(1..=9999).contains(&date.year()) {
                    return Err("Date outside .NET range".into());
                }
                return Ok(date_only_wire(date));
            }
            "System.TimeOnly" => {
                use chrono::Timelike;
                let time = chrono::NaiveTime::parse_from_str(text()?, "%H:%M:%S%.f")
                    .map_err(|_| "Invalid TimeOnly")?;
                if time.nanosecond() >= 1_000_000_000 || time.nanosecond() % 100 != 0 {
                    return Err("TimeOnly requires 100 ns precision".into());
                }
                return Ok((time.num_seconds_from_midnight() as u64 * 10_000_000
                    + time.nanosecond() as u64 / 100)
                    .into());
            }
            "System.DateTime" | "System.DateTimeOffset" => {
                let time = chrono::DateTime::parse_from_rfc3339(text()?)
                    .map_err(|_| "An explicit ISO timestamp offset is required")?;
                if !(1..=9999).contains(&time.year())
                    || !(1..=9999).contains(&time.to_utc().year())
                    || time.timestamp_subsec_nanos() % 100 != 0
                {
                    return Err("Timestamp outside .NET precision or date range".into());
                }
                if kind == "System.DateTime" {
                    return Ok(timestamp_wire(time.to_utc()));
                }
                let seconds = time.offset().local_minus_utc();
                if seconds % 60 != 0 || !(-840..=840).contains(&(seconds / 60)) {
                    return Err("Invalid DateTimeOffset zone".into());
                }
                return Ok(Value::Array(vec![
                    timestamp_wire(time.naive_local().and_utc()),
                    (seconds / 60).into(),
                ]));
            }
            "System.SByte" | "System.Int16" | "System.Int32" | "System.Int64"
            | "System.TimeSpan" => {
                let number = value.as_i64().ok_or("Expected signed integer")?;
                let valid = match kind {
                    "System.SByte" => i8::try_from(number).is_ok(),
                    "System.Int16" => i16::try_from(number).is_ok(),
                    "System.Int32" => i32::try_from(number).is_ok(),
                    _ => true,
                };
                return if valid {
                    Ok(number.into())
                } else {
                    Err("Signed integer overflow".into())
                };
            }
            "System.Byte" | "System.UInt16" | "System.UInt32" | "System.UInt64" => {
                let number = value.as_u64().ok_or("Expected unsigned integer")?;
                let valid = match kind {
                    "System.Byte" => u8::try_from(number).is_ok(),
                    "System.UInt16" => u16::try_from(number).is_ok(),
                    "System.UInt32" => u32::try_from(number).is_ok(),
                    _ => true,
                };
                return if valid {
                    Ok(number.into())
                } else {
                    Err("Unsigned integer overflow".into())
                };
            }
            "System.Double" | "System.Single" => {
                let number = value
                    .as_f64()
                    .filter(|n| n.is_finite())
                    .ok_or("Expected finite number")?;
                if kind == "System.Single" {
                    let number = number as f32;
                    if !number.is_finite() {
                        return Err("Single overflow".into());
                    }
                    return Ok(Value::F32(number));
                }
                return Ok(Value::F64(number));
            }
            _ => {}
        }
        let schema = self
            .schemas
            .get(kind)
            .ok_or_else(|| format!("No input adapter registered for {kind}"))?;
        if schema.is_enum {
            return Ok(self.enum_value(kind, text()?)?.into());
        }
        if !schema.unions.is_empty() {
            return Err("A polymorphic union needs an explicit typed adapter".into());
        }
        let fields = value
            .as_object()
            .ok_or("Expected model object")?
            .iter()
            .map(|(name, value)| {
                let member = schema
                    .members
                    .iter()
                    .find(|m| &m.name == name)
                    .ok_or_else(|| format!("Unknown model field {kind}.{name}"))?;
                Ok((name.clone(), self.value_from_json(&member.kind, value)?))
            })
            .collect::<Result<BTreeMap<_, _>, String>>()?;
        self.encode(kind, &fields)
    }
    /// Read a terminal envelope field without projecting an unrelated large payload.
    pub fn field(&self, kind: &str, name: &str, wire: &Value) -> Result<serde_json::Value, String> {
        let schema = self
            .schemas
            .get(kind)
            .ok_or_else(|| format!("Unknown contract {kind}"))?;
        let member = schema
            .members
            .iter()
            .find(|m| m.name == name)
            .ok_or_else(|| format!("Missing field {kind}.{name}"))?;
        let value = if let Some(index) = member.index {
            wire.as_array().and_then(|a| a.get(index))
        } else {
            wire.as_map().and_then(|m| {
                m.iter()
                    .find(|(key, _)| key.as_str() == member.map_key.as_deref())
                    .map(|(_, value)| value)
            })
        }
        .ok_or_else(|| format!("Missing wire field {kind}.{name}"))?;
        self.project(&member.kind, value)
    }
    pub fn error_id(&self, kind: &str) -> Result<i64, String> {
        self.schemas
            .get(kind)
            .and_then(|s| s.error_id)
            .ok_or_else(|| format!("Missing ErrorId for {kind}"))
    }
    pub fn enum_value(&self, kind: &str, name: &str) -> Result<i64, String> {
        self.schemas
            .get(kind)
            .and_then(|s| s.enum_values.as_ref())
            .and_then(|v| v.iter().find(|v| v.name == name))
            .map(|v| v.value)
            .ok_or_else(|| format!("Unknown enum value {kind}.{name}"))
    }
    pub fn compiled() -> Result<Self, String> {
        let schemas: Vec<Schema> =
            serde_json::from_str(include_str!("../../tests/fixtures/backend/contracts.json"))
                .map_err(|e| e.to_string())?;
        Ok(Self {
            schemas: schemas.into_iter().map(|s| (s.name.clone(), s)).collect(),
        })
    }
    pub fn route(&self, kind: &str) -> Result<(&str, &str), String> {
        let schema = self
            .schemas
            .get(kind)
            .ok_or_else(|| format!("Unknown backend contract {kind}"))?;
        Ok((
            schema.actor.as_deref().ok_or("Missing actor constant")?,
            schema.verb.as_deref().ok_or("Missing verb constant")?,
        ))
    }
    /// Build a keyed contract from wire values. Caller supplies validated fields.
    pub fn encode(&self, kind: &str, fields: &BTreeMap<String, Value>) -> Result<Value, String> {
        let schema = self
            .schemas
            .get(kind)
            .ok_or_else(|| format!("Unknown backend contract {kind}"))?;
        for key in fields.keys() {
            if !schema
                .members
                .iter()
                .any(|m| &m.name == key || m.map_key.as_ref() == Some(key))
            {
                return Err(format!("Unknown field {kind}.{key}"));
            }
        }
        if !schema.unions.is_empty() {
            return Err("Use an explicit union adapter".into());
        }
        if schema.map_by_name || schema.members.iter().any(|m| m.map_key.is_some()) {
            return Ok(Value::Map(
                schema
                    .members
                    .iter()
                    .map(|m| {
                        let key = m.map_key.as_ref().unwrap_or(&m.name);
                        (
                            Value::from(key.clone()),
                            fields
                                .get(&m.name)
                                .or_else(|| fields.get(key))
                                .cloned()
                                .unwrap_or_else(|| self.default_value(&m.kind)),
                        )
                    })
                    .collect(),
            ));
        }
        let mut array = vec![
            Value::Nil;
            schema
                .members
                .iter()
                .filter_map(|m| m.index)
                .max()
                .map_or(0, |n| n + 1)
        ];
        for member in &schema.members {
            let index = member.index.ok_or("Unclassified MessagePack member")?;
            array[index] = fields
                .get(&member.name)
                .cloned()
                .unwrap_or_else(|| self.default_value(&member.kind));
        }
        Ok(Value::Array(array))
    }
    fn default_value(&self, kind: &str) -> Value {
        match kind {
            "System.Boolean" => false.into(),
            "System.Byte" | "System.SByte" | "System.Int16" | "System.Int32" | "System.Int64"
            | "System.UInt16" | "System.UInt32" | "System.UInt64" | "System.DateOnly"
            | "System.TimeSpan" | "System.TimeOnly" => 0.into(),
            "System.Decimal" => "0".into(),
            "System.Double" | "System.Single" => Value::F64(0.0),
            "System.Guid" => "00000000-0000-0000-0000-000000000000".into(),
            _ if self.schemas.get(kind).is_some_and(|s| s.is_enum) => 0.into(),
            _ => Value::Nil,
        }
    }
    /// Shape wire values for presentation. Decimal values remain invariant strings.
    pub fn project(&self, kind: &str, wire: &Value) -> Result<serde_json::Value, String> {
        use serde_json::Value as Json;
        if wire.is_nil() {
            return Ok(Json::Null);
        }
        if matches!(
            kind,
            "System.Byte[]"
                | "System.ReadOnlyMemory`1[System.Byte]"
                | "System.Memory`1[System.Byte]"
        ) {
            return Ok(Json::Array(
                (if let Value::Binary(bytes) = wire {
                    Some(bytes.as_slice())
                } else {
                    None
                })
                .filter(|v| v.len() <= 16 * 1024 * 1024)
                .ok_or("Invalid or oversized binary model")?
                .iter()
                .map(|v| Json::from(*v))
                .collect(),
            ));
        }
        if let Some(inner) = kind
            .strip_prefix("System.Nullable`1[")
            .and_then(|s| s.strip_suffix(']'))
        {
            return self.project(inner, wire);
        }
        if let Some(inner) = kind
            .strip_prefix("System.Collections.Generic.List`1[")
            .and_then(|s| s.strip_suffix(']'))
        {
            let array = wire
                .as_array()
                .ok_or_else(|| format!("Expected list for {kind}"))?;
            return array
                .iter()
                .map(|v| self.project(inner, v))
                .collect::<Result<Vec<_>, _>>()
                .map(Json::Array);
        }
        if let Some((base, args)) = generic(kind) {
            if args.len() == 1
                && matches!(
                    base,
                    "System.Collections.Generic.IReadOnlyList`1"
                        | "System.Collections.Generic.IReadOnlyCollection`1"
                        | "System.Collections.Generic.IEnumerable`1"
                        | "System.Collections.Generic.HashSet`1"
                        | "System.Collections.Immutable.ImmutableArray`1"
                        | "System.Collections.Immutable.ImmutableList`1"
                )
            {
                return wire
                    .as_array()
                    .ok_or("Expected collection")?
                    .iter()
                    .map(|v| self.project(args[0], v))
                    .collect::<Result<Vec<_>, _>>()
                    .map(Json::Array);
            }
            if args.len() == 2
                && matches!(
                    base,
                    "System.Collections.Generic.Dictionary`2"
                        | "System.Collections.Generic.IReadOnlyDictionary`2"
                        | "System.Collections.Generic.IDictionary`2"
                        | "System.Collections.Generic.SortedDictionary`2"
                        | "System.Collections.Immutable.ImmutableDictionary`2"
                )
            {
                let entries = wire.as_map().ok_or("Expected dictionary")?;
                if args[0] == "System.String" {
                    return entries
                        .iter()
                        .map(|(k, v)| {
                            Ok((
                                k.as_str().ok_or("Expected string dictionary key")?.into(),
                                self.project(args[1], v)?,
                            ))
                        })
                        .collect::<Result<serde_json::Map<_, _>, String>>()
                        .map(Json::Object);
                }
                return entries.iter().map(|(k,v)|Ok(serde_json::json!({"Key":self.project(args[0],k)?,"Value":self.project(args[1],v)?}))).collect::<Result<Vec<_>,String>>().map(Json::Array);
            }
        }
        if let Some(inner) = kind.strip_suffix("[]") {
            let array = wire
                .as_array()
                .ok_or_else(|| format!("Expected array for {kind}"))?;
            return array
                .iter()
                .map(|v| self.project(inner, v))
                .collect::<Result<Vec<_>, _>>()
                .map(Json::Array);
        }
        match kind {
            "System.TimeOnly" => {
                // MessagePack-CSharp writes ticks since midnight, not a Unix
                // instant. Preserve 100 ns precision without adding a date/zone.
                let ticks = wire
                    .as_u64()
                    .filter(|t| *t < 864_000_000_000)
                    .ok_or("Invalid TimeOnly ticks")?;
                let seconds = ticks / 10_000_000;
                return Ok(Json::String(format!(
                    "{:02}:{:02}:{:02}.{:07}",
                    seconds / 3600,
                    (seconds / 60) % 60,
                    seconds % 60,
                    ticks % 10_000_000
                )));
            }
            "System.DateOnly" => return Ok(Json::String(date_only(wire)?.to_string())),
            "System.DateTime" => {
                return Ok(Json::String(
                    timestamp(wire)?.to_rfc3339_opts(chrono::SecondsFormat::AutoSi, true),
                ));
            }
            "System.DateTimeOffset" => {
                return Ok(Json::String(
                    super::contracts::datetime_offset(wire)?
                        .to_rfc3339_opts(chrono::SecondsFormat::AutoSi, true),
                ));
            }
            "System.String" | "System.Guid" | "System.Decimal" => {
                return wire
                    .as_str()
                    .map(|s| Json::String(s.into()))
                    .ok_or_else(|| format!("Expected exact string for {kind}"));
            }
            "System.Boolean" => {
                return wire
                    .as_bool()
                    .map(Json::Bool)
                    .ok_or_else(|| "Expected Boolean".into());
            }
            "System.Int16" | "System.Int32" | "System.Int64" | "System.SByte"
            | "System.TimeSpan" => {
                return wire
                    .as_i64()
                    .map(|n| Json::Number(n.into()))
                    .ok_or_else(|| "Expected signed integer".into());
            }
            "System.UInt16" | "System.UInt32" | "System.UInt64" | "System.Byte" => {
                return wire
                    .as_u64()
                    .map(|n| Json::Number(n.into()))
                    .ok_or_else(|| "Expected unsigned integer".into());
            }
            "System.Double" | "System.Single" => {
                return wire
                    .as_f64()
                    .and_then(serde_json::Number::from_f64)
                    .map(Json::Number)
                    .ok_or_else(|| "Nonfinite display number".into());
            }
            _ => {}
        }
        let (schema, bindings) = if let Some(schema) = self.schemas.get(kind) {
            (schema, BTreeMap::new())
        } else if let Some((base, args)) = generic(kind) {
            let schema = self
                .schemas
                .get(base)
                .ok_or_else(|| format!("No projection registered for {kind}"))?;
            if schema.generic_parameters.len() != args.len() {
                return Err("Generic argument count mismatch".into());
            }
            (
                schema,
                schema
                    .generic_parameters
                    .iter()
                    .cloned()
                    .zip(args.into_iter().map(str::to_string))
                    .collect(),
            )
        } else {
            return Err(format!("No projection registered for {kind}"));
        };
        if schema.is_enum {
            let number = wire.as_i64().ok_or("Expected enum integer")?;
            let name = schema
                .enum_values
                .as_ref()
                .and_then(|values| values.iter().find(|v| v.value == number))
                .map(|v| v.name.clone());
            return Ok(name
                .map(Json::String)
                .unwrap_or_else(|| Json::Number(number.into())));
        }
        if !schema.unions.is_empty() {
            let parts = wire
                .as_array()
                .filter(|p| p.len() == 2)
                .ok_or("Invalid MessagePack union")?;
            let tag = parts[0].as_i64().ok_or("Invalid union tag")?;
            let selected = schema
                .unions
                .iter()
                .find(|u| u.tag == tag)
                .ok_or("Unknown union tag")?;
            return self.project(&selected.kind, &parts[1]);
        }
        let map = schema.map_by_name || schema.members.iter().any(|m| m.map_key.is_some());
        if map && wire.as_map().is_none() || !map && wire.as_array().is_none() {
            return Err(format!("Unexpected MessagePack shape for {kind}"));
        }
        let mut result = serde_json::Map::new();
        for member in &schema.members {
            let value = if let Some(index) = member.index {
                wire.as_array().and_then(|a| a.get(index))
            } else {
                wire.as_map().and_then(|m| {
                    m.iter()
                        .find(|(key, _)| key.as_str() == member.map_key.as_deref())
                        .map(|(_, value)| value)
                })
            }
            .unwrap_or(&Value::Nil);
            result.insert(
                member.name.clone(),
                self.project(&substitute(&member.kind, &bindings), value)?,
            );
        }
        Ok(Json::Object(result))
    }
}
fn generic(kind: &str) -> Option<(&str, Vec<&str>)> {
    let start = kind.find('[')?;
    let content = kind.get(start + 1..kind.len().checked_sub(1)?)?;
    if !kind.ends_with(']') || !kind[..start].contains('`') {
        return None;
    }
    let mut args = Vec::new();
    let mut depth = 0;
    let mut begin = 0;
    for (index, ch) in content.char_indices() {
        match ch {
            '[' => depth += 1,
            ']' => {
                if depth == 0 {
                    return None;
                }
                depth -= 1;
            }
            ',' if depth == 0 => {
                args.push(content[begin..index].trim());
                begin = index + 1;
            }
            _ => {}
        }
    }
    if depth != 0 {
        return None;
    }
    args.push(content[begin..].trim());
    Some((&kind[..start], args))
}
fn substitute(kind: &str, bindings: &BTreeMap<String, String>) -> String {
    if let Some(value) = bindings.get(kind) {
        return value.clone();
    }
    if let Some(inner) = kind.strip_suffix("[]") {
        return format!("{}[]", substitute(inner, bindings));
    }
    if let Some((base, args)) = generic(kind) {
        return format!(
            "{base}[{}]",
            args.into_iter()
                .map(|a| substitute(a, bindings))
                .collect::<Vec<_>>()
                .join(",")
        );
    }
    kind.into()
}
