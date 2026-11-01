//! Scalar published enum labels, preserving unknown numeric values.
use super::cells::{Cell, ScalarCell};
use serde::{Deserialize, Serialize};
use std::borrow::Cow;

/// Label used by the labels component.
#[derive(Clone, Debug, Serialize, PartialEq)]
#[serde(untagged)]
pub enum Label {
    Text(String),
    Number(i64),
}
impl<'de> Deserialize<'de> for Label {
    /// Deserializes this value while preserving the declared field and scalar rules.
    fn deserialize<D: serde::Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        /// Visitor used by the labels component.
        struct Visitor;
        impl<'de> serde::de::Visitor<'de> for Visitor {
            type Value = Label;


            /// Describes the accepted input shape in deserialization errors.
            fn expecting(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                formatter.write_str("a published enum name or integer")
            }


            /// Accepts text through this value's deserialization rules.
            fn visit_str<E: serde::de::Error>(self, value: &str) -> Result<Label, E> {
                Ok(Label::Text(value.to_string()))
            }


            /// Accepts owned text through this value's deserialization rules.
            fn visit_string<E: serde::de::Error>(self, value: String) -> Result<Label, E> {
                Ok(Label::Text(value))
            }


            /// Accepts a signed integer through this value's deserialization rules.
            fn visit_i64<E: serde::de::Error>(self, value: i64) -> Result<Label, E> {
                Ok(Label::Number(value))
            }


            /// Accepts an unsigned integer through this value's deserialization rules.
            fn visit_u64<E: serde::de::Error>(self, value: u64) -> Result<Label, E> {
                i64::try_from(value).map(Label::Number).map_err(E::custom)
            }
        }
        deserializer.deserialize_any(Visitor)
    }
}
impl Label {
    /// Returns the display text for the requested value or field.
    pub fn text(&self) -> Option<&str> {
        match self {
            Self::Text(value) => Some(value),
            Self::Number(_) => None,
        }
    }
}
impl ScalarCell for Label {
    /// Exposes this scalar as a typed display cell without a generic value tree.
    fn scalar_cell(&self) -> Cell<'_> {
        match self {
            Self::Text(value) => Cell::Text(Cow::Borrowed(value)),
            Self::Number(value) => Cell::Signed(*value),
        }
    }
}

/// Defines the published enum operations shared by its implementations.
pub trait PublishedEnum {
    /// Returns the published label while preserving unrecognized backend values.
    fn published_label(&self) -> Label;
}
