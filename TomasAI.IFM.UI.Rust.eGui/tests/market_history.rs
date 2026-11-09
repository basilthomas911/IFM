use chrono::{DateTime, NaiveDate, Utc};
use ifm_ui_egui::{market_charts::ChartSnapshot, services::market_history::*};
use serde_json::{Value, json};


/// Server rollover flags choose the front and next expiry, regardless of row order.
#[test]
fn vx_months_follow_active_catalog_assignments() {
    let source = typed(&json!([
        {"ContractId":"VX-LATER","Symbol":"VX","LastTradeDate":"2027-01-20","OnTheRun":false,"Rollover":true},
        {"ContractId":"VX-OLD","Symbol":"VX","LastTradeDate":"2026-10-21","OnTheRun":false,"Rollover":true},
        {"ContractId":"VX-BACK","Symbol":"VX","LastTradeDate":"2026-12-16","OnTheRun":false,"Rollover":true},
        {"ContractId":"VX-FRONT","Symbol":"VX","LastTradeDate":"2026-11-18","OnTheRun":true,"Rollover":true},
        {"ContractId":"VX-INACTIVE","Symbol":"VX","LastTradeDate":"2026-11-25","OnTheRun":false,"Rollover":false}
    ]));
    assert_eq!(vx_contracts(&source).unwrap(), ("VX-FRONT".into(), Some("VX-BACK".into())));
    assert!(vx_contracts(&source[..3]).is_err());
    assert_eq!(vx_contracts(&source[3..4]).unwrap().1, None);
}


/// Both months share bounds and keep distinct colours, even when one leg fails.
#[test]
fn vx_chart_preserves_both_months_and_partial_history() {
    use ifm_ui_egui::market_charts::ChartSeries;
    let start = time(&json!("2026-11-09T00:00:00Z"));
    let end = start + chrono::Duration::hours(6);
    let line = |price: f64| ChartSnapshot {
        minimum: Some(price - 0.025), maximum: Some(price + 0.025),
        series: vec![ChartSeries { points: vec![[market_seconds(start), price]], ..Default::default() }],
        ..Default::default()
    };
    let both = vx_chart(start, end, ("VX-FRONT", Ok(line(17.0))), ("VX-BACK", Ok(line(19.0))));
    assert_eq!(both.series.len(), 2);
    assert_eq!(both.series[0].name, "Front month · VX-FRONT");
    assert_eq!(both.series[0].color, "Fuchsia");
    assert_eq!(both.series[1].name, "Back month · VX-BACK");
    assert_eq!(both.series[1].color, "LightBlue");
    assert_eq!(both.minimum, Some(16.975));
    assert_eq!(both.maximum, Some(19.025));
    let partial = vx_chart(start, end, ("VX-FRONT", Ok(line(17.0))), ("VX-BACK", Err("Timed out".into())));
    assert_eq!(partial.series.len(), 1);
    assert_eq!(partial.series[0].points[0][1], 17.0);
    assert!(partial.status.contains("Back month: Timed out"));
}


// The frozen C# oracle is JSON, which serializes decimal fields as numbers.
// Native monitor publications preserve these same decimals as exact strings.
fn typed<T: serde::de::DeserializeOwned + ifm_ui_egui::models::wire_types::FromBackend>(
    value: &Value,
) -> Vec<T> {
    use ifm_ui_egui::backend::schema::Registry;


    /// Normalizes market history for market history.
    fn normalize(registry: &Registry, kind: &str, value: &mut Value) {
        if value.is_null() {
            return;
        }
        let kind = kind
            .strip_prefix("System.Nullable`1[")
            .and_then(|kind| kind.strip_suffix(']'))
            .unwrap_or(kind);
        if kind == "System.Decimal" {
            if value.is_number() {
                *value = value.to_string().into();
            }
            return;
        }
        if let Some(inner) = kind.strip_suffix("[]") {
            if let Some(values) = value.as_array_mut() {
                for value in values {
                    normalize(registry, inner, value)
                }
            }
            return;
        }
        if let Ok(shape) = registry.describe(kind) {
            for (name, _, kind) in shape.members {
                if let Some(value) = value.get_mut(&name) {
                    normalize(registry, &kind, value)
                }
            }
        }
    }
    static REGISTRY: std::sync::OnceLock<Registry> = std::sync::OnceLock::new();
    let registry = REGISTRY.get_or_init(|| Registry::compiled().unwrap());
    let mut value = value.clone();
    for row in value.as_array_mut().unwrap() {
        normalize(registry, T::KIND, row);
    }
    serde_json::from_value(value).unwrap()
}

use std::{collections::BTreeMap, path::Path};


/// Provides fixture for the market history implementation.
fn fixture(name: &str) -> Value {
    serde_json::from_slice(
        &std::fs::read(
            Path::new(env!("CARGO_MANIFEST_DIR"))
                .join(format!("tests/fixtures/backend/{name}-display.json")),
        )
        .unwrap(),
    )
    .unwrap()
}


/// Compares market history for market history.
fn compare(actual: ChartSnapshot, expected: &Value) {
    assert_eq!(actual.status, expected["status"]);
    assert_eq!(actual.start, expected["start"].as_f64().unwrap());
    assert_eq!(actual.end, expected["end"].as_f64().unwrap());
    assert_eq!(actual.minimum, expected["minimum"].as_f64());
    assert_eq!(actual.maximum, expected["maximum"].as_f64());
    assert_eq!(
        actual.series.len(),
        expected["series"].as_array().unwrap().len()
    );
    for (series, expected) in actual
        .series
        .iter()
        .zip(expected["series"].as_array().unwrap())
    {
        assert_eq!(series.name, expected["name"]);
        assert_eq!(series.color, expected["color"]);
        let points: Vec<[f64; 2]> = serde_json::from_value(expected["points"].clone()).unwrap();
        assert_eq!(series.points, points);
    }
}


/// Provides time for the market history implementation.
fn time(value: &Value) -> DateTime<Utc> {
    DateTime::parse_from_rfc3339(value.as_str().unwrap())
        .unwrap()
        .with_timezone(&Utc)
}


/// Verifies that bars match hybrid for empty single window filters and dst.
#[test]
fn bars_match_hybrid_for_empty_single_window_filters_and_dst() {
    for case in fixture("market-bars").as_array().unwrap() {
        compare(
            bars(
                case["symbol"].as_str().unwrap(),
                time(&case["start"]),
                time(&case["end"]),
                &typed(&case["source"]),
            )
            .unwrap(),
            &case["expected"],
        );
    }
}


/// Verifies that bands match hybrid for warming duplicates validity and forty day bound.
#[test]
fn bands_match_hybrid_for_warming_duplicates_validity_and_forty_day_bound() {
    for case in fixture("market-bands").as_array().unwrap() {
        compare(
            bands(
                NaiveDate::parse_from_str(case["date"].as_str().unwrap(), "%Y-%m-%d").unwrap(),
                &typed(&case["source"]),
            )
            .unwrap(),
            &case["expected"],
        );
    }
}


/// Verifies that refresh errors preserve points but do not hide the failure.
#[test]
fn refresh_errors_preserve_points_but_do_not_hide_the_failure() {
    let case = &fixture("market-bars")[1];
    let chart = bars(
        "ES",
        time(&case["start"]),
        time(&case["end"]),
        &typed(&case["source"]),
    )
    .unwrap();
    let previous = BTreeMap::from([("ES".into(), chart.clone())]);
    let mut next = BTreeMap::from([(
        "ES".into(),
        ChartSnapshot {
            status: "No provider".into(),
            ..Default::default()
        },
    )]);
    retain_previous(&previous, &mut next);
    assert_eq!(next["ES"].status, "Reconnecting: No provider");
    assert_eq!(next["ES"].series[0].points, chart.series[0].points);
    assert!(
        bars(
            "ES",
            time(&case["start"]),
            time(&case["end"]),
            &typed(
                &json!([{"BarRateType":"FifteenSeconds","BarDate":case["end"],"BarValue":"NaN"}])
            )
        )
        .is_err()
    );
}
