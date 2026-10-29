//! Cancellation and absolute-deadline primitives shared by dataset owners.
//!
//! A deadline is created once per episode, then passed by value. Reusing it never
//! extends the recovery window. Tokio timers use elapsed time, not API ValueDate.

use std::{future::Future, time::Duration};
use tokio::time::Instant;
use tokio_util::sync::CancellationToken;

pub mod availability;
pub mod clock;
pub mod owner;
pub mod panic_capture;
pub mod recovery;

/// One soft-recovery or final-reset allowance, including its own cleanup and readiness.
pub const RECOVERY_BUDGET: Duration = Duration::from_secs(120);

/// An absolute monotonic deadline. Clones share the same instant, not a new timer.
#[derive(Debug, Copy, Clone)]
pub struct Deadline(Instant);

impl Deadline {
    /// Creates a recovery deadline using the production two-minute budget.
    pub fn recovery() -> Self {
        Self::after(RECOVERY_BUDGET)
    }

    /// Reuses an observed native stall's start instant. Detecting a blocked
    /// publication later must not grant another two-minute recovery window.
    pub fn recovery_from(started: Instant) -> Self {
        Self(started.min(Instant::now()) + RECOVERY_BUDGET)
    }

    /// Creates a caller/test deadline measured from now, capped at two minutes.
    ///
    /// Capping also prevents overflowing the monotonic clock with a malformed
    /// foreign duration. A zero budget is already expired.
    pub fn after(budget: Duration) -> Self {
        Self(Instant::now() + budget.min(RECOVERY_BUDGET))
    }

    /// Creates an ordinary caller-owned historical/reference operation deadline,
    /// capped at the existing Host's thirty-minute maximum. This is not a recovery
    /// allowance: dataset lifecycle admission independently clamps it to 120 s.
    /// A long import retains one deadline rather than renewing it per HTTP page.
    pub fn operation(budget: Duration) -> Self {
        Self(Instant::now() + budget.min(Duration::from_secs(30 * 60)))
    }

    /// Combines budgets without ever extending either original deadline.
    pub fn earliest(self, other: Self) -> Self {
        Self(self.0.min(other.0))
    }

    /// Returns the remaining duration, or zero if the original deadline expired.
    pub fn remaining(self) -> Duration {
        self.0.saturating_duration_since(Instant::now())
    }

    /// Projects the remaining operation allowance onto an authoritative API UTC
    /// timestamp (100 ns ticks). Input validity may shorten a result further.
    /// This does not create or extend the underlying monotonic deadline.
    pub fn utc_until(self, at: i64) -> Option<i64> {
        at.checked_add(i64::try_from(self.remaining().as_nanos() / 100).ok()?)
    }

    /// Races an owned asynchronous operation against cancellation and expiry.
    ///
    /// # Errors
    /// Returns [`Interrupted::Cancelled`] or [`Interrupted::DeadlineExpired`].
    /// The losing operation future is dropped before this function returns. A
    /// caller must place all protocol resources inside that future if cancellation
    /// requires abandoning a whole session, rather than reuse a partially started
    /// protocol client. A detached child task is not owned by a future's drop.
    pub async fn run<F: Future>(
        self,
        cancellation: &CancellationToken,
        operation: F,
    ) -> Result<F::Output, Interrupted> {
        // Tokio's timer driver need not mark a newly-created expired Sleep ready
        // before an immediately-ready operation is polled. Check the monotonic
        // instant explicitly so zero/consumed budgets cannot execute ready work.
        if cancellation.is_cancelled() {
            return Err(Interrupted::Cancelled);
        }
        if Instant::now() >= self.0 {
            return Err(Interrupted::DeadlineExpired);
        }
        tokio::select! {
            biased;
            () = cancellation.cancelled() => Err(Interrupted::Cancelled),
            () = tokio::time::sleep_until(self.0) => Err(Interrupted::DeadlineExpired),
            result = operation => {
                if cancellation.is_cancelled() {
                    Err(Interrupted::Cancelled)
                } else if Instant::now() >= self.0 {
                    Err(Interrupted::DeadlineExpired)
                } else {
                    Ok(result)
                }
            },
        }
    }
}

/// Why a cancellable operation stopped before normal completion.
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum Interrupted {
    /// The explicit owner/caller cancellation signal fired.
    Cancelled,
    /// The original absolute deadline expired.
    DeadlineExpired,
}

impl std::fmt::Display for Interrupted {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(match self {
            Self::Cancelled => "operation cancelled",
            Self::DeadlineExpired => "original operation deadline expired",
        })
    }
}

impl std::error::Error for Interrupted {}
#[cfg(test)]
mod heartbeat_tests;
