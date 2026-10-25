//! Shared MessagePack serialization and numeric-display compatibility.
//!
//! The bridge wire format is MessagePack even when an intent is assembled with JSON
//! helpers. The JSON adapter emits ordinary i64/u64/f64 MessagePack numbers and rejects
//! numbers outside that supported range. Managed decimal display values arrive as
//! invariant strings. `WireNumber` accepts numeric values or numeric strings for
//! plotting and display; domain arithmetic remains in managed code.
//!
//! # Example
//!
//! ```
//! use ifm_ui_egui::wire;
//! let payload = wire::encode_value(&serde_json::json!({"price": "12.345"})).unwrap();
//! let value: serde_json::Value = wire::decode(&payload).unwrap();
//! assert_eq!(value["price"], "12.345"); // Published decimal text stays text.
//! ```

//! ABI v4 uses ordinary uncompressed MessagePack maps; no LZ4 or .NET typeless extensions.
use serde::{Serialize, de::DeserializeOwned};

/// Serialize a value into named-field MessagePack bytes and return serialization errors as text.
pub fn encode<T: Serialize>(value: &T) -> Result<Vec<u8>, String> {
    rmp_serde::to_vec_named(value).map_err(|e| format!("MessagePack encode: {e}"))
}

/// Deserialize MessagePack bytes into the requested Rust type and return decoding errors as text.
pub fn decode<T: DeserializeOwned>(bytes: &[u8]) -> Result<T, String> {
    rmp_serde::from_slice(bytes).map_err(|e| format!("MessagePack decode: {e}"))
}

/// Editors currently compose JSON document values internally. Only binary MessagePack crosses the ABI.
pub fn encode_intent(document: &str) -> Result<Vec<u8>, String> {
    let value: serde_json::Value =
        serde_json::from_str(document).map_err(|e| format!("Invalid editor intent: {e}"))?;
    encode_value(&value)
}

/// Serialize a JSON-shaped value through the standard-number/binary-document MessagePack adapter.
pub fn encode_value(value: &serde_json::Value) -> Result<Vec<u8>, String> {
    encode(&WireValue(value))
}

// serde_json's arbitrary_precision feature has a private serialization token. Emit standard
// MessagePack numbers explicitly, rather than leaking that JSON-specific representation.
/// Serde adapter emitting ordinary MessagePack scalar numbers and named binary documents from JSON-shaped intents.
struct WireValue<'a>(&'a serde_json::Value);
impl Serialize for WireValue<'_> {
    /// Serialize JSON values recursively as standard MessagePack scalars; reject numbers outside i64/u64/f64 range and encode named documents as binary payloads.
    fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        use serde::ser::{SerializeMap, SerializeSeq};
        match self.0 {
            serde_json::Value::Null => serializer.serialize_none(),
            serde_json::Value::Bool(v) => serializer.serialize_bool(*v),
            serde_json::Value::String(v) => serializer.serialize_str(v),
            serde_json::Value::Number(v) => {
                if let Some(v) = v.as_i64() {
                    serializer.serialize_i64(v)
                } else if let Some(v) = v.as_u64() {
                    serializer.serialize_u64(v)
                } else if let Some(v) = v.as_f64() {
                    serializer.serialize_f64(v)
                } else {
                    Err(serde::ser::Error::custom(
                        "Number outside the bridge wire range",
                    ))
                }
            }
            serde_json::Value::Array(values) => {
                let mut seq = serializer.serialize_seq(Some(values.len()))?;
                for value in values {
                    seq.serialize_element(&WireValue(value))?;
                }
                seq.end()
            }
            serde_json::Value::Object(values) => {
                let mut map = serializer.serialize_map(Some(values.len()))?;
                for (key, value) in values {
                    if key == "documents" {
                        let documents = value
                            .as_object()
                            .ok_or_else(|| serde::ser::Error::custom("Expected documents map"))?;
                        let encoded: std::collections::BTreeMap<_, serde_bytes::ByteBuf> =
                            documents
                                .iter()
                                .map(|(name, document)| {
                                    encode(&WireValue(document)).map(|bytes| (name, bytes.into()))
                                })
                                .collect::<Result<_, _>>()
                                .map_err(serde::ser::Error::custom)?;
                        map.serialize_entry(key, &encoded)?;
                    } else {
                        map.serialize_entry(key, &WireValue(value))?;
                    }
                }
                map.end()
            }
        }
    }
}

/// Bridge decimals retain exact invariant text. Convert only for plotting and formatting.
pub trait WireNumber {
    /** Return a numeric value or parse preserved numeric text; return None for absent or incompatible data. This is a display conversion, not financial validation. */
    fn wire_number(&self) -> Option<f64>;
}
impl WireNumber for serde_json::Value {
    /// Return a numeric value or parse preserved numeric text; return None for absent or incompatible data. This is a display conversion, not financial validation.
    fn wire_number(&self) -> Option<f64> {
        self.as_f64()
            .or_else(|| self.as_str().and_then(|text| text.parse().ok()))
    }
}
