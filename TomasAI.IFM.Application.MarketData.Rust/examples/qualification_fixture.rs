//! Offline managed-bridge fixture generator. Never used by a production backend.
use databento::dbn::{
    Mbp1Msg, MetadataBuilder, SType, StatMsg, SymbolMappingMsg, SystemCode, SystemMsg, TradeMsg,
    encode::dbn::{AsyncMetadataEncoder, AsyncRecordEncoder},
};
#[tokio::main]
async fn main() {
    let path = std::env::args().nth(1).expect("fixture output path");
    let dataset = std::env::args()
        .nth(2)
        .unwrap_or_else(|| "GLBX.MDP3".into());
    let acknowledgements = std::env::args()
        .nth(3)
        .map_or(1, |value| value.parse::<usize>().unwrap());
    assert!((1..=64).contains(&acknowledgements));
    let pricing = std::env::args().nth(4).is_some_and(|v| v == "pricing");
    let futures = std::env::args().nth(4).is_some_and(|v| v == "futures");
    let base = std::env::args()
        .nth(5)
        .map_or(0, |value| value.parse::<u64>().unwrap());
    let mut bytes = Vec::new();
    AsyncMetadataEncoder::new(&mut bytes)
        .encode(
            &MetadataBuilder::new()
                .dataset(dataset)
                .start(0)
                .schema(None)
                .stype_in(None)
                .stype_out(SType::InstrumentId)
                .build(),
        )
        .await
        .unwrap();
    let mut records = AsyncRecordEncoder::new(&mut bytes);
    records
        .encode(
            &SymbolMappingMsg::new(
                42,
                10,
                SType::RawSymbol,
                "ESZ6",
                SType::RawSymbol,
                "ESZ6",
                0,
                u64::MAX,
            )
            .unwrap(),
        )
        .await
        .unwrap();
    for subscription in 1..=acknowledgements {
        records
            .encode(
                &SystemMsg::new(
                    11,
                    Some(SystemCode::SubscriptionAck),
                    &format!("subscription_id={subscription}"),
                )
                .unwrap(),
            )
            .await
            .unwrap();
    }
    records
        .encode(
            &SystemMsg::new(
                12,
                Some(SystemCode::SlowReaderWarning),
                "complete test warning",
            )
            .unwrap(),
        )
        .await
        .unwrap();
    let mut trade = TradeMsg {
        price: 6_000_250_000_000,
        size: 4,
        sequence: 7,
        ts_recv: base + 1500,
        action: b'T' as _,
        side: b'B' as _,
        ..Default::default()
    };
    trade.hd.instrument_id = 42;
    trade.hd.publisher_id = 3;
    trade.hd.ts_event = base + 1400;
    records.encode(&trade).await.unwrap();
    let mut quote = Mbp1Msg {
        sequence: 9,
        ts_recv: base + 1700,
        ..Default::default()
    };
    quote.hd.instrument_id = 42;
    quote.hd.publisher_id = 3;
    quote.hd.ts_event = base + 1600;
    quote.levels[0].bid_px = 6_000_000_000_000;
    quote.levels[0].ask_px = 6_000_250_000_000;
    quote.levels[0].bid_sz = 123;
    quote.levels[0].ask_sz = 456;
    quote.levels[0].bid_ct = 7;
    quote.levels[0].ask_ct = 8;
    records.encode(&quote).await.unwrap();
    if pricing {
        quote.hd.instrument_id = 43;
        quote.hd.ts_event = base + 1800;
        quote.ts_recv = base + 1900;
        quote.sequence = 10;
        quote.levels[0].bid_px = 99_750_000_000;
        quote.levels[0].ask_px = 100_250_000_000;
        quote.levels[0].bid_sz = 10;
        quote.levels[0].ask_sz = 10;
        records.encode(&quote).await.unwrap();
        trade.hd.instrument_id = 43;
        trade.hd.ts_event = base + 2000;
        trade.ts_recv = base + 2100;
        trade.sequence = 11;
        trade.price = 100_000_000_000;
        trade.size = 5;
        records.encode(&trade).await.unwrap();
    }
    if futures {
        records
            .encode(
                &SystemMsg::new(
                    base + 1900,
                    Some(SystemCode::ReplayCompleted),
                    "Finished replaying trades",
                )
                .unwrap(),
            )
            .await
            .unwrap();
        records
            .encode(
                &SystemMsg::new(
                    base + 1950,
                    Some(SystemCode::ReplayCompleted),
                    "Finished replaying statistics",
                )
                .unwrap(),
            )
            .await
            .unwrap();
        for sequence in 10..=14 {
            quote.sequence = sequence;
            quote.hd.ts_event = base + u64::from(sequence) * 200;
            quote.ts_recv = quote.hd.ts_event + 100;
            quote.levels[0].bid_px = 6_000_000_000_000 + i64::from(sequence) * 10_000_000;
            quote.levels[0].ask_px = quote.levels[0].bid_px + 250_000_000;
            records.encode(&quote).await.unwrap();
        }
        for (sequence, price) in [
            (20, 6_000_250_000_000),
            (21, 6_000_250_000_000),
            (22, 6_000_500_000_000),
            (23, 6_000_600_000_000),
        ] {
            trade.sequence = sequence;
            trade.price = price;
            trade.hd.ts_event = base + u64::from(sequence) * 200;
            trade.ts_recv = trade.hd.ts_event + 100;
            records.encode(&trade).await.unwrap();
        }
        for (sequence, kind) in [(30, 1), (31, 3), (32, 4), (33, 6)] {
            let mut stat = StatMsg {
                sequence,
                price: 6_000_750_000_000,
                quantity: 9999,
                stat_type: kind,
                update_action: 1,
                ts_ref: base,
                ts_recv: base + u64::from(sequence) * 200 + 100,
                ..Default::default()
            };
            stat.hd.instrument_id = 42;
            stat.hd.publisher_id = 3;
            stat.hd.ts_event = base + u64::from(sequence) * 200;
            records.encode(&stat).await.unwrap();
        }
        records
            .encode(
                &SystemMsg::new(
                    base + 8000,
                    Some(SystemCode::SlowReaderWarning),
                    "futures fixture complete",
                )
                .unwrap(),
            )
            .await
            .unwrap();
    }
    std::fs::write(path, bytes).unwrap();
}
