//! Nested physical-session ownership beneath one logical dataset. Cancellation
//! cannot claim root cleanup while a provider future still owns its socket.
use ifm_market_data_native::runtime::{
    Deadline,
    owner::{Owner, OwnerError},
};
use std::{
    sync::{
        Arc,
        atomic::{AtomicUsize, Ordering},
    },
    time::Duration,
};
use tokio::{
    io::AsyncReadExt,
    net::{TcpListener, TcpStream},
};
use tokio_util::sync::CancellationToken;

struct Resource {
    socket: Option<TcpStream>,
    dropped: Arc<AtomicUsize>,
}
impl Drop for Resource {
    fn drop(&mut self) {
        drop(self.socket.take());
        self.dropped.fetch_add(1, Ordering::SeqCst);
    }
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn dropping_options_owner_closes_all_nested_sessions_without_cancelling_futures() {
    let root = Arc::new(Owner::default());
    let futures = root.child().unwrap();
    let other_futures = root.child().unwrap();
    let options = root.child().unwrap();
    assert!(matches!(root.child(), Err(OwnerError::Capacity)));
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let address = listener.local_addr().unwrap();
    let dropped = Arc::new(AtomicUsize::new(0));
    let (ready_tx, mut ready) = tokio::sync::mpsc::channel(3);
    let mut handles = Vec::new();
    for index in 0..3 {
        let dropped = dropped.clone();
        let ready = ready_tx.clone();
        let task = options
            .spawn(async move {
                let socket = TcpStream::connect(address).await.unwrap();
                let _resource = Resource {
                    socket: Some(socket),
                    dropped,
                };
                ready.send(()).await.unwrap();
                std::future::pending::<()>().await;
            })
            .unwrap();
        // An abandoned JoinHandle must not detach the resource from cleanup.
        if index == 0 {
            drop(task);
        } else {
            handles.push(task);
        }
    }
    let peers = tokio::spawn(async move {
        let mut sockets = Vec::new();
        for _ in 0..3 {
            sockets.push(listener.accept().await.unwrap().0);
        }
        for mut socket in sockets {
            let mut one = [0];
            assert_eq!(socket.read(&mut one).await.unwrap(), 0);
        }
    });
    for _ in 0..3 {
        ready.recv().await.unwrap();
    }
    drop(options);
    for task in handles {
        task.await.unwrap();
    }
    tokio::time::timeout(Duration::from_secs(2), peers)
        .await
        .unwrap()
        .unwrap();
    assert_eq!(dropped.load(Ordering::SeqCst), 3);
    assert!(!futures.cancellation().is_cancelled());
    assert!(!other_futures.cancellation().is_cancelled());
    // The options root slot becomes available only after its nested state drops.
    let replacement = root.child().unwrap();
    drop(replacement);
    drop(futures);
    drop(other_futures);
    root.stop().unwrap();
    root.wait_idle(
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    assert!(root.disposable().unwrap());
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn nested_capacity_panic_and_unpolled_cancellation_release_tracking_without_leaks() {
    let root = Arc::new(Owner::default());
    let parent = root.child().unwrap();
    for _ in 0..1000 {
        parent.spawn(async {}).unwrap().await.unwrap();
    }
    assert!(
        parent
            .spawn(async { panic!("contained qualification panic") })
            .unwrap()
            .await
            .unwrap_err()
            .is_panic()
    );
    let mut tasks = Vec::new();
    for _ in 0..256 {
        tasks.push(parent.spawn(std::future::pending()).unwrap());
    }
    assert!(matches!(parent.spawn(async {}), Err(OwnerError::Capacity)));
    let token = parent.cancellation();
    token.cancel();
    parent
        .wait_nested(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    assert!(matches!(parent.spawn(async {}), Err(OwnerError::Closed)));
    drop(parent);
    root.stop().unwrap();
    for task in tasks {
        task.await.unwrap();
    }
    root.wait_idle(
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    assert!(root.disposable().unwrap());
}
