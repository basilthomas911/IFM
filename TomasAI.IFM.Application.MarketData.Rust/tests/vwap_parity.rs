//! Exact decimal coefficient/scale and continuity oracle from unchanged C# source.
use ifm_market_data_native::aggregation::{
    normalization::TradeAction,
    vwap::{Decimal, Input, Vwap},
};
use serde_json::Value;
fn bits(d: Decimal) -> [u32; 4] {
    [
        d.coefficient as u32,
        (d.coefficient >> 32) as u32,
        (d.coefficient >> 64) as u32,
        d.scale << 16,
    ]
}
#[test]
fn vwap_checkpoints_match_every_csharp_transition_and_overflow_boundary() {
    let cases: Vec<Value> =
        serde_json::from_str(include_str!("../baseline/VwapOracle.json")).unwrap();
    let mut vwap = Vwap::default();
    for (index, c) in cases.iter().enumerate() {
        let i = |n: &str| c[n].as_i64().unwrap();
        let action = match i("action") {
            1 => TradeAction::New,
            2 => TradeAction::Change,
            3 => TradeAction::Cancel,
            4 => TradeAction::Correct,
            5 => TradeAction::Clear,
            6 => TradeAction::None,
            _ => TradeAction::Unknown,
        };
        let input = Input {
            price: i("price"),
            size: i("size"),
            sequence: i("sequence"),
            at: i("timestamp"),
            action,
            conditions: i("conditions") as u16,
        };
        let result = match c["operation"].as_str().unwrap() {
            "reset" => {
                vwap.reset(i("epoch") as u128);
                Ok(())
            }
            "complete" => {
                vwap.complete_replay(i("epoch") as u128);
                Ok(())
            }
            "replay" => vwap.replay(input),
            _ => vwap.live(input, i("epoch") as u128, i("ordinal")),
        };
        assert_eq!(
            result.is_err(),
            c["failed"].as_bool().unwrap(),
            "case {index}"
        );
        let s = vwap.snapshot();
        let publication = ifm_market_data_native::ffi::publication::NativePublication::from_state(
            Default::default(),
            ifm_market_data_native::dataset::book::PublicationState {
                vwap: Some(s),
                ..Default::default()
            },
        );
        assert_eq!(publication.flags, 16);
        let f = publication.fields;
        assert_eq!(f[38] as u64, s.price_volume.coefficient as u64);
        assert_eq!(f[39] as u64, (s.price_volume.coefficient >> 64) as u64);
        assert_eq!(f[40], i64::from(s.price_volume.scale));
        assert_eq!((f[41], f[42], f[43]), (s.volume, s.eligible, s.rejected));
        assert_eq!(
            (f[47], f[48], f[51]),
            (
                i64::from(s.valid),
                i64::from(s.replay_complete),
                s.reason as i64
            )
        );
        let expected = serde_json::json!({"stream":s.epoch,"lastOrdinal":s.ordinal,"lastSequence":s.sequence,"at":s.at,"pv":bits(s.price_volume),"volume":s.volume,"eligible":s.eligible,"rejected":s.rejected,"last":bits(s.last_price),"valid":s.valid,"complete":s.replay_complete,"generation":s.generation,"reason":s.reason as u8});
        for (name, value) in expected.as_object().unwrap() {
            assert_eq!(value, &c[name], "case {index} {name}");
        }
    }
}
