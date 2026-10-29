//! Direct native publication of existing IFM NATS contracts.
//!
//! Streaming bytes never cross the managed FFI. Dataset owners give this module
//! copied frames, not native resource leases. Socket workers and queued buffers
//! are explicitly cancelled/joined independently of downstream consumers.

pub mod cache_delivery;
pub mod codec;
pub mod current;
pub mod diagnostic;
pub mod diagnostic_delivery;
pub mod events;
pub mod evidence_delivery;
pub mod evidence_outbox;
pub mod evidence_protocol;
pub mod futures;
pub mod futures_delivery;
pub mod option_cache;
pub mod option_evidence;
pub mod option_ingress;
pub mod option_observation;
pub mod option_pricing;
pub mod option_risk;
pub mod option_source;
pub mod options;
pub mod ordered;
pub mod publisher;
pub mod wire;

pub mod recovery_notice;

pub mod recovery_delivery;

pub mod quote_greeks;

pub mod raw_prices;

pub mod trade_greeks;

pub mod raw_io;

pub mod retired_evidence;
