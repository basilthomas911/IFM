//! Borrowed, schema-checked reads for explicit inspection projections.
//! These cursors never own a business-data tree; controllers publish concrete
//! records and typed inspection nodes. JSON text is streamed only for an opaque
//! evidence cell that must retain the existing textual display format.
use super::{
    contracts::{date_only, datetime_offset, timestamp},
    schema::Registry,
};
use rmpv::Value;
use serde::{
    Serialize,
    ser::{Error, SerializeMap, SerializeSeq},
};
#[derive(Clone, Copy)]
pub struct Field<'a> {
    pub registry: &'a Registry,
    pub kind: &'a str,
    pub value: &'a Value,
    depth: usize,
}
impl<'a> Field<'a> {
    pub fn new(registry: &'a Registry, kind: &'a str, value: &'a Value) -> Self {
        Self {
            registry,
            kind,
            value,
            depth: 0,
        }
    }
    fn concrete_kind(self) -> &'a str {
        self.kind
            .strip_prefix("System.Nullable`1[")
            .and_then(|kind| kind.strip_suffix(']'))
            .unwrap_or(self.kind)
    }
    pub fn field(self, name: &str) -> Result<Self, String> {
        let kind = self.concrete_kind();
        let child_kind = self.registry.field_kind(kind, name)?;
        let value = if self.value.is_nil() {
            &Value::Nil
        } else if self.value.is_array() {
            self.registry
                .wire_field(kind, name, self.value)
                .unwrap_or(&Value::Nil)
        } else {
            self.registry.wire_field(kind, name, self.value)?
        };
        Ok(Self {
            registry: self.registry,
            kind: child_kind,
            value,
            depth: self.depth + 1,
        })
    }
    pub fn path(self, names: &[&str]) -> Result<Self, String> {
        let mut value = self;
        for name in names {
            value = value.field(name)?;
        }
        Ok(value)
    }
    pub fn is_nil(self) -> bool {
        self.value.is_nil()
    }
    pub fn string(self) -> Option<&'a str> {
        self.value.as_str()
    }
    pub fn integer(self) -> Option<i64> {
        self.value.as_i64()
    }
    pub fn unsigned(self) -> Option<u64> {
        self.value.as_u64()
    }
    fn collection_kind(self) -> Result<&'a str, String> {
        let kind = self.concrete_kind();
        kind.strip_suffix("[]")
            .or_else(|| {
                kind.strip_prefix("System.Collections.Immutable.ImmutableArray`1[")
                    .and_then(|kind| kind.strip_suffix(']'))
            })
            .ok_or_else(|| format!("Expected inspection collection {kind}"))
    }
    pub fn items(self, maximum: usize) -> Result<Vec<Self>, String> {
        let kind = self.collection_kind()?;
        let values = self
            .value
            .as_array()
            .filter(|values| values.len() <= maximum)
            .ok_or("Invalid inspection collection")?;
        Ok(values
            .iter()
            .map(|value| Self {
                registry: self.registry,
                kind,
                value,
                depth: self.depth + 1,
            })
            .collect())
    }
    pub fn utc(self) -> Result<Option<chrono::DateTime<chrono::Utc>>, String> {
        if self.is_nil() {
            return Ok(None);
        }
        if self.concrete_kind() == "System.DateTimeOffset" {
            datetime_offset(self.value).map(|value| Some(value.to_utc()))
        } else {
            timestamp(self.value).map(Some)
        }
    }
    pub fn text(self) -> Result<String, String> {
        if self.is_nil() {
            return Ok(String::new());
        }
        let kind = self.concrete_kind();
        if let Some(value) = self.string() {
            return Ok(value.into());
        }
        if let Some(value) = self.value.as_bool() {
            return Ok(if value { "True" } else { "False" }.into());
        }
        if kind == "System.DateOnly" {
            return Ok(date_only(self.value)?.to_string());
        }
        if kind == "System.DateTime" || kind == "System.DateTimeOffset" {
            return Ok(self
                .utc()?
                .ok_or("Missing inspection instant")?
                .to_rfc3339_opts(chrono::SecondsFormat::AutoSi, true));
        }
        if let Some(value) = self.integer() {
            return self
                .registry
                .enum_name(kind, value)
                .or_else(|_| Ok(value.to_string()));
        }
        if let Some(value) = self.unsigned() {
            return Ok(value.to_string());
        }
        let text = serde_json::to_string(&self).map_err(|error| error.to_string())?;
        if text.len() > super::codec::MAX_MESSAGE {
            return Err("Inspection text exceeds its display bound".into());
        }
        Ok(text)
    }
}
impl Serialize for Field<'_> {
    fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        if self.depth > 64 {
            return Err(S::Error::custom("Inspection nesting bound exceeded"));
        }
        if self.value.is_nil() {
            return serializer.serialize_none();
        }
        let kind = self.concrete_kind();
        match kind {
            "System.String" | "System.Guid" | "System.Decimal" => {
                return serializer.serialize_str(
                    self.string()
                        .ok_or_else(|| S::Error::custom("Missing exact inspection string"))?,
                );
            }
            "System.Boolean" => {
                return serializer.serialize_bool(
                    self.value
                        .as_bool()
                        .ok_or_else(|| S::Error::custom("Invalid inspection Boolean"))?,
                );
            }
            "System.DateOnly" => {
                return serializer
                    .serialize_str(&date_only(self.value).map_err(S::Error::custom)?.to_string());
            }
            "System.DateTime" | "System.DateTimeOffset" => {
                return serializer.serialize_str(
                    &self
                        .utc()
                        .map_err(S::Error::custom)?
                        .ok_or_else(|| S::Error::custom("Missing inspection instant"))?
                        .to_rfc3339_opts(chrono::SecondsFormat::AutoSi, true),
                );
            }
            "System.Int16" | "System.Int32" | "System.Int64" | "System.SByte"
            | "System.TimeSpan" => {
                return serializer.serialize_i64(
                    self.integer()
                        .ok_or_else(|| S::Error::custom("Invalid inspection integer"))?,
                );
            }
            "System.UInt16" | "System.UInt32" | "System.UInt64" | "System.Byte" => {
                return serializer.serialize_u64(
                    self.unsigned()
                        .ok_or_else(|| S::Error::custom("Invalid unsigned inspection integer"))?,
                );
            }
            "System.Double" | "System.Single" => {
                return serializer.serialize_f64(
                    self.value
                        .as_f64()
                        .filter(|value| value.is_finite())
                        .ok_or_else(|| S::Error::custom("Invalid finite inspection number"))?,
                );
            }
            "System.TimeOnly" => {
                use crate::models::wire_types::{FromBackend, TimeText};
                return serializer.serialize_str(
                    TimeText::from_backend(self.value)
                        .map_err(S::Error::custom)?
                        .as_str(),
                );
            }
            "System.Byte[]"
            | "System.ReadOnlyMemory`1[System.Byte]"
            | "System.Memory`1[System.Byte]" => {
                let bytes = self
                    .value
                    .as_slice()
                    .ok_or_else(|| S::Error::custom("Invalid inspection bytes"))?;
                let mut output = serializer.serialize_seq(Some(bytes.len()))?;
                for byte in bytes {
                    output.serialize_element(byte)?;
                }
                return output.end();
            }
            _ => {}
        }
        if self.collection_kind().is_ok() {
            let values = self.items(100_000).map_err(S::Error::custom)?;
            let mut output = serializer.serialize_seq(Some(values.len()))?;
            for value in values {
                output.serialize_element(&value)?;
            }
            return output.end();
        }
        let mut shape = self.registry.describe(kind).map_err(S::Error::custom)?;
        if shape.is_enum {
            let value = self
                .integer()
                .ok_or_else(|| S::Error::custom("Invalid inspection enum"))?;
            let label = self
                .registry
                .enum_name(kind, value)
                .map_err(S::Error::custom)?;
            return if label == value.to_string() {
                serializer.serialize_i64(value)
            } else {
                serializer.serialize_str(&label)
            };
        }
        if !shape.unions.is_empty() {
            let parts = self
                .value
                .as_array()
                .filter(|parts| parts.len() == 2)
                .ok_or_else(|| S::Error::custom("Invalid inspection union"))?;
            let tag = parts[0]
                .as_i64()
                .ok_or_else(|| S::Error::custom("Invalid inspection union tag"))?;
            let kind = &shape
                .unions
                .iter()
                .find(|(value, _)| *value == tag)
                .ok_or_else(|| S::Error::custom("Unknown inspection union tag"))?
                .1;
            return Field {
                registry: self.registry,
                kind,
                value: &parts[1],
                depth: self.depth + 1,
            }
            .serialize(serializer);
        }
        if !self.value.is_array() {
            return Err(S::Error::custom("Expected positional inspection record"));
        }
        shape.members.sort_by(|a, b| a.0.cmp(&b.0));
        let mut output = serializer.serialize_map(Some(shape.members.len()))?;
        for (name, _, kind) in &shape.members {
            let value = self
                .registry
                .wire_field(self.concrete_kind(), name, self.value)
                .unwrap_or(&Value::Nil);
            output.serialize_entry(
                name,
                &Field {
                    registry: self.registry,
                    kind,
                    value,
                    depth: self.depth + 1,
                },
            )?;
        }
        output.end()
    }
}
