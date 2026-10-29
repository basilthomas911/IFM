//! Deterministic DBN fixtures for current/new historical-provider comparison.
use databento::dbn::{
    MetadataBuilder, OhlcvMsg, RecordHeader, SType, Schema, TradeMsg,
    encode::dbn::{AsyncMetadataEncoder, AsyncRecordEncoder},
    rtype,
};
#[tokio::main(flavor = "current_thread")]
async fn main() {
    let directory =
        std::path::PathBuf::from(std::env::args().nth(1).expect("fixture output directory"));
    std::fs::create_dir_all(&directory).unwrap();
    let metadata = MetadataBuilder::new()
        .dataset("GLBX.MDP3")
        .schema(Some(Schema::Trades))
        .stype_in(Some(SType::RawSymbol))
        .stype_out(SType::InstrumentId)
        .symbols(vec!["ESZ6".into()])
        .start(0)
        .build();
    for (name, ohlcv) in [("trades", false), ("ohlcv", true)] {
        let mut wire = Vec::new();
        AsyncMetadataEncoder::new(&mut wire)
            .encode(&metadata)
            .await
            .unwrap();
        let mut encoder = AsyncRecordEncoder::new(&mut wire);
        for sequence in 0..5000u32 {
            let at = 1_790_000_000_000_000_000 + u64::from(sequence) * 100;
            if ohlcv {
                encoder
                    .encode(&OhlcvMsg {
                        hd: RecordHeader::new::<OhlcvMsg>(rtype::OHLCV_1M, 3, 42, at),
                        open: 5_000_000_000,
                        high: 5_002_000_000,
                        low: 4_998_000_000,
                        close: 5_000_500_000,
                        volume: u64::from(sequence),
                    })
                    .await
                    .unwrap();
            } else {
                encoder
                    .encode(&TradeMsg {
                        hd: RecordHeader::new::<TradeMsg>(rtype::MBP_0, 3, 42, at),
                        price: 5_000_000_000 + i64::from(sequence),
                        size: 7,
                        sequence,
                        action: b'T' as _,
                        side: if sequence % 2 == 0 { b'B' } else { b'A' } as _,
                        ..Default::default()
                    })
                    .await
                    .unwrap();
            }
        }
        std::fs::write(directory.join(format!("{name}.dbn")), &wire).unwrap();
        std::fs::write(
            directory.join(format!("{name}.dbn.zst")),
            zstd::stream::encode_all(wire.as_slice(), 1).unwrap(),
        )
        .unwrap();
    }
    let mut fixed = vec![0; 96];
    fixed[..4].copy_from_slice(b"DBZ\x01");
    fixed[4..13].copy_from_slice(b"GLBX.MDP3");
    fixed[20..22].copy_from_slice(&(Schema::Trades as u16).to_le_bytes());
    fixed[55] = SType::RawSymbol as u8;
    fixed[56] = SType::InstrumentId as u8;
    fixed.extend_from_slice(&zstd::stream::encode_all(&[0u8; 20][..], 1).unwrap());
    let mut legacy = 0x184D2A50_u32.to_le_bytes().to_vec();
    legacy.extend_from_slice(&(fixed.len() as u32).to_le_bytes());
    legacy.extend_from_slice(&fixed);
    let mut records = Vec::new();
    AsyncRecordEncoder::new(&mut records)
        .encode(&TradeMsg {
            price: 12345,
            size: 7,
            sequence: 8,
            ..Default::default()
        })
        .await
        .unwrap();
    legacy.extend_from_slice(&zstd::stream::encode_all(records.as_slice(), 1).unwrap());
    std::fs::write(directory.join("legacy-trades.dbz"), legacy).unwrap();
}
