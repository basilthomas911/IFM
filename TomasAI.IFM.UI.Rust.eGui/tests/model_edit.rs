use ifm_ui_egui::backend::{codec, schema::Registry};
use rmpv::Value;
use serde_json::json;
use std::{collections::BTreeMap, path::Path};
fn fixture(name: &str) -> Value {
    codec::decode(
        &std::fs::read(
            Path::new(env!("CARGO_MANIFEST_DIR"))
                .join(format!("tests/fixtures/backend/{name}.wire.msgpack")),
        )
        .unwrap(),
    )
    .unwrap()
}
#[test]
fn typed_contract_edits_match_csharp_and_preserve_all_unedited_provider_evidence() {
    let registry = Registry::compiled().unwrap();
    let original = fixture("provider-contract");
    let fields = BTreeMap::from([
        ("Description".into(), json!("Reviewed fixture")),
        ("ReviewState".into(), json!("Reviewed")),
        ("TickSize".into(), json!("0.1250")),
        ("ExpirationUtc".into(), json!("2026-12-18T08:30:00-04:00")),
    ]);
    let actual = registry
        .patch(
            "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel",
            &original,
            &fields,
        )
        .unwrap();
    assert_eq!(actual, fixture("provider-contract-patched"));
    // A newer backend may append fields. Editing a known slot must preserve them.
    let mut newer = original.clone();
    if let Value::Array(values) = &mut newer {
        values.push("future-backend-evidence".into());
    }
    let patched = registry
        .patch(
            "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel",
            &newer,
            &fields,
        )
        .unwrap();
    assert_eq!(
        patched.as_array().unwrap().last().unwrap().as_str(),
        Some("future-backend-evidence")
    );
}
#[test]
fn input_types_reject_unknown_fields_rounding_overflow_and_ambiguous_unions() {
    let registry = Registry::compiled().unwrap();
    for (kind, value) in [
        ("System.Byte", json!(256)),
        ("System.Int16", json!(-32769)),
        ("System.UInt32", json!(-1)),
        ("System.Decimal", json!(0.125)),
        ("System.Decimal", json!("1e3")),
        ("System.DateOnly", json!("0000-01-01")),
        ("System.DateTime", json!("2026-10-26T12:30:00.123456789Z")),
        ("System.TimeOnly", json!("24:00:00")),
        ("System.Guid", json!("bad")),
        (
            "TomasAI.IFM.Shared.EventModelActor.Contracts.IActorEntityId",
            json!({"Value":"none"}),
        ),
    ] {
        assert!(
            registry.value_from_json(kind, &value).is_err(),
            "{kind}: {value}"
        );
    }
    assert!(
        registry
            .patch(
                "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel",
                &fixture("provider-contract"),
                &BTreeMap::from([("typo".into(), json!("value"))])
            )
            .is_err()
    );
    assert_eq!(
        registry
            .value_from_json("System.UInt16", &json!(65535))
            .unwrap()
            .as_u64(),
        Some(65535)
    );
}
#[test]
fn convention_display_matches_hybrid_and_preserves_original_timestamp_offsets() {
    let registry = Registry::compiled().unwrap();
    let row = ifm_ui_egui::state::futures_contract::display_contract(
        &registry,
        &fixture("provider-contract-review-display"),
    )
    .unwrap();
    let expected: serde_json::Value = serde_json::from_slice(
        &std::fs::read(
            Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("tests/fixtures/backend/contract-review-display.json"),
        )
        .unwrap(),
    )
    .unwrap();
    for (key, value) in expected.as_object().unwrap() {
        assert_eq!(
            row.fields.get(key).unwrap(),
            value.as_str().unwrap(),
            "{key}"
        );
    }
    assert_eq!(
        row.fields["effectiveFromUtc"],
        "2026-10-26T08:30:00.0000000-04:00"
    );
}
#[test]
fn timezone_validation_accepts_windows_and_iana_names_without_using_a_local_clock() {
    assert!(ifm_ui_egui::backend::time_zone::is_known(
        "America/New_York"
    ));
    #[cfg(windows)]
    assert!(ifm_ui_egui::backend::time_zone::is_known(
        "Eastern Standard Time"
    ));
    assert!(!ifm_ui_egui::backend::time_zone::is_known("Not a timezone"));
}

#[test]
fn typed_edits_reject_wrong_shapes_and_dates_whose_utc_instant_overflows_dotnet() {
    let registry = Registry::compiled().unwrap();
    assert!(
        registry
            .patch(
                "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel",
                &rmpv::Value::Nil,
                &BTreeMap::new()
            )
            .is_err()
    );
    for timestamp in ["0001-01-01T00:00:00+14:00", "9999-12-31T23:59:59-14:00"] {
        for kind in ["System.DateTime", "System.DateTimeOffset"] {
            assert!(
                registry.value_from_json(kind, &json!(timestamp)).is_err(),
                "{kind}: {timestamp}"
            );
        }
    }
    for timestamp in ["0001-01-01T14:00:00+14:00", "9999-12-31T09:59:59-14:00"] {
        assert!(
            registry
                .value_from_json("System.DateTimeOffset", &json!(timestamp))
                .is_ok()
        );
    }
}

#[test]
fn malformed_reference_offsets_are_errors_instead_of_arithmetic_panics() {
    use ifm_ui_egui::backend::contracts::{reference_offset, timestamp_wire};
    let time = timestamp_wire("2026-10-24T12:00:00Z".parse().unwrap());
    for minutes in [i64::MIN, -841, 841, i64::MAX] {
        assert!(reference_offset(&rmpv::Value::Array(vec![time.clone(), minutes.into()])).is_err());
    }
}
