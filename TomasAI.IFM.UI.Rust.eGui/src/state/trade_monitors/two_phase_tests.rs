//! Stored financial data remains visible while a live source is being restored.
use super::*;


/// Provides typed for the two phase tests implementation.
fn typed<T: serde::de::DeserializeOwned>(value: Json) -> T {
    serde_json::from_value(value).unwrap()
}


/// Builds the deterministic input used by this test or measurement.
fn fixture(name: &str, kind: &str) -> Json {
    let registry = Registry::compiled().unwrap();
    let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
        .join(format!("tests/fixtures/backend/{name}.msgpack"));
    crate::services::parameters::document(
        &registry,
        kind,
        &crate::backend::codec::decode(&std::fs::read(path).unwrap()).unwrap(),
        true,
    )
    .unwrap()
}


/// Provides stored for the two phase tests implementation.
fn stored() -> data::Monitor {
    let scope = api::Scope {
        portfolio: 1,
        fund: 2,
        order: 701,
        trade: 456,
    };
    let date = chrono::NaiveDate::from_ymd_opt(2026, 10, 26).unwrap();
    let context = BusinessContext {
        value_date: date,
        active_value_date: Some(date),
        as_of_utc: chrono::DateTime::parse_from_rfc3339("2026-10-26T12:30:00Z")
            .unwrap()
            .to_utc(),
        revision: 1,
        market_state: 2,
        allow_development_emulator: false,
    };
    let request = OpenRequest {
        scope,
        strategy: "IronCondor".into(),
        trade_type: "ShortIronCondor".into(),
        trade_action: "Sell".into(),
        fund_name: "Fixture".into(),
    };
    let mut state = data::Monitor::new(
        request,
        typed(fixture(
            "monitor-trade",
            "TomasAI.IFM.Domain.Trade.Shared.EstablishedTradeDefinition",
        )),
        context,
    )
    .unwrap();
    let positions = fixture(
        "monitor-positions",
        "TomasAI.IFM.Domain.Trade.Shared.StrategyPositionSnapshot[]",
    );
    state.selected = Some(typed(positions[1].clone()));
    state.page = Some(typed(
        json!({"Items":[fixture("monitor-plan","TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanSnapshot")],"MaximumRevision":7,"Offset":0,"TotalCount":1}),
    ));
    state.set_chart(Some(crate::models::trade_chart::TradeChartSeries {
        end_utc: "2026-10-26T12:30:00Z".into(),
        ..Default::default()
    }));
    state
}


/// Provides document for the two phase tests implementation.
fn document(state: &data::Monitor, kind: &str) -> Json {
    let mut result = ContractResult::default();
    state.append(&mut result).unwrap();
    crate::wire::decode(
        result
            .rows
            .iter()
            .find(|r| r.fields["kind"] == kind)
            .unwrap()
            .data
            .as_ref()
            .unwrap(),
    )
    .unwrap()
}


/// Verifies that starting and acknowledged live without snapshot keep saved position and plans.
#[test]
fn starting_and_acknowledged_live_without_snapshot_keep_saved_position_and_plans() {
    let mut state = stored();
    let position = document(&state, "position-details");
    let plan = document(&state, "plan");
    let chart = document(&state, "chart");
    state.starting = true;
    state.collecting = true;
    assert_eq!(document(&state, "position-details"), position);
    assert_eq!(document(&state, "plan"), plan);
    state.starting = false;
    state.live = true;
    assert_eq!(document(&state, "position-details"), position);
    assert_eq!(document(&state, "plan"), plan);
    assert_eq!(document(&state, "chart"), chart);
}


/// Verifies that failed live restore keeps stored data and allows retry.
#[test]
fn failed_live_restore_keeps_stored_data_and_allows_retry() {
    let mut state = stored();
    let position = document(&state, "position-details");
    state.status = "Stored trade loaded. Live-feed restoration failed: synthetic timeout".into();
    assert!(state.can_live());
    assert_eq!(document(&state, "position-details"), position);
    assert!(!document(&state, "plan").is_null());
}


/// Verifies that authoritative live snapshot replaces saved fallback.
#[test]
fn authoritative_live_snapshot_replaces_saved_fallback() {
    let mut state = stored();
    let saved = document(&state, "position-details");
    state.live = true;
    assert!(state.snapshot(typed(fixture(
        "monitor-realtime",
        "TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshot"
    ))));
    assert_ne!(document(&state, "position-details"), saved);
    assert!(!document(&state, "realtime").is_null());
}


/// Verifies that unchanged monitor sections share payloads and chart changes invalidate only the
/// chart.
#[test]
fn unchanged_monitor_sections_share_payloads_and_chart_changes_invalidate_only_the_chart() {
    let mut state = stored();
    let mut first = ContractResult::default();
    state.append(&mut first).unwrap();
    let mut second = ContractResult::default();
    state.status = "another status".into();
    state.append(&mut second).unwrap();
    for old in &first.rows {
        let new = second.rows.iter().find(|r| r.id == old.id).unwrap();
        assert!(
            old.data
                .as_ref()
                .unwrap()
                .ptr_eq(new.data.as_ref().unwrap()),
            "{} rebuilt without a data change",
            old.id
        );
    }
    state.set_chart(Some(crate::models::trade_chart::TradeChartSeries {
        start_utc: "2026-10-26T12:00:00Z".into(),
        end_utc: "2026-10-26T13:00:00Z".into(),
        points: vec![],
    }));
    let mut changed = ContractResult::default();
    state.append(&mut changed).unwrap();
    for old in &second.rows {
        let new = changed.rows.iter().find(|r| r.id == old.id).unwrap();
        assert_eq!(
            old.data
                .as_ref()
                .unwrap()
                .ptr_eq(new.data.as_ref().unwrap()),
            old.fields["kind"] != "chart",
            "{} invalidation",
            old.id
        );
    }
}
