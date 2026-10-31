use chrono::{DateTime, NaiveDate, Utc};
use ifm_ui_egui::{market_charts::ChartSnapshot, services::market_history::*};
use serde_json::{Value, json};
// The frozen C# oracle is JSON, which serializes decimal fields as numbers.
// Native monitor publications preserve these same decimals as exact strings.
fn typed<T: serde::de::DeserializeOwned + ifm_ui_egui::models::wire_types::FromBackend>(
    value: &Value,
) -> Vec<T> {
    use ifm_ui_egui::backend::schema::Registry;
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
fn time(value: &Value) -> DateTime<Utc> {
    DateTime::parse_from_rfc3339(value.as_str().unwrap())
        .unwrap()
        .with_timezone(&Utc)
}
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
