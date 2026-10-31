//! Scalar adapters for concrete C# contracts. Financial values retain exact text.
use crate::backend::contracts;
use rmpv::Value;
use serde::{Deserialize, Serialize};

pub trait FromBackend: Sized {
    const KIND: &'static str;
    fn from_backend(value: &Value) -> Result<Self, String>;
}

impl<T: FromBackend> FromBackend for Box<T> {
    const KIND: &'static str = T::KIND;
    fn from_backend(value: &Value) -> Result<Self, String> {
        T::from_backend(value).map(Box::new)
    }
}
impl<T: FromBackend> FromBackend for Option<T> {
    const KIND: &'static str = T::KIND;
    fn from_backend(value: &Value) -> Result<Self, String> {
        if value.is_nil() {
            Ok(None)
        } else {
            T::from_backend(value).map(Some)
        }
    }
}

impl<T: FromBackend> FromBackend for Vec<T> {
    const KIND: &'static str = "bounded contract collection";
    fn from_backend(value: &Value) -> Result<Self, String> {
        let values = value
            .as_array()
            .filter(|values| values.len() <= 100_000)
            .ok_or("Invalid or oversized contract collection")?;
        values.iter().map(T::from_backend).collect()
    }
}

macro_rules! integer {
    ($kind:ty, $read:ident) => {
        impl FromBackend for $kind {
            const KIND: &'static str = stringify!($kind);
            fn from_backend(value: &Value) -> Result<Self, String> {
                value
                    .$read()
                    .and_then(|value| <$kind>::try_from(value).ok())
                    .ok_or_else(|| format!("Invalid {} contract integer", Self::KIND))
            }
        }
    };
}
integer!(i32, as_i64);
integer!(i64, as_i64);
integer!(u8, as_u64);
integer!(u16, as_u64);
integer!(u32, as_u64);
impl FromBackend for f64 {
    const KIND: &'static str = "finite double";
    fn from_backend(value: &Value) -> Result<Self, String> {
        value
            .as_f64()
            .filter(|value| value.is_finite())
            .ok_or("Invalid finite contract number".into())
    }
}
impl FromBackend for bool {
    const KIND: &'static str = "boolean";
    fn from_backend(value: &Value) -> Result<Self, String> {
        value.as_bool().ok_or("Invalid contract boolean".into())
    }
}
impl FromBackend for String {
    const KIND: &'static str = "string";
    fn from_backend(value: &Value) -> Result<Self, String> {
        value
            .as_str()
            .map(str::to_owned)
            .ok_or("Invalid contract string".into())
    }
}

macro_rules! text_scalar {
    ($name:ident, $kind:literal, $decode:expr) => {
        #[derive(Clone, Debug, Default, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
        #[serde(transparent)]
        pub struct $name(pub String);
        impl $name {
            pub fn as_str(&self) -> &str {
                &self.0
            }
        }
        impl std::fmt::Display for $name {
            fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                self.0.fmt(f)
            }
        }
        impl FromBackend for $name {
            const KIND: &'static str = $kind;
            fn from_backend(value: &Value) -> Result<Self, String> {
                ($decode)(value).map(Self)
            }
        }
    };
}
text_scalar!(DecimalText, "System.Decimal", |value: &Value| {
    let text = value.as_str().ok_or("Expected exact decimal text")?;
    contracts::ExactDecimal::parse(text)?;
    Ok::<_, String>(text.to_owned())
});
text_scalar!(DateText, "System.DateOnly", |value: &Value| {
    contracts::date_only(value).map(|value| value.to_string())
});
text_scalar!(UtcText, "System.DateTime", |value: &Value| {
    contracts::timestamp(value).map(contracts::utc_roundtrip)
});
text_scalar!(OffsetText, "System.DateTimeOffset", |value: &Value| {
    contracts::reference_offset(value)
});
text_scalar!(TimeText, "System.TimeOnly", |value: &Value| {
    let ticks = value
        .as_u64()
        .filter(|ticks| *ticks < 864_000_000_000)
        .ok_or("Invalid TimeOnly ticks")?;
    let seconds = ticks / 10_000_000;
    Ok::<_, String>(format!(
        "{:02}:{:02}:{:02}.{:07}",
        seconds / 3600,
        seconds / 60 % 60,
        seconds % 60,
        ticks % 10_000_000
    ))
});

/// Positional backend binary is kept as bytes; presentation compatibility uses a numeric array.
#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(transparent)]
pub struct ByteBuffer(pub Vec<u8>);
impl FromBackend for ByteBuffer {
    const KIND: &'static str = "System.Byte[]";
    fn from_backend(value: &Value) -> Result<Self, String> {
        match value {
            Value::Binary(value) if value.len() <= 16 * 1024 * 1024 => Ok(Self(value.clone())),
            _ => Err("Invalid or oversized binary contract".into()),
        }
    }
}
