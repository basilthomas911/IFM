//! Native owner for application services. Rendering never performs backend I/O.
use super::super::Update;
use eframe::egui;
use ifm_ui_egui::{Catalog, Snapshot, backend::transport::*};
use ifm_ui_egui::{backend::schema::Registry, state::lookup::LookupController};
use ifm_ui_egui::services::business_context::BusinessContext;
use std::{sync::mpsc, time::Duration};

/// Keep open fill views synchronized with committed projections and reconnects.
async fn observe_order_fills(
    backend: Backend,
    registry: std::sync::Arc<Registry>,
    state: std::sync::Arc<tokio::sync::Mutex<ifm_ui_egui::state::trades::TradeController>>,
    results: tokio::sync::mpsc::Sender<Update>,
    mut session: tokio::sync::watch::Receiver<Option<BusinessContext>>,
) -> Result<(), String> {
    use futures_util::{FutureExt, StreamExt};
    let mut executions = backend.subscribe("Event", "OrderExecutionEvent").await?;
    let mut orders = backend.subscribe("Event", "TradeOrderEvent").await?;
    let mut brokers = backend.subscribe("Event", "BrokerOrderEvent").await?;
    let mut connection = backend.watch_connection();
    let mut value_date = session.borrow().as_ref().map(|session| session.value_date);
    loop {
        let subject = tokio::select! {
            event = executions.next() => Some(event.ok_or("Order execution subscription closed")?.subject),
            event = orders.next() => Some(event.ok_or("Trade order subscription closed")?.subject),
            event = brokers.next() => Some(event.ok_or("Broker order subscription closed")?.subject),
            changed = connection.changed() => {
                if changed.is_err() { return Ok(()); }
                if !connection.borrow_and_update().connected { continue; }
                None
            }
            changed = session.changed() => {
                if changed.is_err() { return Ok(()); }
                let next = session.borrow_and_update().as_ref().map(|session| session.value_date);
                if next.is_none() || next == value_date { continue; }
                value_date = next;
                None
            }
        };
        let mut scope = subject;
        for _ in 0..256 {
            let next = executions.next().now_or_never().flatten()
                .or_else(|| orders.next().now_or_never().flatten())
                .or_else(|| brokers.next().now_or_never().flatten());
            let Some(next) = next else { break; };
            if scope.as_ref() != Some(&next.subject) {
                scope = None;
            }
        }
        let controller = &mut *state.lock().await;
        match controller.refresh_fills_after_notice(&backend, &registry, scope.as_deref()).await {
            Ok(Some(publication)) => {
                if results.send(Update::Trades(publication)).await.is_err() { return Ok(()); }
            }
            Ok(None) => {}
            Err(error) => {
                if results.send(Update::Warning(format!("Order Fills refresh failed: {error}"))).await.is_err() { return Ok(()); }
            }
        }
    }
}


/// Rechecks the API at session boundaries and reconciles changes between them.
async fn watch_market_session(
    backend: Backend,
    session: tokio::sync::watch::Sender<Option<BusinessContext>>,
) {
    while !session.is_closed() {
        let delay = match BusinessContext::load_with_transition(&backend).await {
            Ok((context, transition)) => {
                let cadence = if context.is_market_open() { 5 } else { 60 };
                let boundary_delay = (transition - context.as_of_utc)
                    .to_std()
                    .unwrap_or_default()
                    .saturating_add(Duration::from_millis(100));
                session.send_replace(Some(context));
                boundary_delay.min(Duration::from_secs(cadence))
            }
            Err(_) => {
                session.send_replace(None);
                Duration::from_secs(5)
            }
        };
        tokio::select! {
            _ = tokio::time::sleep(delay) => {},
            _ = session.closed() => return,
        }
    }
}


/// Refreshes throughout open overnight/regular sessions and pauses periodic work after close.
async fn poll_market_view<F, Refresh>(
    mut session: tokio::sync::watch::Receiver<Option<BusinessContext>>,
    cadence: Duration,
    mut refresh: F,
) where
    F: FnMut(Option<BusinessContext>) -> Refresh,
    Refresh: std::future::Future<Output = ()>,
{
    loop {
        let current = session.borrow_and_update().clone();
        if !current.as_ref().is_some_and(BusinessContext::is_market_open) {
            let was_known = current.is_some();
            let changed = async {
                session.wait_for(|next| {
                    next.is_some() != was_known
                        || next.as_ref().is_some_and(BusinessContext::is_market_open)
                }).await.is_ok()
            };
            tokio::pin!(changed);
            let keep_running = tokio::select! {
                biased;
                next = &mut changed => next,
                _ = refresh(current) => changed.await,
            };
            if !keep_running {
                return;
            }
            continue;
        }

        let identity = current.as_ref().map(|value| (value.value_date, value.active_value_date));
        let mut active_session = session.clone();
        let refresh_while_trading = async {
            let mut timer = tokio::time::interval(cadence);
            timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Delay);
            loop {
                timer.tick().await;
                let current = active_session.borrow_and_update().clone();
                if current.as_ref().is_some_and(BusinessContext::is_market_open) {
                    refresh(current).await;
                }
            }
        };
        tokio::select! {
            biased;
            paused = session.wait_for(|next| !next.as_ref().is_some_and(BusinessContext::is_market_open)
                || next.as_ref().map(|value| (value.value_date, value.active_value_date)) != identity) => {
                if paused.is_err() {
                    return;
                }
            }
            _ = refresh_while_trading => {},
        }
    }
}


/// Owns the native async runtime, dispatches UI intents and delivers bounded result batches until
/// shutdown.
pub(crate) fn run(
    updates: mpsc::SyncSender<Update>,
    actions: mpsc::Receiver<u32>,
    intents: mpsc::Receiver<String>,
    context: egui::Context,
    stopping: std::sync::Arc<std::sync::atomic::AtomicBool>,
    wake: std::sync::Arc<tokio::sync::Notify>,
) {
    let runtime = match tokio::runtime::Builder::new_multi_thread()
        .worker_threads(2)
        .enable_all()
        .build()
    {
        Ok(value) => value,
        Err(error) => {
            ifm_ui_egui::diagnostics::errors::record(
                "ui.runtime_start_failed",
                None,
                None,
                &format!("{error:?}"),
            );
            let _ = updates.try_send(Update::Error(format!(
                "Cannot start native runtime: {error}"
            )));
            context.request_repaint();
            return;
        }
    };
    runtime.block_on(async move {
        let mut pending = Vec::new();
        let (results, mut received) = tokio::sync::mpsc::channel::<Update>(32);
        let initial = results.clone();
        let startup = tokio::spawn(async move {
            let result = async {
                let settings = Settings::from_env()?;
                let deadline = tokio::time::Instant::now() + settings.request_timeout;
                let backend = tokio::time::timeout_at(deadline, Backend::connect(settings))
                    .await.map_err(|_| "Native API startup deadline expired".to_string())??;
                let session = BusinessContext::await_ready(&backend,deadline).await?;
                let date=session.value_date;
                let catalog: Catalog = serde_json::from_str(include_str!("../../catalog/strings.en.json")).map_err(|e| e.to_string())?;
                let snapshot: Snapshot = serde_json::from_value(serde_json::json!({
                    "schema":1,"epoch":uuid::Uuid::new_v4().to_string(),"generation":1,"mode":"Live","workflowId":"","revision":0,"roots":[],
                    "status":format!("Connected â€” API ValueDate {date}")
                })).map_err(|e| e.to_string())?;
                initial.send(Update::Ready(catalog, snapshot)).await.map_err(|_| "UI closed")?;
                initial.send(Update::Dashboard(ifm_ui_egui::shell::Dashboard {
                    title: ifm_ui_egui::WINDOW_TITLE.to_owned(),
                    status: format!("API ValueDate {date}"),
                    ..Default::default()
                })).await.map_err(|_| "UI closed")?;
                Ok::<_,String>((backend, session))
            }.await;
            if let Err(error) = &result { let _ = initial.send(Update::Error(error.clone())).await; }
            result
        });
        // Exit remains responsive even if a backend operation consumes its entire deadline.
        let mut startup = Some(startup);
        let mut backend = None;
        let registry = match Registry::compiled() {
            Ok(registry)=>std::sync::Arc::new(registry),
            Err(error)=>{ifm_ui_egui::diagnostics::errors::record("ui.metadata_invalid",None,None,&error);if let Some(task)=startup.take(){task.abort();}let _=updates.try_send(Update::Error(format!("Invalid compiled backend metadata: {error}")));context.request_repaint();return;}
        };
        let lookup_state = std::sync::Arc::new(tokio::sync::Mutex::new(LookupController::default()));
        let calendar_state = std::sync::Arc::new(tokio::sync::Mutex::new(ifm_ui_egui::state::calendar::CalendarController::default()));
        let yield_state = std::sync::Arc::new(tokio::sync::Mutex::new(ifm_ui_egui::state::yield_curve::YieldController::default()));
        let futures_state=std::sync::Arc::new(tokio::sync::Mutex::new(ifm_ui_egui::state::futures_contract::FuturesContractController::default()));
        let options_state=std::sync::Arc::new(tokio::sync::Mutex::new(ifm_ui_egui::state::option_contract::OptionContractController::default()));
        let provider_state=std::sync::Arc::new(tokio::sync::Mutex::new(ifm_ui_egui::state::provider_definitions::ProviderController::default()));
        let scheduled_state=std::sync::Arc::new(tokio::sync::Mutex::new(ifm_ui_egui::state::scheduled_tasks::ScheduledController::default()));
        let backup_state=std::sync::Arc::new(tokio::sync::Mutex::new(ifm_ui_egui::state::database_backup::BackupController::default()));
        let parameter_state=std::sync::Arc::new(tokio::sync::Mutex::new(ifm_ui_egui::state::parameters::ParameterController::default()));
        let inspection_state=std::sync::Arc::new(tokio::sync::Mutex::new(ifm_ui_egui::state::strategy_inspection::InspectionController::default()));
        let portfolio_state=std::sync::Arc::new(tokio::sync::Mutex::new(ifm_ui_egui::state::portfolio::PortfolioController::default()));
        let trade_controller=ifm_ui_egui::state::trades::TradeController::default();
        let mut monitor_updates=trade_controller.monitors.watch();
        let trade_state=std::sync::Arc::new(tokio::sync::Mutex::new(trade_controller));
        let catalog_state = std::sync::Arc::new(tokio::sync::Mutex::new(ifm_ui_egui::state::strategy_catalog::CatalogController::default()));
        let actor_health_state=std::sync::Arc::new(tokio::sync::Mutex::new(None::<ifm_ui_egui::state::actor_health::ActorHealthController>));
        let health_state = std::sync::Arc::new(tokio::sync::Mutex::new(None::<ifm_ui_egui::state::operations_health::OperationsHealthController>));
        let mut operations = tokio::task::JoinSet::new();
        let monitor_results=results.clone();
        operations.spawn(async move{while monitor_updates.changed().await.is_ok(){let publication=monitor_updates.borrow_and_update().clone();if monitor_results.send(Update::TradeMonitor(publication)).await.is_err(){break;}}});
        let mut sidebar_updates:Option<tokio::sync::mpsc::Receiver<ifm_ui_egui::sidebar::SidebarPublication>> = None;
        let mut sidebar_actions:Option<tokio::sync::mpsc::Sender<u32>>=None;
        let mut strategy_actions:Option<tokio::sync::mpsc::Sender<u32>>=None;

        'running: loop {
            if stopping.load(std::sync::atomic::Ordering::Acquire) { break; }
            let mut completed_startup = None;
            tokio::select! {
                _=wake.notified()=>{},
                result=async{startup.as_mut().unwrap().await},if startup.is_some()=>{startup.take();completed_startup=Some(result);},
                outcome=operations.join_next(),if !operations.is_empty()=>{
                    if let Some(Err(error))=outcome {pending.push(Update::Warning(format!("Native controller task failed: {error}")));}
                },
                update=received.recv(),if pending.len()<64=>{
                    if let Some(update)=update {if matches!(&update,Update::TradeMonitor(_)){pending.retain(|old|!matches!(old,Update::TradeMonitor(_)));}pending.push(update);}
                },
                publication=async{sidebar_updates.as_mut().unwrap().recv().await},if sidebar_updates.is_some()=>{
                    if let Some(publication)=publication {pending.retain(|old|!matches!(old,Update::Sidebar(_)));pending.push(Update::Sidebar(publication)); }
                    else { sidebar_updates=None; }
                }
            }
            if stopping.load(std::sync::atomic::Ordering::Acquire) { break; }
            if let Some(outcome) = completed_startup {
                    match outcome {
                        Ok(Ok((client, initial_session))) => {
                            let (strategy_sender,strategy_receiver)=tokio::sync::mpsc::channel(32);strategy_actions=Some(strategy_sender);
                            let (strategy_updates,mut strategy_received)=tokio::sync::mpsc::channel(8);
                            let strategy_backend=client.clone();let strategy_registry=registry.clone();let strategy_results=results.clone();
                            operations.spawn(ifm_ui_egui::services::strategy_stream::run(strategy_backend,strategy_registry,strategy_receiver,strategy_updates));
                            operations.spawn(async move{while let Some(update)=strategy_received.recv().await{let update=match update{Ok(snapshot)=>Update::Snapshot(snapshot),Err(error)=>Update::Warning(error)};if strategy_results.send(update).await.is_err(){break;}}});
                            let (sidebar_sender, sidebar_receiver) = tokio::sync::mpsc::channel(32);
                            sidebar_updates = Some(sidebar_receiver);
                            let (action_sender,action_receiver)=tokio::sync::mpsc::channel(32);sidebar_actions=Some(action_sender);
                            let monitor_backend = client.clone();
                            let chart_backend=client.clone();let chart_registry=registry.clone();let chart_results=results.clone();
                            let outlook_backend=client.clone();let outlook_registry=registry.clone();let outlook_results=results.clone();
                            let diagnostic_backend=client.clone();let diagnostic_results=results.clone();
                            let monitor_registry = registry.clone();
                            let monitor_results = results.clone();
                            operations.spawn(async move {
                                if let Err(error) = ifm_ui_egui::services::notifications::monitor(monitor_backend, monitor_registry, sidebar_sender, action_receiver).await {
                                    let _ = monitor_results.send(Update::Warning(error)).await;
                                }
                            });
                            let (session_sender, session) = tokio::sync::watch::channel(Some(initial_session));
                            operations.spawn(watch_market_session(client.clone(), session_sender));
                            let fills_backend = client.clone();
                            let fills_registry = chart_registry.clone();
                            let fills_state = trade_state.clone();
                            let fills_results = results.clone();
                            let fills_session = session.clone();
                            operations.spawn(async move {
                                if let Err(error) = observe_order_fills(fills_backend, fills_registry, fills_state, fills_results.clone(), fills_session).await {
                                    let _ = fills_results.send(Update::Warning(error)).await;
                                }
                            });
                            operations.spawn(poll_market_view(
                                session.clone(), Duration::from_secs(15), move |session| {
                                    let backend = chart_backend.clone();
                                    let registry = chart_registry.clone();
                                    let results = chart_results.clone();
                                    async move {
                                        if let Some(session) = session {
                                            let charts = ifm_ui_egui::services::market_history::load_for_session(
                                                &backend, &registry, &session,
                                            ).await;
                                            let _ = results.send(Update::MarketCharts(charts)).await;
                                        }
                                    }
                                },
                            ));
                            operations.spawn(poll_market_view(
                                session, Duration::from_secs(5), move |session: Option<BusinessContext>| {
                                    let backend = outlook_backend.clone();
                                    let registry = outlook_registry.clone();
                                    let results = outlook_results.clone();
                                    async move {
                                        let outlook = ifm_ui_egui::services::outlook::load_for_session(
                                            &backend, &registry, session.as_ref(),
                                        ).await;
                                        let _ = results.send(Update::MarketOutlook(outlook)).await;
                                    }
                                },
                            ));
                            operations.spawn(async move{
                                let client=ifm_ui_egui::services::dashboard_health::DashboardHealth::from_env();
                                let mut timer=tokio::time::interval(Duration::from_secs(5));
                                timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Delay);
                                loop{
                                    timer.tick().await;
                                    let observation=match &client{
                                        Ok(client)=>client.load(&diagnostic_backend).await.unwrap_or_else(|error|ifm_ui_egui::services::dashboard_health::PipelineHealth::unavailable(&error)),
                                        Err(error)=>ifm_ui_egui::services::dashboard_health::PipelineHealth::unavailable(error),
                                    };
                                    if diagnostic_results.send(Update::PipelineHealth(observation)).await.is_err(){break;}
                                }
                            });
                            backend = Some(client);
                        },
                        Ok(Err(_)) => {}, // Startup already published the specific error.
                        Err(error) => pending.push(Update::Error(format!("Native startup task failed: {error}"))),
                    }
            }
            while let Some(outcome) = operations.try_join_next() {
                if let Err(error) = outcome { pending.push(Update::Warning(format!("Native controller task failed: {error}"))); }
            }
            for _ in 0..32 { if pending.len() >= 64 {break;} match actions.try_recv() {
                Ok(u32::MAX) | Err(mpsc::TryRecvError::Disconnected) => break 'running,
                Ok(action) if matches!(action&0xf000_0000,0x8000_0000|0x9000_0000|0xa000_0000|0xb000_0000)=>{
                    if sidebar_actions.as_ref().is_none_or(|sender|sender.try_send(action).is_err()){pending.push(Update::Warning("Calendar action is unavailable while its service is starting or busy".into()));}
                }
                Ok(action) => {
                    if matches!(action,2|10..=15|1000..=0x0fff_ffff){
                        if strategy_actions.as_ref().is_none_or(|sender|sender.try_send(action).is_err()){pending.push(Update::Warning("Strategy action is unavailable while its service is starting or busy".into()));}
                    }else{
                        // Every unsupported action returns a visible rejection, never a successful fixture outcome.
                        pending.push(Update::Warning(format!("Standalone action {action} has no native controller yet")));
                    }
                }
                Err(mpsc::TryRecvError::Empty) => break
            } }
            for _ in 0..32 {
                if pending.len() >= 64 { break; }
                let Ok(intent) = intents.try_recv() else { break; };
                match serde_json::from_str::<serde_json::Value>(&intent) {
                    Ok(value) if value["view"]=="economic-calendar" && operations.len()<32 && backend.is_some()=>{
                        let backend=backend.as_ref().unwrap().clone();let state=calendar_state.clone();let registry=registry.clone();let results=results.clone();
                        operations.spawn(async move{
                            let mut state=state.lock().await;
                            let update=match state.execute(&backend,&registry,&value).await{Ok(publication)=>Update::Contracts(publication),Err(error)=>Update::SubmissionRejected(value["requestId"].as_str().unwrap_or_default().into(),error)};
                            let _=results.send(update).await;
                        });
                    }
                    Ok(value) if value["view"]=="futures-contract" && operations.len()<32 && backend.is_some()=>{
                        let backend=backend.as_ref().unwrap().clone();let state=futures_state.clone();let registry=registry.clone();let results=results.clone();
                        operations.spawn(async move{
                            let mut controller=state.lock().await;
                            let update=match controller.execute(&backend,&registry,&value).await{Ok(publication)=>Update::Contracts(publication),Err(error)=>Update::SubmissionRejected(value["requestId"].as_str().unwrap_or_default().into(),error)};
                            let _=results.send(update).await;
                        });
                    }
                    Ok(value) if value["view"]=="option-contract" && operations.len()<32 && backend.is_some()=>{
                        let backend=backend.as_ref().unwrap().clone();let state=options_state.clone();let registry=registry.clone();let results=results.clone();
                        operations.spawn(async move{
                            let mut controller=state.lock().await;
                            let update=match controller.execute(&backend,&registry,&value).await{Ok(publication)=>Update::Contracts(publication),Err(error)=>Update::SubmissionRejected(value["requestId"].as_str().unwrap_or_default().into(),error)};
                            let _=results.send(update).await;
                        });
                    }
                    Ok(value) if value["view"]=="provider-definitions" && operations.len()<32 && backend.is_some()=>{
                        let backend=backend.as_ref().unwrap().clone();let state=provider_state.clone();let registry=registry.clone();let results=results.clone();
                        operations.spawn(async move{
                            let mut state=state.lock().await;
                            let update=match state.execute(&backend,&registry,&value).await{Ok(publication)=>Update::Contracts(publication),Err(error)=>Update::SubmissionRejected(value["requestId"].as_str().unwrap_or_default().into(),error)};
                            let _=results.send(update).await;
                        });
                    }
                    Ok(value) if matches!(value["view"].as_str(),Some("observation"|"risk")) && operations.len()<32 && backend.is_some()=>{
                        let backend=backend.as_ref().unwrap().clone();let state=inspection_state.clone();let registry=registry.clone();let results=results.clone();
                        operations.spawn(async move{
                            let mut controller=state.lock().await;
                            let update=match controller.execute(&backend,&registry,&value).await{Ok(publication)=>Update::Inspection(publication),Err(error)=>Update::SubmissionRejected(value["requestId"].as_str().unwrap_or_default().into(),error)};
                            let _=results.send(update).await;
                        });
                    }
                    Ok(value) if value["view"]=="parameter-sets" && operations.len()<32 && backend.is_some()=>{
                        let backend=backend.as_ref().unwrap().clone();let state=parameter_state.clone();let registry=registry.clone();let results=results.clone();
                        operations.spawn(async move{
                            let mut controller=state.lock().await;
                            let update=match controller.execute(&backend,&registry,&value).await{Ok(publication)=>Update::Contracts(publication),Err(error)=>Update::SubmissionRejected(value["requestId"].as_str().unwrap_or_default().into(),error)};
                            let _=results.send(update).await;
                        });
                    }
                    Ok(value) if value["view"]=="database-backup" && operations.len()<32 && backend.is_some()=>{
                        let backend=backend.as_ref().unwrap().clone();let state=backup_state.clone();let registry=registry.clone();let results=results.clone();
                        operations.spawn(async move{
                            let mut controller=state.lock().await;
                            let update=match controller.execute(&backend,&registry,&value).await{Ok(publication)=>Update::Contracts(publication),Err(error)=>Update::SubmissionRejected(value["requestId"].as_str().unwrap_or_default().into(),error)};
                            if let Some(mut events)=controller.take_subscription(){
                                let weak=std::sync::Arc::downgrade(&state);let backend=backend.clone();let registry=registry.clone();let results=results.clone();
                                controller.set_observer(tokio::spawn(async move{
                                    use futures_util::{StreamExt,FutureExt};let mut connection=backend.watch_connection();
                                    loop {
                                        let refresh=tokio::select!{event=events.next()=>{if event.is_none(){break;}true},changed=connection.changed()=>{if changed.is_err(){break;}connection.borrow_and_update().connected}};
                                        if !refresh{continue;}
                                        for _ in 0..256{if events.next().now_or_never().flatten().is_none(){break;}}
                                        let Some(state)=weak.upgrade()else{break;};
                                        if let Some(publication)=state.lock().await.refresh_if_loaded(&backend,&registry).await{if results.send(Update::Contracts(publication)).await.is_err(){break;}}
                                    }
                                    let _=results.send(Update::Warning("Backup notifications stopped; reload the view to reattach.".into())).await;
                                }));
                            }
                            let _=results.send(update).await;
                        });
                    }
                    Ok(value) if value["view"]=="trade-orders" && operations.len()<32 && backend.is_some()=>{
                        let backend=backend.as_ref().unwrap().clone();let state=trade_state.clone();let registry=registry.clone();let results=results.clone();
                        operations.spawn(async move{
                            let mut controller=state.lock().await;
                            let update=match controller.execute(&backend,&registry,&value).await{Ok(publication)=>Update::Trades(publication),Err(error)=>Update::SubmissionRejected(value["requestId"].as_str().unwrap_or_default().into(),error)};
                            let _=results.send(update).await;
                        });
                    }
                    Ok(value) if value["view"]=="portfolio" && operations.len()<32 && backend.is_some()=>{
                        let backend=backend.as_ref().unwrap().clone();let state=portfolio_state.clone();let registry=registry.clone();let results=results.clone();
                        operations.spawn(async move{
                            // The owner serializes drafts and authoritative reads; the renderer
                            // only receives complete documents and never holds API resources.
                            let mut controller=state.lock().await;
                            let update=match controller.execute(&backend,&registry,&value).await{Ok(publication)=>Update::Contracts(publication),Err(error)=>Update::SubmissionRejected(value["requestId"].as_str().unwrap_or_default().into(),error)};
                            let _=results.send(update).await;
                        });
                    }
                    Ok(value) if value["view"]=="scheduled-tasks" && operations.len()<32 && backend.is_some()=>{
                        let backend=backend.as_ref().unwrap().clone();let state=scheduled_state.clone();let registry=registry.clone();let results=results.clone();
                        operations.spawn(async move{
                            let mut controller=state.lock().await;
                            let update=match controller.execute(&backend,&registry,&value).await{Ok(publication)=>Update::Contracts(publication),Err(error)=>Update::SubmissionRejected(value["requestId"].as_str().unwrap_or_default().into(),error)};
                            if let Some(mut events)=controller.take_subscription(){
                                let weak=std::sync::Arc::downgrade(&state);let backend=backend.clone();let registry=registry.clone();let results=results.clone();
                                controller.set_observer(tokio::spawn(async move{
                                    use futures_util::{StreamExt,FutureExt};
                                    let mut connection=backend.watch_connection();
                                    loop{
                                        let refresh=tokio::select!{
                                            event=events.next()=>match event{Some(event)=>{let verb=event.subject.splitn(4,'.').nth(2).unwrap_or_default();verb.ends_with("Complete")||verb.ends_with("Fail")},None=>break},
                                            changed=connection.changed()=>{if changed.is_err(){break;}connection.borrow_and_update().connected},
                                        };
                                        if !refresh{continue;}
                                        // Coalesce a bounded burst of admin notifications. This
                                        // observer performs reads only and never executes a job.
                                        for _ in 0..256{if events.next().now_or_never().flatten().is_none(){break;}}
                                        let Some(state)=weak.upgrade()else{break;};
                                        if let Some(publication)=state.lock().await.refresh_if_loaded(&backend,&registry).await{if results.send(Update::Contracts(publication)).await.is_err(){break;}}
                                    }
                                    if let Some(state)=weak.upgrade(){state.lock().await.observer_unavailable();let _=results.send(Update::Warning("Scheduled-task notifications stopped; reload the view to reattach.".into())).await;}
                                }));
                            }
                            let _=results.send(update).await;
                        });
                    }
                    Ok(value) if value["view"]=="yield-curve" && operations.len()<32 && backend.is_some()=>{
                        let backend=backend.as_ref().unwrap().clone();let state=yield_state.clone();let registry=registry.clone();let results=results.clone();
                        operations.spawn(async move{
                            let mut state=state.lock().await;
                            let update=match state.execute(&backend,&registry,&value).await{Ok(publication)=>Update::Contracts(publication),Err(error)=>Update::SubmissionRejected(value["requestId"].as_str().unwrap_or_default().into(),error)};
                            let _=results.send(update).await;
                        });
                    }
                    Ok(value) if value["view"] == "actor-health" && operations.len() < 32 && backend.is_some() => {
                        let backend=backend.as_ref().unwrap().clone();let state=actor_health_state.clone();let registry=registry.clone();let results=results.clone();
                        operations.spawn(async move {
                            let mut state=state.lock().await;
                            let result=async {
                                if state.is_none(){*state=Some(ifm_ui_egui::state::actor_health::ActorHealthController::from_env()?);}
                                state.as_mut().unwrap().execute(&backend,&registry,&value).await
                            }.await;
                            let update=match result{Ok(publication)=>Update::Contracts(publication),Err(error)=>Update::SubmissionRejected(value["requestId"].as_str().unwrap_or_default().into(),error)};
                            let _=results.send(update).await;
                        });
                    }
                    Ok(value) if value["view"] == "operations-health" && operations.len() < 32 && backend.is_some() => {
                        let backend=backend.as_ref().unwrap().clone();let state=health_state.clone();let results=results.clone();
                        operations.spawn(async move {
                            let mut state=state.lock().await;
                            let initialized=if state.is_none(){match ifm_ui_egui::state::operations_health::OperationsHealthController::from_env(){Ok(controller)=>{*state=Some(controller);Ok(())},Err(error)=>Err(error)}}else{Ok(())};
                            let result=match initialized {Ok(())=>state.as_mut().unwrap().execute(&backend,&value).await,Err(error)=>Err(error)};
                            let update=match result{Ok(publication)=>Update::Contracts(publication),Err(error)=>Update::SubmissionRejected(value["requestId"].as_str().unwrap_or_default().into(),error)};
                            let _=results.send(update).await;
                        });
                    }
                    Ok(value) if value["view"] == "strategy-catalog" && operations.len() < 32 && backend.is_some() => {
                        let backend = backend.as_ref().unwrap().clone();
                        let registry = registry.clone();
                        let state = catalog_state.clone();
                        let results = results.clone();
                        operations.spawn(async move {
                            let mut state = state.lock().await;
                            let result = state.execute(&backend, &registry, &value).await;
                            let publication = match result {
                                Ok(publication) => Ok(publication),
                                Err(error) => state.failed(value["requestId"].as_str().unwrap_or_default().into(), &error),
                            };
                            let update = match publication { Ok(p) => Update::Contracts(p), Err(error) => Update::Warning(error) };
                            let _ = results.send(update).await;
                        });
                    }
                    Ok(value) if value["view"] == "lookup" && operations.len() < 32 && backend.is_some() => {
                        let backend = backend.as_ref().unwrap().clone();
                        let registry = registry.clone();
                        let state = lookup_state.clone();
                        let results = results.clone();
                        operations.spawn(async move {
                            let id = value["requestId"].as_str().unwrap_or_default().to_string();
                            let mut state = state.lock().await;
                            let update = match state.execute(&backend,&registry,&value).await {
                                Ok(publication) => Update::Editor(publication),
                                Err(error) => Update::SubmissionRejected(id,error),
                            };
                            let _ = results.send(update).await;
                        });
                    }
                    Ok(value) => pending.push(Update::SubmissionRejected(
                        value["requestId"].as_str().unwrap_or_default().into(),
                        format!("Native controller is not yet available for {} / {}", value["view"].as_str().unwrap_or("unknown"), value["intent"].as_str().unwrap_or("unknown")),
                    )),
                    Err(error) => pending.push(Update::Warning(format!("Invalid UI intent: {error}"))),
                }
            }
            while pending.len() < 64 {
                if let Some(sidebar) = sidebar_updates.as_mut().and_then(|receiver| receiver.try_recv().ok()) {
                    pending.retain(|old|!matches!(old,Update::Sidebar(_)));
                    pending.push(Update::Sidebar(sidebar));
                    continue;
                }
                let Ok(update) = received.try_recv() else { break; };
                // Monitor publications contain complete retained state. Keep the
                // newest one when the GUI is behind; command receipts stay ordered.
                if matches!(&update,Update::TradeMonitor(_)){pending.retain(|old|!matches!(old,Update::TradeMonitor(_)));}
                pending.push(update);
            }
            let mut delivered = false;
            while !pending.is_empty() {
                // Audit on the service thread, outside paint callbacks. A full
                // UI channel retries delivery without repeatedly logging it.
                let audit = match &pending[0] {
                    Update::Error(error) => Some((None, error.clone())),
                    Update::Warning(error) => { ifm_ui_egui::diagnostics::errors::record("ui.warning_handed_to_ui",None,None,error); None },
                    Update::SubmissionRejected(id,error) => Some((Some(id.clone()),error.clone())),
                    _ => None,
                };
                match updates.try_send(pending.remove(0)) {
                    Ok(()) => {
                        if let Some((operation,error))=audit {
                            ifm_ui_egui::diagnostics::errors::record("ui.error_handed_to_ui",None,operation.as_deref(),&error);
                        }
                        delivered = true;
                    },
                    Err(mpsc::TrySendError::Full(update)) => { pending.insert(0, update); break; }
                    Err(mpsc::TrySendError::Disconnected(_)) => {
                        if let Some(task) = startup.take() { task.abort(); }
                        break 'running;
                    }
                }
            }
            if delivered { context.request_repaint(); }
        }
        if let Some(task) = startup { task.abort(); }
        operations.abort_all();
        if let Some(client)=backend.as_ref(){
            let deadline=tokio::time::Instant::now()+Duration::from_secs(1);
            let _=tokio::time::timeout_at(deadline,async{
                while operations.join_next().await.is_some(){}
                trade_state.lock().await.release_preview(client,&registry,deadline).await
            }).await;
        }
    });
    runtime.shutdown_timeout(Duration::from_secs(1));
    ifm_ui_egui::diagnostics::writer::shutdown();
}

#[cfg(test)]
mod tests {
    use super::*;


    #[tokio::test]
    async fn market_view_polls_overnight_and_pauses_only_on_close_or_unknown_session() {
        let date = chrono::NaiveDate::from_ymd_opt(2026, 11, 6).unwrap();
        let context = |market_state| BusinessContext {
            value_date: date,
            active_value_date: (market_state != 0).then_some(date),
            as_of_utc: "2026-11-06T21:59:00Z".parse().unwrap(),
            revision: 1,
            market_state,
            allow_development_emulator: false,
        };
        let (session, receiver) = tokio::sync::watch::channel(Some(context(2)));
        let (refresh, mut refreshed) = tokio::sync::mpsc::unbounded_channel();
        let task = tokio::spawn(poll_market_view(
            receiver,
            Duration::from_millis(20),
            move |context| {
                refresh.send(context.map(|session| session.market_state)).unwrap();
                std::future::ready(())
            },
        ));
        async fn next(receiver: &mut tokio::sync::mpsc::UnboundedReceiver<Option<i64>>) -> Option<i64> {
            tokio::time::timeout(Duration::from_secs(2), receiver.recv())
                .await.unwrap().unwrap()
        }
        assert_eq!(next(&mut refreshed).await, Some(2));
        assert_eq!(next(&mut refreshed).await, Some(2));

        session.send_replace(Some(context(0)));
        assert_eq!(next(&mut refreshed).await, Some(0));
        assert!(tokio::time::timeout(Duration::from_millis(80), refreshed.recv()).await.is_err());
        session.send_replace(Some(context(1)));
        assert_eq!(next(&mut refreshed).await, Some(1));
        assert_eq!(next(&mut refreshed).await, Some(1));

        session.send_replace(Some(context(2)));
        assert_eq!(next(&mut refreshed).await, Some(2));
        session.send_replace(None);
        assert_eq!(next(&mut refreshed).await, None);
        assert!(tokio::time::timeout(Duration::from_millis(80), refreshed.recv()).await.is_err());
        drop(session);
        task.await.unwrap();
    }


    /// A reconnect can skip the closed hour; a new session must cancel the old in-flight read.
    #[tokio::test]
    async fn market_view_cancels_a_previous_date_read_even_without_observing_close() {
        let date = chrono::NaiveDate::from_ymd_opt(2026, 11, 9).unwrap();
        let initial = BusinessContext { value_date: date, active_value_date: Some(date),
            as_of_utc: "2026-11-09T21:59:00Z".parse().unwrap(), revision: 1,
            market_state: 1, allow_development_emulator: false };
        let (session, receiver) = tokio::sync::watch::channel(Some(initial.clone()));
        let started = std::sync::Arc::new(tokio::sync::Notify::new());
        let release = std::sync::Arc::new(tokio::sync::Notify::new());
        let (published, mut publications) = tokio::sync::mpsc::unbounded_channel();
        let entered = started.notified();
        let task = tokio::spawn(poll_market_view(receiver, Duration::from_millis(20), {
            let started = started.clone(); let release = release.clone();
            move |context| {
                let started = started.clone(); let release = release.clone(); let published = published.clone();
                async move {
                    let Some(context) = context else { return; };
                    if context.value_date == date { started.notify_one(); release.notified().await; }
                    published.send(context.value_date).unwrap();
                }
            }
        }));
        tokio::time::timeout(Duration::from_secs(2), entered).await.unwrap();
        let next = date.succ_opt().unwrap();
        session.send_replace(Some(BusinessContext { value_date: next, active_value_date: Some(next), ..initial }));
        assert_eq!(tokio::time::timeout(Duration::from_secs(2), publications.recv()).await.unwrap(), Some(next));
        release.notify_waiters();
        for _ in 0..3 {
            assert_eq!(tokio::time::timeout(Duration::from_secs(2), publications.recv()).await.unwrap(), Some(next));
        }
        drop(session);
        task.await.unwrap();
    }

    /// Verifies that shutdown does not wait for space in a full action or publication queue.
    #[test]
    fn shutdown_does_not_wait_for_space_in_a_full_action_or_publication_queue() {
        let (updates, _received) = mpsc::sync_channel(4);
        for _ in 0..4 {
            updates.send(Update::Error("occupied".into())).unwrap();
        }
        let (actions, action_receiver) = mpsc::sync_channel(32);
        for _ in 0..32 {
            actions.send(1).unwrap();
        }
        let (_intents, intent_receiver) = mpsc::sync_channel(32);
        let stopping = std::sync::Arc::new(std::sync::atomic::AtomicBool::new(true));
        let started = std::time::Instant::now();
        run(
            updates,
            action_receiver,
            intent_receiver,
            egui::Context::default(),
            stopping,
            std::sync::Arc::new(tokio::sync::Notify::new()),
        );
        assert!(started.elapsed() < Duration::from_secs(2));
    }
}
