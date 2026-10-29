//! Original unchanged C# catalog projection is the oracle for the thin facade.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::{
    catalog::{Definition, option_models},
    messaging::codec,
};
#[test]
fn configured_and_discovered_option_models_match_original_catalog() {
    let rows: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/CatalogBridge/option-models.json"
    ))
    .unwrap();
    assert_eq!(rows.as_array().unwrap().len(), 48);
    for row in rows.as_array().unwrap() {
        let definition = Definition::parse("GLBX.MDP3", row["source"].as_str().unwrap()).unwrap();
        let configured = row["configured"].as_bool().unwrap();
        let id = if configured {
            "ES20261225C6000"
        } else {
            definition.symbol.as_str()
        };
        let underlying = row["underlying"]
            .as_str()
            .map(|id| (id, row["underlyingPublisher"].as_u64().unwrap() as u16));
        let future = option_models::future(
            &definition,
            "ES20261225",
            row["fallback"].as_str(),
            configured,
            !configured,
        );
        if let Some(bytes) = row["futureContent"].as_str() {
            assert_eq!(
                codec::encode(&future.unwrap()).unwrap(),
                STANDARD.decode(bytes).unwrap(),
                "future {} flags={configured}",
                row["mode"]
            );
        } else {
            assert!(future.is_err(), "future {} flags={configured}", row["mode"]);
        }
        let result = option_models::project(
            &definition,
            id,
            row["fallback"].as_str(),
            underlying,
            configured,
        );
        if let Some(bytes) = row["content"].as_str() {
            assert_eq!(
                codec::encode(&result.unwrap()).unwrap(),
                STANDARD.decode(bytes).unwrap(),
                "{} configured={configured}",
                row["mode"]
            );
        } else {
            assert!(result.is_err(), "{} configured={configured}", row["mode"]);
        }
    }
}
