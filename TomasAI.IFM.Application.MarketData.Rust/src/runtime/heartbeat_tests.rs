//! Quote-free connection liveness is independent of financial readiness.
use super::{
    Deadline,
    owner::{Child, Owner},
    recovery::{Phase, Proof, Requirements},
};
use crate::dataset::supervisor::{Factory, Readiness, Resources, Supervisor};
use futures_util::future::BoxFuture;
use std::sync::{
    Arc, Mutex, Weak,
    atomic::{AtomicUsize, Ordering},
};
use tokio::sync::watch;
use tokio_util::sync::CancellationToken;

struct QuietFactory {
    reconstructed: bool,
    source: Mutex<Weak<Child>>,
    starts: AtomicUsize,
}
struct Quiet {
    parent: Arc<Child>,
    status: watch::Receiver<Readiness>,
    _sender: watch::Sender<Readiness>,
}
impl Resources for Quiet {
    fn cancel(&self, _: Deadline) {
        self.parent.cancellation().cancel();
    }
    fn changes(&self) -> watch::Receiver<Readiness> {
        self.status.clone()
    }
    fn provider(&self) -> Option<super::owner::ProviderUpdates> {
        Some(self.parent.provider_state())
    }
    fn join(&mut self, _: Deadline) -> BoxFuture<'_, Result<(), String>> {
        Box::pin(async { Ok(()) })
    }
}
impl Factory for QuietFactory {
    type Intent = ();
    fn start(
        &self,
        _: Arc<()>,
        parent: Arc<Child>,
        _: Deadline,
    ) -> Result<Box<dyn Resources>, String> {
        self.starts.fetch_add(1, Ordering::SeqCst);
        *self.source.lock().unwrap() = Arc::downgrade(&parent);
        // No quote, reference, pricing or publication proof is supplied.
        let (_sender, status) = watch::channel(Readiness {
            setup_complete: self.reconstructed,
            proofs: if self.reconstructed { vec![Proof::Transport] } else { vec![] },
            ..Default::default()
        });
        parent.provider_received();
        Ok(Box::new(Quiet {
            parent,
            status,
            _sender,
        }))
    }
}
async fn settle() {
    for _ in 0..32 {
        tokio::task::yield_now().await;
    }
}

#[tokio::test(start_paused = true)]
async fn fifteen_minutes_of_provider_activity_without_quotes_cannot_start_recovery() {
    let root = Arc::new(Owner::default());
    let factory = Arc::new(QuietFactory {
        reconstructed: true,
        source: Mutex::new(Weak::new()),
        starts: AtomicUsize::new(0),
    });
    let supervisor = Supervisor::start(
        root.clone(),
        factory.clone(),
        Some(Arc::new(())),
        Requirements::new(&[Proof::Transport, Proof::Pricing, Proof::Publication]),
        Deadline::recovery(),
    )
    .unwrap();
    settle().await;
    for _ in 0..30 {
        tokio::time::advance(std::time::Duration::from_secs(30)).await;
        factory
            .source
            .lock()
            .unwrap()
            .upgrade()
            .unwrap()
            .provider_received();
        settle().await;
        assert!(supervisor.recovery().connection_alive());
        assert_eq!(supervisor.recovery().status().phase, Phase::Starting);
        assert!(
            supervisor
                .recovery()
                .status()
                .missing
                .contains(&Proof::Pricing)
        );
        assert!(supervisor.recovery().failures(0, 16).is_empty());
        assert_eq!(factory.starts.load(Ordering::SeqCst), 1);
    }
    supervisor.cancel();
    supervisor.join(Deadline::recovery()).await.unwrap();
    drop(supervisor);
    drop(factory);
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
}

#[tokio::test(start_paused = true)]
async fn bootstrap_activity_cannot_cancel_reconstruction_deadline_or_prevent_final_reset() {
    let root = Arc::new(Owner::default());
    let factory = Arc::new(QuietFactory {
        reconstructed: false,
        source: Mutex::new(Weak::new()), starts: AtomicUsize::new(0),
    });
    let supervisor = Supervisor::start(root.clone(), factory.clone(), Some(Arc::new(())),
        Requirements::new(&[Proof::Transport, Proof::Pricing]), Deadline::recovery()).unwrap();
    settle().await;
    let old = factory.source.lock().unwrap().upgrade().unwrap();
    for _ in 0..3 {
        tokio::time::advance(std::time::Duration::from_secs(30)).await;
        old.provider_received();
        settle().await;
        assert!(supervisor.recovery().status().deadline.is_some());
        assert!(!supervisor.recovery().connection_alive());
        assert_eq!(factory.starts.load(Ordering::SeqCst), 1);
    }
    tokio::time::advance(std::time::Duration::from_secs(30)).await;
    settle().await;
    assert!(old.cancellation().is_cancelled());
    assert_eq!(factory.starts.load(Ordering::SeqCst), 2, "Exactly one final replacement must occur");
    drop(old);
    supervisor.cancel(); supervisor.join(Deadline::recovery()).await.unwrap();
    drop(supervisor); drop(factory); root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery()).await.unwrap();
    assert!(root.disposable().unwrap());
}
