//! Native API control can start/stop without option definitions or a market
//! connection. Copied health and terminal evidence never pin runtime resources.
use ifm_market_data_native::{
    api::owner::MarketData,
    dataset::transport::Lane,
    runtime::{Deadline, clock::Clock, owner::Owner, recovery::Phase},
};
use std::{sync::Arc, time::Duration};
use tokio_util::sync::CancellationToken;
#[tokio::test]
async fn future_only_idle_owner_needs_no_option_reference_preparation_and_retains_failed_notifications_after_stop()
 {
    let root = Arc::new(Owner::default());
    let market =
        MarketData::new(root.clone(), Arc::new(Clock::new(0, false).unwrap()), None).unwrap();
    assert!(market.option_claims().is_none());
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf, Lane::OptionsGlbx] {
        let health = market.health(lane);
        let mut changes = health.subscribe();
        tokio::time::timeout(Duration::from_secs(1), async {
            while health.status().phase != Phase::Running {
                changes.changed().await.unwrap();
            }
        })
        .await
        .unwrap();
    }
    let snapshot = market.read_snapshot(true).unwrap();
    let parts = snapshot.as_array().unwrap();
    assert_eq!(parts.len(), 8);
    for field in [1, 2, 3, 4, 5, 6] {
        assert!(parts[field].as_array().unwrap().is_empty());
    }
    assert_eq!(parts[7].as_array().unwrap()[2].as_bool(), Some(false));
    let initial = market.backend_status(true).unwrap();
    let fields = initial.as_array().unwrap();
    assert_eq!(fields.len(), 10);
    assert!(
        fields[9].is_nil(),
        "Audit diagnostics remain absent until configured"
    );
    assert_eq!(fields[0].as_bool(), Some(true));
    assert!(fields[1].is_nil());
    assert_eq!(fields[8].as_i64(), Some(1));
    assert_ne!(
        fields[3].as_str(),
        Some("00000000-0000-0000-0000-000000000000")
    );
    assert_eq!(initial, market.backend_status(true).unwrap());
    for row in fields[2].as_array().unwrap() {
        let row = row.as_array().unwrap();
        assert_eq!(row.len(), 14);
        assert_eq!(row[4].as_u64(), Some(u64::from(std::process::id())));
        assert_eq!(row[5].as_bool(), Some(false));
        assert_eq!(row[6].as_bool(), Some(true));
        assert_eq!(row[7].as_str(), Some("Idle"));
        assert!(row[9].is_nil());
        assert!(row[12].as_array().is_some());
    }
    let disconnected = market.backend_status(false).unwrap();
    assert!(disconnected.as_array().unwrap()[4].as_i64() > fields[4].as_i64());
    assert!(market.futures_current(Lane::FuturesGlbx).is_none());
    assert!(market.futures_current(Lane::FuturesXcbf).is_none());
    assert!(market.options_current().is_none());
    assert!(market.stop_futures(Lane::OptionsGlbx).is_err());
    market
        .report_fault(
            Lane::OptionsGlbx,
            "original native failure",
            Deadline::after(Duration::ZERO),
        )
        .unwrap();
    assert_eq!(
        market.health(Lane::OptionsGlbx).status().phase,
        Phase::Failed
    );
    let failed = market.backend_status(true).unwrap();
    let rows = failed.as_array().unwrap()[2].as_array().unwrap();
    assert_eq!(
        rows[2].as_array().unwrap()[7].as_str(),
        Some("StoppedError")
    );
    assert_eq!(rows[2].as_array().unwrap()[6].as_bool(), Some(false));
    assert_eq!(rows[0].as_array().unwrap()[7].as_str(), Some("Idle"));
    assert_eq!(market.failures().len(), 1);
    assert_eq!(
        market.health(Lane::FuturesGlbx).status().phase,
        Phase::Running
    );
    market.shutdown(Deadline::recovery()).await.unwrap();
    assert_eq!(
        market.failures().len(),
        1,
        "stop cannot hide a failure awaiting UI delivery"
    );
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
}

#[tokio::test(start_paused = true)]
async fn authoritative_empty_discovery_is_bounded_one_shot_and_never_starts_or_replaces_a_dataset()
{
    use ifm_market_data_native::dataset::background::ScopeDemand;
    let root = Arc::new(Owner::default());
    let market =
        MarketData::new(root.clone(), Arc::new(Clock::new(0, false).unwrap()), None).unwrap();
    market.wait_ready(Lane::OptionsGlbx, false).await.unwrap();
    let original = market.health(Lane::OptionsGlbx).status().ticket;
    let date = time::Date::from_calendar_date(1970, time::Month::January, 1).unwrap();
    let demand = |owner| {
        ScopeDemand::from_command(&serde_json::json!({"owner":[owner,0],"maturity":719164,"complete":true,"separate":false,"independent":false,"candidates":[]})).unwrap()
    };
    for owner in 1..=64 {
        assert!(market.accept_empty_discovery(&demand(owner), date).unwrap());
    }
    assert!(market.accept_empty_discovery(&demand(65), date).is_err());
    assert_eq!(market.health(Lane::OptionsGlbx).status().ticket, original);
    assert!(market.option_claims().is_none());
    assert!(market.options_current().is_none());
    assert_eq!(
        market
            .discovery_scope_result(1u128.to_le_bytes())
            .unwrap()
            .as_array()
            .unwrap()[2]
            .as_bool(),
        Some(true)
    );
    market
        .complete_rejected_discovery(1u128.to_le_bytes())
        .unwrap();
    assert!(
        !market
            .discovery_scope_result(1u128.to_le_bytes())
            .unwrap()
            .as_array()
            .unwrap()[2]
            .as_bool()
            .unwrap()
    );
    assert!(market.accept_empty_discovery(&demand(65), date).unwrap());
    tokio::time::advance(Duration::from_secs(60)).await;
    assert!(market.accept_empty_discovery(&demand(2), date).unwrap());
    tokio::time::advance(Duration::from_secs(60)).await;
    assert!(
        !market
            .discovery_scope_result(2u128.to_le_bytes())
            .unwrap()
            .as_array()
            .unwrap()[2]
            .as_bool()
            .unwrap(),
        "Duplicate commands must not extend copied-result retention"
    );
    assert!(market.accept_empty_discovery(&demand(100), date).unwrap());
    market.stop_feeds().unwrap();
    assert!(
        !market
            .discovery_scope_result(100u128.to_le_bytes())
            .unwrap()
            .as_array()
            .unwrap()[2]
            .as_bool()
            .unwrap()
    );
    market.shutdown(Deadline::recovery()).await.unwrap();
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
}
