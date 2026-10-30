#[path = "support/chart_model_baseline.rs"]
mod baseline;
use ifm_ui_egui::models::trade_chart::{TradeChartRow, TradeChartSeries};
use ifm_ui_egui::{models::trade_chart::PreparedTradeChart, wire};
use serde_json::json;

fn series(seconds: i64, points: &[(i64, &str, Option<&str>)]) -> rmpv::Value {
    use ifm_ui_egui::backend::contracts::timestamp_wire;
    let start = chrono::DateTime::parse_from_rfc3339("2026-10-30T00:00:00Z")
        .unwrap()
        .to_utc();
    rmpv::Value::Array(vec![
        timestamp_wire(start),
        timestamp_wire(start + chrono::Duration::seconds(seconds)),
        rmpv::Value::Array(
            points
                .iter()
                .map(|(second, net, forward)| {
                    rmpv::Value::Array(vec![
                        timestamp_wire(start + chrono::Duration::seconds(*second)),
                        (*net).into(),
                        forward.map(rmpv::Value::from).unwrap_or(rmpv::Value::Nil),
                    ])
                })
                .collect(),
        ),
    ])
}

#[test]
fn backend_typed_series_preserves_exact_decimal_text_and_existing_projection() {
    let registry = ifm_ui_egui::backend::schema::Registry::compiled().unwrap();
    let source = series(
        21_600,
        &[
            (0, "-12.345678901234567890", None),
            (
                21_600,
                "0.0000000000000000000000000001",
                Some("13.456789012345678901"),
            ),
        ],
    );
    let typed = TradeChartSeries::from_backend(&source).unwrap();
    assert_eq!(typed.points[0].net_spread, "-12.345678901234567890");
    assert_eq!(
        typed.points[1].forward_spread.as_deref(),
        Some("13.456789012345678901")
    );
    let named = ifm_ui_egui::services::monitor_display::named(
        &registry,
        "TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanChartSeries",
        &source,
    )
    .unwrap();
    let expected = json!({"chartEndUtc":named["EndUtc"],"points":ifm_ui_egui::services::monitor_display::document(&named["Points"])});
    let wire = wire::encode(&TradeChartRow::from(Some(&typed))).unwrap();
    let actual: serde_json::Value = wire::decode(&wire).unwrap();
    assert_eq!(actual, expected);
    assert_eq!(
        PreparedTradeChart::from_messagepack(&wire).unwrap(),
        baseline::legacy(&wire)
    );
    assert!(TradeChartSeries::from_backend(&series(21_601, &[])).is_err());
    assert!(TradeChartSeries::from_backend(&series(-1, &[])).is_err());
    assert!(TradeChartSeries::from_backend(&series(60, &[(61, "1", None)])).is_err());
    assert!(
        TradeChartSeries::from_backend(&series(60, &[(30, "1", None), (29, "2", None)])).is_err()
    );
    assert!(TradeChartSeries::from_backend(&series(60, &[(0, "invalid", None)])).is_err());
}

#[test]
fn typed_backend_slots_match_the_exported_csharp_contracts() {
    let registry = ifm_ui_egui::backend::schema::Registry::compiled().unwrap();
    for (name, expected) in [
        (
            "StrategyTradePlanChartSeries",
            ["StartUtc", "EndUtc", "Points"],
        ),
        (
            "StrategyTradePlanChartPoint",
            ["AtUtc", "NetSpread", "ForwardSpread"],
        ),
    ] {
        let shape = registry
            .describe(&format!(
                "TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.{name}"
            ))
            .unwrap();
        assert_eq!(shape.members.len(), expected.len());
        for (index, expected) in expected.iter().enumerate() {
            assert!(
                shape
                    .members
                    .iter()
                    .any(|(name, slot, _)| name == expected && *slot == index)
            );
        }
    }
}

#[test]
fn backend_chart_capacity_boundary_is_enforced() {
    let point = (0, "1", None);
    assert_eq!(
        TradeChartSeries::from_backend(&series(60, &vec![point; 100_000]))
            .unwrap()
            .points
            .len(),
        100_000
    );
    assert!(TradeChartSeries::from_backend(&series(60, &vec![point; 100_001])).is_err());
}

#[test]
fn direct_typed_decode_matches_previous_plot_coordinates() {
    for count in [0, 1, 100, 10_000] {
        let bytes = baseline::input(count);
        assert_eq!(
            PreparedTradeChart::from_messagepack(&bytes).unwrap(),
            baseline::legacy(&bytes)
        );
    }
}

#[test]
fn optional_and_invalid_cells_preserve_existing_skip_behavior() {
    let bytes = wire::encode_value(
        &json!({"chartEndUtc":"2026-12-01T09:30:00-05:00", "unknownExtension":"ignored", "points":[
            {"atUtc":"2026-12-01T14:30:00.1234567Z", "netSpread":12.5, "forwardSpread":null},
            {"atUtc":"2026-12-01T09:31:00-05:00", "netSpread":"-12.50", "forwardSpread":"13.25"},
            {"atUtc":"invalid", "netSpread":1},
            {"atUtc":null, "netSpread":1},
            {"atUtc":false, "netSpread":1},
            {"atUtc":"2026-12-01T14:32:00Z", "netSpread":"invalid"},
            {"atUtc":"2026-12-01T14:32:00Z", "netSpread":true},
            {"atUtc":"2026-12-01T14:32:00Z", "netSpread":{}, "forwardSpread":1},
            null, false, "invalid point", {}
        ]}),
    )
    .unwrap();
    let typed = PreparedTradeChart::from_messagepack(&bytes).unwrap();
    assert_eq!(typed, baseline::legacy(&bytes));
    assert_eq!(typed.net.len(), 2);
    assert_eq!(typed.forward.len(), 1);
}

#[test]
fn null_points_retain_the_backend_window_end_and_corrupt_wire_fails() {
    for value in [
        json!({}),
        json!({"chartEndUtc":"2026-10-30T06:00:00Z","points":null}),
    ] {
        let bytes = wire::encode_value(&value).unwrap();
        assert_eq!(
            PreparedTradeChart::from_messagepack(&bytes).unwrap(),
            baseline::legacy(&bytes)
        );
    }
    for bytes in [&[][..], &[0xc1][..], &[0x81, 0xa1][..]] {
        assert!(PreparedTradeChart::from_messagepack(bytes).is_err());
    }
}
