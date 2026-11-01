//! Streaming pilot reuses the existing scalar contract rules, preserving exact
//! decimal/timestamp handling without retaining a whole generic business tree.
use super::wire_types::FromBackend;
use serde::{
    Deserialize, Deserializer,
    de::{self, IgnoredAny, SeqAccess, Visitor},
};
use std::marker::PhantomData;

/// Defines the direct record operations shared by its implementations.
pub trait DirectRecord: Sized {
    /// Streams the positional backend record using its pinned field slots.
    fn deserialize_backend<'de, D: Deserializer<'de>>(d: D) -> Result<Self, D::Error>;
}

/// Direct used by the direct backend component.
pub struct Direct<T>(
    /// Typed backend record read directly from the input sequence.
    pub T,
);
impl<'de, T: DirectRecord> Deserialize<'de> for Direct<T> {
    /// Deserializes this value while preserving the declared field and scalar rules.
    fn deserialize<D: Deserializer<'de>>(d: D) -> Result<Self, D::Error> {
        T::deserialize_backend(d).map(Self)
    }
}

/// Scalar used by the direct backend component.
pub struct Scalar<T>(
    /// Scalar decoded using the existing backend validation rules.
    pub T,
);
impl<'de, T: FromBackend> Deserialize<'de> for Scalar<T> {
    /// Deserializes this value while preserving the declared field and scalar rules.
    fn deserialize<D: Deserializer<'de>>(d: D) -> Result<Self, D::Error> {
        let value = rmpv::Value::deserialize(d)?;
        T::from_backend(&value).map(Self).map_err(de::Error::custom)
    }
}

/// Optional Records used by the direct backend component.
pub struct OptionalRecords<T>(
    /// Optional bounded collection of directly decoded records.
    pub Option<Vec<T>>,
);
impl<'de, T: DirectRecord> Deserialize<'de> for OptionalRecords<T> {
    /// Deserializes this value while preserving the declared field and scalar rules.
    fn deserialize<D: Deserializer<'de>>(d: D) -> Result<Self, D::Error> {
        /// Optional Visitor used by the direct backend component.
        struct OptionalVisitor<T>(
            /// Wrapped `PhantomData<T>` value owned by OptionalVisitor.
            PhantomData<T>,
        );
        impl<'de, T: DirectRecord> Visitor<'de> for OptionalVisitor<T> {
            type Value = OptionalRecords<T>;


            /// Describes the accepted input shape in deserialization errors.
            fn expecting(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
                f.write_str("optional bounded records")
            }


            /// Handles an absent optional value during deserialization.
            fn visit_none<E: de::Error>(self) -> Result<Self::Value, E> {
                Ok(OptionalRecords(None))
            }


            /// Handles an explicit null value during deserialization.
            fn visit_unit<E: de::Error>(self) -> Result<Self::Value, E> {
                self.visit_none()
            }


            /// Reads the supplied optional value through the nested deserializer.
            fn visit_some<D: Deserializer<'de>>(self, d: D) -> Result<Self::Value, D::Error> {
                /// Records Visitor used by the direct backend component.
                struct RecordsVisitor<T>(
                    /// Wrapped `PhantomData<T>` value owned by RecordsVisitor.
                    PhantomData<T>,
                );
                impl<'de, T: DirectRecord> Visitor<'de> for RecordsVisitor<T> {
                    type Value = OptionalRecords<T>;


                    /// Describes the accepted input shape in deserialization errors.
                    fn expecting(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
                        f.write_str("bounded records")
                    }


                    /// Reads the sequence entries through this value's deserialization rules.
                    fn visit_seq<A: SeqAccess<'de>>(
                        self,
                        mut seq: A,
                    ) -> Result<Self::Value, A::Error> {
                        if seq.size_hint().is_some_and(|n| n > 100_000) {
                            return Err(de::Error::custom("Oversized contract collection"));
                        }
                        let mut values =
                            Vec::with_capacity(seq.size_hint().unwrap_or(0).min(100_000));
                        while let Some(v) = seq.next_element::<Direct<T>>()? {
                            if values.len() == 100_000 {
                                return Err(de::Error::custom("Oversized contract collection"));
                            }
                            values.push(v.0);
                        }
                        Ok(OptionalRecords(Some(values)))
                    }
                }
                d.deserialize_seq(RecordsVisitor(PhantomData))
            }
        }
        d.deserialize_option(OptionalVisitor(PhantomData))
    }
}

/// Service used by the direct backend component.
struct Service<T>(
    /// Wrapped `T` value owned by Service.
    T,
);
impl<'de, T: DirectRecord> Deserialize<'de> for Service<T> {
    /// Deserializes this value while preserving the declared field and scalar rules.
    fn deserialize<D: Deserializer<'de>>(d: D) -> Result<Self, D::Error> {
        /// Service Visitor used by the direct backend component.
        struct ServiceVisitor<T>(
            /// Wrapped `PhantomData<T>` value owned by ServiceVisitor.
            PhantomData<T>,
        );
        impl<'de, T: DirectRecord> Visitor<'de> for ServiceVisitor<T> {
            type Value = Service<T>;


            /// Describes the accepted input shape in deserialization errors.
            fn expecting(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
                f.write_str("backend service result")
            }


            /// Reads the sequence entries through this value's deserialization rules.
            fn visit_seq<A: SeqAccess<'de>>(self, mut seq: A) -> Result<Self::Value, A::Error> {
                let success = seq
                    .next_element::<rmpv::Value>()?
                    .ok_or_else(|| de::Error::custom("Missing service success"))?;
                let code = seq
                    .next_element::<rmpv::Value>()?
                    .ok_or_else(|| de::Error::custom("Missing service error code"))?;
                let message = seq
                    .next_element::<rmpv::Value>()?
                    .ok_or_else(|| de::Error::custom("Missing service message"))?;
                if success.as_bool() != Some(true) {
                    seq.next_element::<IgnoredAny>()?
                        .ok_or_else(|| de::Error::custom("Missing service value"))?;
                    while seq.next_element::<IgnoredAny>()?.is_some() {}
                    let envelope =
                        rmpv::Value::Array(vec![success, code, message, rmpv::Value::Nil]);
                    return Err(de::Error::custom(
                        crate::backend::contracts::service_result(&envelope).unwrap_err(),
                    ));
                }
                let value = seq
                    .next_element::<Direct<T>>()?
                    .ok_or_else(|| de::Error::custom("Missing service value"))?;
                while seq.next_element::<IgnoredAny>()?.is_some() {}
                Ok(Service(value.0))
            }
        }
        d.deserialize_seq(ServiceVisitor(PhantomData))
    }
}


/// Decodes service for direct backend.
pub fn decode_service<T: DirectRecord>(bytes: &[u8]) -> Result<T, String> {
    let plain = crate::backend::codec::message_bytes(bytes)?;
    decode_plain_service(plain.as_ref())
}


/// Decodes plain service for direct backend.
pub fn decode_plain_service<T: DirectRecord>(plain: &[u8]) -> Result<T, String> {
    let mut cursor = std::io::Cursor::new(plain);
    let value = {
        let mut decoder = rmp_serde::Deserializer::new(&mut cursor);
        decoder.set_max_depth(64);
        Service::<T>::deserialize(&mut decoder).map_err(|e| e.to_string())?
    };
    if cursor.position() != plain.len() as u64 {
        return Err("Trailing MessagePack bytes".into());
    }
    Ok(value.0)
}
