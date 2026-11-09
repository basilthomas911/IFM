//! Detached numeric diagnostics. Counters observe processing without owning a
//! provider, queue reader, cancellation token or recovery decision.
use super::{records::Observation, transport::Event};
use std::{collections::BTreeMap, sync::atomic::{AtomicU64, Ordering}};

/// Source-lifetime totals; a replacement resource starts a new counter set.
#[derive(Default)]
pub struct Counters {
    received: AtomicU64,
    completed: AtomicU64,
    failed: AtomicU64,
    trades: AtomicU64,
    quotes: AtomicU64,
    orders: AtomicU64,
    statistics: AtomicU64,
    mappings: AtomicU64,
    system: AtomicU64,
    provider_errors: AtomicU64,
    other: AtomicU64,
}

impl Counters {
    /// Measures one normalized provider event, including failed processing.
    pub fn measure<T, E>(&self, event: &Event, process: impl FnOnce() -> Result<T, E>) -> Result<T, E> {
        self.received.fetch_add(1, Ordering::Relaxed);
        let kind = match event {
            Event::Market(Observation::Trade(_)) => &self.trades,
            Event::Market(Observation::Quote(_)) => &self.quotes,
            Event::Market(Observation::Order(_)) => &self.orders,
            Event::Market(Observation::Statistic(_)) => &self.statistics,
            Event::Mapping { .. } => &self.mappings,
            Event::System { .. } => &self.system,
            Event::Error { .. } => &self.provider_errors,
            Event::Other { .. } => &self.other,
        };
        kind.fetch_add(1, Ordering::Relaxed);
        let mut guard = Processing { counters: self, completed: false };
        let result = process();
        guard.completed = result.is_ok();
        result
    }

    /// Copies totals on demand. Observation never resets a counter.
    pub fn snapshot(&self) -> BTreeMap<String, i64> {
        [
            ("Provider events received", &self.received),
            ("Provider events processed", &self.completed),
            ("Processing failures", &self.failed),
            ("Trade events", &self.trades),
            ("Quote events", &self.quotes),
            ("Order book events", &self.orders),
            ("Statistics events", &self.statistics),
            ("Symbol mappings", &self.mappings),
            ("System events and heartbeats", &self.system),
            ("Provider error events", &self.provider_errors),
            ("Other provider events", &self.other),
        ].into_iter().map(|(name, value)| (name.into(), value.load(Ordering::Relaxed).min(i64::MAX as u64) as i64)).collect()
    }
}

struct Processing<'a> { counters: &'a Counters, completed: bool }
impl Drop for Processing<'_> {
    fn drop(&mut self) {
        let total = if self.completed { &self.counters.completed } else { &self.counters.failed };
        total.fetch_add(1, Ordering::Relaxed);
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn samples_preserve_totals_and_count_failures_and_panics() {
        let counters = Counters::default();
        let event = Event::System { code: 0, message: "Heartbeat".into() };
        counters.measure(&event, || Ok::<_, ()>(())).unwrap();
        assert!(counters.measure(&event, || Err::<(), _>(())).is_err());
        let _ = std::panic::catch_unwind(|| counters.measure(&event, || -> Result<(), ()> { panic!("test"); }));
        let first = counters.snapshot();
        assert_eq!(first["Provider events received"], 3);
        assert_eq!(first["Provider events processed"], 1);
        assert_eq!(first["Processing failures"], 2);
        assert_eq!(first["System events and heartbeats"], 3);
        assert_eq!(first, counters.snapshot());
        assert_eq!(Counters::default().snapshot()["Provider events received"], 0);
    }
}
