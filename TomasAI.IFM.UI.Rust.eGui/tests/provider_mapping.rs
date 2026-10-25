use chrono::NaiveDate;
use ifm_ui_egui::{
    backend::{codec, schema::Registry, time_zone},
    services::provider_mapping::*,
};
use rmpv::Value;
use std::path::Path;
fn wire(name: &str) -> Value {
    codec::decode(
        &std::fs::read(
            Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("tests/fixtures/backend")
                .join(format!("{name}.wire.msgpack")),
        )
        .unwrap(),
    )
    .unwrap()
}
#[test]
fn provider_drafts_match_hybrid_exchange_dates_and_exact_provider_evidence() {
    let registry = Registry::compiled().unwrap();
    let at = "2026-10-24T12:00:00Z".parse().unwrap();
    let underlying = wire("definition-mapped-future");
    for (source, expected, under) in [
        ("definition-future", "definition-mapped-future", None),
        (
            "definition-option",
            "definition-mapped-option",
            Some(&underlying),
        ),
    ] {
        let definition = wire(source);
        let projected = registry.project(DEFINITION, &definition).unwrap();
        let expiration =
            chrono::DateTime::parse_from_rfc3339(projected["ExpirationUtc"].as_str().unwrap())
                .unwrap()
                .to_utc();
        let day = time_zone::local_date("America/Chicago", expiration).unwrap();
        let actual = prepare(
            &registry,
            &definition,
            "America/Chicago",
            at,
            day,
            under,
            None,
        )
        .unwrap();
        assert_eq!(actual, wire(expected), "{source}");
        let name = if under.is_none() {
            "definition-future-preview.json"
        } else {
            "definition-option-preview.json"
        };
        let golden: serde_json::Value = serde_json::from_slice(
            &std::fs::read(
                Path::new(env!("CARGO_MANIFEST_DIR"))
                    .join("tests/fixtures/backend")
                    .join(name),
            )
            .unwrap(),
        )
        .unwrap();
        assert_eq!(
            preview_document(
                &registry,
                if under.is_none() { FUTURE } else { OPTION },
                &actual
            )
            .unwrap(),
            golden
        );
    }
}
#[test]
fn windows_and_iana_exchange_dates_match_across_dst_and_utc_midnight() {
    for instant in [
        "2026-11-01T05:59:59Z",
        "2026-11-01T06:00:00Z",
        "2026-03-08T07:00:00Z",
        "2026-12-18T00:30:00Z",
    ] {
        let instant = instant.parse().unwrap();
        #[cfg(windows)]
        assert_eq!(
            time_zone::local_date("America/Chicago", instant).unwrap(),
            time_zone::local_date("Central Standard Time", instant).unwrap()
        );
    }
    assert_eq!(
        time_zone::local_date("America/Chicago", "2026-12-18T00:30:00Z".parse().unwrap()).unwrap(),
        NaiveDate::from_ymd_opt(2026, 12, 17).unwrap()
    );
    assert!(time_zone::local_date("not/a-zone", "2026-12-18T00:30:00Z".parse().unwrap()).is_err());
}
#[test]
fn stale_incomplete_or_wrong_underlying_definitions_cannot_be_prepared() {
    let registry = Registry::compiled().unwrap();
    let definition = wire("definition-option");
    let day = NaiveDate::from_ymd_opt(2026, 11, 19).unwrap();
    let at = "2026-10-24T12:00:00Z".parse().unwrap();
    assert!(
        prepare(
            &registry,
            &definition,
            "America/Chicago",
            at,
            day,
            None,
            None
        )
        .is_err()
    );
    let future = wire("definition-mapped-future");
    for (member, value) in [
        ("Deleted", serde_json::json!(true)),
        ("ExpirationUtc", serde_json::json!("2026-10-01T00:00:00Z")),
        ("ActivationUtc", serde_json::json!("2026-11-01T00:00:00Z")),
        ("DefinitionDigest", serde_json::json!("short")),
        ("UnderlyingInstrumentId", serde_json::json!(9999)),
    ] {
        let bad = registry
            .patch(
                DEFINITION,
                &definition,
                &std::collections::BTreeMap::from([(member.into(), value)]),
            )
            .unwrap();
        assert!(
            prepare(
                &registry,
                &bad,
                "America/Chicago",
                at,
                day,
                Some(&future),
                None
            )
            .is_err(),
            "{member}"
        );
    }
}

#[test]
fn existing_import_preserves_review_fields_aliases_and_legacy_exact_strike() {
    let registry = Registry::compiled().unwrap();
    let at = "2026-10-24T12:00:00Z".parse().unwrap();
    let underlying = wire("definition-mapped-future");
    for (definition, old, next, under, day, kind) in [
        (
            "definition-future",
            "definition-existing-future",
            "definition-remapped-future",
            None,
            "2026-12-17",
            FUTURE,
        ),
        (
            "definition-option",
            "definition-existing-option",
            "definition-remapped-option",
            Some(&underlying),
            "2026-11-19",
            OPTION,
        ),
    ] {
        let definition = wire(definition);
        let original = wire(old);
        let day = day.parse().unwrap();
        let actual = prepare(
            &registry,
            &definition,
            "America/Chicago",
            at,
            day,
            under,
            Some(&original),
        )
        .unwrap();
        assert_eq!(actual, wire(next));
        let reviewed = registry
            .patch(
                kind,
                &original,
                &std::collections::BTreeMap::from([(
                    "ReviewState".into(),
                    serde_json::json!("Reviewed"),
                )]),
            )
            .unwrap();
        assert!(
            prepare(
                &registry,
                &definition,
                "America/Chicago",
                at,
                day,
                under,
                Some(&reviewed)
            )
            .unwrap_err()
            .contains("qualification")
        );
    }
}
