//! Bounded wake-ups for detached capture requests. A subscription retains
//! numeric notification state only, never a lane, worker, lease or socket.
use std::sync::{
    Arc, Mutex, Weak,
    atomic::{AtomicBool, Ordering},
};
use tokio::sync::watch;

const MAX_WAITERS: usize = 128;
const MAX_INSTRUMENTS: usize = crate::option_chain::MAX_LIVE_OPTIONS;

#[derive(Default)]
pub(crate) struct CaptureUpdates {
    occupied: AtomicBool,
    observers: Mutex<Vec<Arc<Observer>>>,
}

struct Observer {
    instruments: Vec<u32>,
    changes: watch::Sender<()>,
}

pub(crate) struct Subscription {
    owner: Weak<CaptureUpdates>,
    observer: Arc<Observer>,
    changes: watch::Receiver<()>,
}

impl CaptureUpdates {
    /// Callers fall back to their original broad watch when the directory
    /// reaches its finite capacity or the requested membership is unknown.
    pub(crate) fn subscribe(self: &Arc<Self>, mut instruments: Vec<u32>) -> Option<Subscription> {
        instruments.sort_unstable();
        instruments.dedup();
        if instruments.is_empty() || instruments.len() > MAX_INSTRUMENTS {
            return None;
        }
        let mut observers = self.observers.lock().ok()?;
        if observers.len() == MAX_WAITERS {
            return None;
        }
        let (changes, receiver) = watch::channel(());
        let observer = Arc::new(Observer {
            instruments,
            changes,
        });
        observers.push(observer.clone());
        self.occupied.store(true, Ordering::Release);
        Some(Subscription {
            owner: Arc::downgrade(self),
            observer,
            changes: receiver,
        })
    }

    /// None signals a source-wide readiness or reference transition.
    pub(crate) fn notify(&self, instrument: Option<u32>) {
        // Subscribe before reading committed state. A writer that sees no
        // observers before subscription cannot race past that subsequent read.
        if !self.occupied.load(Ordering::Acquire) {
            return;
        }
        if let Ok(observers) = self.observers.lock() {
            for observer in observers.iter() {
                if instrument.is_none_or(|id| observer.instruments.binary_search(&id).is_ok()) {
                    observer.changes.send_modify(|()| {});
                }
            }
        }
    }
}

impl Subscription {
    pub(crate) fn mark_read(&mut self) {
        self.changes.borrow_and_update();
    }

    pub(crate) async fn changed(&mut self) -> Result<(), watch::error::RecvError> {
        self.changes.changed().await
    }
}

impl Drop for Subscription {
    fn drop(&mut self) {
        if let Some(owner) = self.owner.upgrade()
            && let Ok(mut observers) = owner.observers.lock()
        {
            observers.retain(|observer| !Arc::ptr_eq(observer, &self.observer));
            owner
                .occupied
                .store(!observers.is_empty(), Ordering::Release);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test(start_paused = true)]
    async fn unrelated_update_does_not_overwrite_relevant_or_readiness_notification() {
        let changes = Arc::new(CaptureUpdates::default());
        let mut scope = changes.subscribe(vec![7, 7, 4]).unwrap();
        scope.mark_read();
        changes.notify(Some(4));
        changes.notify(Some(12));
        tokio::time::timeout(std::time::Duration::ZERO, scope.changed())
            .await
            .unwrap()
            .unwrap();
        assert!(!scope.changes.has_changed().unwrap());
        changes.notify(None);
        assert!(scope.changes.has_changed().unwrap());
    }

    #[test]
    fn waiter_capacity_is_finite_and_dropped_interest_releases_admission() {
        let changes = Arc::new(CaptureUpdates::default());
        assert!(changes.subscribe(Vec::new()).is_none());
        let mut waiters = (0..MAX_WAITERS)
            .map(|_| changes.subscribe(vec![42]).unwrap())
            .collect::<Vec<_>>();
        assert!(changes.subscribe(vec![42]).is_none());
        waiters.pop();
        assert!(changes.subscribe(vec![42]).is_some());
        drop(waiters);
        assert!(!changes.occupied.load(Ordering::Acquire));
    }
}
