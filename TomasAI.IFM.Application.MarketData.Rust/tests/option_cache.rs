//! Multi-chunk native cache protocol. The exported fixture is also consumed by
//! the actual C# OptionChainPublicationAssembler, not just a Rust mirror decoder.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::{
    messaging::{codec, option_cache::Publication},
    option_chain::composition::model::{Future, Instrument, Snapshot, Valued},
    reference::decimal,
};
use sha2::{Digest, Sha256};
#[test]
fn complete_2048_contract_cache_is_bounded_chunked_and_csharp_readable() {
    let mut snapshot = Snapshot {
        schema: 1,
        id: [1; 16],
        scope: "cache-fixture".into(),
        token: "source".into(),
        horizon: "Daily".into(),
        generation: [2; 16],
        at: 17931024000000000,
        until: 17931024010000000,
        instruments: Vec::new(),
        digest: String::new(),
        ranking: None,
    };
    for i in 0u32..2048 {
        let contract = format!("ES-{i:04}");
        snapshot.instruments.push(Valued {
            instrument: Instrument {
                contract: contract.clone(),
                quote: None,
                context: None,
                strike: None,
                call: None,
                underlying: None,
                future: Some(Future {
                    contract,
                    root: "ES".into(),
                    dataset: "GLBX.MDP3".into(),
                    exchange: "XCME".into(),
                    currency: "USD".into(),
                    last_trading: snapshot.until,
                    multiplier: decimal::parse("50").unwrap(),
                    tick: decimal::parse(".25").unwrap(),
                    digest: format!("{:X}", Sha256::digest(i.to_le_bytes())),
                }),
                selection: None,
                volume: Some(i64::from(i)),
                interest: None,
                statistics_at: None,
                unavailable: None,
                last_event: None,
                last_received: None,
                volume_date: None,
                interest_date: None,
            },
            valuation: None,
        });
    }
    snapshot.seal().unwrap();
    let publication = Publication::new(&snapshot, 739915, "configuration", 7, "lookup").unwrap();
    assert!(
        publication.chunks() > 1,
        "fixture must actually cross the 64 KiB chunk boundary"
    );
    assert!(publication.len() <= 8 * 1024 * 1024);
    assert!(publication.frame(publication.chunks()).is_err());
    assert!(publication.frame(usize::MAX).is_err());
    let frames = (0..publication.chunks())
        .map(|index| STANDARD.encode(publication.frame(index).unwrap()))
        .collect::<Vec<_>>();
    let expected = codec::encode(&rmpv::Value::Array(vec![
        snapshot.value().unwrap(),
        739915.into(),
        "configuration".into(),
        7.into(),
        "lookup".into(),
    ]))
    .unwrap();
    let output = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("../.artifacts/rust-marketdata-offline/native-cache-frames.json");
    std::fs::create_dir_all(output.parent().unwrap()).unwrap();
    std::fs::write(output,serde_json::to_vec(&serde_json::json!({"frames":frames,"content":STANDARD.encode(expected),"digest":snapshot.digest})).unwrap()).unwrap();
    assert!(Publication::new(&snapshot, 739915, "configuration", 0, "lookup").is_err());
    assert!(Publication::new(&snapshot, -1, "configuration", 7, "lookup").is_err());
    assert!(Publication::new(&snapshot, 739915, "", 7, "lookup").is_err());
    snapshot.digest.clear();
    assert!(Publication::new(&snapshot, 739915, "configuration", 7, "lookup").is_err());
}
