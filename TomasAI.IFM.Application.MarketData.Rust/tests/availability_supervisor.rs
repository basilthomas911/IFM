//! Offline integration of missing output, whole-dataset retirement/rebuild and
//! retained terminal notification. No broker or C# consumer drives cancellation.
use futures_util::future::BoxFuture;
use ifm_market_data_native::{
    dataset::supervisor::{Factory, Readiness, Resources, Supervisor},
    runtime::{
        Deadline,
        availability::Availability,
        owner::{Child, Owner},
        recovery::{Phase, Proof, Requirements},
    },
};
use std::{
    sync::{
        Arc,
        atomic::{AtomicUsize, Ordering},
    },
    time::Duration,
};
use tokio::{sync::watch, task::JoinHandle, time::advance};
use tokio_util::sync::CancellationToken;

struct Feed {
    available: watch::Sender<bool>,
    restore: bool,
    starts: AtomicUsize,
    live: Arc<AtomicUsize>,
    maximum: Arc<AtomicUsize>,
}
struct Group {
    parent: Arc<Child>,
    task: Option<JoinHandle<()>>,
    health: watch::Receiver<Readiness>,
}
struct Live(Arc<AtomicUsize>);
impl Drop for Live {
    fn drop(&mut self) {
        self.0.fetch_sub(1, Ordering::SeqCst);
    }
}
impl Factory for Feed {
    type Intent = ();
    fn start(
        &self,
        _: Arc<()>,
        parent: Arc<Child>,
        deadline: Deadline,
    ) -> Result<Box<dyn Resources>, String> {
        let attempt = self.starts.fetch_add(1, Ordering::SeqCst);
        let mut input = self.available.subscribe();
        let restored = self.restore && attempt > 0;
        let count = self.live.fetch_add(1, Ordering::SeqCst) + 1;
        self.maximum.fetch_max(count, Ordering::SeqCst);
        let live = Live(self.live.clone());
        let (health, observed) = watch::channel(Readiness::default());
        let task = parent
            .spawn_joined(move |stop| async move {
                let _live = live;
                let mut setup = Some(deadline);
                let mut availability = Availability::default();
                loop {
                    let usable = restored || *input.borrow_and_update();
                    availability.observe("short-put", usable).unwrap();
                    if let Some(key) = availability.expired() {
                        health.send_replace(Readiness {
                            setup_complete: false,
                            proofs: vec![],
                            failure: Some(format!(
                                "Required output unavailable for 30 seconds: {key}"
                            )),
                            deadline: Some(setup.unwrap_or_else(Deadline::recovery)),
                        });
                        stop.cancelled().await;
                        break;
                    }
                    if usable {
                        setup = None;
                    }
                    health.send_replace(Readiness {
                        setup_complete: false,
                        proofs: if usable {
                            vec![Proof::Transport, Proof::Publication]
                        } else {
                            vec![Proof::Transport]
                        },
                        failure: None,
                        deadline: setup,
                    });
                    tokio::select! {biased;
                        ()=stop.cancelled()=>break,
                        ()=Availability::pending(availability.next_expiration())=>{},
                        changed=input.changed()=>{if changed.is_err(){break;}},
                    }
                }
            })
            .map_err(|e| format!("fixture admission: {e:?}"))?;
        Ok(Box::new(Group {
            parent,
            task: Some(task),
            health: observed,
        }))
    }
}
impl Resources for Group {
    fn cancel(&self, _: Deadline) {
        self.parent.cancellation().cancel();
    }
    fn changes(&self) -> watch::Receiver<Readiness> {
        self.health.clone()
    }
    fn join(&mut self, deadline: Deadline) -> BoxFuture<'_, Result<(), String>> {
        Box::pin(async move {
            if let Some(task) = self.task.as_mut() {
                deadline
                    .run(&CancellationToken::new(), task)
                    .await
                    .map_err(|e| e.to_string())?
                    .map_err(|e| e.to_string())?;
                self.task.take();
            }
            self.parent
                .wait_nested(&CancellationToken::new(), deadline)
                .await
                .map_err(|e| format!("fixture join: {e:?}"))
        })
    }
}
async fn settle() {
    for _ in 0..40 {
        tokio::task::yield_now().await;
    }
}
async fn case(restore: bool) {
    let root = Arc::new(Owner::default());
    let feed = Arc::new(Feed {
        available: watch::channel(true).0,
        restore,
        starts: AtomicUsize::new(0),
        live: Arc::default(),
        maximum: Arc::default(),
    });
    let supervisor = Supervisor::start(
        root.clone(),
        feed.clone(),
        Some(Arc::new(())),
        Requirements::new(&[Proof::Transport, Proof::Publication]),
        Deadline::recovery(),
    )
    .unwrap();
    settle().await;
    assert_eq!(supervisor.recovery().status().phase, Phase::Running);
    // Reproduce a first incident long after startup without advancing API time.
    advance(Duration::from_secs(3600)).await;
    feed.available.send_replace(false);
    settle().await;
    advance(Duration::from_secs(29)).await;
    settle().await;
    assert_eq!(feed.starts.load(Ordering::SeqCst), 1);
    advance(Duration::from_secs(1)).await;
    settle().await;
    assert_eq!(feed.starts.load(Ordering::SeqCst), 2);
    assert_eq!(
        feed.maximum.load(Ordering::SeqCst),
        1,
        "replacement overlapped retired resources"
    );
    if restore {
        assert_eq!(supervisor.recovery().status().phase, Phase::Running);
        assert!(supervisor.recovery().failures(0, 16).is_empty());
    } else {
        let original = supervisor.recovery().status().deadline.unwrap();
        assert_eq!(original.remaining(), Duration::from_secs(120));
        for _ in 0..3 {
            advance(Duration::from_secs(30)).await;
            settle().await;
        }
        assert_eq!(original.remaining(), Duration::from_secs(30));
        advance(Duration::from_secs(29)).await;
        settle().await;
        assert_eq!(supervisor.recovery().status().phase, Phase::Recovering);
        advance(Duration::from_secs(1)).await;
        settle().await;
        assert_eq!(supervisor.recovery().status().phase, Phase::Recovering);
        let failures = supervisor.recovery().failures(0, 16);
        assert_eq!(failures.len(), 1);
        assert!(
            failures[0]
                .status
                .root_error
                .as_deref()
                .unwrap()
                .contains("short-put")
        );
        advance(Duration::from_secs(120)).await;
        settle().await;
        assert_eq!(supervisor.recovery().status().phase, Phase::Failed);
        assert_eq!(feed.live.load(Ordering::SeqCst), 0);
        advance(Duration::from_secs(600)).await;
        settle().await;
        assert_eq!(
            supervisor.recovery().failures(0, 16).len(),
            2,
            "broker absence erased or duplicated the terminal notice"
        );
    }
    supervisor.cancel();
    supervisor.join(Deadline::recovery()).await.unwrap();
    assert_eq!(feed.live.load(Ordering::SeqCst), 0);
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
}
#[tokio::test(start_paused = true)]
async fn missing_required_quote_rebuilds_and_recovers_without_overlapping_owners() {
    case(true).await;
}
#[tokio::test(start_paused = true)]
async fn permanent_missing_quote_retries_for_original_120_seconds_then_retains_ui_failure() {
    case(false).await;
}
