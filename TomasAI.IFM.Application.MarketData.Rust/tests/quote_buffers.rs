//! Buffer ownership under saturation, cancellation and generation replacement.
use ifm_market_data_native::{
    aggregation::buffers::{Failure, Pool},
    runtime::{Deadline, Interrupted},
};
use std::time::Duration;
use tokio_util::sync::CancellationToken;
#[tokio::test]
async fn fixed_buffers_reuse_storage_and_saturated_waits_cancel_without_return() {
    let pool = Pool::<u64>::new(64, 1).unwrap();
    let mut lease = pool.try_rent().unwrap();
    let pointer = lease.items().as_ptr();
    for n in 0..64 {
        lease.push(n).unwrap();
    }
    assert_eq!(lease.push(99), Err(Failure::Saturated));
    assert_eq!(lease.items().len(), 64);
    assert_eq!(pool.outstanding().unwrap(), 1);
    assert!(matches!(pool.try_rent(), Err(Failure::Saturated)));
    let token = CancellationToken::new();
    token.cancel();
    assert!(matches!(
        pool.rent(&token, Deadline::recovery()).await,
        Err(Failure::Interrupted(Interrupted::Cancelled))
    ));
    assert!(matches!(
        pool.rent(
            &CancellationToken::new(),
            Deadline::after(Duration::from_millis(5))
        )
        .await,
        Err(Failure::Interrupted(Interrupted::DeadlineExpired))
    ));
    drop(lease);
    for _ in 0..10000 {
        let lease = pool.try_rent().unwrap();
        assert_eq!(lease.items().as_ptr(), pointer);
        assert!(lease.items().is_empty());
    }
    assert_eq!(pool.outstanding().unwrap(), 0);
}
#[tokio::test]
async fn retired_pool_wakes_waiters_and_keeps_old_inflight_storage_valid() {
    let pool = Pool::<u64>::new(2, 1).unwrap();
    let mut held = pool.try_rent().unwrap();
    held.push(42).unwrap();
    let waiting = pool.clone();
    let waiter = tokio::spawn(async move {
        waiting
            .rent(&CancellationToken::new(), Deadline::recovery())
            .await
    });
    pool.retire().unwrap();
    assert!(matches!(waiter.await.unwrap(), Err(Failure::Retired)));
    assert_eq!(held.items(), [42]);
    let replacement = Pool::<u64>::new(2, 1).unwrap();
    drop(held);
    assert!(matches!(pool.try_rent(), Err(Failure::Retired)));
    assert_eq!(pool.outstanding().unwrap(), 0);
    assert!(replacement.try_rent().is_ok());
}
#[test]
fn reservation_limits_fail_before_allocation() {
    assert!(matches!(Pool::<u64>::new(0, 1), Err(Failure::InvalidInput)));
    assert!(matches!(
        Pool::<u64>::new(4097, 1),
        Err(Failure::InvalidInput)
    ));
    assert!(matches!(
        Pool::<[u64; 64]>::new(4096, 65536),
        Err(Failure::InvalidInput)
    ));
}
