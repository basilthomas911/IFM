//! Native supervisory lifecycle integration with independently tracked source
//! tasks. Numerical, SDK, broker and storage protocol parity have separate tests.
use futures_util::future::BoxFuture;
use ifm_market_data_native::{
    dataset::supervisor::{Factory, Readiness, Resources, Supervisor},
    runtime::{
        Deadline,
        owner::{Child, Owner},
        recovery::{Phase, Proof, Requirements},
    },
};
use std::{
    sync::{
        Arc, Mutex,
        atomic::{AtomicBool, AtomicUsize, Ordering},
    },
    time::Duration,
};
use tokio::{sync::watch, task::JoinHandle, time::advance};
use tokio_util::sync::CancellationToken;
struct NativeFactory {
    starts: AtomicUsize,
    failures: AtomicUsize,
    panic_first: AtomicBool,
    live: Arc<AtomicUsize>,
    maximum: Arc<AtomicUsize>,
    controls: Mutex<Vec<watch::Sender<Readiness>>>,
    proofs: Vec<Proof>,
    join_error: Arc<AtomicBool>,
    stall_next_task: AtomicBool,
    resets: Arc<AtomicUsize>,
}
impl NativeFactory {
    fn new(proofs: Vec<Proof>) -> Arc<Self> {
        Arc::new(Self {
            starts: AtomicUsize::new(0),
            failures: AtomicUsize::new(0),
            panic_first: AtomicBool::new(false),
            live: Arc::new(AtomicUsize::new(0)),
            maximum: Arc::new(AtomicUsize::new(0)),
            controls: Mutex::new(Vec::new()),
            proofs,
            join_error: Arc::new(AtomicBool::new(false)),
            stall_next_task: AtomicBool::new(false),
            resets: Arc::new(AtomicUsize::new(0)),
        })
    }
    fn fault(&self) {
        self.controls
            .lock()
            .unwrap()
            .last()
            .unwrap()
            .send_replace(Readiness {
                failure: Some("injected source failure".into()),
                deadline: Some(Deadline::recovery()),
                proofs: vec![],
            });
    }
}
struct NativeResources {
    parent: Arc<Child>,
    task: Option<JoinHandle<()>>,
    health: watch::Receiver<Readiness>,
    join_error: Arc<AtomicBool>,
    resets: Arc<AtomicUsize>,
}
struct Live(Arc<AtomicUsize>);
impl Drop for Live {
    fn drop(&mut self) {
        self.0.fetch_sub(1, Ordering::SeqCst);
    }
}
impl Factory for NativeFactory {
    type Intent = u64;
    fn start(
        &self,
        _: Arc<u64>,
        parent: Arc<Child>,
        _: Deadline,
    ) -> Result<Box<dyn Resources>, String> {
        self.starts.fetch_add(1, Ordering::SeqCst);
        if self.panic_first.swap(false, Ordering::SeqCst) {
            panic!("injected native factory panic");
        }
        if self
            .failures
            .fetch_update(Ordering::SeqCst, Ordering::SeqCst, |n| n.checked_sub(1))
            .is_ok()
        {
            return Err("injected setup failure".into());
        }
        let count = self.live.fetch_add(1, Ordering::SeqCst) + 1;
        self.maximum.fetch_max(count, Ordering::SeqCst);
        let guard = Live(self.live.clone());
        let stalled = self.stall_next_task.swap(false, Ordering::SeqCst);
        let task = parent
            .spawn_joined(move |stop| async move {
                let _guard = guard;
                if stalled {
                    std::future::pending::<()>().await;
                } else {
                    stop.cancelled().await;
                }
            })
            .unwrap();
        let (health, observed) = watch::channel(Readiness {
            proofs: self.proofs.clone(),
            failure: None,
            deadline: None,
        });
        self.controls.lock().unwrap().push(health);
        Ok(Box::new(NativeResources {
            parent,
            task: Some(task),
            health: observed,
            join_error: self.join_error.clone(),
            resets: self.resets.clone(),
        }))
    }
}
impl Resources for NativeResources {
    fn reset(&mut self, deadline: Deadline) -> BoxFuture<'_, Result<(), String>> {
        self.resets.fetch_add(1, Ordering::SeqCst);
        Box::pin(async move { self.parent.reset_task(&mut self.task, deadline).await })
    }
    fn cancel(&self, _: Deadline) {
        self.parent.cancellation().cancel();
    }
    fn changes(&self) -> watch::Receiver<Readiness> {
        self.health.clone()
    }
    fn join(&mut self, deadline: Deadline) -> BoxFuture<'_, Result<(), String>> {
        Box::pin(async move {
            if self.join_error.swap(false, Ordering::SeqCst) {
                return Err("injected retirement failure".into());
            }
            if let Some(task) = self.task.as_mut() {
                deadline
                    .run(&CancellationToken::new(), task)
                    .await
                    .map_err(|_| "test original deadline expired")?
                    .map_err(|_| "test native source panic")?;
                self.task.take();
            }
            Ok(())
        })
    }
}
impl Drop for NativeResources {
    fn drop(&mut self) {
        self.parent.cancellation().cancel();
    }
}
async fn settle() {
    for _ in 0..30 {
        tokio::task::yield_now().await;
    }
}
#[tokio::test(start_paused = true)]
async fn closed_health_channel_after_hours_idle_starts_a_full_recovery_window() {
    let root = Arc::new(Owner::default());
    let factory = NativeFactory::new(proofs());
    let controller = Supervisor::start(
        root.clone(),
        factory.clone(),
        Some(Arc::new(1)),
        required(),
        Deadline::recovery(),
    )
    .unwrap();
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Running);
    advance(Duration::from_secs(3600)).await;
    // Keep replacement setup failing so its actual allowance can be inspected.
    factory.failures.store(10000, Ordering::SeqCst);
    factory.controls.lock().unwrap().clear();
    settle().await;
    let state = controller.recovery().status();
    assert_eq!(state.phase, Phase::Recovering);
    assert_eq!(
        state.deadline.unwrap().remaining(),
        Duration::from_secs(120)
    );
    advance(Duration::from_secs(119)).await;
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Recovering);
    advance(Duration::from_secs(1)).await;
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Recovering);
    assert_eq!(controller.recovery().failures(0, 16).len(), 1);
    advance(Duration::from_secs(120)).await;
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Failed);
    controller.cancel();
    controller.join(Deadline::recovery()).await.unwrap();
    root.stop().unwrap();
}
fn required() -> Requirements {
    Requirements::new(&[Proof::Transport, Proof::Publication])
}
fn proofs() -> Vec<Proof> {
    vec![Proof::Transport, Proof::Publication]
}
#[tokio::test(start_paused = true)]
async fn native_source_rebuild_joins_old_owner_before_replacement_and_caller_disappearance_is_harmless()
 {
    let root = Arc::new(Owner::default());
    let factory = NativeFactory::new(proofs());
    let controller = Supervisor::start(
        root.clone(),
        factory.clone(),
        Some(Arc::new(1)),
        required(),
        Deadline::recovery(),
    )
    .unwrap();
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Running);
    let first = controller.recovery().status().ticket;
    factory.fault();
    settle().await;
    assert_eq!(factory.starts.load(Ordering::SeqCst), 2);
    assert_eq!(controller.recovery().status().phase, Phase::Running);
    assert_ne!(controller.recovery().status().ticket, first);
    assert_eq!(factory.maximum.load(Ordering::SeqCst), 1);
    let accepted = controller.apply(Some(Arc::new(2)), required()).unwrap();
    // No asynchronous caller waiter exists, yet the accepted revision realizes.
    assert_eq!(accepted.revision, 2);
    settle().await;
    assert_eq!(factory.starts.load(Ordering::SeqCst), 3);
    assert_eq!(factory.maximum.load(Ordering::SeqCst), 1);
    controller.cancel();
    controller.join(Deadline::recovery()).await.unwrap();
    assert_eq!(factory.live.load(Ordering::SeqCst), 0);
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
}
#[tokio::test(start_paused = true)]
async fn failed_setup_and_retirement_retry_until_original_deadline_without_overlapping_resources() {
    let root = Arc::new(Owner::default());
    let factory = NativeFactory::new(proofs());
    factory.failures.store(2, Ordering::SeqCst);
    let controller = Supervisor::start(
        root.clone(),
        factory.clone(),
        Some(Arc::new(1)),
        required(),
        Deadline::recovery(),
    )
    .unwrap();
    settle().await;
    advance(Duration::from_millis(200)).await;
    settle().await;
    advance(Duration::from_millis(200)).await;
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Running);
    assert_eq!(factory.starts.load(Ordering::SeqCst), 3);
    factory.join_error.store(true, Ordering::SeqCst);
    factory.fault();
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Recovering);
    assert_eq!(factory.starts.load(Ordering::SeqCst), 3);
    advance(Duration::from_millis(200)).await;
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Running);
    assert_eq!(factory.starts.load(Ordering::SeqCst), 4);
    assert_eq!(factory.maximum.load(Ordering::SeqCst), 1);
    controller.cancel();
    controller.join(Deadline::recovery()).await.unwrap();
    root.stop().unwrap();
}
#[tokio::test(start_paused = true)]
async fn partial_readiness_resets_at_120_seconds_then_removes_dataset_on_final_failure() {
    let root = Arc::new(Owner::default());
    let factory = NativeFactory::new(vec![Proof::Transport]);
    let controller = Supervisor::start(
        root.clone(),
        factory.clone(),
        Some(Arc::new(1)),
        required(),
        Deadline::recovery(),
    )
    .unwrap();
    settle().await;
    advance(Duration::from_secs(119)).await;
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Starting);
    advance(Duration::from_secs(1)).await;
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Recovering);
    assert_eq!(factory.starts.load(Ordering::SeqCst), 2);
    assert_eq!(factory.maximum.load(Ordering::SeqCst), 1);
    advance(Duration::from_secs(120)).await;
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Failed);
    assert_eq!(factory.live.load(Ordering::SeqCst), 0);
    let outcomes = controller.recovery().failures(0, 16);
    assert_eq!(outcomes.len(), 2);
    assert!(
        outcomes[0].resetting,
        "soft expiry announces the full reset"
    );
    assert!(
        !outcomes[1].resetting,
        "final failure cannot announce another reset"
    );
    advance(Duration::from_secs(600)).await;
    settle().await;
    assert_eq!(factory.starts.load(Ordering::SeqCst), 2);
    let accepted = controller.apply(None, Requirements::new(&[])).unwrap();
    settle().await;
    assert_eq!(accepted.revision, 2);
    assert_eq!(controller.recovery().status().phase, Phase::Running);
    assert_eq!(factory.live.load(Ordering::SeqCst), 0);
    controller.cancel();
    controller.join(Deadline::recovery()).await.unwrap();
    root.stop().unwrap();
}
#[tokio::test(start_paused = true)]
async fn contained_factory_panic_retries_and_unrelated_dataset_stays_running() {
    let root = Arc::new(Owner::default());
    let a = NativeFactory::new(proofs());
    let b = NativeFactory::new(proofs());
    a.panic_first.store(true, Ordering::SeqCst);
    let left = Supervisor::start(
        root.clone(),
        a.clone(),
        Some(Arc::new(1)),
        required(),
        Deadline::recovery(),
    )
    .unwrap();
    let right = Supervisor::start(
        root.clone(),
        b.clone(),
        Some(Arc::new(2)),
        required(),
        Deadline::recovery(),
    )
    .unwrap();
    settle().await;
    assert_eq!(left.recovery().status().phase, Phase::Recovering);
    assert_eq!(right.recovery().status().phase, Phase::Running);
    advance(Duration::from_millis(200)).await;
    settle().await;
    assert_eq!(left.recovery().status().phase, Phase::Running);
    assert_eq!(b.starts.load(Ordering::SeqCst), 1);
    root.stop().unwrap();
    left.join(Deadline::recovery()).await.unwrap();
    right.join(Deadline::recovery()).await.unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
}

#[tokio::test(start_paused = true)]
async fn automatic_membership_retirement_preserves_terminal_failure_until_explicit_retry() {
    let root = Arc::new(Owner::default());
    let factory = NativeFactory::new(vec![]);
    let controller = Supervisor::start(
        root.clone(),
        factory.clone(),
        Some(Arc::new(1)),
        required(),
        Deadline::recovery(),
    )
    .unwrap();
    settle().await;
    advance(Duration::from_secs(120)).await;
    controller
        .apply_maintenance(None, Requirements::new(&[]))
        .unwrap();
    settle().await;
    let failed = controller.recovery().status();
    assert_eq!(failed.phase, Phase::Failed);
    assert!(!controller.has_intent());
    assert_eq!(factory.starts.load(Ordering::SeqCst), 1);
    assert_eq!(controller.recovery().failures(0, 16).len(), 1);
    advance(Duration::from_secs(600)).await;
    controller
        .apply_maintenance(None, Requirements::new(&[]))
        .unwrap();
    settle().await;
    assert_eq!(controller.recovery().status().ticket, failed.ticket);
    controller.apply(None, Requirements::new(&[])).unwrap();
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Running);
    assert_eq!(factory.starts.load(Ordering::SeqCst), 1);
    controller.cancel();
    controller.join(Deadline::recovery()).await.unwrap();
    root.stop().unwrap();
}

#[tokio::test(start_paused = true)]
async fn automatic_expiry_during_a_fault_cannot_disguise_incomplete_recovery_as_healthy_idle() {
    let root = Arc::new(Owner::default());
    let factory = NativeFactory::new(vec![]);
    let controller = Supervisor::start(
        root.clone(),
        factory.clone(),
        Some(Arc::new(1)),
        required(),
        Deadline::recovery(),
    )
    .unwrap();
    settle().await;
    factory.fault();
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Recovering);
    advance(Duration::from_secs(90)).await;
    controller
        .apply_maintenance(None, Requirements::new(&[]))
        .unwrap();
    settle().await;
    assert!(!controller.has_intent());
    assert_eq!(controller.recovery().status().phase, Phase::Recovering);
    assert_eq!(
        controller.recovery().status().deadline.unwrap().remaining(),
        Duration::from_secs(30)
    );
    assert!(!controller.recovery().status().missing.is_empty());
    advance(Duration::from_secs(30)).await;
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Failed);
    assert_eq!(controller.recovery().failures(0, 16).len(), 1);
    controller.cancel();
    controller.join(Deadline::recovery()).await.unwrap();
    root.stop().unwrap();
}
// The fixture deliberately ignores its cancellation token. The final reset must
// abort and join it; neither a caller nor a broker event drives this transition.
#[tokio::test(start_paused = true)]
async fn hard_deadline_aborts_noncooperative_async_task_then_rebuilds_without_overlap() {
    let root = Arc::new(Owner::default());
    let factory = NativeFactory::new(proofs());
    factory.stall_next_task.store(true, Ordering::SeqCst);
    let controller = Supervisor::start(
        root.clone(),
        factory.clone(),
        Some(Arc::new(1)),
        required(),
        Deadline::recovery(),
    )
    .unwrap();
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Running);
    factory.fault();
    settle().await;
    advance(Duration::from_secs(119)).await;
    settle().await;
    assert_eq!(factory.starts.load(Ordering::SeqCst), 1);
    assert_eq!(factory.resets.load(Ordering::SeqCst), 0);
    assert!(controller.recovery().failures(0, 16).is_empty());
    factory.stall_next_task.store(true, Ordering::SeqCst);
    advance(Duration::from_secs(1)).await;
    settle().await;
    assert_eq!(factory.resets.load(Ordering::SeqCst), 1);
    assert!(
        controller.recovery().failures(0, 16)[0].resetting,
        "the deadline record announces reset without waiting for any UI/broker acknowledgment"
    );
    assert_eq!(factory.starts.load(Ordering::SeqCst), 2);
    assert_eq!(factory.maximum.load(Ordering::SeqCst), 1);
    assert_eq!(factory.live.load(Ordering::SeqCst), 1);
    assert_eq!(controller.recovery().status().phase, Phase::Running);
    // A healed dataset resets the per-incident final-attempt allowance.
    factory.fault();
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Recovering);
    advance(Duration::from_secs(120)).await;
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Running);
    assert_eq!(factory.resets.load(Ordering::SeqCst), 2);
    assert_eq!(factory.starts.load(Ordering::SeqCst), 3);
    controller.cancel();
    controller.join(Deadline::recovery()).await.unwrap();
    assert_eq!(factory.live.load(Ordering::SeqCst), 0);
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
}
#[tokio::test]
async fn emergency_reset_accepts_an_already_panicked_task_only_after_descendants_retire() {
    let root = Arc::new(Owner::default());
    let parent = Arc::new(root.child().unwrap());
    let mut task = Some(
        parent
            .spawn_joined(|_| async {
                panic!("injected old dataset panic");
            })
            .unwrap(),
    );
    while !task.as_ref().unwrap().is_finished() {
        tokio::task::yield_now().await;
    }
    parent
        .reset_task(&mut task, Deadline::recovery())
        .await
        .unwrap();
    assert!(task.is_none());
    drop(parent);
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
}
#[tokio::test(start_paused = true)]
async fn explicit_retry_after_terminal_failure_gets_one_final_reset_but_maintenance_does_not() {
    let root = Arc::new(Owner::default());
    let factory = NativeFactory::new(vec![]);
    let controller = Supervisor::start(
        root.clone(),
        factory.clone(),
        Some(Arc::new(1)),
        required(),
        Deadline::recovery(),
    )
    .unwrap();
    settle().await;
    advance(Duration::from_secs(120)).await;
    settle().await;
    advance(Duration::from_secs(120)).await;
    settle().await;
    assert_eq!(controller.recovery().status().phase, Phase::Failed);
    assert_eq!(factory.starts.load(Ordering::SeqCst), 2);
    controller.apply(Some(Arc::new(2)), required()).unwrap();
    settle().await;
    assert_eq!(factory.starts.load(Ordering::SeqCst), 3);
    advance(Duration::from_secs(120)).await;
    settle().await;
    assert_eq!(factory.starts.load(Ordering::SeqCst), 4);
    assert_eq!(controller.recovery().status().phase, Phase::Recovering);
    assert_eq!(factory.maximum.load(Ordering::SeqCst), 1);
    controller.cancel();
    controller.join(Deadline::recovery()).await.unwrap();
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
}
