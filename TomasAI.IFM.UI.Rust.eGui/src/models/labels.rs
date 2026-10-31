//! Scalar published enum labels, preserving unknown numeric values.
use super::cells::{Cell, ScalarCell};
use serde::{Deserialize, Serialize};
use std::borrow::Cow;
#[derive(Clone, Debug, Serialize, PartialEq)]
#[serde(untagged)]
pub enum Label {
    Text(String),
    Number(i64),
}
impl<'de> Deserialize<'de> for Label {
    fn deserialize<D: serde::Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        struct Visitor;
        impl<'de> serde::de::Visitor<'de> for Visitor {
            type Value = Label;
            fn expecting(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                formatter.write_str("a published enum name or integer")
            }
            fn visit_str<E: serde::de::Error>(self, value: &str) -> Result<Label, E> {
                Ok(Label::Text(value.to_string()))
            }
            fn visit_string<E: serde::de::Error>(self, value: String) -> Result<Label, E> {
                Ok(Label::Text(value))
            }
            fn visit_i64<E: serde::de::Error>(self, value: i64) -> Result<Label, E> {
                Ok(Label::Number(value))
            }
            fn visit_u64<E: serde::de::Error>(self, value: u64) -> Result<Label, E> {
                i64::try_from(value).map(Label::Number).map_err(E::custom)
            }
        }
        deserializer.deserialize_any(Visitor)
    }
}
impl Label {
    pub fn text(&self) -> Option<&str> {
        match self {
            Self::Text(value) => Some(value),
            Self::Number(_) => None,
        }
    }
}
impl ScalarCell for Label {
    fn scalar_cell(&self) -> Cell<'_> {
        match self {
            Self::Text(value) => Cell::Text(Cow::Borrowed(value)),
            Self::Number(value) => Cell::Signed(*value),
        }
    }
}

pub trait PublishedEnum {
    fn published_label(&self) -> Label;
}
