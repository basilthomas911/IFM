//! Required-output availability, independent of exchange quote freshness.
//!
//! Each missing workload keeps its first monotonic deadline for request-level
//! diagnostics. Missing financial output does not establish provider failure.
//! Dataset liveness is monitored independently through incoming DBN records.
use std::{collections::BTreeMap, time::Duration};
use tokio::time::Instant;

/// Maximum continuous absence of a required obtainable quote or scope result.
pub const QUOTE_AVAILABILITY: Duration = Duration::from_secs(30);

/// Bounded missing-output directory belonging to one physical dataset owner.
#[derive(Default)]
pub struct Availability {
    missing: BTreeMap<String, Instant>,
}
impl Availability {
    /// Observes an exact required output. A usable result clears only its own
    /// absence; it does not require a changed price or a recent exchange tick.
    ///
    /// # Errors
    /// More than 4,096 simultaneous requirements or an empty workload identity.
    pub fn observe(&mut self, key: &str, available: bool) -> Result<(), &'static str> {
        if key.is_empty() {
            return Err("Availability workload identity is empty");
        }
        if available {
            self.missing.remove(key);
        } else if !self.missing.contains_key(key) {
            if self.missing.len() == 4096 {
                return Err("Availability workload capacity exceeded");
            }
            self.missing
                .insert(key.into(), Instant::now() + QUOTE_AVAILABILITY);
        }
        Ok(())
    }
    /// Earliest event-driven wakeup; no periodic polling or per-key task exists.
    pub fn next_expiration(&self) -> Option<Instant> {
        self.missing.values().copied().min()
    }
    /// First expired workload in stable identity order, including exact boundary.
    pub fn expired(&self) -> Option<&str> {
        self.missing
            .iter()
            .find(|(_, until)| **until <= Instant::now())
            .map(|(key, _)| key.as_str())
    }
    /// Sleeps until missing output must be reported. A healthy/idle directory
    /// sleeps indefinitely; the owning select still observes cancellation/events.
    pub async fn pending(until: Option<Instant>) {
        match until {
            Some(until) => tokio::time::sleep_until(until).await,
            None => std::future::pending().await,
        }
    }
}
