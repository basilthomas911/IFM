//! Explicit read-only live diagnostics; never submits a trade or changes feed intent.
#[allow(dead_code)]
#[path = "support/monitor_display_baseline.rs"]
mod monitor_display;
use ifm_ui_egui::{
    backend::{
        schema::Registry,
        transport::{Backend, Settings},
    },
    services::{
        business_context::BusinessContext,
        trade_monitor_queries::{self, Scope},
    },
};
use std::time::Duration;

#[tokio::test]
#[ignore = "Explicit live read-only query; set IFM_LIVE_DIAGNOSTIC_NATS and IFM_LIVE_DIAGNOSTIC_TRADE"]
async fn inspect_current_trade_plan_and_saved_option_observations() {
    let url = std::env::var("IFM_LIVE_DIAGNOSTIC_NATS").unwrap();
    let ids = std::env::var("IFM_LIVE_DIAGNOSTIC_TRADE")
        .unwrap()
        .split('.')
        .map(|s| s.parse::<i32>().unwrap())
        .collect::<Vec<_>>();
    assert_eq!(ids.len(), 4);
    let scope = Scope {
        portfolio: ids[0],
        fund: ids[1],
        order: ids[2],
        trade: ids[3],
    };
    let backend = Backend::connect(Settings {
        url,
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(30),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let context = BusinessContext::load(&backend).await.unwrap();
    for sample in 0..2 {
        let deadline = tokio::time::Instant::now() + Duration::from_secs(30);
        let plan = trade_monitor_queries::current_plan(
            &backend,
            &registry,
            scope,
            context.value_date,
            deadline,
        )
        .await
        .unwrap();
        let realtime = trade_monitor_queries::realtime(
            &backend,
            &registry,
            scope,
            context.value_date,
            deadline,
        )
        .await
        .unwrap();
        let history = trade_monitor_queries::plan_page(
            &backend,
            &registry,
            scope,
            context.value_date,
            0,
            2,
            None,
            deadline,
        )
        .await
        .unwrap();
        println!(
            "DIAGNOSTIC {}",
            serde_json::json!({"sample":sample,"valueDate":context.value_date.to_string(),"plan":plan,"realtime":realtime,"latestHistory":history})
        );
        if let Some(legs) = plan
            .as_ref()
            .and_then(|plan| plan.position.as_ref())
            .and_then(|position| position.legs.as_ref())
        {
            for leg in legs {
                if let Some(contract) = leg.contract_id.as_deref() {
                    let quote = trade_monitor_queries::saved_leg(
                        &backend,
                        &registry,
                        contract,
                        context.value_date,
                        deadline,
                    )
                    .await;
                    println!(
                        "OPTION {}",
                        serde_json::json!({"contract":contract,"observation":quote})
                    );
                }
            }
        }
        if sample == 0 {
            tokio::time::sleep(Duration::from_secs(4)).await;
        }
    }
}

/// Explicit acceptance uses the same per-trade API as the UI, restoring saved
/// intent even if the stop observation fails. No order command is reachable.
#[tokio::test]
#[ignore = "Explicit live start/stop acceptance; requires IFM_LIVE_ACCEPTANCE=stop-start"]
async fn live_trade_feed_stop_start_acceptance() {
    use ifm_ui_egui::services::trade_feed_recovery::{Action, Client};
    assert_eq!(std::env::var("IFM_LIVE_ACCEPTANCE").unwrap(), "stop-start");
    let ids = std::env::var("IFM_LIVE_DIAGNOSTIC_TRADE")
        .unwrap()
        .split('.')
        .map(|s| s.parse::<i32>().unwrap())
        .collect::<Vec<_>>();
    assert_eq!(ids.len(), 4);
    let scope = Scope {
        portfolio: ids[0],
        fund: ids[1],
        order: ids[2],
        trade: ids[3],
    };
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_LIVE_DIAGNOSTIC_NATS").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(120),
    })
    .await
    .unwrap();
    let feed = Client::new(backend.clone(), "development").unwrap();
    let context = BusinessContext::load(&backend).await.unwrap();
    let deadline = || tokio::time::Instant::now() + Duration::from_secs(120);
    assert!(
        feed.invoke(
            Action::Load,
            scope,
            &context,
            uuid::Uuid::new_v4(),
            deadline()
        )
        .await
        .unwrap(),
        "Saved live intent must be enabled before this test"
    );
    let disabled = feed
        .invoke(
            Action::Disable,
            scope,
            &context,
            uuid::Uuid::new_v4(),
            deadline(),
        )
        .await;
    let stopped = feed
        .invoke(
            Action::Load,
            scope,
            &context,
            uuid::Uuid::new_v4(),
            deadline(),
        )
        .await;
    println!("STOP_ACCEPTANCE disabled={disabled:?} saved_intent={stopped:?}");
    // Always restore the original enabled state before asserting stop evidence.
    let context = BusinessContext::load(&backend).await.unwrap();
    let restored = feed
        .invoke(
            Action::Enable,
            scope,
            &context,
            uuid::Uuid::new_v4(),
            deadline(),
        )
        .await;
    println!("START_ACCEPTANCE restored={restored:?}");
    assert_eq!(disabled, Ok(true));
    assert_eq!(stopped, Ok(false));
    assert_eq!(restored, Ok(true));
}

/// Passive push observer for a manually authorized stability window. The timer
/// only stops this test; it neither polls production state nor triggers recovery.
#[tokio::test]
#[ignore = "Explicit passive live observer; set IFM_LIVE_OBSERVE_UNTIL RFC3339"]
async fn observe_live_trade_realtime_progress() {
    use futures_util::StreamExt;
    let until =
        chrono::DateTime::parse_from_rfc3339(&std::env::var("IFM_LIVE_OBSERVE_UNTIL").unwrap())
            .unwrap()
            .to_utc();
    let remaining = (until - chrono::Utc::now()).to_std().unwrap();
    assert!(remaining <= Duration::from_secs(1000));
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_LIVE_DIAGNOSTIC_NATS").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(120),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let trade = std::env::var("IFM_LIVE_DIAGNOSTIC_TRADE").unwrap();
    let expected = format!("Notify.TradeRealtimeSnapshot.Changed.{trade}");
    let mut events = backend
        .subscribe("Notify", "TradeRealtimeSnapshot")
        .await
        .unwrap();
    backend.flush().await.unwrap();
    let end = tokio::time::sleep(remaining);
    tokio::pin!(end);
    let mut tick = tokio::time::interval(Duration::from_secs(60));
    tick.tick().await;
    let (mut count, mut advances, mut revision, mut plan, mut last_observation) =
        (0u64, 0u64, 0i64, 0i64, serde_json::Value::Null);
    loop {
        tokio::select! {
            _=&mut end=>break,
            _=tick.tick()=>println!("PROGRESS {}",serde_json::json!({"atUtc":chrono::Utc::now().to_rfc3339(),"events":count,"revisionAdvances":advances,"snapshotRevision":revision,"planRevision":plan,"lastMarketObservationUtc":last_observation})),
            message=events.next()=>{
                let message=message.expect("Live realtime subscription ended");if message.subject.as_str()!=expected {continue;}
                let wire=ifm_ui_egui::backend::codec::decode(&message.payload).unwrap();
                let value=monitor_display::named(&registry,"TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshotChangedEvent",&wire).unwrap();
                let snapshot=&value["Snapshot"];let next=snapshot["SnapshotRevision"].as_i64().unwrap();
                count+=1;if next>revision {advances+=1;} revision=next;
                plan=snapshot["Plan"]["PlanRevision"].as_i64().unwrap_or(plan);
                last_observation=snapshot["Position"]["LastMarketObservationUtc"].clone();
            }
        }
    }
    println!(
        "FINAL_PROGRESS {}",
        serde_json::json!({"atUtc":chrono::Utc::now().to_rfc3339(),"events":count,"revisionAdvances":advances,"snapshotRevision":revision,"planRevision":plan,"lastMarketObservationUtc":last_observation})
    );
    assert!(
        count > 1 && advances > 1,
        "No advancing realtime trade publications observed"
    );
}
