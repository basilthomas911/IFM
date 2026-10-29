//! Coalesces expensive prepared-chain captures without polling idle datasets.
//! Quotes, trade evidence and recovery continue on their independent paths.
use std::time::Duration;
use tokio::time::Instant;

/// Minimum spacing between complete display-cache capture attempts. The first
/// capture is immediate; continuous updates cannot postpone an already due one.
pub struct Cadence {
    next: Option<Instant>,
}
impl Default for Cadence {
    fn default() -> Self {
        Self::new()
    }
}
impl Cadence {
    /// Creates an immediately eligible first capture, without starting a timer.
    pub fn new() -> Self {
        Self { next: None }
    }
    /// Whether pending work may capture now. Callers separately check readiness.
    pub fn ready(&self) -> bool {
        self.next.is_none_or(|next| Instant::now() >= next)
    }
    /// Records an attempted capture, including stale/incomplete input. Retrying
    /// unusable data must not create a busy loop or reset recovery's deadline.
    pub fn attempted(&mut self) {
        self.next = Some(Instant::now() + Duration::from_millis(100));
    }
    /// Waits only when actual pending, source-ready work needs a future wakeup.
    /// No recurring interval exists. The surrounding select owns cancellation.
    pub async fn pending(&self, eligible: bool) {
        if eligible && let Some(next) = self.next {
            tokio::time::sleep_until(next).await;
        } else {
            std::future::pending::<()>().await;
        }
    }
}
