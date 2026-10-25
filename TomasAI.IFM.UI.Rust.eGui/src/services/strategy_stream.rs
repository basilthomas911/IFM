//! One owned strategy subscription lifetime. Listeners attach before hydration;
//! reconnection repairs through read-only API history, never by restarting feeds.
use crate::{
    Snapshot,
    backend::{codec, schema::Registry, transport::Backend},
    services::strategy,
    state::strategy::StrategyController,
};
use futures_util::StreamExt;
use std::sync::Arc;
pub async fn run(
    backend: Backend,
    registry: Arc<Registry>,
    mut actions: tokio::sync::mpsc::Receiver<u32>,
    updates: tokio::sync::mpsc::Sender<Result<Snapshot, String>>,
) {
    let workflow = "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent";
    let signal =
        "TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events.FuturesItiSignalUpdatedNotifyEvent";
    let mut state = StrategyController::default();
    let listeners = async {
        let workflow = backend
            .subscribe("Notify", "IntrinsicTimeStrategyWorkflowNotification")
            .await?;
        let signal = backend
            .subscribe("Notify", "FuturesItiSignalNotification")
            .await?;
        backend
            .flush_until(tokio::time::Instant::now() + backend.default_timeout())
            .await?;
        Ok::<_, String>((workflow, signal))
    }
    .await;
    let (mut workflows, mut signals) = match listeners {
        Ok(value) => value,
        Err(error) => {
            let _ = updates.send(Err(error)).await;
            return;
        }
    };
    if let Err(error) = state.refresh(&backend, &registry, false).await {
        state.set_error(&error);
        if updates.send(Err(error)).await.is_err() {
            return;
        }
    }
    if updates.send(state.snapshot(&registry)).await.is_err() {
        return;
    }
    let mut timer = tokio::time::interval(std::time::Duration::from_secs(30));
    timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Delay);
    timer.reset();
    let mut connection = backend.watch_connection();
    // Hydration already covered the connection that created this owner. A cloned
    // watch receiver may still carry that initial Connected change as unread.
    // Only subsequent transitions require another repair, not an immediate
    // duplicate publication after the first successful load.
    connection.borrow_and_update();
    loop {
        let result = tokio::select! {
         value=actions.recv()=>{let Some(action)=value else{return;};match state.action(action){Err(error)=>Err(error),Ok(())=>if action==2||action==13{state.refresh(&backend,&registry,action==13).await.map(|_|true)}else{Ok(true)}}},
         value=workflows.next()=>{let Some(event)=value else{let _=updates.send(Err("Strategy workflow subscription stopped; reopen the UI to reattach.".into())).await;return;};
          (||{if event.subject.splitn(4,'.').nth(2)!=Some("Updated"){return Ok(false);}let wire=codec::decode(&event.payload)?;let value=strategy::member(&registry,workflow,&wire,"State")?;let declared=registry.project(workflow,&wire)?;let actual=registry.project(crate::services::strategy_display::VIEW,&value)?;if declared["WorkflowId"]!=actual["WorkflowId"]||declared["WorkflowRevision"]!=actual["WorkflowRevision"]{return Err("Strategy notification identity/revision mismatch".into());}state.apply_view(&registry,value)})()
         },
         value=signals.next()=>{let Some(event)=value else{let _=updates.send(Err("ITI subscription stopped; reopen the UI to reattach.".into())).await;return;};
          (||{if event.subject.splitn(4,'.').nth(2)!=Some("Updated"){return Ok(false);}let wire=codec::decode(&event.payload)?;let value=strategy::member(&registry,signal,&wire,"FuturesItiSignal")?;state.apply_signal(&registry,value,false)})()
         },
         _=timer.tick()=>state.refresh(&backend,&registry,false).await.map(|_|true),
         changed=connection.changed()=>{if changed.is_err(){return;}let connected=connection.borrow_and_update().connected;if connected{state.refresh(&backend,&registry,false).await.map(|_|true)}else{Err("Strategy stream disconnected; awaiting backend connection".into())}},
        };
        if matches!(result, Ok(false)) {
            continue;
        }
        if let Err(error) = result {
            state.set_error(&error);
            if updates.send(Err(error)).await.is_err() {
                return;
            }
        }
        if updates.send(state.snapshot(&registry)).await.is_err() {
            return;
        }
    }
}
