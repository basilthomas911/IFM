//! Fixed-schema HTTP inspection fields preserve missing versus explicit null.
//! The distinction keeps old health endpoints' property grids unchanged.
use super::cells::{Cell, Cells, ScalarCell};
use serde::{Deserialize, Deserializer};
use std::collections::BTreeMap;
#[derive(Clone, Debug)]
pub enum Present<T> {
    Missing,
    Null,
    Value(T),
}
impl<T> Default for Present<T> {
    fn default() -> Self {
        Self::Missing
    }
}
impl<'de, T: Deserialize<'de>> Deserialize<'de> for Present<T> {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        Option::<T>::deserialize(deserializer)
            .map(|value| value.map(Self::Value).unwrap_or(Self::Null))
    }
}
impl<T> Present<T> {
    pub fn as_ref(&self) -> Option<&T> {
        if let Self::Value(value) = self {
            Some(value)
        } else {
            None
        }
    }
    pub fn is_missing(&self) -> bool {
        matches!(self, Self::Missing)
    }
    pub fn is_nil(&self) -> bool {
        self.as_ref().is_none()
    }
}
impl<T: ScalarCell> ScalarCell for Present<T> {
    fn scalar_cell(&self) -> Cell<'_> {
        self.as_ref()
            .map(ScalarCell::scalar_cell)
            .unwrap_or_default()
    }
}
impl<T: Cells> Cells for Present<T> {
    fn cell(&self, column: &str) -> Cell<'_> {
        self.as_ref()
            .map(|value| value.cell(column))
            .unwrap_or_default()
    }
}
pub trait HealthFields: Cells {
    fn fields(&self, prefix: &str, output: &mut BTreeMap<String, String>);
}
impl<T: HealthFields> HealthFields for Present<T> {
    fn fields(&self, prefix: &str, output: &mut BTreeMap<String, String>) {
        if let Some(value) = self.as_ref() {
            value.fields(prefix, output)
        } else {
            output.insert(prefix.into(), String::new());
        }
    }
}
pub fn key(prefix: &str, name: &str) -> String {
    if prefix.is_empty() {
        name.into()
    } else {
        format!("{prefix}.{name}")
    }
}
pub fn scalar<T: ScalarCell>(
    value: &Present<T>,
    key: String,
    output: &mut BTreeMap<String, String>,
) {
    if value.is_missing() {
        return;
    }
    let cell = value.scalar_cell();
    let mut text = match &cell {
        Cell::Boolean(value) => if *value { "True" } else { "False" }.into(),
        _ => cell.text(),
    };
    if key.ends_with("Utc") {
        if let Some(value) = cell.as_str() {
            if let Ok(value) = chrono::DateTime::parse_from_rfc3339(value) {
                text = crate::backend::contracts::utc_roundtrip(value.to_utc());
            } else if let Ok(value) =
                chrono::NaiveDateTime::parse_from_str(value, "%Y-%m-%dT%H:%M:%S%.f")
            {
                use chrono::Timelike;
                text = format!(
                    "{}.{:07}",
                    value.format("%Y-%m-%dT%H:%M:%S"),
                    value.nanosecond() / 100
                );
            }
        }
    }
    output.insert(key, text);
}
