//! Bounded UI channels that wake native dispatch without a polling timer.
use std::sync::{Arc, mpsc};
use tokio::sync::Notify;

/// Wake Sender used by the dispatch component.
pub struct WakeSender<T> {
    /// Optional sender; `None` represents an absent value.
    sender: Option<mpsc::SyncSender<T>>,
    /// Notification used to resume dispatch when channel state changes.
    wake: Arc<Notify>,
}
impl<T> WakeSender<T> {
    /// Offers a message to the bounded channel and returns any delivery failure.
    pub fn try_send(&self, value: T) -> Result<(), mpsc::TrySendError<T>> {
        let result = self.sender.as_ref().unwrap().try_send(value);
        self.wake.notify_one();
        result
    }
}
impl<T> Drop for WakeSender<T> {
    /// Releases resources owned by this value when its lifetime ends.
    fn drop(&mut self) {
        self.sender.take();
        self.wake.notify_one();
    }
}


/// Provides channel for the dispatch implementation.
pub fn channel<T>(capacity: usize, wake: Arc<Notify>) -> (WakeSender<T>, mpsc::Receiver<T>) {
    let (sender, receiver) = mpsc::sync_channel(capacity);
    (
        WakeSender {
            sender: Some(sender),
            wake,
        },
        receiver,
    )
}

/// Bound intake work while allowing every frame to make progress, even if one
/// update consumes the elapsed-time budget. Remaining messages stay queued.
pub struct IntakeBudget {
    /// Started associated with this intake budget.
    started: std::time::Instant,
    /// Limit associated with this intake budget.
    limit: std::time::Duration,
    /// Remaining associated with this intake budget.
    remaining: usize,
    /// Whether first is enabled or currently true.
    first: bool,
}
impl IntakeBudget {
    /// Constructs IntakeBudget from the supplied inputs.
    pub fn new(limit: std::time::Duration, max_messages: usize) -> Self {
        Self {
            started: std::time::Instant::now(),
            limit,
            remaining: max_messages,
            first: true,
        }
    }


    /// Provides next for the intake budget implementation.
    pub fn next(&mut self) -> bool {
        if self.remaining == 0 || (!self.first && self.started.elapsed() >= self.limit) {
            return false;
        }
        self.first = false;
        self.remaining -= 1;
        true
    }
}
#[cfg(test)]
mod tests {
    use super::*;


    /// Verifies that notification retains send before wait and full queue wakes.
    #[tokio::test]
    async fn notification_retains_send_before_wait_and_full_queue_wakes() {
        let wake = Arc::new(Notify::new());
        let (tx, rx) = channel(1, wake.clone());
        tx.try_send(1).unwrap();
        tokio::time::timeout(std::time::Duration::from_secs(1), wake.notified())
            .await
            .unwrap();
        assert!(tx.try_send(2).is_err());
        tokio::time::timeout(std::time::Duration::from_secs(1), wake.notified())
            .await
            .unwrap();
        assert_eq!(rx.try_recv().unwrap(), 1);
        tx.try_send(3).unwrap();
        assert_eq!(rx.try_recv().unwrap(), 3);
    }


    /// Verifies that zero time budget still makes bounded progress.
    #[test]
    fn zero_time_budget_still_makes_bounded_progress() {
        let mut b = IntakeBudget::new(std::time::Duration::ZERO, 10);
        assert!(b.next());
        assert!(!b.next());
        let mut b = IntakeBudget::new(std::time::Duration::from_secs(1), 2);
        assert!(b.next());
        assert!(b.next());
        assert!(!b.next());
    }
}
