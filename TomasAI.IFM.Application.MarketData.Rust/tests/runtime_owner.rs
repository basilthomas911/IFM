//! Operation ownership, queue pressure and explicit task-exit qualification.

use ifm_market_data_native::runtime::{
    Deadline,
    owner::{Completion, OPERATION_CAPACITY, Outcome, Owner, OwnerError},
};
use std::{
    sync::{
        Arc,
        atomic::{AtomicUsize, Ordering},
    },
    time::Duration,
};
use tokio_util::sync::CancellationToken;

fn buffer() -> [Completion; OPERATION_CAPACITY] {
    [Completion {
        operation: 0,
        outcome: Outcome::InternalFailure,
        value: 0.0,
    }; OPERATION_CAPACITY]
}

#[tokio::test]
async fn an_expired_budget_never_polls_immediately_ready_work() {
    let polled = AtomicUsize::new(0);
    let result = Deadline::after(Duration::ZERO)
        .run(&CancellationToken::new(), async {
            polled.fetch_add(1, Ordering::SeqCst);
            3.0
        })
        .await;
    assert_eq!(
        result,
        Err(ifm_market_data_native::runtime::Interrupted::DeadlineExpired)
    );
    assert_eq!(polled.load(Ordering::SeqCst), 0);
}

struct Resource(Arc<AtomicUsize>);
impl Drop for Resource {
    fn drop(&mut self) {
        self.0.fetch_sub(1, Ordering::SeqCst);
    }
}

#[tokio::test]
async fn cancellation_and_deadline_drop_resources_before_completion_and_task_exit() {
    let owner = Arc::new(Owner::default());
    let active = Arc::new(AtomicUsize::new(0));
    for (operation, budget) in [(1, Duration::from_secs(2)), (2, Duration::from_millis(40))] {
        let active = active.clone();
        let (entered, ready) = tokio::sync::oneshot::channel();
        owner
            .submit(
                &tokio::runtime::Handle::current(),
                operation,
                Deadline::after(budget),
                async move {
                    active.fetch_add(1, Ordering::SeqCst);
                    let _resource = Resource(active);
                    entered.send(()).unwrap();
                    std::future::pending().await
                },
            )
            .unwrap();
        ready.await.unwrap();
        if operation == 1 {
            owner.cancel(1).unwrap();
        }
    }
    tokio::time::sleep(Duration::from_millis(70)).await;
    owner.stop().unwrap();
    owner
        .wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(1)),
        )
        .await
        .unwrap();
    assert_eq!(active.load(Ordering::SeqCst), 0);
    let mut output = buffer();
    let count = owner.drain(&mut output).unwrap();
    assert_eq!(count, 2);
    assert!(
        output[..count]
            .iter()
            .any(|c| c.operation == 1 && c.outcome == Outcome::Cancelled)
    );
    assert!(
        output[..count]
            .iter()
            .any(|c| c.operation == 2 && c.outcome == Outcome::DeadlineExpired)
    );
    assert!(owner.disposable().unwrap());
    assert!(!owner.wait().unwrap());
}

#[tokio::test]
async fn saturated_completion_capacity_never_prevents_stop_or_loses_a_completion() {
    let owner = Arc::new(Owner::default());
    for operation in 1..=OPERATION_CAPACITY as u64 {
        owner
            .submit(
                &tokio::runtime::Handle::current(),
                operation,
                Deadline::recovery(),
                async { Ok(12.0) },
            )
            .unwrap();
    }
    assert_eq!(
        owner.submit(
            &tokio::runtime::Handle::current(),
            500,
            Deadline::recovery(),
            async { Ok(0.0) }
        ),
        Err(OwnerError::Capacity)
    );
    assert_eq!(
        owner.submit(
            &tokio::runtime::Handle::current(),
            1,
            Deadline::recovery(),
            async { Ok(0.0) }
        ),
        Err(OwnerError::InvalidOperation)
    );
    assert_eq!(owner.reserved().unwrap(), OPERATION_CAPACITY);
    assert_eq!(owner.drain(&mut []).unwrap(), 0);
    owner.stop().unwrap();
    owner
        .wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    assert!(
        !owner.disposable().unwrap(),
        "Undelivered results still own reservations"
    );
    let mut output = buffer();
    assert_eq!(owner.drain(&mut output).unwrap(), OPERATION_CAPACITY);
    let mut ids: Vec<_> = output.iter().map(|c| c.operation).collect();
    ids.sort();
    ids.dedup();
    assert_eq!(ids.len(), OPERATION_CAPACITY);
    assert_eq!(owner.reserved().unwrap(), 0);
    assert_eq!(
        owner.submit(
            &tokio::runtime::Handle::current(),
            500,
            Deadline::recovery(),
            async { Ok(0.0) }
        ),
        Err(OwnerError::Closed)
    );
    assert!(owner.disposable().unwrap());
}

#[tokio::test]
async fn panic_is_contained_and_resource_cleanup_is_not_reported_as_success() {
    let owner = Arc::new(Owner::default());
    let active = Arc::new(AtomicUsize::new(1));
    let resource = Resource(active.clone());
    let (entered, ready) = tokio::sync::oneshot::channel();
    owner
        .submit(
            &tokio::runtime::Handle::current(),
            1,
            Deadline::recovery(),
            async move {
                let _resource = resource;
                entered.send(()).unwrap();
                panic!("injected task failure");
            },
        )
        .unwrap();
    ready.await.unwrap();
    owner.stop().unwrap();
    owner
        .wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    let mut output = buffer();
    assert_eq!(owner.drain(&mut output).unwrap(), 1);
    assert_eq!(output[0].outcome, Outcome::InternalFailure);
    assert_eq!(active.load(Ordering::SeqCst), 0);
}

#[tokio::test]
async fn blocked_delivery_wait_is_signalled_by_completion_and_by_shutdown() {
    let owner = Arc::new(Owner::default());
    let peer = owner.clone();
    let (observed, waiting) = tokio::sync::oneshot::channel();
    let thread = std::thread::spawn(move || observed.send(peer.wait()).unwrap());
    owner
        .submit(
            &tokio::runtime::Handle::current(),
            1,
            Deadline::recovery(),
            async { Ok(3.0) },
        )
        .unwrap();
    assert!(
        tokio::time::timeout(Duration::from_secs(2), waiting)
            .await
            .unwrap()
            .unwrap()
            .unwrap()
    );
    thread.join().unwrap();
    let mut output = buffer();
    assert_eq!(owner.drain(&mut output).unwrap(), 1);
    let peer = owner.clone();
    let (observed, waiting) = tokio::sync::oneshot::channel();
    let thread = std::thread::spawn(move || observed.send(peer.wait()).unwrap());
    owner.stop().unwrap();
    assert!(
        !tokio::time::timeout(Duration::from_secs(2), waiting)
            .await
            .unwrap()
            .unwrap()
            .unwrap()
    );
    thread.join().unwrap();
}

#[tokio::test(start_paused = true)]
async fn health_observer_copies_the_terminal_result_at_the_original_deadline_without_a_competing_timer()
 {
    use ifm_market_data_native::runtime::recovery::{Phase, Proof, Recovery, Requirements};
    let owner = Arc::new(Owner::default());
    let health = Recovery::new(Requirements::new(&[Proof::Transport]), Deadline::recovery());
    let watchdog = health.watchdog(&owner).unwrap();
    let observed = health.clone();
    let (finished, done) = tokio::sync::oneshot::channel();
    owner
        .submit_observer(&tokio::runtime::Handle::current(), 77, async move {
            let mut changes = observed.subscribe();
            loop {
                if observed.status().phase == Phase::Failed {
                    break;
                }
                changes.changed().await.unwrap();
            }
            assert_eq!(observed.failures(0, 16).len(), 1);
            finished.send(()).unwrap();
            Ok(120.0)
        })
        .unwrap();
    tokio::task::yield_now().await;
    tokio::time::advance(Duration::from_secs(120)).await;
    done.await.unwrap();
    let mut output = buffer();
    let count = owner.drain(&mut output).unwrap();
    assert_eq!(count, 1);
    assert_eq!(output[0].outcome, Outcome::Success);
    assert_eq!(output[0].value, 120.0);
    owner.stop().unwrap();
    watchdog.await.unwrap();
    owner
        .wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(owner.disposable().unwrap());
}
#[tokio::test]
async fn stopping_cancels_a_stalled_health_observer_before_signalling_its_completion() {
    let owner = Arc::new(Owner::default());
    let active = Arc::new(AtomicUsize::new(1));
    let resource = Resource(active.clone());
    let (entered, ready) = tokio::sync::oneshot::channel();
    owner
        .submit_observer(&tokio::runtime::Handle::current(), 1, async move {
            let _resource = resource;
            entered.send(()).unwrap();
            std::future::pending().await
        })
        .unwrap();
    ready.await.unwrap();
    owner.stop().unwrap();
    owner
        .wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert_eq!(active.load(Ordering::SeqCst), 0);
    let mut output = buffer();
    assert_eq!(owner.drain(&mut output).unwrap(), 1);
    assert_eq!(output[0].outcome, Outcome::Cancelled);
    assert!(owner.disposable().unwrap());
}
