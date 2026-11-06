//! Event-driven quote readiness over detached numeric views. This waiter owns no
//! source resources and cannot postpone dataset cancellation, join or replacement.
use super::MarketData;
use crate::{
    option_chain::composition::{
        capture::{Failure, Request},
        model::Snapshot,
    },
    runtime::{Deadline, capture_updates::Subscription},
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

struct Changes {
    quotes: watch::Receiver<u64>,
    pricing: Option<watch::Receiver<u64>>,
    scoped: Option<ScopedChanges>,
    clock: Option<watch::Receiver<u64>>,
}

struct ScopedChanges {
    quotes: Subscription,
    pricing: Option<Subscription>,
    membership: Option<watch::Receiver<u64>>,
}

impl Changes {
    fn broad(quotes: watch::Receiver<u64>, pricing: Option<watch::Receiver<u64>>) -> Self {
        Self {
            quotes,
            pricing,
            scoped: None,
            clock: None,
        }
    }

    fn mark_read(&mut self) {
        if let Some(clock) = self.clock.as_mut() {
            clock.borrow_and_update();
        }
        if self
            .scoped
            .as_ref()
            .and_then(|scoped| scoped.membership.as_ref())
            .is_some_and(|membership| membership.has_changed().unwrap_or(true))
        {
            self.scoped = None;
        }
        if let Some(scoped) = self.scoped.as_mut() {
            scoped.quotes.mark_read();
            if let Some(pricing) = scoped.pricing.as_mut() {
                pricing.mark_read();
            }
        } else {
            self.quotes.borrow_and_update();
            if let Some(pricing) = self.pricing.as_mut() {
                pricing.borrow_and_update();
            }
        }
    }

    async fn changed(&mut self) -> Result<(), watch::error::RecvError> {
        async fn optional(
            receiver: &mut Option<watch::Receiver<u64>>,
        ) -> Result<(), watch::error::RecvError> {
            match receiver {
                Some(receiver) => receiver.changed().await,
                None => std::future::pending().await,
            }
        }
        if let Some(scoped) = self.scoped.as_mut() {
            tokio::select! { biased;
                _ = optional(&mut scoped.membership) => {
                    // A lease change can replace the dependencies without
                    // replacing the physical dataset. Use broad notifications
                    // before reading that new membership.
                    self.scoped = None;
                    Ok(())
                },
                changed = optional(&mut self.clock) => changed,
                changed = scoped.quotes.changed() => changed,
                changed = async {
                    match &mut scoped.pricing {
                        Some(pricing) => pricing.changed().await,
                        None => std::future::pending().await,
                    }
                } => changed,
            }
        } else {
            tokio::select! {
                changed = optional(&mut self.clock) => changed,
                changed = self.quotes.changed() => changed,
                changed = optional(&mut self.pricing) => changed,
            }
        }
    }
}

/// Only known quote readiness misses wait. Unknown failures leave immediately;
/// they cannot disappear into a retry loop. Neither events nor attempts renew
/// the original absolute deadline. Subscribe before the first source read.
async fn wait<T, F, Fut>(
    mut changes: Changes,
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
        changes.mark_read();
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
            result = deadline.run(stop, changes.changed()) => match result {
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
        let mut changes = Changes::broad(view.queries.updates(), Some(view.pricing.updates()));
        changes.clock = Some(self.clock.changes());
        if let Some(claims) = self.option_claims() {
            // Subscribe before resolving the claimed membership. A monitored
            // risk alias uses this view's immutable membership. Background
            // discovery has its own authority and keeps broad watches.
            let membership = claims.scope_changes();
            let instruments = match claims.active_scope(&request.scope) {
                Ok(Some(scope)) => Some(scope.options),
                Ok(None) => {
                    super::composition_query::monitored_capture_definition(&view, &request.scope)
                        .map(|definition| std::collections::BTreeSet::from([definition.instrument]))
                }
                Err(_) => None,
            };
            if let Some(instruments) = instruments {
                let definitions = view
                    .definitions
                    .iter()
                    .filter(|definition| instruments.contains(&definition.instrument))
                    .collect::<Vec<_>>();
                if definitions.len() == instruments.len()
                    && let Some(quotes) = view.queries.capture_updates(
                        definitions
                            .iter()
                            .flat_map(|d| [d.instrument, d.underlying])
                            .collect(),
                    )
                    && let Some(pricing) = view
                        .pricing
                        .capture_updates(definitions.iter().map(|d| d.instrument).collect())
                {
                    changes.scoped = Some(ScopedChanges {
                        quotes,
                        pricing: Some(pricing),
                        membership: Some(membership),
                    });
                }
            }
        }
        wait(
            changes,
            stop,
            async {
                tokio::select! {
                    () = view.cancelled() => {},
                    () = view.queries.cancelled() => {},
                }
            },
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
        let logical_retired = async move {
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
        let retired = async {
            tokio::select! {
                () = logical_retired => {},
                () = queries.cancelled() => {},
            }
        };
        let mut changes = Changes::broad(queries.updates(), None);
        changes.clock = Some(self.clock.changes());
        if definitions.iter().all(|definition| {
            definitions
                .first()
                .is_some_and(|first| definition.dataset == first.dataset)
        }) && let Some(registrations) = source.factory.registrations()
            && let Some(instruments) = definitions
                .iter()
                .map(|definition| {
                    registrations
                        .iter()
                        .find(|r| r.contract == definition.contract)
                        .map(|registration| registration.instrument)
                })
                .collect::<Option<Vec<_>>>()
            && let Some(quotes) = queries.capture_updates(instruments)
        {
            changes.scoped = Some(ScopedChanges {
                quotes,
                pricing: None,
                membership: None,
            });
        }
        wait(changes, stop, retired, deadline, || {
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
    use crate::runtime::capture_updates::CaptureUpdates;
    use std::{
        sync::{
            Arc,
            atomic::{AtomicBool, AtomicUsize, Ordering},
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
                Changes::broad(b.clone(), Some(d.clone())),
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
                Changes::broad(b, Some(d)),
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
                    Changes::broad(b, Some(d)),
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
    async fn scoped_quote_and_selection_wakes_ignore_unrelated_instruments() {
        let (broad_quotes, quotes, broad_pricing, pricing) = signals();
        let raw = Arc::new(CaptureUpdates::default());
        let calculated = Arc::new(CaptureUpdates::default());
        let (_membership, scope) = watch::channel(0);
        let mut changes = Changes::broad(quotes, Some(pricing));
        changes.scoped = Some(ScopedChanges {
            quotes: raw.subscribe(vec![7, 12]).unwrap(),
            pricing: Some(calculated.subscribe(vec![7]).unwrap()),
            membership: Some(scope),
        });
        let calls = Arc::new(AtomicUsize::new(0));
        let count = calls.clone();
        let available = Arc::new(AtomicBool::new(false));
        let complete = available.clone();
        let task = tokio::spawn(async move {
            wait(
                changes,
                &CancellationToken::new(),
                std::future::pending(),
                Deadline::recovery(),
                || async {
                    count.fetch_add(1, Ordering::SeqCst);
                    if complete.load(Ordering::SeqCst) {
                        Ok(9)
                    } else {
                        Err(failure("QuoteUnavailable"))
                    }
                },
            )
            .await
        });
        tokio::task::yield_now().await;
        for version in 1..=32 {
            raw.notify(Some(8));
            calculated.notify(Some(12));
            broad_quotes.send_replace(version);
            broad_pricing.send_replace(version);
            tokio::task::yield_now().await;
        }
        assert_eq!(calls.load(Ordering::SeqCst), 1);
        raw.notify(Some(12));
        tokio::task::yield_now().await;
        assert_eq!(calls.load(Ordering::SeqCst), 2);
        available.store(true, Ordering::SeqCst);
        calculated.notify(Some(7));
        assert_eq!(task.await.unwrap(), Ok(9));
        assert_eq!(calls.load(Ordering::SeqCst), 3);
    }

    #[tokio::test(start_paused = true)]
    async fn scope_change_during_read_wakes_then_accepts_new_dependencies() {
        let (broad_quotes, quotes, _broad_pricing, _) = signals();
        let raw = Arc::new(CaptureUpdates::default());
        let (membership, scope) = watch::channel(0);
        let mut changes = Changes::broad(quotes, None);
        changes.scoped = Some(ScopedChanges {
            quotes: raw.subscribe(vec![1]).unwrap(),
            pricing: None,
            membership: Some(scope),
        });
        let calls = Arc::new(AtomicUsize::new(0));
        let count = calls.clone();
        let task = tokio::spawn(async move {
            wait(
                changes,
                &CancellationToken::new(),
                std::future::pending(),
                Deadline::recovery(),
                || {
                    let attempt = count.fetch_add(1, Ordering::SeqCst);
                    if attempt == 0 {
                        membership.send_replace(1);
                    }
                    async move {
                        if attempt < 2 {
                            Err(failure("QuoteUnavailable"))
                        } else {
                            Ok(9)
                        }
                    }
                },
            )
            .await
        });
        tokio::task::yield_now().await;
        assert_eq!(calls.load(Ordering::SeqCst), 2);
        broad_quotes.send_replace(1);
        assert_eq!(task.await.unwrap(), Ok(9));
    }

    #[tokio::test(start_paused = true)]
    async fn controlled_clock_change_wakes_scoped_capture_without_a_quote() {
        let (_broad, quotes) = watch::channel(0);
        let raw = Arc::new(CaptureUpdates::default());
        let clock = Arc::new(crate::runtime::clock::Clock::new(100, false).unwrap());
        let mut changes = Changes::broad(quotes, None);
        changes.clock = Some(clock.changes());
        changes.scoped = Some(ScopedChanges {
            quotes: raw.subscribe(vec![7]).unwrap(),
            pricing: None,
            membership: None,
        });
        let source_clock = clock.clone();
        let task = tokio::spawn(async move {
            wait(
                changes,
                &CancellationToken::new(),
                std::future::pending(),
                Deadline::recovery(),
                || async {
                    if source_clock.utc_ticks().unwrap() <= 90 {
                        Ok(9)
                    } else {
                        Err(failure("StaleData"))
                    }
                },
            )
            .await
        });
        tokio::task::yield_now().await;
        clock.update(90, false).unwrap();
        assert_eq!(task.await.unwrap(), Ok(9));
    }

    #[tokio::test(start_paused = true)]
    #[ignore = "Explicit paired capture wake-up measurement"]
    async fn capture_wakeup_before_after_timings() {
        const UNRELATED: usize = 256;
        async fn capture(scoped: bool) -> (usize, u128) {
            let (broad, quotes) = watch::channel(0);
            let raw = Arc::new(CaptureUpdates::default());
            let mut changes = Changes::broad(quotes, None);
            if scoped {
                changes.scoped = Some(ScopedChanges {
                    quotes: raw.subscribe(vec![7]).unwrap(),
                    pricing: None,
                    membership: None,
                });
            }
            let attempts = Arc::new(AtomicUsize::new(0));
            let calls = attempts.clone();
            let available = Arc::new(AtomicBool::new(false));
            let complete = available.clone();
            let started = std::time::Instant::now();
            let task = tokio::spawn(async move {
                wait(
                    changes,
                    &CancellationToken::new(),
                    std::future::pending(),
                    Deadline::recovery(),
                    || async {
                        calls.fetch_add(1, Ordering::SeqCst);
                        if complete.load(Ordering::SeqCst) {
                            Ok(())
                        } else {
                            Err(failure("QuoteUnavailable"))
                        }
                    },
                )
                .await
            });
            tokio::task::yield_now().await;
            for version in 1..=UNRELATED {
                broad.send_replace(version as u64);
                if scoped {
                    raw.notify(Some(8));
                }
                tokio::task::yield_now().await;
            }
            available.store(true, Ordering::SeqCst);
            broad.send_replace((UNRELATED + 1) as u64);
            if scoped {
                raw.notify(Some(7));
            }
            assert_eq!(task.await.unwrap(), Ok(()));
            (
                attempts.load(Ordering::SeqCst),
                started.elapsed().as_nanos(),
            )
        }
        let _ = capture(false).await;
        let _ = capture(true).await;
        for pair in 0..5 {
            for scoped in if pair % 2 == 0 {
                [false, true]
            } else {
                [true, false]
            } {
                let (attempts, elapsed_ns) = capture(scoped).await;
                assert_eq!(attempts, if scoped { 2 } else { UNRELATED + 2 });
                println!("pair={pair} scoped={scoped} attempts={attempts} elapsed_ns={elapsed_ns}");
            }
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
                Changes::broad(b, Some(d)),
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
                    Changes::broad(b, Some(d)),
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
                Changes::broad(b.clone(), Some(d.clone())),
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
                Changes::broad(b, Some(d)),
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
