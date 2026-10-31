#[allow(dead_code)]
#[path = "support/monitor_display_baseline.rs"]
mod monitor_display;
use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    services::trade_monitor_queries::Scope,
    state::monitor_cache::{History, Realtime},
    wire,
};
use serde_json::{Value as Json, json};
fn typed<T: serde::de::DeserializeOwned>(value: Json) -> T {
    serde_json::from_value(value).unwrap()
}
fn load(name: &str, kind: &str) -> Json {
    let r = Registry::compiled().unwrap();
    let root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend");
    monitor_display::named(
        &r,
        kind,
        &codec::decode(&std::fs::read(root.join(format!("{name}.msgpack"))).unwrap()).unwrap(),
    )
    .unwrap()
}
#[test]
fn retained_position_history_matches_hybrid_and_sealed_eod_survives_later_quotes() {
    let trade = load(
        "monitor-trade",
        "TomasAI.IFM.Domain.Trade.Shared.EstablishedTradeDefinition",
    );
    let positions = load(
        "monitor-positions",
        "TomasAI.IFM.Domain.Trade.Shared.StrategyPositionSnapshot[]",
    );
    let mut history = History::new(typed(trade), "ShortIronCondor".into());
    for position in positions.as_array().unwrap() {
        history.remember(typed(position.clone()));
    }
    let expected: Json = wire::decode(
        &std::fs::read(
            std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("tests/fixtures/backend/monitor-position-rows.msgpack"),
        )
        .unwrap(),
    )
    .unwrap();
    assert_eq!(serde_json::to_value(history.rows()).unwrap(), expected);
    assert_eq!(
        serde_json::to_value(history.rows()).unwrap()[1]["tradeStatus"],
        "EndOfDay"
    );
    assert_eq!(
        serde_json::to_value(history.rows()).unwrap()[1]["tradePnl"],
        "-50"
    );
    let before = history.rows();
    assert!(!history.remember(typed(positions[2].clone())));
    assert_eq!(history.rows(), before);
    assert_eq!(
        serde_json::to_value(history.saved(1).unwrap()).unwrap()["phase"],
        "EndOfDay"
    );
    assert_eq!(
        serde_json::to_value(history.saved(2).unwrap()).unwrap()["phase"],
        "Close"
    );
}
#[test]
fn realtime_identity_and_generation_fences_prevent_late_query_overwrites() {
    let trade = load(
        "monitor-trade",
        "TomasAI.IFM.Domain.Trade.Shared.EstablishedTradeDefinition",
    );
    let snapshot = load(
        "monitor-realtime",
        "TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshot",
    );
    let date = chrono::NaiveDate::from_ymd_opt(2026, 10, 26).unwrap();
    let scope = Scope {
        portfolio: 1,
        fund: 2,
        order: 701,
        trade: 456,
    };
    let mut mirror = Realtime::new(scope, date, &typed(trade)).unwrap();
    assert!(mirror.accept(typed(snapshot.clone())));
    assert!(!mirror.accept(typed(snapshot.clone())));
    let mut wrong = snapshot.clone();
    wrong["TradeId"]["FundId"] = 3.into();
    wrong["SnapshotRevision"] = 11.into();
    assert!(!mirror.accept(typed(wrong)));
    let mut wrong = snapshot.clone();
    wrong["Legs"][1] = wrong["Legs"][0].clone();
    wrong["SnapshotRevision"] = 11.into();
    assert!(!mirror.accept(typed(wrong)));
    let mut newer = snapshot.clone();
    newer["MonitoringGeneration"] = uuid::Uuid::new_v4().to_string().into();
    newer["SnapshotRevision"] = 1.into();
    newer["PublishedAtUtc"] = "2026-10-26T12:31:00+00:00".into();
    assert!(mirror.accept(typed(newer.clone())));
    let mut late = snapshot;
    late["SnapshotRevision"] = 99.into();
    late["PublishedAtUtc"] = "2026-10-26T12:32:00+00:00".into();
    assert!(!mirror.accept(typed(late)));
    assert_eq!(mirror.current.unwrap(), typed(newer));
    let _ = json!({});
}

#[test]
fn monitor_documents_match_hybrid_computed_getters_and_compact_plan_shape() {
    use crate::monitor_display;
    for (input, kind, expected, history) in [
        (
            "monitor-trade",
            "TomasAI.IFM.Domain.Trade.Shared.EstablishedTradeDefinition",
            "monitor-established-display",
            false,
        ),
        (
            "monitor-realtime",
            "TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshot",
            "monitor-realtime-display",
            false,
        ),
        (
            "monitor-plan",
            "TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanSnapshot",
            "monitor-plan-history-display",
            true,
        ),
    ] {
        let value = load(input, kind);
        let expected: Json = wire::decode(
            &std::fs::read(
                std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                    .join(format!("tests/fixtures/backend/{expected}.msgpack")),
            )
            .unwrap(),
        )
        .unwrap();
        assert_eq!(
            if history {
                monitor_display::plan_history(&value)
            } else {
                monitor_display::document(&value)
            },
            expected,
            "{input}"
        );
    }
}
