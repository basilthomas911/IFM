//! Exact qualification against the actual existing C# complete-universe class.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::{
    messaging::codec,
    option_chain::{
        convention::Convention,
        qualification::Decimal,
        universe::{Candidate, qualify},
    },
    runtime::Deadline,
};
use tokio_util::sync::CancellationToken;
fn candidate(v: &serde_json::Value) -> Candidate {
    let text = |key: &str| v[key].as_str().unwrap().to_owned();
    Candidate {
        contract: text("contract"),
        mapping_version: text("version"),
        definition_digest: text("digest"),
        dataset: text("dataset"),
        publisher: v["publisher"].as_u64().unwrap() as u16,
        instrument: v["instrument"].as_u64().unwrap() as u32,
        raw_symbol: text("symbol"),
        underlying: text("underlying"),
        right: v["right"].as_u64().unwrap() as u8,
        strike: Decimal {
            bits: std::array::from_fn(|i| v["strike"][i].as_i64().unwrap() as u32),
        },
        expiration_ns: v["expiration"].as_u64(),
        mapping: v["mapping"].as_str().map(|wire| {
            Convention::new(
                rmpv::decode::read_value(&mut STANDARD.decode(wire).unwrap().as_slice()).unwrap(),
            )
            .unwrap()
        }),
    }
}
#[test]
fn complete_scope_qualification_matches_csharp_in_all_metadata_and_exclusion_cases() {
    let fixtures: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/contexts.json"
    ))
    .unwrap();
    let mut cases = 0;
    for row in fixtures.as_array().unwrap() {
        for fixture in row["universes"].as_array().unwrap() {
            let candidates = fixture["candidates"]
                .as_array()
                .unwrap()
                .iter()
                .map(candidate)
                .collect::<Vec<_>>();
            let result = qualify(
                &candidates,
                fixture["complete"].as_bool().unwrap(),
                fixture["at"].as_i64().unwrap(),
                0,
                &CancellationToken::new(),
                Deadline::recovery(),
            )
            .unwrap();
            assert_eq!(
                codec::encode(&result.value().unwrap()).unwrap(),
                STANDARD
                    .decode(fixture["content"].as_str().unwrap())
                    .unwrap(),
                "{} {}",
                row["name"],
                fixture["mode"]
            );
            cases += 1;
        }
    }
    assert_eq!(cases, 128);
}
#[test]
fn qualification_capacity_cancellation_and_original_deadline_have_no_partial_success() {
    let fixtures: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/contexts.json"
    ))
    .unwrap();
    let valid = candidate(&fixtures[0]["universes"][0]["candidates"][0]);
    let too_many = vec![valid.clone(); 2049];
    let stop = CancellationToken::new();
    assert_eq!(
        qualify(&too_many, true, 0, 0, &stop, Deadline::recovery())
            .unwrap()
            .failure
            .unwrap()
            .code,
        "SnapshotLimit"
    );
    assert_eq!(
        qualify(&too_many, false, 0, 0, &stop, Deadline::recovery())
            .unwrap()
            .failure
            .unwrap()
            .code,
        "IncompleteChain"
    );
    let expired = Deadline::after(std::time::Duration::ZERO);
    assert!(
        qualify(std::slice::from_ref(&valid), true, 0, 0, &stop, expired)
            .err()
            .unwrap()
            .contains("deadline")
    );
    stop.cancel();
    assert!(
        qualify(&[valid], true, 0, 0, &stop, Deadline::recovery())
            .err()
            .unwrap()
            .contains("cancelled")
    );
}
