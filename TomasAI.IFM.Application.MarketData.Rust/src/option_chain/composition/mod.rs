//! Native coherent prepared-chain snapshots. Models contain copied observations
//! and immutable context only; snapshot queries cannot retain a dataset resource.
pub mod capture;
pub mod model;
pub mod source;
