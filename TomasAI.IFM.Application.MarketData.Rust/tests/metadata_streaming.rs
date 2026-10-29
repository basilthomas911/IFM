//! Valid-input parity and hostile-wire qualification of the actual SDK hook parser.
use databento::dbn::{
    MappingInterval, Metadata, MetadataBuilder, SType, Schema, SymbolMapping,
    decode::dbn::MetadataDecoder, encode::dbn::AsyncMetadataEncoder,
};
use ifm_market_data_native::{
    dataset::metadata::{INPUT_CAPACITY, Policy, Retention, decode},
    runtime::{Deadline, Interrupted},
};
use std::{
    num::NonZeroU64,
    pin::Pin,
    task::{Context, Poll},
    time::Duration,
};
use tokio::io::{AsyncRead, AsyncReadExt, ReadBuf};
use tokio_util::sync::CancellationToken;

fn fixture(version: u8, width: usize, count: usize) -> Metadata {
    let mut m = MetadataBuilder::new()
        .dataset("GLBX.MDP3")
        .schema(Some(Schema::Trades))
        .stype_in(Some(SType::RawSymbol))
        .stype_out(SType::InstrumentId)
        .start(123)
        .build();
    m.version = version;
    m.symbol_cstr_len = width;
    m.ts_out = true;
    m.end = NonZeroU64::new(999);
    m.limit = NonZeroU64::new(17);
    m.symbols = (0..count).map(|i| format!("S{i}")).collect();
    m.partial = vec!["PARTIAL".into()];
    m.not_found = vec!["MISSING".into()];
    m.mappings = vec![SymbolMapping {
        raw_symbol: "ES".into(),
        intervals: vec![MappingInterval {
            start_date: time::Date::from_calendar_date(2026, time::Month::October, 23).unwrap(),
            end_date: time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap(),
            symbol: "42".into(),
        }],
    }];
    m
}
async fn encode(m: &Metadata) -> Vec<u8> {
    let mut out = Vec::new();
    AsyncMetadataEncoder::new(&mut out).encode(m).await.unwrap();
    out
}
struct Fragmented<'a> {
    bytes: &'a [u8],
    chunk: usize,
}
impl AsyncRead for Fragmented<'_> {
    fn poll_read(
        mut self: Pin<&mut Self>,
        _: &mut Context<'_>,
        out: &mut ReadBuf<'_>,
    ) -> Poll<std::io::Result<()>> {
        let n = self.bytes.len().min(self.chunk).min(out.remaining());
        out.put_slice(&self.bytes[..n]);
        self.bytes = &self.bytes[n..];
        Poll::Ready(Ok(()))
    }
}
#[tokio::test]
async fn exact_upstream_parity_versions_widths_fragmentation_and_large_valid_metadata() {
    let mut cases = 0;
    for version in 1..=3 {
        for width in if version == 1 {
            vec![22]
        } else {
            vec![8, 71, 65535]
        } {
            for count in [0, 1, 1001] {
                // The all-retained budget is explicit for the large-width case.
                let bytes = encode(&fixture(version, width, count)).await;
                let expected = MetadataDecoder::with_upgrade_policy(
                    bytes.as_slice(),
                    databento::dbn::VersionUpgradePolicy::AsIs,
                )
                .decode()
                .unwrap();
                for chunk in [1, 3, 7, 65535, 65536, 65537] {
                    if bytes.len() > 1024 * 1024 && chunk < 65535 {
                        continue;
                    }
                    let mut reader = Fragmented {
                        bytes: &bytes,
                        chunk,
                    };
                    let (actual, metrics) = decode(
                        &mut reader,
                        Policy {
                            retained_bytes: 128 * 1024 * 1024,
                            retention: Retention::All,
                        },
                    )
                    .await
                    .unwrap();
                    use sha2::{Digest, Sha256};
                    let identity = format!(
                        "{version}:{width}:{count}:{:x}",
                        Sha256::digest(format!("{actual:?}").as_bytes())
                    );
                    assert!(
                        include_str!("../baseline/MetadataOracle.txt")
                            .lines()
                            .any(|line| line == identity),
                        "unpatched oracle mismatch"
                    );
                    assert_eq!(
                        actual, expected,
                        "version={version} width={width} count={count} chunk={chunk}"
                    );
                    assert_eq!(metrics.input_capacity, INPUT_CAPACITY);
                    assert!(metrics.peak_buffered <= INPUT_CAPACITY);
                    assert!(reader.bytes.is_empty());
                    cases += 1;
                }
            }
        }
    }
    eprintln!("exact_metadata_parity_cases={cases}");
}
#[tokio::test]
async fn records_are_not_consumed_and_readiness_discards_only_unneeded_fields() {
    let m = fixture(3, 71, 1001);
    let mut bytes = encode(&m).await;
    bytes.extend_from_slice(b"NEXT RECORD");
    let mut reader = bytes.as_slice();
    let (actual, metrics) = decode(
        &mut reader,
        Policy {
            retention: Retention::Readiness,
            ..Policy::default()
        },
    )
    .await
    .unwrap();
    assert_eq!(reader, b"NEXT RECORD");
    assert_eq!(actual.partial, m.partial);
    assert_eq!(actual.not_found, m.not_found);
    assert!(actual.symbols.is_empty() && actual.mappings.is_empty());
    assert!(metrics.retained_bytes < 1024);
}
#[tokio::test]
async fn every_truncation_point_fails_without_panicking() {
    let bytes = encode(&fixture(3, 71, 2)).await;
    for len in 0..bytes.len() {
        assert!(
            decode(&mut &bytes[..len], Policy::default()).await.is_err(),
            "truncation={len}"
        );
    }
}
#[tokio::test]
async fn invalid_headers_counts_enums_utf8_dates_and_budget_are_errors() {
    let bytes = encode(&fixture(3, 71, 1)).await;
    let mut cases = Vec::new();
    for offset in [0, 1, 2, 3] {
        let mut b = bytes.clone();
        b[offset] = 255;
        cases.push(b);
    }
    for length in [0_u32, 1, 99, 100, 119] {
        let mut b = bytes.clone();
        b[4..8].copy_from_slice(&length.to_le_bytes());
        cases.push(b);
    }
    for (offset, value) in [(24, 0xfe), (50, 0xfe), (51, 0xfe), (116, 0xff)] {
        let mut b = bytes.clone();
        b[offset] = value;
        cases.push(b);
    }
    let mut b = bytes.clone();
    b[53..55].fill(0);
    cases.push(b);
    let mut b = bytes.clone();
    b[108..112].copy_from_slice(&1_u32.to_le_bytes());
    cases.push(b);
    for offset in [112, 187, 262, 337] {
        let mut b = bytes.clone();
        b[offset..offset + 4].copy_from_slice(&u32::MAX.to_le_bytes());
        cases.push(b);
    }
    // One mapping follows the three one-element lists; mapping interval count and dates.
    for offset in [412, 416, 420] {
        let mut b = bytes.clone();
        b[offset..offset + 4].copy_from_slice(&u32::MAX.to_le_bytes());
        cases.push(b);
    }
    for (index, b) in cases.into_iter().enumerate() {
        assert!(
            decode(&mut b.as_slice(), Policy::default()).await.is_err(),
            "invalid case {index}"
        );
    }
    assert!(
        decode(
            &mut bytes.as_slice(),
            Policy {
                retained_bytes: 0,
                ..Policy::default()
            }
        )
        .await
        .is_err()
    );
    let (_, stats) = decode(&mut bytes.as_slice(), Policy::default())
        .await
        .unwrap();
    assert!(
        decode(
            &mut bytes.as_slice(),
            Policy {
                retained_bytes: stats.retained_bytes - 1,
                ..Policy::default()
            }
        )
        .await
        .is_err()
    );
    assert!(
        decode(
            &mut bytes.as_slice(),
            Policy {
                retained_bytes: stats.retained_bytes,
                ..Policy::default()
            }
        )
        .await
        .is_ok()
    );
}
#[tokio::test]
async fn stalled_header_body_and_ready_stream_are_cancellable_under_original_deadline() {
    for prefix in [
        Vec::new(),
        b"DBN\x03".to_vec(),
        [b"DBN\x03".as_slice(), &u32::MAX.to_le_bytes()].concat(),
    ] {
        let mut reader = prefix.as_slice().chain(tokio::io::duplex(64).0);
        // Keep the peer alive, otherwise duplex EOF would incorrectly make this a stall test.
        let (mut writer, stalled) = tokio::io::duplex(64);
        use tokio::io::AsyncWriteExt;
        writer.write_all(&prefix).await.unwrap();
        let token = CancellationToken::new();
        let mut stalled = stalled;
        assert!(matches!(
            Deadline::after(Duration::from_millis(30))
                .run(&token, decode(&mut stalled, Policy::default()))
                .await,
            Err(Interrupted::DeadlineExpired)
        ));
        token.cancel();
        assert!(matches!(
            Deadline::recovery()
                .run(&token, decode(&mut reader, Policy::default()))
                .await,
            Err(Interrupted::Cancelled)
        ));
        drop(writer);
    }
}

#[tokio::test]
async fn exact_buffer_boundaries_and_deterministic_mutation_corpus() {
    let base = encode(&fixture(3, 71, 1)).await;
    for length in [65535_u32, 65536, 65537, 131071, 131072, 131073] {
        let mut bytes = base.clone();
        bytes.resize(length as usize + 8, 0);
        bytes[4..8].copy_from_slice(&length.to_le_bytes());
        let expected = MetadataDecoder::with_upgrade_policy(
            bytes.as_slice(),
            databento::dbn::VersionUpgradePolicy::AsIs,
        )
        .decode()
        .unwrap();
        bytes.extend_from_slice(b"SENTINEL");
        let mut reader = bytes.as_slice();
        let (actual, metrics) = decode(&mut reader, Policy::default()).await.unwrap();
        assert_eq!(actual, expected);
        assert_eq!(reader, b"SENTINEL");
        assert_eq!(metrics.input_capacity, INPUT_CAPACITY);
    }
    let mut random = 0x12345678_u32;
    for case in 0..4000 {
        let mut bytes = base.clone();
        for _ in 0..1 + case % 8 {
            random = random.wrapping_mul(1664525).wrapping_add(1013904223);
            let at = random as usize % bytes.len();
            random = random.rotate_left(7);
            bytes[at] = random as u8;
        }
        // Both successful valid mutations and explicit parse errors are allowed;
        // a panic, excessive allocation or pending read is a test failure.
        let result = decode(
            &mut bytes.as_slice(),
            Policy {
                retained_bytes: 65536,
                ..Policy::default()
            },
        )
        .await;
        if let Ok((_, stats)) = result {
            assert_eq!(stats.input_capacity, INPUT_CAPACITY);
            assert!(stats.retained_bytes <= 65536);
        }
    }
}
