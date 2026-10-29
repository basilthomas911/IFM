//! Independently owned Tokio market-data sessions for the IFM application boundary.
//!
//! The current backend and its native ABI remain untouched. This crate starts with
//! lifecycle qualification; it must not be registered as a production backend until
//! every operation in `baseline/CurrentBackend.json` has passed differential tests.
//! Cancellation requests are distinct from completion: only an awaited owner
//! result proves that its clients, borrowed records and task-local buffers dropped.

pub mod aggregation;
pub mod api;
pub mod dataset;
pub mod dns;
pub mod ffi;
pub mod option_chain;
pub mod reference;
pub mod runtime;

/// Direct native NATS streaming with bounded, joinable transport ownership.
pub mod messaging;

/// Epoch-local coherent latest observation and risk storage.
pub mod latest;

/// Deadline-aware futures-option numerical engines.
pub mod pricing;

/// Qualified owned historical data acquisition.
pub mod historical;

/// Source-exact bounded instrument catalogs.
pub mod catalog;

/// Bounded physical subscription ownership below durable application authority.
pub mod subscriptions;

/// Durable recovery observation, independent of provider ownership.
pub mod audit;
