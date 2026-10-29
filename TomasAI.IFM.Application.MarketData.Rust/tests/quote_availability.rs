//! Availability deadlines must survive unrelated traffic and idle clocks.
use ifm_market_data_native::runtime::{Deadline, availability::Availability};
use std::time::Duration;
use tokio::time::{Instant, advance};
use tokio_util::sync::CancellationToken;

#[tokio::test(start_paused = true)]
async fn exact_30_second_boundary_and_unrelated_traffic_cannot_extend_absence() {
    let mut availability = Availability::default();
    availability.observe("short-put", false).unwrap();
    let first = availability.next_expiration();
    for _ in 0..29 {
        advance(Duration::from_secs(1)).await;
        availability.observe("short-put", false).unwrap();
        availability.observe("other-contract", true).unwrap();
        assert_eq!(availability.next_expiration(), first);
        assert!(availability.expired().is_none());
    }
    advance(Duration::from_secs(1)).await;
    assert_eq!(availability.expired(), Some("short-put"));
}

#[tokio::test(start_paused = true)]
async fn only_usable_required_output_clears_absence_and_recurrence_has_its_own_window() {
    let mut a = Availability::default();
    a.observe("put", false).unwrap();
    advance(Duration::from_secs(10)).await;
    a.observe("call", false).unwrap();
    advance(Duration::from_secs(19)).await;
    a.observe("put", true).unwrap();
    advance(Duration::from_secs(1)).await;
    assert!(a.expired().is_none());
    assert_eq!(
        a.next_expiration().unwrap() - Instant::now(),
        Duration::from_secs(10)
    );
    advance(Duration::from_secs(10)).await;
    assert_eq!(a.expired(), Some("call"));
    a.observe("call", true).unwrap();
    assert!(a.next_expiration().is_none());
    a.observe("put", false).unwrap();
    assert_eq!(
        a.next_expiration().unwrap() - Instant::now(),
        Duration::from_secs(30)
    );
}

#[tokio::test(start_paused = true)]
async fn silent_owner_wakes_without_traffic_and_healthy_owner_still_cancels() {
    let mut a = Availability::default();
    a.observe("cache/Daily", false).unwrap();
    Availability::pending(a.next_expiration()).await;
    assert_eq!(a.expired(), Some("cache/Daily"));
    a.observe("cache/Daily", true).unwrap();
    let stop = CancellationToken::new();
    stop.cancel();
    tokio::select! {
        biased;
        ()=stop.cancelled()=>{},
        ()=Availability::pending(a.next_expiration())=>panic!("healthy owner woke without cancellation"),
    }
}

#[tokio::test(start_paused = true)]
async fn availability_check_never_renews_existing_recovery_or_requires_new_exchange_timestamp() {
    let original = Deadline::recovery();
    let mut a = Availability::default();
    a.observe("quote", false).unwrap();
    advance(Duration::from_secs(30)).await;
    assert!(a.expired().is_some());
    assert_eq!(original.remaining(), Duration::from_secs(90));
    // The owner supplies usability; an unchanged retained price is acceptable.
    a.observe("quote", true).unwrap();
    advance(Duration::from_secs(90)).await;
    assert!(original.remaining().is_zero());
    assert!(a.expired().is_none());
}

#[tokio::test(start_paused = true)]
async fn bounded_missing_directory_does_not_evict_live_requirements() {
    let mut a = Availability::default();
    assert!(a.observe("", false).is_err());
    for id in 0..4096 {
        a.observe(&format!("leg/{id}"), false).unwrap();
    }
    assert!(a.observe("overflow", false).is_err());
    a.observe("leg/0", true).unwrap();
    a.observe("overflow", false).unwrap();
    advance(Duration::from_secs(30)).await;
    assert!(a.expired().is_some());
}

#[tokio::test(start_paused = true)]
async fn capture_projection_uses_remaining_operation_budget_without_one_second_overlay() {
    let operation = Deadline::recovery();
    let at = 1_000_000_000i64;
    assert_eq!(operation.utc_until(at), Some(at + 120 * 10_000_000));
    advance(Duration::from_secs(2)).await;
    assert_eq!(operation.utc_until(at), Some(at + 118 * 10_000_000));
    assert_eq!(operation.utc_until(i64::MAX), None);
    advance(Duration::from_secs(118)).await;
    assert_eq!(operation.utc_until(at), Some(at));
}
