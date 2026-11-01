//! Shared native plan payload with a lazy, byte-compatible MessagePack boundary.
//! Wire-only rows retain their original bytes. Reading native plans never encodes
//! or decodes them; an inspector or serializer materializes wire bytes once.
use super::{trade_contracts::StrategyTradePlanSnapshot, trade_presentation::DisplayRecord};
use std::{
    ops::Deref,
    sync::{Arc, OnceLock},
};

/// Shared immutable publication payload with lazy typed display and compatibility-wire
/// representations.
#[derive(Clone)]
pub struct RowData(
    /// Shared payload ownership; cloning the wrapper does not clone its wire bytes.
    Arc<Payload>,
);

/// Non-owning reference used to reuse a payload without retaining an inactive publication.
#[derive(Clone)]
pub struct WeakRowData(
    /// Weak payload ownership used by producer caches.
    std::sync::Weak<Payload>,
);
impl WeakRowData {
    /// Acquires shared payload ownership if the referenced publication is still alive.
    pub fn upgrade(&self) -> Option<RowData> {
        self.0.upgrade().map(RowData)
    }
}

/// Storage alternatives for original wire bytes and compact native history-plan delivery.
enum Payload {
    Wire {
        /// Original MessagePack bytes retained as the compatibility source.
        bytes: Vec<u8>,
        /// One lazily decoded, schema-checked presentation record shared by publication clones.
        decoded: OnceLock<Arc<dyn std::any::Any + Send + Sync>>,
    },
    Plan {
        /// Compact authoritative history-plan fields before display-only normalization.
        raw: Arc<StrategyTradePlanSnapshot>,
        /// Lazy normalized display copy, separate from the raw compatibility representation.
        display: OnceLock<Arc<StrategyTradePlanSnapshot>>,
        /// Compatibility bytes materialized only when a serializer or inspector requests them.
        wire: OnceLock<Vec<u8>>,
    },
}
impl RowData {
    /// Creates a non-owning payload reference for reuse without extending its lifetime.
    pub fn downgrade(&self) -> WeakRowData {
        WeakRowData(Arc::downgrade(&self.0))
    }


    /// Creates a shared compact history-plan payload with lazy wire materialization.
    pub fn history_plan(raw: Arc<StrategyTradePlanSnapshot>) -> Self {
        let raw = if raw.is_history_view() {
            raw
        } else {
            Arc::new(raw.history_view())
        };
        Self(Arc::new(Payload::Plan {
            raw,
            display: OnceLock::new(),
            wire: OnceLock::new(),
        }))
    }


    /// Returns the shared normalized native plan without encoding a wire round trip.
    pub fn native_plan(&self) -> Option<Arc<StrategyTradePlanSnapshot>> {
        let Payload::Plan { raw, display, .. } = &*self.0 else {
            return None;
        };
        Some(
            display
                .get_or_init(|| {
                    let mut value = (**raw).clone();
                    value.position = None;
                    value.parameters = None;
                    if let Some(detail) = value.iron_condor_trade_plan_snapshot.as_mut() {
                        detail.position = None;
                        detail.legacy_legs = None;
                        detail.strategy_risk_parameter_set = None;
                        detail.present_forward_risk();
                    }
                    Arc::new(value)
                })
                .clone(),
        )
    }


    /// Reports whether compatibility bytes have already been stored for this payload.
    pub fn wire_is_materialized(&self) -> bool {
        match &*self.0 {
            Payload::Wire { .. } => true,
            Payload::Plan { wire, .. } => wire.get().is_some(),
        }
    }


    /// Reuse a declared immutable presentation record across publication clones.
    /// The cache is payload-owned, bounded to one decoded type, and never used
    /// as command authority. A different requested schema is decoded separately.
    pub fn presentation<T: PresentationCacheRecord>(&self) -> Result<Arc<T>, String> {
        if std::any::TypeId::of::<T>() == std::any::TypeId::of::<StrategyTradePlanSnapshot>() {
            if let Some(plan) = self.native_plan() {
                let erased: Arc<dyn std::any::Any + Send + Sync> = plan;
                return erased
                    .downcast::<T>()
                    .map_err(|_| "Native presentation type mismatch".into());
            }
        }
        if let Payload::Wire { bytes, decoded } = &*self.0 {
            if let Some(value) = decoded.get() {
                if let Ok(value) = value.clone().downcast::<T>() {
                    return Ok(value);
                }
            }
            let value = Arc::new(T::decode_presentation(bytes)?);
            let _ = decoded.set(value.clone());
            if let Some(cached) = decoded.get() {
                if let Ok(cached) = cached.clone().downcast::<T>() {
                    return Ok(cached);
                }
            }
            Ok(value)
        } else {
            T::decode_presentation(self.bytes()).map(Arc::new)
        }
    }


    /// Checks whether both wrappers share the same immutable payload allocation.
    pub fn ptr_eq(&self, other: &Self) -> bool {
        Arc::ptr_eq(&self.0, &other.0)
    }


    /// Borrows stored wire bytes, materializing a lazy compatibility representation when required.
    fn bytes(&self) -> &[u8] {
        match &*self.0 {
            Payload::Wire { bytes, .. } => bytes,
            Payload::Plan { raw, wire, .. } => wire.get_or_init(|| {
                // This closed, bounded generated record contains only supported
                // scalar/record/collection types and writes into an owned Vec.
                // Serialization has no external I/O or fallible custom callbacks.
                crate::wire::encode(&DisplayRecord {
                    value: raw.as_ref(),
                    history: true,
                })
                .expect("bounded native plan must serialize")
            }),
        }
    }
}
impl Deref for RowData {
    type Target = [u8];


    /// Borrows the underlying value without transferring ownership.
    fn deref(&self) -> &[u8] {
        self.bytes()
    }
}
impl AsRef<[u8]> for RowData {
    /// Borrows the underlying representation without transferring ownership.
    fn as_ref(&self) -> &[u8] {
        self.bytes()
    }
}
impl From<Vec<u8>> for RowData {
    /// Converts the supplied value into this representation.
    fn from(value: Vec<u8>) -> Self {
        Self(Arc::new(Payload::Wire {
            bytes: value,
            decoded: OnceLock::new(),
        }))
    }
}
impl From<serde_bytes::ByteBuf> for RowData {
    /// Converts the supplied value into this representation.
    fn from(value: serde_bytes::ByteBuf) -> Self {
        value.into_vec().into()
    }
}
impl Default for RowData {
    /// Constructs the initial state used when no explicit value is supplied.
    fn default() -> Self {
        Vec::new().into()
    }
}
impl std::fmt::Debug for RowData {
    /// Formats this value for the requested formatter.
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("RowData")
            .field("wire_materialized", &self.wire_is_materialized())
            .finish()
    }
}
impl serde::Serialize for RowData {
    /// Serializes this value through the supplied serializer.
    fn serialize<S: serde::Serializer>(&self, s: S) -> Result<S::Ok, S::Error> {
        s.serialize_bytes(self.bytes())
    }
}
impl<'de> serde::Deserialize<'de> for RowData {
    /// Deserializes this value while preserving the declared field and scalar rules.
    fn deserialize<D: serde::Deserializer<'de>>(d: D) -> Result<Self, D::Error> {
        serde_bytes::ByteBuf::deserialize(d).map(Into::into)
    }
}

// Seal the cache to concrete schemas; arbitrary business-data trees are not accepted.
mod sealed {
    /// Defines the sealed operations shared by its implementations.
    pub trait Sealed {}
}

/// Defines the presentation cache record operations shared by its implementations.
pub trait PresentationCacheRecord: sealed::Sealed + Send + Sync + 'static {
    /// Decodes the immutable presentation record from its wire bytes.
    fn decode_presentation(bytes: &[u8]) -> Result<Self, String>
    where
        Self: Sized;
}
// Only these concrete schemas can participate in the shared presentation cache.
macro_rules! cached_records {
    ($($t:ty),*$(,)?) => {
        $(
            impl sealed::Sealed for $t {}

            impl PresentationCacheRecord for $t {
                /// Decodes one concrete presentation schema from immutable wire bytes.
                fn decode_presentation(bytes: &[u8]) -> Result<Self, String> {
                    crate::wire::decode(bytes)
                }
            }
        )*
    };
}
use super::{
    broker_presentation::*,
    trade_contracts::*,
    trade_presentation::{LimitDisplayRow, ObservationRow, PositionHistoryRow, WorkspaceTabRow},
};
cached_records!(
    OrderDisplay,
    SetupTradeDisplay,
    OrderDraftDisplay,
    BrokerOrderDefinition,
    OrderExecutionDefinition,
    ExecutionFillEvidence,
    BrokerSummary,
    BrokerMutation,
    Properties,
    Caption,
    PreviewDisplay,
    BrokerAccountDefinition,
    QualificationReport,
    MarketDisplay,
    MarketMetrics,
    LegDisplay,
    super::eod::EodDocument,
    EstablishedTradeDefinition,
    PositionHistoryRow,
    Option<StrategyPositionSnapshot>,
    LimitDisplayRow,
    ObservationRow,
    WorkspaceTabRow
);
impl sealed::Sealed for StrategyTradePlanSnapshot {}
impl PresentationCacheRecord for StrategyTradePlanSnapshot {
    /// Decodes the immutable presentation record from its wire bytes.
    fn decode_presentation(bytes: &[u8]) -> Result<Self, String> {
        let mut value: Self = crate::wire::decode(bytes)?;
        if let Some(d) = value.iron_condor_trade_plan_snapshot.as_mut() {
            d.present_forward_risk();
        }
        Ok(value)
    }
}
impl sealed::Sealed for TradeRealtimeSnapshot {}
impl PresentationCacheRecord for TradeRealtimeSnapshot {
    /// Decodes the immutable presentation record from its wire bytes.
    fn decode_presentation(bytes: &[u8]) -> Result<Self, String> {
        let mut value: Self = crate::wire::decode(bytes)?;
        if value.plan.is_some() {
            for plan in [&mut value.plan, &mut value.last_available_plan]
                .into_iter()
                .flatten()
            {
                if let Some(d) = plan.iron_condor_trade_plan_snapshot.as_mut() {
                    d.present_forward_risk();
                }
            }
        }
        Ok(value)
    }
}
impl sealed::Sealed for super::trade_chart::PreparedTradeChart {}
impl PresentationCacheRecord for super::trade_chart::PreparedTradeChart {
    /// Decodes the immutable presentation record from its wire bytes.
    fn decode_presentation(bytes: &[u8]) -> Result<Self, String> {
        Self::from_messagepack(bytes)
    }
}

impl sealed::Sealed for super::prepared_views::PreparedMarket {}
impl PresentationCacheRecord for super::prepared_views::PreparedMarket {
    /// Decodes the immutable presentation record from its wire bytes.
    fn decode_presentation(bytes: &[u8]) -> Result<Self, String> {
        crate::wire::decode(bytes).map(Self::new)
    }
}
