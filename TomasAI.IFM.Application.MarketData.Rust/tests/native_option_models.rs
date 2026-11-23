//! Original unchanged C# catalog projection is the oracle for the thin facade.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::{
    catalog::{Definition, option_models},
    messaging::codec,
};
use sha2::{Digest, Sha256};

fn source_future(micro: bool) -> serde_json::Value {
    serde_json::json!({"hd":{"rtype":19,"publisher_id":1,"instrument_id":10252,"ts_event":"1795132800123456789"},
        "ts_recv":"1795132800123556789","raw_symbol":if micro {"MESZ6"} else {"ESZ6"},
        "asset":if micro {"MES"} else {"ES"},"instrument_class":"F","currency":"USD","exchange":"XCME",
        "underlying_id":0,"expiration":"1797604200000000000","contract_multiplier":1,
        "maturity_year":2026,"maturity_month":12,"maturity_day":18,
        "unit_of_measure":"IPNT","unit_of_measure_qty":if micro {"5000000000"} else {"50000000000"},
        "display_factor":"1000000000","min_price_increment":"250000000",
        "min_price_increment_amount":if micro {"1250000000"} else {"12500000000"}})
}

#[test]
fn native_futures_publish_exact_definition_and_verified_usd_point_economics() {
    for micro in [false, true] {
        let raw = source_future(micro).to_string();
        let definition = Definition::parse("GLBX.MDP3", &raw).unwrap();
        let model = option_models::future(&definition, "ES20261218", None, true, true).unwrap();
        let f = model.as_array().unwrap();
        assert_eq!(f.len(), 32);
        assert_eq!(f[7].as_str(), Some(if micro { "5" } else { "50" }));
        assert_eq!(f[11].as_i64(), Some(1));
        assert_eq!(f[12].as_i64(), Some(1)); // Native Draft, not Reviewed.
        assert_eq!(f[13].as_str(), Some("GLBX.MDP3"));
        assert_eq!(f[15].as_u64(), Some(10252));
        assert_eq!(
            f[18].as_str(),
            Some(format!("{:x}", Sha256::digest(raw.as_bytes())).as_str())
        );
        assert_eq!(f[17], codec::utc_offset(17951328001234567).unwrap());
        assert_eq!(f[21], codec::utc_offset(17976042000000000).unwrap());
        assert_eq!(f[24], f[7]);
        assert_eq!(f[26].as_str(), Some("0.25"));
        assert!(f[22].is_nil()); // No invented exchange timezone/calendar/settlement.
        assert!(f[27].is_nil());
    }
}

#[test]
fn missing_or_conflicting_native_economics_never_qualify_futures_composition() {
    for name in [
        "unit_of_measure_qty",
        "display_factor",
        "min_price_increment_amount",
        "expiration",
    ] {
        let mut source = source_future(false);
        source.as_object_mut().unwrap().remove(name);
        let definition = Definition::parse("GLBX.MDP3", &source.to_string()).unwrap();
        let model = option_models::future(&definition, "ES20261218", None, true, true).unwrap();
        assert_eq!(model[11].as_i64(), Some(0), "missing {name}");
        assert!(model[24].is_nil());
    }
    let mut source = source_future(false);
    source["min_price_increment_amount"] = "1000000000".into();
    let definition = Definition::parse("GLBX.MDP3", &source.to_string()).unwrap();
    let model = option_models::future(&definition, "ES20261218", None, true, true).unwrap();
    assert_eq!(model[11].as_i64(), Some(0));
    assert!(model[24].is_nil());
}
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
