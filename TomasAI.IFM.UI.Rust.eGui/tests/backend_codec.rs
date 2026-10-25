use ifm_ui_egui::backend::{codec, contracts::*};
use rmpv::Value;
use std::path::Path;

fn fixture(name: &str, kind: &str) -> Vec<u8> {
    std::fs::read(
        Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("tests/fixtures/backend")
            .join(format!("{name}.{kind}.msgpack")),
    )
    .unwrap()
}

#[test]
fn every_csharp_fixture_decodes_and_rust_compression_roundtrips() {
    let manifest: serde_json::Value =
        serde_json::from_slice(include_bytes!("fixtures/backend/fixtures.json")).unwrap();
    let out = Path::new(env!("CARGO_MANIFEST_DIR")).join("target/cross-language");
    std::fs::create_dir_all(&out).unwrap();
    for item in manifest.as_array().unwrap() {
        let name = item["name"].as_str().unwrap();
        let value = codec::decode(&fixture(name, "raw")).unwrap();
        assert_eq!(
            codec::decode(&fixture(name, "wire")).unwrap(),
            value,
            "{name}"
        );
        let rust = codec::encode(&value).unwrap();
        assert_eq!(codec::decode(&rust).unwrap(), value, "{name}");
        std::fs::write(out.join(format!("{name}.msgpack")), rust).unwrap();
    }
}

#[test]
fn value_date_and_query_match_compiled_csharp_contracts() {
    let subject = ActorSubject::query("MarketDataQuery", "GetValueDate", "valueDate");
    // The C# union formatter serializes the unregistered parameter implementation as nil.
    // Preserve its actual wire representation; identity is carried by Subject.
    let query = Value::Array(vec![subject.wire(), Value::Nil]);
    assert_eq!(
        codec::raw(&query).unwrap(),
        fixture("value-date-query", "raw")
    );
    let result = codec::decode(&fixture("value-date-result", "wire")).unwrap();
    let day = date_only(&service_result(&result).unwrap().as_array().unwrap()[0]).unwrap();
    assert_eq!(day.to_string(), "2026-10-26");
    assert_eq!(date_only_wire(day).as_u64(), Some(739914));
    let error = codec::decode(&fixture("error-result", "wire")).unwrap();
    assert_eq!(
        service_result(&error).unwrap_err(),
        "Backend error 1015: Server clock unavailable"
    );
}

#[test]
fn decimal_precision_and_datetime_are_not_lost() {
    let decimal = codec::decode(&fixture("decimal", "wire")).unwrap();
    assert_eq!(
        ExactDecimal::parse(decimal.as_str().unwrap())
            .unwrap()
            .as_str(),
        "12345678901234567890.123456789"
    );
    for text in [
        "-0.0000000000000000000000000001",
        "79228162514264337593543950335",
        "0.00",
    ] {
        assert_eq!(ExactDecimal::parse(text).unwrap().as_str(), text);
    }
    for text in [
        ".",
        "1.",
        ".1",
        "",
        "-",
        "NaN",
        "1e2",
        "79228162514264337593543950336",
        "0.00000000000000000000000000001",
    ] {
        assert!(ExactDecimal::parse(text).is_err(), "{text}");
    }
    let time = timestamp(&codec::decode(&fixture("datetime", "wire")).unwrap()).unwrap();
    assert_eq!(
        time.to_rfc3339_opts(chrono::SecondsFormat::Nanos, true),
        "2026-10-26T12:30:00.123456700Z"
    );
}

#[test]
fn malformed_compression_and_trailing_data_are_rejected() {
    let oversized = codec::raw(&Value::Array(vec![
        Value::Ext(
            98,
            codec::raw(&Value::from(codec::MAX_MESSAGE as u64 + 1)).unwrap(),
        ),
        Value::Binary(vec![]),
    ]))
    .unwrap();
    assert!(codec::decode(&oversized).unwrap_err().contains("16 MiB"));
    for value in [
        Value::Array(vec![Value::Ext(98, vec![])]),
        Value::Array(vec![Value::Ext(98, vec![10]), Value::Nil]),
        Value::Array(vec![Value::Ext(98, vec![10]), Value::Binary(vec![0xff])]),
        Value::Ext(99, vec![0xff]),
    ] {
        assert!(codec::decode(&codec::raw(&value).unwrap()).is_err());
    }
    assert!(codec::decode(&[0xc0, 0xc0]).is_err());
}

#[test]
fn actor_entities_can_contain_dots_without_permitting_wildcards() {
    let subject = ActorSubject::query(
        "FuturesOptionTradeQuery",
        "GetTradeRealtimeSnapshot",
        "101.701.1901.1501",
    );
    assert_eq!(
        subject.route().unwrap(),
        "Query.FuturesOptionTradeQuery.GetTradeRealtimeSnapshot.101.701.1901.1501"
    );
    assert!(
        ActorSubject::query("Trade", "Read", "101.>")
            .route()
            .is_err()
    );
}

#[test]
fn compiled_schemas_preserve_retired_slots_and_lookup_projection() {
    use ifm_ui_egui::backend::schema::Registry;
    let registry = Registry::compiled().unwrap();
    let reply = codec::decode(&fixture("market-session-result", "wire")).unwrap();
    let named = registry
        .project(
            "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.MarketSessionReadModel",
            service_result(&reply).unwrap(),
        )
        .unwrap();
    assert_eq!(named["OperationalValueDate"], "2026-10-26");
    assert_eq!(named["Revision"], 42);
    assert_eq!(named["State"], "LiveTrading");
    assert!(named.get("IsLiveSessionOpen").is_none());
    let subject = ActorSubject::query("LookupTypeQuery", "GetLookupTypes", "all");
    let request = registry
        .encode(
            "TomasAI.IFM.Domain.Reference.Shared.Queries.GetLookupTypesQuery",
            &std::collections::BTreeMap::from([
                ("Subject".into(), subject.wire()),
                (
                    "EntityId".into(),
                    Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]),
                ),
            ]),
        )
        .unwrap();
    assert_eq!(
        codec::raw(&request).unwrap(),
        fixture("lookup-query", "raw")
    );
    let reply = codec::decode(&fixture("lookup-result", "wire")).unwrap();
    let rows = ifm_ui_egui::services::reference::project_lookups(
        &registry,
        service_result(&reply).unwrap(),
    )
    .unwrap();
    assert_eq!(rows.len(), 1);
    assert_eq!(rows[0].lookup_type_name, "Currency");
    assert_eq!(rows[0].short_code, "USD");
    assert_eq!(rows[0].order_id, 1);
    assert_eq!(rows[0].created_on, "2026-10-23T12:00:00Z");
}

#[test]
fn compiled_name_keyed_models_unions_and_closed_generics_are_projected() {
    let registry = ifm_ui_egui::backend::schema::Registry::compiled().unwrap();
    let value = codec::decode(&fixture("status-log-map", "wire")).unwrap();
    let named = registry
        .project(
            "TomasAI.IFM.Shared.StatusConsole.ViewModels.StatusConsoleLogReadModel",
            &value,
        )
        .unwrap();
    assert_eq!(named["Message"], "Recovery deadline expired");
    assert_eq!(named["StatusCode"], 503);
    assert_eq!(named["Source"], "MarketDataFeed");
    let value = codec::decode(&fixture("actor-entity-union", "wire")).unwrap();
    assert_eq!(
        registry
            .project(
                "TomasAI.IFM.Shared.EventModelActor.Contracts.IActorEntityId",
                &value
            )
            .unwrap()["Value"],
        "none"
    );
    let value = codec::decode(&fixture("value-date-result", "wire")).unwrap();
    let named=registry.project("TomasAI.IFM.Shared.EventSourcing.ServiceResult`1[TomasAI.IFM.Shared.EventSourcing.ScalarReadModel`1[System.DateOnly]]",&value).unwrap();
    assert_eq!(named["Value"]["Value"], "2026-10-26");
    assert!(
        registry
            .project(
                "TomasAI.IFM.Shared.StatusConsole.ViewModels.StatusConsoleLogReadModel",
                &Value::Array(vec![])
            )
            .is_err()
    );
}
