//! Prepared, generation-scoped option chain snapshots.
//!
//! Provider acquisition and strategy policy are separate from this bounded store.
//! Readers share immutable payloads; old-generation retirement cannot affect a
//! replacement, and publication versions cannot move backwards.

/// Native scope claim, renewal and expiry authority.
pub mod claims;
pub mod coalesced;
/// Native complete pricing context preparation and immutable manifest encoding.
pub mod context;
/// Reviewed convention validation and context inputs without managed callbacks.
pub mod convention;
pub mod manifest;
pub mod owner;
pub mod pipeline;
/// Native reference acquisition and generation-independent preparation inputs.
pub mod preparation;
pub mod qualification;
/// Tracked native reference refresh and original-deadline recovery supervision.
pub mod reference_task;
pub mod snapshot;
pub mod statistics;
pub mod trade;

/// Native prepared-chain selection values and immutable IV provenance.
pub mod selection;

/// Native complete composition snapshot contracts and qualification.
pub mod composition;

pub mod cadence;

pub mod universe;

pub mod candidates;

pub mod strategy;

pub mod coverage;

pub mod reference_bootstrap;

/// Native request-specific resident-cache validation.
pub mod cache_read;

pub mod discovery;

/// Preferred-expiry complete cache membership and fallback ordering.
pub mod cache_scopes;

/// Native exact-expiry displayed and buffered option windows.
pub mod browser_window;

/// Native stored-input fallback selection for option browsing.
pub mod browser_inputs;

/// Copied committed business ownership and reconstruction validation.
pub mod committed;

/// Native bounded per-view exact-expiry browsing ownership.
pub mod browser;
