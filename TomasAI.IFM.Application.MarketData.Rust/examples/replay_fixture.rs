//! Deterministic read-only replay wire fixture; no provider connection.
use databento::dbn::{
    MetadataBuilder, SType, SymbolMappingMsg, SystemCode, SystemMsg, TradeMsg,
    encode::dbn::{AsyncMetadataEncoder, AsyncRecordEncoder},
};
#[tokio::main]
async fn main() {
    let path = std::env::args().nth(1).expect("output path");
    let mut bytes = Vec::new();
    AsyncMetadataEncoder::new(&mut bytes)
        .encode(
            &MetadataBuilder::new()
                .dataset("GLBX.MDP3")
                .start(0)
                .schema(None)
                .stype_in(None)
                .stype_out(SType::InstrumentId)
                .build(),
        )
        .await
        .unwrap();
    let mut encoder = AsyncRecordEncoder::new(&mut bytes);
    encoder
        .encode(
            &SymbolMappingMsg::new(
                42,
                10,
                SType::RawSymbol,
                "ESZ6",
                SType::InstrumentId,
                "42",
                0,
                u64::MAX,
            )
            .unwrap(),
        )
        .await
        .unwrap();
    encoder
        .encode(
            &SystemMsg::new(11, Some(SystemCode::SubscriptionAck), "subscription_id=1").unwrap(),
        )
        .await
        .unwrap();
    encoder
        .encode(
            &SystemMsg::new(
                12,
                Some(SystemCode::SlowReaderWarning),
                "replay warning fixture",
            )
            .unwrap(),
        )
        .await
        .unwrap();
    for sequence in 1..=10000_u32 {
        let mut trade = TradeMsg {
            price: 6_000_000_000_000 + i64::from(sequence),
            size: sequence % 9 + 1,
            action: b'T' as _,
            side: b'B' as _,
            sequence,
            ..Default::default()
        };
        trade.hd.instrument_id = 42;
        trade.hd.publisher_id = 3;
        trade.hd.ts_event = 1_790_000_000_000_000_000 + u64::from(sequence) * 100;
        trade.ts_recv = trade.hd.ts_event + 100;
        encoder.encode(&trade).await.unwrap();
    }
    encoder
        .encode(
            &SystemMsg::new(
                13,
                Some(SystemCode::ReplayCompleted),
                "Finished replaying trades",
            )
            .unwrap(),
        )
        .await
        .unwrap();
    // This live record must never enter the replay result after completion.
    let mut live = TradeMsg {
        price: 1,
        size: 1,
        action: b'T' as _,
        sequence: 10001,
        ..Default::default()
    };
    live.hd.instrument_id = 42;
    live.hd.publisher_id = 3;
    encoder.encode(&live).await.unwrap();
    tokio::fs::write(path, bytes).await.unwrap();
}
