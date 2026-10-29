//! Prepared-cache scheduling is event-driven; no idle polling or quote delay.
use futures_util::poll;
use ifm_market_data_native::option_chain::cadence::Cadence;
use std::{task::Poll, time::Duration};

#[tokio::test(start_paused = true)]
async fn first_capture_is_immediate_and_continuous_updates_cannot_starve_pending_work() {
    let mut cadence = Cadence::new();
    assert!(cadence.ready());
    cadence.attempted();
    assert!(!cadence.ready());
    // Source updates do not reset the deadline: only an actual capture does.
    for _ in 0..99 {
        tokio::time::advance(Duration::from_millis(1)).await;
        assert!(!cadence.ready());
    }
    tokio::time::advance(Duration::from_millis(1)).await;
    assert!(cadence.ready());
    cadence.pending(true).await;
    cadence.attempted();
    cadence.pending(true).await;
    assert!(cadence.ready());
}

#[tokio::test(start_paused = true)]
async fn idle_unready_and_no_timer_states_never_generate_a_wakeup() {
    let mut cadence = Cadence::new();
    assert!(matches!(
        poll!(Box::pin(cadence.pending(true))),
        Poll::Pending
    ));
    cadence.attempted();
    tokio::time::advance(Duration::from_secs(3600)).await;
    assert!(cadence.ready());
    assert!(matches!(
        poll!(Box::pin(cadence.pending(false))),
        Poll::Pending
    ));
    // Cancellation is independently selectable even when a cache timer waits.
    cadence.attempted();
    let stop = tokio_util::sync::CancellationToken::new();
    stop.cancel();
    tokio::select! {biased;
        () = stop.cancelled() => {},
        () = cadence.pending(true) => panic!("cache scheduling delayed cancellation"),
    }
}
