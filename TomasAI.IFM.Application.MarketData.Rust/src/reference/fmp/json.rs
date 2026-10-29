//! Streaming array normalization. Only one provider row is materialized at a
//! time; duplicate keys follow System.Text.Json's last case-insensitive value.
use super::{Error, Result};
use serde::{
    Deserialize, Deserializer,
    de::{Error as _, MapAccess, SeqAccess, Visitor},
};
use serde_json::value::RawValue;
use std::{collections::BTreeMap, fmt};
pub(super) struct Row(BTreeMap<String, Box<RawValue>>);
impl<'de> Deserialize<'de> for Row {
    fn deserialize<D: Deserializer<'de>>(d: D) -> std::result::Result<Self, D::Error> {
        struct Object;
        impl<'de> Visitor<'de> for Object {
            type Value = Row;
            fn expecting(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
                f.write_str("an FMP row object")
            }
            fn visit_map<M: MapAccess<'de>>(
                self,
                mut map: M,
            ) -> std::result::Result<Row, M::Error> {
                let mut row = BTreeMap::new();
                while let Some((key, value)) = map.next_entry::<String, Box<RawValue>>()? {
                    row.insert(key.to_ascii_lowercase(), value);
                }
                Ok(Row(row))
            }
        }
        d.deserialize_map(Object)
    }
}
impl Row {
    pub(super) fn text(&self, key: &str) -> Result<Option<String>> {
        match self.0.get(key) {
            None => Ok(None),
            Some(v) if v.get() == "null" => Ok(None),
            Some(v) => serde_json::from_str::<String>(v.get())
                .map(Some)
                .map_err(|_| Error::contract("FMP field must be a string")),
        }
    }
    pub(super) fn scalar(&self, key: &str) -> Result<Option<String>> {
        match self.0.get(key) {
            None => Ok(None),
            Some(v) if v.get() == "null" => Ok(None),
            Some(v) if v.get().starts_with('"') => self.text(key),
            Some(v) if !v.get().starts_with(['{', '[']) => Ok(Some(v.get().to_owned())),
            _ => Err(Error::contract("FMP calendar field must be a scalar")),
        }
    }
    pub(super) fn decimal(&self, key: &str) -> Result<crate::option_chain::qualification::Decimal> {
        self.0
            .get(key)
            .and_then(|v| super::super::decimal::parse_json(v.get()))
            .ok_or_else(|| Error::contract("FMP Treasury decimal is missing or invalid"))
    }
}
pub(super) fn rows(bytes: &[u8], mut accept: impl FnMut(Row) -> Result<()>) -> Result<()> {
    // RawValue skips its own nested values; explicitly enforce the CLR default
    // maximum JSON depth before parsing, including fields we otherwise ignore.
    let (mut depth, mut string, mut escape) = (0usize, false, false);
    for &b in bytes {
        if string {
            if escape {
                escape = false;
            } else if b == b'\\' {
                escape = true;
            } else if b == b'"' {
                string = false;
            }
        } else {
            match b {
                b'"' => string = true,
                b'{' | b'[' => {
                    depth += 1;
                    if depth > 64 {
                        return Err(Error::contract("FMP JSON depth exceeds 64"));
                    }
                }
                b'}' | b']' => depth = depth.saturating_sub(1),
                _ => {}
            }
        }
    }
    struct Array<'a, F>(&'a mut F);
    impl<'de, F: FnMut(Row) -> Result<()>> Visitor<'de> for Array<'_, F> {
        type Value = ();
        fn expecting(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
            f.write_str("an FMP JSON array")
        }
        fn visit_seq<S: SeqAccess<'de>>(self, mut seq: S) -> std::result::Result<(), S::Error> {
            while let Some(row) = seq.next_element::<Row>()? {
                (self.0)(row).map_err(|_| S::Error::custom("FMP row contract failed"))?;
            }
            Ok(())
        }
    }
    // Preserve typed domain errors from the visitor rather than burying them in
    // a JSON error or accidentally returning an already-normalized partial page.
    let mut domain_error = None;
    let mut wrapper = |row| match accept(row) {
        Ok(()) => Ok(()),
        Err(e) => {
            domain_error = Some(e);
            Err(Error::contract("row rejected"))
        }
    };
    let mut decoder = serde_json::Deserializer::from_slice(bytes);
    let result = decoder
        .deserialize_seq(Array(&mut wrapper))
        .and_then(|()| decoder.end());
    if let Some(e) = domain_error {
        return Err(e);
    }
    result.map_err(|_| Error::contract("FMP response is not a valid row array"))
}
