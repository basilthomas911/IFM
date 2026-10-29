//! Event-driven quote readiness over detached numeric views. This waiter owns no
//! source resources and cannot postpone dataset cancellation, join or replacement.
use super::MarketData;
use crate::{
    option_chain::composition::{
        capture::{Failure, Request},
        model::Snapshot,
    },
    runtime::Deadline,
};
use std::future::Future;
use tokio::sync::watch;
use tokio_util::sync::CancellationToken;

fn failure(code: &str) -> Failure {
    Failure {
        code: code.into(),
        input: "Snapshot".into(),
    }
}
fn waiting(error: &Failure) -> bool {
    matches!(
        error.code.as_str(),
        "QuoteUnavailable" | "UnderlyingQuoteUnavailable" | "StaleData" | "IncoherentQuotes"
    )
}

/// Only known quote readiness misses wait. Unknown failures leave immediately;
/// they cannot disappear into a retry loop. Neither events nor attempts renew
/// the original absolute deadline. Subscribe before the first source read.
async fn wait<T, F, Fut>(
    mut quotes: watch::Receiver<u64>,
    mut pricing: watch::Receiver<u64>,
    stop: &CancellationToken,
    retired: impl Future<Output = ()>,
    deadline: Deadline,
    mut capture: F,
) -> Result<T, Failure>
where
    F: FnMut() -> Fut,
    Fut: Future<Output = Result<T, Failure>>,
{
    tokio::pin!(retired);
    loop {
        // Clear only notifications preceding the next read. An update between
        // that read and changed() stays pending, preventing lost wake-ups.
        quotes.borrow_and_update();
        pricing.borrow_and_update();
        let result = tokio::select! { biased;
            () = stop.cancelled() => return Err(failure("DeadlineExpired")),
            () = &mut retired => return Err(failure("Recovering")),
            result = deadline.run(stop, capture()) => result
                .map_err(|_| failure("DeadlineExpired"))?,
        };
        match result {
            Err(error) if waiting(&error) => {}
            result => return result,
        }
        tokio::select! { biased;
            () = stop.cancelled() => return Err(failure("DeadlineExpired")),
            () = &mut retired => return Err(failure("Recovering")),
            result = deadline.run(stop, async {
                tokio::select! {
                    value = quotes.changed() => value,
                    value = pricing.changed() => value,
                }
            }) => match result {
                Err(_) => return Err(failure("DeadlineExpired")),
                Ok(Err(_)) => return Err(failure("Recovering")),
                Ok(Ok(())) => {},
            },
        }
    }
}
impl MarketData {
    /// Waits for coherent option evidence using native observation events.
    /// The supplied generation stays fenced; source replacement is Recovering,
    /// never an implicit capture from a different physical dataset.
    ///
    /// # Errors
    /// Existing typed capture failures, deadline expiry or source retirement.
    pub async fn capture_options_ready(
        &self,
        request: Request,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Snapshot, Failure> {
        let view = self
            .options_current()
            .ok_or_else(|| failure("Recovering"))?;
        wait(
            view.queries.updates(),
            view.pricing.updates(),
            stop,
            view.cancelled(),
            deadline,
            || self.capture_options(request.clone(), stop, deadline),
        )
        .await
    }
}

impl MarketData {
    /// Event-driven readiness for an outright-futures composition. Only copied
    /// numeric queries and health notifications are retained; retirement of the
    /// source ticket ends the wait without retaining its connection or task.
    ///
    /// # Errors
    /// Existing capture failures, original request expiry or source replacement.
    pub async fn capture_futures_ready(
        &self,
        request: Request,
        definitions: Vec<crate::option_chain::composition::model::Future>,
        non_utc: bool,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Snapshot, Failure> {
        use crate::{dataset::transport::Lane, runtime::recovery::Phase};
        let lane = match definitions
            .first()
            .map(|definition| definition.dataset.as_str())
        {
            Some("GLBX.MDP3") => Lane::FuturesGlbx,
            Some("XCBF.PITCH") => Lane::FuturesXcbf,
            _ => return Err(failure("ContractMetadataUnavailable")),
        };
        let source = self
            .futures
            .get(&lane)
            .ok_or_else(|| failure("ContractMetadataUnavailable"))?;
        let queries = source
            .factory
            .queries()
            .ok_or_else(|| failure("Recovering"))?;
        let recovery = source.control.recovery();
        let mut health = recovery.subscribe();
        let ticket = health.borrow().ticket;
        let retired = async move {
            loop {
                let changed = {
                    let status = health.borrow_and_update();
                    status.ticket != ticket || status.phase != Phase::Running
                };
                if changed || health.changed().await.is_err() {
                    break;
                }
            }
        };
        let changes = queries.updates();
        wait(changes.clone(), changes, stop, retired, deadline, || {
            self.capture_futures(
                request.clone(),
                definitions.clone(),
                non_utc,
                stop,
                deadline,
            )
        })
        .await
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::{
        sync::{
            Arc,
            atomic::{AtomicUsize, Ordering},
        },
        time::Duration,
    };
    fn signals() -> (
        watch::Sender<u64>,
        watch::Receiver<u64>,
        watch::Sender<u64>,
        watch::Receiver<u64>,
    ) {
        let (a, b) = watch::channel(0);
        let (c, d) = watch::channel(0);
        (a, b, c, d)
    }
    #[tokio::test(start_paused = true)]
    async fn immediate_success_and_unknown_failure_do_not_wait() {
        let (_a, b, _c, d) = signals();
        let stop = CancellationToken::new();
        assert_eq!(
            wait(
                b.clone(),
                d.clone(),
                &stop,
                std::future::pending(),
                Deadline::recovery(),
                || async { Ok(7) }
            )
            .await,
            Ok(7)
        );
        assert_eq!(
            wait::<(), _, _>(
                b,
                d,
                &stop,
                std::future::pending(),
                Deadline::recovery(),
                || async { Err(failure("InvalidSnapshotRequest")) }
            )
            .await
            .unwrap_err()
            .code,
            "InvalidSnapshotRequest"
        );
    }
    #[tokio::test(start_paused = true)]
    async fn observations_and_pricing_wake_without_polling_or_lost_notifications() {
        for pricing_event in [false, true] {
            let (a, b, c, d) = signals();
            let stop = CancellationToken::new();
            let calls = Arc::new(AtomicUsize::new(0));
            let count = calls.clone();
            let task = tokio::spawn(async move {
                wait(
                    b,
                    d,
                    &stop,
                    std::future::pending(),
                    Deadline::recovery(),
                    || {
                        let attempt = count.fetch_add(1, Ordering::SeqCst);
                        // Event arriving during a failed capture must stay visible.
                        if attempt == 0 {
                            if pricing_event {
                                c.send_replace(1);
                            } else {
                                a.send_replace(1);
                            }
                        }
                        async move {
                            if attempt == 0 {
                                Err(failure("QuoteUnavailable"))
                            } else {
                                Ok(9)
                            }
                        }
                    },
                )
                .await
            });
            assert_eq!(task.await.unwrap(), Ok(9));
            assert_eq!(calls.load(Ordering::SeqCst), 2);
        }
    }
    #[tokio::test(start_paused = true)]
    async fn repeated_misses_share_original_deadline_and_no_periodic_retries() {
        let (a, b, _c, d) = signals();
        let stop = CancellationToken::new();
        let calls = Arc::new(AtomicUsize::new(0));
        let count = calls.clone();
        let started = tokio::time::Instant::now();
        let task = tokio::spawn(async move {
            wait::<(), _, _>(
                b,
                d,
                &stop,
                std::future::pending(),
                Deadline::recovery(),
                || {
                    count.fetch_add(1, Ordering::SeqCst);
                    async { Err(failure("StaleData")) }
                },
            )
            .await
        });
        tokio::task::yield_now().await;
        for n in 1..=3 {
            tokio::time::advance(Duration::from_secs(30)).await;
            tokio::task::yield_now().await;
            assert_eq!(calls.load(Ordering::SeqCst), n);
            a.send_replace(n as u64);
            tokio::task::yield_now().await;
        }
        tokio::time::advance(Duration::from_secs(30)).await;
        assert_eq!(task.await.unwrap().unwrap_err().code, "DeadlineExpired");
        assert_eq!(
            tokio::time::Instant::now() - started,
            Duration::from_secs(120)
        );
        assert_eq!(calls.load(Ordering::SeqCst), 4);
    }
    #[tokio::test(start_paused = true)]
    async fn cancellation_retirement_and_closed_source_never_hang() {
        for mode in 0..3 {
            let (a, b, _c, d) = signals();
            let stop = CancellationToken::new();
            let cancel = stop.clone();
            let retired = CancellationToken::new();
            let retire = retired.clone();
            let task = tokio::spawn(async move {
                wait::<(), _, _>(
                    b,
                    d,
                    &stop,
                    retired.cancelled(),
                    Deadline::recovery(),
                    || async { Err(failure("IncoherentQuotes")) },
                )
                .await
            });
            tokio::task::yield_now().await;
            match mode {
                0 => cancel.cancel(),
                1 => retire.cancel(),
                _ => drop(a),
            };
            assert_eq!(
                task.await.unwrap().unwrap_err().code,
                if mode == 0 {
                    "DeadlineExpired"
                } else {
                    "Recovering"
                }
            );
        }
    }
    #[tokio::test(start_paused = true)]
    async fn already_expired_budget_does_not_read_and_stalled_capture_is_dropped() {
        let (_a, b, _c, d) = signals();
        let stop = CancellationToken::new();
        assert_eq!(
            wait::<(), _, _>(
                b.clone(),
                d.clone(),
                &stop,
                std::future::pending(),
                Deadline::after(Duration::ZERO),
                || async { panic!("expired read") }
            )
            .await
            .unwrap_err()
            .code,
            "DeadlineExpired"
        );
        assert_eq!(
            wait::<(), _, _>(
                b,
                d,
                &stop,
                std::future::pending(),
                Deadline::after(Duration::from_secs(120)),
                std::future::pending
            )
            .await
            .unwrap_err()
            .code,
            "DeadlineExpired"
        );
    }
}
