//! Named CLR documents only at unchanged canonical JSON request/journal boundaries.
//! Native state and UI publications retain concrete records instead of these documents.
use super::wire_types::*;
use serde::Serialize;
use serde::ser::SerializeSeq;

/// Backend Document used by the backend document component.
pub struct BackendDocument<'a, T: BackendRecord + ?Sized>(
    /// Borrowed backend record; the wrapper does not take ownership of the referenced record.
    pub &'a T,
);

/// Defines the backend record operations shared by its implementations.
pub trait BackendRecord {
    /// Writes the backend representation using the declared wire layout.
    fn serialize_backend<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error>;
}
impl<T: BackendRecord + ?Sized> Serialize for BackendDocument<'_, T> {
    /// Serializes this value through the supplied serializer.
    fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        self.0.serialize_backend(serializer)
    }
}
// Scalar records use their existing serde representation at the backend boundary.
macro_rules! scalar {
    ($($kind:ty),*) => {
        $(
            impl BackendRecord for $kind {
                /// Writes this scalar using its declared serde representation.
                fn serialize_backend<S: serde::Serializer>(
                    &self,
                    serializer: S,
                ) -> Result<S::Ok, S::Error> {
                    self.serialize(serializer)
                }
            }
        )*
    };
}
scalar!(
    String,
    bool,
    u8,
    u16,
    u32,
    u64,
    i8,
    i16,
    i32,
    i64,
    f32,
    f64,
    DecimalText,
    DateText,
    UtcText,
    OffsetText,
    TimeText,
    ByteBuffer
);
impl<T: BackendRecord> BackendRecord for Option<T> {
    /// Writes the backend representation using the declared wire layout.
    fn serialize_backend<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        match self {
            Some(value) => serializer.serialize_some(&BackendDocument(value)),
            None => serializer.serialize_none(),
        }
    }
}
impl<T: BackendRecord + ?Sized> BackendRecord for Box<T> {
    /// Writes the backend representation using the declared wire layout.
    fn serialize_backend<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        (**self).serialize_backend(serializer)
    }
}
impl<T: BackendRecord> BackendRecord for Vec<T> {
    /// Writes the backend representation using the declared wire layout.
    fn serialize_backend<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        let mut output = serializer.serialize_seq(Some(self.len()))?;
        for value in self {
            output.serialize_element(&BackendDocument(value))?;
        }
        output.end()
    }
}


/// Materialize a named value only for an existing JSON command/journal adapter.
pub fn request_document<T: BackendRecord>(record: &T) -> Result<serde_json::Value, String> {
    serde_json::to_value(BackendDocument(record)).map_err(|error| error.to_string())
}
