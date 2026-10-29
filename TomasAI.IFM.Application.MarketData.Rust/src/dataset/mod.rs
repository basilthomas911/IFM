//! Provider-session ownership and lifecycle qualification.
//!
//! This module deliberately has no process launch or global provider singleton.
//! Every client belongs to a single asynchronous owner, and cancellation abandons
//! a partially initialized client rather than resuming its protocol state.

pub mod book;
pub mod databento;
pub mod lanes;
pub mod live;
mod output;
pub mod records;
pub mod transport;

/// Fixed-buffer incremental metadata decoding.
pub mod metadata;

/// Persistent native desired-intent and replaceable dataset resource supervision.
pub mod supervisor;

/// Concrete native futures dataset resources for the persistent supervisor.
pub mod futures_owner;

/// Concrete native options resources, preparation and direct publication.
pub mod options_owner;

/// Persistent native business claims and exact-expiry dataset desired union.
pub mod option_claims;

pub mod futures_bootstrap;

/// Typed source plans derived natively from copied raw option facts.
pub mod raw_options;

/// Temporary native underlying acquisition under the options dataset lifetime.
pub mod bootstrap_quotes;

/// Native complete background policy, dependency and ranked-universe planning.
pub mod background;
