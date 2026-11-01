//! Borrowed scalar cells for reusable grids over concrete records.
//! The grid never owns a JSON tree or an untyped business-data map.
use super::wire_types::*;
use std::borrow::Cow;

/// Cell used by the cells component.
#[derive(Clone, Debug, Default, PartialEq)]
pub enum Cell<'a> {
    #[default]
    Missing,
    Text(Cow<'a, str>),
    Signed(i64),
    Unsigned(u64),
    Number(f64),
    Boolean(bool),
}
impl Cell<'_> {
    /// Reads a signed integer when this value has the expected representation.
    pub fn as_i64(&self) -> Option<i64> {
        match self {
            Self::Signed(value) => Some(*value),
            Self::Unsigned(value) => i64::try_from(*value).ok(),
            _ => None,
        }
    }


    /// Reads an unsigned integer when this value has the expected representation.
    pub fn as_u64(&self) -> Option<u64> {
        match self {
            Self::Unsigned(value) => Some(*value),
            Self::Signed(value) => u64::try_from(*value).ok(),
            _ => None,
        }
    }


    /// Reads a boolean when this value has the expected representation.
    pub fn as_bool(&self) -> Option<bool> {
        if let Self::Boolean(value) = self {
            Some(*value)
        } else {
            None
        }
    }


    /// Borrows the stored text without allocating a replacement string.
    pub fn as_str(&self) -> Option<&str> {
        if let Self::Text(value) = self {
            Some(value.as_ref())
        } else {
            None
        }
    }


    /// Checks is missing against the current cells state.
    pub fn is_missing(&self) -> bool {
        matches!(self, Self::Missing)
    }


    /// Reads the numeric representation supported by the supplied value.
    pub fn number(&self) -> Option<f64> {
        match self {
            Self::Signed(value) => Some(*value as f64),
            Self::Unsigned(value) => Some(*value as f64),
            Self::Number(value) => Some(*value),
            Self::Text(value) => value.parse().ok(),
            _ => None,
        }
        .filter(|value| value.is_finite())
    }


    /// Returns the display text for the requested value or field.
    pub fn text(&self) -> String {
        match self {
            Self::Missing => String::new(),
            Self::Text(value) => value.to_string(),
            Self::Signed(value) => value.to_string(),
            Self::Unsigned(value) => value.to_string(),
            Self::Number(value) => value.to_string(),
            Self::Boolean(value) => value.to_string(),
        }
    }
}

/// A fixed, schema-owned set of columns; unknown columns remain unavailable.
pub trait Cells {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, column: &str) -> Cell<'_>;
}
impl<T: Cells + ?Sized> Cells for Box<T> {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, column: &str) -> Cell<'_> {
        (**self).cell(column)
    }
}
impl<T: Cells + ?Sized> Cells for &T {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, column: &str) -> Cell<'_> {
        (**self).cell(column)
    }
}
impl<T: Cells> Cells for Option<T> {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, column: &str) -> Cell<'_> {
        self.as_ref()
            .map(|value| value.cell(column))
            .unwrap_or_default()
    }
}

/// Defines the scalar cell operations shared by its implementations.
pub trait ScalarCell {
    /// Exposes this scalar as a typed display cell without a generic value tree.
    fn scalar_cell(&self) -> Cell<'_>;
}
impl<T: ScalarCell> ScalarCell for Option<T> {
    /// Exposes this scalar as a typed display cell without a generic value tree.
    fn scalar_cell(&self) -> Cell<'_> {
        self.as_ref()
            .map(ScalarCell::scalar_cell)
            .unwrap_or_default()
    }
}
impl ScalarCell for String {
    /// Exposes this scalar as a typed display cell without a generic value tree.
    fn scalar_cell(&self) -> Cell<'_> {
        Cell::Text(self.as_str().into())
    }
}
impl ScalarCell for bool {
    /// Exposes this scalar as a typed display cell without a generic value tree.
    fn scalar_cell(&self) -> Cell<'_> {
        Cell::Boolean(*self)
    }
}
impl ScalarCell for f64 {
    /// Exposes this scalar as a typed display cell without a generic value tree.
    fn scalar_cell(&self) -> Cell<'_> {
        if self.is_finite() {
            Cell::Number(*self)
        } else {
            Cell::Missing
        }
    }
}
// Retain integer signedness when exposing values to display renderers.
macro_rules! signed {
    ($($kind:ty),*) => {
        $(
            impl ScalarCell for $kind {
                /// Exposes a signed display integer without formatting it eagerly.
                fn scalar_cell(&self) -> Cell<'_> {
                    Cell::Signed(*self as i64)
                }
            }
        )*
    };
}

macro_rules! unsigned {
    ($($kind:ty),*) => {
        $(
            impl ScalarCell for $kind {
                /// Exposes an unsigned display integer without formatting it eagerly.
                fn scalar_cell(&self) -> Cell<'_> {
                    Cell::Unsigned(*self as u64)
                }
            }
        )*
    };
}
signed!(i32, i64);
unsigned!(u8, u16, u32, u64);
macro_rules! text {
    ($($kind:ty),*) => {
        $(
            impl ScalarCell for $kind {
                /// Borrows exact scalar text for display without numeric conversion.
                fn scalar_cell(&self) -> Cell<'_> {
                    Cell::Text(self.as_str().into())
                }
            }
        )*
    };
}
text!(DecimalText, DateText, UtcText, OffsetText, TimeText);

#[cfg(test)]
impl Cells for serde_json::Value {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, column: &str) -> Cell<'_> {
        match &self[column] {
            serde_json::Value::String(value) => Cell::Text(value.as_str().into()),
            serde_json::Value::Bool(value) => Cell::Boolean(*value),
            serde_json::Value::Number(value) => {
                value.as_f64().map(Cell::Number).unwrap_or_default()
            }
            _ => Cell::Missing,
        }
    }
}

impl<T: Cells + ?Sized> Cells for std::sync::Arc<T> {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, column: &str) -> Cell<'_> {
        (**self).cell(column)
    }
}
