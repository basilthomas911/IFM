//! Operational diagnostics and scheduled-task administration. Actions are sent through existing managed services and require the published eligibility/confirmation state.

/// Actor hierarchy, resource health, and operator actions. A confirmation captures generation and target identity so a changed selection cannot silently redirect an operation.
pub mod actor_health;
/// Operational health and dataset projections. Visible rows are rendered from the published query result; severity colors communicate backend state without recalculating it.
pub mod operations_health;
/// Scheduled-task setup, daily-time editing, preview/confirmation, and run history. Draft schedule fields are sent to the managed scheduler; Rust does not create its own timer or execute jobs.
pub mod scheduled_tasks;
