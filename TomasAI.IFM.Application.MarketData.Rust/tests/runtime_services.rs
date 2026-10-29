//! Native control supervisors live outside replaceable dataset children, so
//! rebuilding a dataset never waits for the supervisor's own tracked task.
use ifm_market_data_native::runtime::{
    Deadline,
    owner::{Owner, OwnerError},
};
use std::{sync::Arc, time::Duration};
use tokio_util::sync::CancellationToken;

#[tokio::test]
async fn service_capacity_and_dataset_slots_are_independent_and_root_joins_both() {
    let root = Arc::new(Owner::default());
    let mut services = Vec::new();
    for _ in 0..4 {
        services.push(
            root.spawn_service(|stop| async move {
                stop.cancelled().await;
            })
            .unwrap(),
        );
    }
    assert!(matches!(
        root.spawn_service(|_| async {}),
        Err(OwnerError::Capacity)
    ));
    let a = root.child().unwrap();
    let b = root.child().unwrap();
    let c = root.child().unwrap();
    assert!(matches!(root.child(), Err(OwnerError::Capacity)));
    root.stop().unwrap();
    drop(a);
    drop(b);
    drop(c);
    for service in services {
        service.await.unwrap();
    }
    root.wait_idle(
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(1)),
    )
    .await
    .unwrap();
    assert!(root.disposable().unwrap());
    assert!(matches!(
        root.spawn_service(|_| async {}),
        Err(OwnerError::Closed)
    ));
}
#[tokio::test]
async fn dropped_control_handle_and_stalled_copied_consumer_do_not_abandon_a_supervisor() {
    let root = Arc::new(Owner::default());
    let (sender, _consumer) = tokio::sync::mpsc::channel(1);
    sender.try_send(vec![0u8; 65536]).unwrap();
    let (started, ready) = tokio::sync::oneshot::channel();
    let service=root.spawn_service(move|stop|async move{
        started.send(()).unwrap();
        tokio::select!{biased;()=stop.cancelled()=>{},_=sender.send(vec![1;65536])=>panic!("consumer unexpectedly progressed")}
    }).unwrap();
    ready.await.unwrap();
    drop(service);
    root.stop().unwrap();
    root.wait_idle(
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(1)),
    )
    .await
    .unwrap();
    assert!(root.disposable().unwrap());
}
