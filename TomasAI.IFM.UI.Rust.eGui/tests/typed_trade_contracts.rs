#[allow(dead_code)]
#[path = "support/monitor_display_baseline.rs"]
mod monitor_display;
use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    models::{trade_contracts::*, trade_presentation::DisplayRecord, wire_types::FromBackend},
    wire,
};
use serde::Serialize;

fn assert_parity<T: FromBackend + Serialize>(name: &str, history: bool)
where
    T: ifm_ui_egui::models::trade_presentation::PresentationValue,
{
    let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
        .join(format!("tests/fixtures/backend/{name}.msgpack"));
    let bytes = std::fs::read(path).unwrap();
    let backend = codec::decode(&bytes).unwrap();
    let registry = Registry::compiled().unwrap();
    let named = monitor_display::named(&registry, T::KIND, &backend).unwrap();
    let expected = if history {
        monitor_display::plan_history(&named)
    } else {
        monitor_display::document(&named)
    };
    let typed = T::from_backend(&backend).unwrap();
    let encoded = wire::encode(&DisplayRecord {
        value: &typed,
        history,
    })
    .unwrap();
    let actual: serde_json::Value = wire::decode(&encoded).unwrap();
    fn differences(
        path: &str,
        a: &serde_json::Value,
        b: &serde_json::Value,
        out: &mut Vec<String>,
    ) {
        if a == b {
            return;
        }
        match (a, b) {
            (serde_json::Value::Object(a), serde_json::Value::Object(b)) => {
                for key in a
                    .keys()
                    .chain(b.keys())
                    .collect::<std::collections::BTreeSet<_>>()
                {
                    differences(
                        &format!("{path}.{key}"),
                        a.get(key).unwrap_or(&serde_json::Value::Null),
                        b.get(key).unwrap_or(&serde_json::Value::Null),
                        out,
                    );
                    if a.contains_key(key) != b.contains_key(key) {
                        out.push(format!("{path}.{key}: key absent"));
                    }
                }
            }
            (serde_json::Value::Array(a), serde_json::Value::Array(b)) if a.len() == b.len() => {
                for (index, (a, b)) in a.iter().zip(b).enumerate() {
                    differences(&format!("{path}[{index}]"), a, b, out)
                }
            }
            _ => out.push(format!("{path}: typed={a}, expected={b}")),
        }
    }
    let mut differences_found = Vec::new();
    differences(name, &actual, &expected, &mut differences_found);
    assert!(
        differences_found.is_empty(),
        "{}",
        differences_found.join("\n")
    );
}

#[test]
fn backend_trade_plan_and_realtime_have_exact_display_parity_without_json_in_production() {
    assert_parity::<EstablishedTradeDefinition>("monitor-trade", false);
    assert_parity::<StrategyTradePlanSnapshot>("monitor-plan", true);
    assert_parity::<TradeRealtimeSnapshot>("monitor-realtime", false);
}

#[test]
fn every_generated_field_matches_the_compiled_csharp_slot_and_type() {
    let schema: serde_json::Value =
        serde_json::from_str(include_str!("fixtures/backend/contracts.json")).unwrap();
    for &(kind, member, index, field_type) in CONTRACT_FIELDS {
        let shape = schema
            .as_array()
            .unwrap()
            .iter()
            .find(|shape| shape["type"] == kind)
            .unwrap();
        let field = shape["members"]
            .as_array()
            .unwrap()
            .iter()
            .find(|field| field["name"] == member)
            .unwrap();
        assert_eq!(field["index"], index, "{kind}.{member}");
        assert_eq!(field["type"], field_type, "{kind}.{member}");
    }
}

#[test]
fn retired_plan_slots_are_ignored_and_known_fields_keep_their_original_positions() {
    let mut value = codec::decode(include_bytes!("fixtures/backend/monitor-plan.msgpack")).unwrap();
    let rmpv::Value::Array(root) = &mut value else {
        panic!("Expected plan array")
    };
    let detail = &mut root[19];
    let rmpv::Value::Array(fields) = detail else {
        panic!("Expected detail array")
    };
    let before =
        IronCondorTradePlanSnapshot::from_backend(&rmpv::Value::Array(fields.clone())).unwrap();
    for slot in [
        14, 15, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 35, 40, 41, 42, 43, 44, 45, 46, 47, 50,
    ] {
        if let Some(field) = fields.get_mut(slot) {
            *field = "retired slot must never become a live property".into();
        }
    }
    let after = IronCondorTradePlanSnapshot::from_backend(detail).unwrap();
    assert_eq!(before, after);
}

#[test]
fn unknown_enum_values_retain_their_numeric_label_and_malformed_values_fail() {
    let unknown = TradeStrategyKind::from_backend(&999.into()).unwrap();
    assert_eq!(unknown.label(), "999");
    assert!(TradeStrategyKind::from_backend(&"IronCondor".into()).is_err());
    assert!(StrategyPositionSnapshot::from_backend(&rmpv::Value::Map(vec![])).is_err());
}

#[test]
fn typed_records_keep_unchanged_clr_json_only_at_protocol_boundaries() {
    use ifm_ui_egui::models::backend_document::BackendRecord;
    let registry = Registry::compiled().unwrap();
    fn verify<T: FromBackend + BackendRecord>(registry: &Registry, bytes: &[u8]) {
        let raw = ifm_ui_egui::backend::codec::decode(bytes).unwrap();
        let record = T::from_backend(&raw).unwrap();
        assert_eq!(
            ifm_ui_egui::models::backend_document::request_document(&record).unwrap(),
            ifm_ui_egui::services::parameters::document(registry, T::KIND, &raw, true).unwrap()
        );
    }
    verify::<StrategyTradePlanSnapshot>(
        &registry,
        include_bytes!("fixtures/backend/monitor-plan.msgpack"),
    );
    verify::<TradeRealtimeSnapshot>(
        &registry,
        include_bytes!("fixtures/backend/monitor-realtime.msgpack"),
    );
    verify::<EstablishedTradeDefinition>(
        &registry,
        include_bytes!("fixtures/backend/monitor-trade.msgpack"),
    );
    verify::<FundMandateReadModel>(
        &registry,
        include_bytes!("fixtures/backend/portfolio-fund.msgpack"),
    );
}

#[test]
fn unnamed_enum_labels_remain_strings_in_monitor_publications_recursively() {
    let registry = Registry::compiled().unwrap();
    let mut raw = codec::decode(include_bytes!("fixtures/backend/monitor-plan.msgpack")).unwrap();
    let slot = |kind: &str, name: &str| {
        registry
            .describe(kind)
            .unwrap()
            .members
            .into_iter()
            .find(|m| m.0 == name)
            .unwrap()
            .1
    };
    let rmpv::Value::Array(fields) = &mut raw else {
        panic!("Expected plan")
    };
    fields[slot(StrategyTradePlanSnapshot::KIND, "State")] = 999.into();
    let position_slot = slot(StrategyTradePlanSnapshot::KIND, "Position");
    let rmpv::Value::Array(position) = &mut fields[position_slot] else {
        panic!("Expected position")
    };
    position[slot(StrategyPositionSnapshot::KIND, "StrategyKind")] = 998.into();
    let named = monitor_display::named(&registry, StrategyTradePlanSnapshot::KIND, &raw).unwrap();
    let typed = StrategyTradePlanSnapshot::from_backend(&raw).unwrap();
    for history in [false, true] {
        let before = if history {
            monitor_display::plan_history(&named)
        } else {
            monitor_display::document(&named)
        };
        let after: serde_json::Value = wire::decode(
            &wire::encode(&DisplayRecord {
                value: &typed,
                history,
            })
            .unwrap(),
        )
        .unwrap();
        assert_eq!(after, before);
        assert_eq!(after["state"], "999");
        if !history {
            assert_eq!(after["position"]["strategyKind"], "998");
        }
    }
    // The same declared enum retains its original numeric broker representation.
    assert_eq!(
        serde_json::to_value(TradeStrategyKind::Unrecognized(999)).unwrap(),
        999
    );
}
