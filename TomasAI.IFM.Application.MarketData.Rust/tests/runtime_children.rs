//! A cancellation signal is not proof that a registered dataset owner has exited.
use ifm_market_data_native::runtime::{
    Deadline, Interrupted,
    owner::{Owner, OwnerError},
};
use std::{sync::Arc, time::Duration};
use tokio_util::sync::CancellationToken;

#[tokio::test]
async fn terminal_notification_subtree_is_independent_bounded_and_joined_on_root_stop() {
    let owner = Arc::new(Owner::default());
    let datasets = [
        owner.child().unwrap(),
        owner.child().unwrap(),
        owner.child().unwrap(),
    ];
    assert!(matches!(owner.child(), Err(OwnerError::Capacity)));
    let notification = owner.notification_child().unwrap();
    assert!(matches!(
        owner.notification_child(),
        Err(OwnerError::Capacity)
    ));
    assert!(matches!(owner.child(), Err(OwnerError::Capacity)));
    drop(datasets);
    owner.stop().unwrap();
    assert!(notification.cancellation().is_cancelled());
    assert!(!owner.disposable().unwrap());
    drop(notification);
    owner
        .wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(1)),
        )
        .await
        .unwrap();
    assert!(owner.disposable().unwrap());
}
#[tokio::test]
async fn retained_child_keeps_runtime_busy_until_actual_drop() {
    let owner = Arc::new(Owner::default());
    let child = owner.child().unwrap();
    let token = child.cancellation();
    owner.stop().unwrap();
    assert!(token.is_cancelled());
    assert!(!owner.disposable().unwrap());
    assert!(matches!(owner.child(), Err(OwnerError::Closed)));
    assert_eq!(
        owner
            .wait_idle(
                &CancellationToken::new(),
                Deadline::after(Duration::from_millis(5))
            )
            .await,
        Err(Interrupted::DeadlineExpired)
    );
    drop(child);
    owner
        .wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(1)),
        )
        .await
        .unwrap();
    assert!(owner.disposable().unwrap());
    assert!(!owner.wait().unwrap());
}
