//! Native exact-expiry browser ownership and copied command policy. Physical
//! subscriptions and recovery remain children of the dataset supervisor.
/// Native ATM-IV estimator matching the existing backend.
pub mod atm;
mod command;
mod source;
mod state;
pub use command::{Command, Family};
pub use source::{Facts, Selection};
pub use state::Directory;
