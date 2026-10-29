//! Public SDK historical and DBN reader paths; local HTTP only, fabricated key.
use databento::{
    HistoricalClient,
    dbn::{
        self, Compression, MetadataBuilder, SType, Schema, TradeMsg, VersionUpgradePolicy,
        decode::{
            DbnMetadata, DecodeRecordRef,
            dbn::{AsyncDecoder, AsyncMetadataDecoder, Decoder, MetadataDecoder},
        },
        encode::dbn::{AsyncMetadataEncoder, AsyncRecordEncoder},
    },
    historical::timeseries::{GetRangeParams, GetRangeToFileParams},
};
use ifm_market_data_native::runtime::{Deadline, Interrupted};
use std::time::Duration;
use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio_util::sync::CancellationToken;
fn local_http_builder() -> reqwest::ClientBuilder {
    // These SDK-path fixtures use numeric localhost HTTP. Configure TLS explicitly
    // because the new crate deliberately enables rustls without a global provider;
    // production History owns its TLS/socket driver and never uses this client.
    let tls = rustls::ClientConfig::builder_with_provider(std::sync::Arc::new(
        rustls::crypto::ring::default_provider(),
    ))
    .with_safe_default_protocol_versions()
    .unwrap()
    .with_root_certificates(rustls::RootCertStore::empty())
    .with_no_client_auth();
    reqwest::Client::builder()
        .tls_backend_preconfigured(tls)
        .no_proxy()
        .redirect(reqwest::redirect::Policy::none())
}
async fn wire(version: u8) -> Vec<u8> {
    let mut m = MetadataBuilder::new()
        .dataset("GLBX.MDP3")
        .schema(Some(Schema::Trades))
        .stype_in(Some(SType::RawSymbol))
        .stype_out(SType::InstrumentId)
        .start(0)
        .build();
    m.version = version;
    m.symbol_cstr_len = if version == 1 { 22 } else { 71 };
    m.symbols = (0..4000).map(|i| format!("S{i}")).collect();
    let mut out = Vec::new();
    AsyncMetadataEncoder::new(&mut out)
        .encode(&m)
        .await
        .unwrap();
    let trade = TradeMsg {
        price: 12345,
        size: 7,
        ..TradeMsg::default()
    };
    AsyncRecordEncoder::new(&mut out)
        .encode(&trade)
        .await
        .unwrap();
    out
}
fn check_sync<R: std::io::Read>(mut decoder: Decoder<R>, version: u8) {
    assert_eq!(decoder.metadata().version, version);
    assert_eq!(decoder.metadata().symbols.len(), 4000);
    assert_eq!(
        decoder
            .decode_record_ref()
            .unwrap()
            .unwrap()
            .get::<TradeMsg>()
            .unwrap()
            .price,
        12345
    );
    assert!(decoder.decode_record_ref().unwrap().is_none());
}
#[tokio::test]
async fn public_sync_async_dynamic_compressed_readers_preserve_large_metadata_and_records() {
    for version in 1..=3 {
        let bytes = wire(version).await;
        for policy in [
            VersionUpgradePolicy::AsIs,
            VersionUpgradePolicy::UpgradeToV3,
        ] {
            let expected = if matches!(policy, VersionUpgradePolicy::AsIs) {
                version
            } else {
                3
            };
            check_sync(
                Decoder::with_upgrade_policy(bytes.as_slice(), policy).unwrap(),
                expected,
            );
            let mut decoder = AsyncDecoder::with_upgrade_policy(bytes.as_slice(), policy)
                .await
                .unwrap();
            assert_eq!(decoder.metadata().version, expected);
            assert_eq!(decoder.metadata().symbols.len(), 4000);
            assert_eq!(
                decoder
                    .decode_record_ref()
                    .await
                    .unwrap()
                    .unwrap()
                    .get::<TradeMsg>()
                    .unwrap()
                    .price,
                12345
            );
            assert!(decoder.decode_record_ref().await.unwrap().is_none());
        }
        let compressed = zstd::stream::encode_all(bytes.as_slice(), 1).unwrap();
        let mut dynamic = dbn::decode::DynDecoder::new_inferred(
            compressed.as_slice(),
            VersionUpgradePolicy::AsIs,
        )
        .unwrap();
        assert_eq!(dynamic.metadata().symbols.len(), 4000);
        assert_eq!(
            dynamic
                .decode_record_ref()
                .unwrap()
                .unwrap()
                .get::<TradeMsg>()
                .unwrap()
                .size,
            7
        );
        let mut decoder = AsyncDecoder::with_upgrade_policy(
            dbn::decode::AsyncDynReader::with_buffer(
                tokio::io::BufReader::new(compressed.as_slice()),
                Compression::Zstd,
            ),
            VersionUpgradePolicy::AsIs,
        )
        .await
        .unwrap();
        assert_eq!(decoder.metadata().symbols.len(), 4000);
        assert!(decoder.decode_record_ref().await.unwrap().is_some());
    }
}
async fn serve(body: Vec<u8>, stall: bool) -> (String, tokio::task::JoinHandle<()>) {
    let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await.unwrap();
    let url = format!("http://{}/", listener.local_addr().unwrap());
    let task = tokio::spawn(async move {
        let (mut peer, _) = listener.accept().await.unwrap();
        let mut header = Vec::new();
        let mut byte = [0];
        while !header.ends_with(b"\r\n\r\n") {
            peer.read_exact(&mut byte).await.unwrap();
            header.push(byte[0]);
        }
        let header = String::from_utf8(header).unwrap();
        assert!(header.contains("timeseries.get_range"));
        let len = header
            .lines()
            .find_map(|s| {
                s.to_ascii_lowercase()
                    .strip_prefix("content-length:")
                    .map(|v| v.trim().parse::<usize>().unwrap())
            })
            .unwrap();
        let mut request = vec![0; len];
        peer.read_exact(&mut request).await.unwrap();
        let declared = if stall { body.len() + 1024 } else { body.len() };
        peer.write_all(format!("HTTP/1.1 200 OK\r\nContent-Length: {declared}\r\nConnection: close\r\nContent-Type: application/octet-stream\r\n\r\n").as_bytes()).await.unwrap();
        peer.write_all(&body).await.unwrap();
        if stall {
            assert_eq!(peer.read(&mut byte).await.unwrap(), 0);
        }
    });
    (url, task)
}
#[tokio::test]
async fn actual_historical_http_stream_and_get_range_to_file_use_bounded_metadata_decoder() {
    for save in [false, true] {
        let bytes = wire(3).await;
        let compressed = zstd::stream::encode_all(bytes.as_slice(), 1).unwrap();
        let (url, server) = serve(compressed, false).await;
        let mut client = HistoricalClient::builder()
            .http_client_builder(local_http_builder())
            .base_url(url.parse().unwrap())
            .key("x".repeat(32))
            .unwrap()
            .build()
            .unwrap();
        let start = time::OffsetDateTime::from_unix_timestamp(0).unwrap();
        let end = start + time::Duration::seconds(1);
        if save {
            let path = std::env::temp_dir().join(format!(
                "ifm-history-metadata-{}.dbn.zst",
                std::process::id()
            ));
            let mut decoder = client
                .timeseries()
                .get_range_to_file(
                    &GetRangeToFileParams::builder()
                        .dataset("GLBX.MDP3")
                        .schema(Schema::Trades)
                        .symbols(vec!["ESZ6"])
                        .date_time_range(start..end)
                        .path(path.clone())
                        .build(),
                )
                .await
                .unwrap();
            assert_eq!(decoder.metadata().symbols.len(), 4000);
            assert!(decoder.decode_record_ref().await.unwrap().is_some());
            drop(decoder);
            std::fs::remove_file(path).unwrap();
        } else {
            let mut decoder = client
                .timeseries()
                .get_range(
                    &GetRangeParams::builder()
                        .dataset("GLBX.MDP3")
                        .schema(Schema::Trades)
                        .symbols(vec!["ESZ6"])
                        .date_time_range(start..end)
                        .build(),
                )
                .await
                .unwrap();
            assert_eq!(decoder.metadata().symbols.len(), 4000);
            assert!(decoder.decode_record_ref().await.unwrap().is_some());
        }
        server.await.unwrap();
    }
}
#[tokio::test]
async fn historical_invalid_and_truncated_metadata_fail_and_stalls_obey_original_deadline() {
    for kind in 0..3 {
        let mut bytes = wire(3).await;
        if kind == 0 {
            bytes[112..116].copy_from_slice(&u32::MAX.to_le_bytes());
        }
        if kind == 1 {
            bytes.truncate(127);
        }
        if kind == 2 {
            bytes.truncate(8);
            bytes[4..8].copy_from_slice(&u32::MAX.to_le_bytes());
        }
        assert!(MetadataDecoder::new(bytes.as_slice()).decode().is_err());
        assert!(
            AsyncMetadataDecoder::new(bytes.as_slice())
                .decode()
                .await
                .is_err()
        );
        let (url, server) = serve(
            zstd::stream::encode_all(bytes.as_slice(), 1).unwrap(),
            false,
        )
        .await;
        let mut client = HistoricalClient::builder()
            .http_client_builder(local_http_builder())
            .base_url(url.parse().unwrap())
            .key("x".repeat(32))
            .unwrap()
            .build()
            .unwrap();
        let start = time::OffsetDateTime::from_unix_timestamp(0).unwrap();
        let params = GetRangeParams::builder()
            .dataset("GLBX.MDP3")
            .schema(Schema::Trades)
            .symbols(vec!["ESZ6"])
            .date_time_range(start..start + time::Duration::seconds(1))
            .build();
        assert!(client.timeseries().get_range(&params).await.is_err());
        server.await.unwrap();
    }
    let header = [b"DBN\x03".as_slice(), &u32::MAX.to_le_bytes()].concat();
    let (url, server) = serve(
        zstd::stream::encode_all(header.as_slice(), 1).unwrap(),
        true,
    )
    .await;
    let mut client = HistoricalClient::builder()
        .http_client_builder(local_http_builder())
        .base_url(url.parse().unwrap())
        .key("x".repeat(32))
        .unwrap()
        .build()
        .unwrap();
    let start = time::OffsetDateTime::from_unix_timestamp(0).unwrap();
    let params = GetRangeParams::builder()
        .dataset("GLBX.MDP3")
        .schema(Schema::Trades)
        .symbols(vec!["ESZ6"])
        .date_time_range(start..start + time::Duration::seconds(1))
        .build();
    let mut timeseries = client.timeseries();
    let result = Deadline::after(Duration::from_millis(100))
        .run(&CancellationToken::new(), timeseries.get_range(&params))
        .await;
    assert!(matches!(result, Err(Interrupted::DeadlineExpired)));
    drop(client);
    tokio::time::timeout(Duration::from_secs(1), server)
        .await
        .unwrap()
        .unwrap();
}

#[test]
fn legacy_dbz_dynamic_reader_has_no_unbounded_metadata_bypass() {
    fn legacy(variable: &[u8]) -> Vec<u8> {
        let compressed = zstd::stream::encode_all(variable, 1).unwrap();
        let mut fixed = vec![0; 96];
        fixed[..4].copy_from_slice(b"DBZ\x01");
        fixed[4..13].copy_from_slice(b"GLBX.MDP3");
        fixed[20..22].copy_from_slice(&(Schema::Trades as u16).to_le_bytes());
        fixed[55] = SType::RawSymbol as u8;
        fixed[56] = SType::InstrumentId as u8;
        fixed.extend_from_slice(&compressed);
        let mut out = 0x184D2A50_u32.to_le_bytes().to_vec();
        out.extend_from_slice(&(fixed.len() as u32).to_le_bytes());
        out.extend_from_slice(&fixed);
        // Empty record stream after the skippable metadata frame.
        out.extend_from_slice(&zstd::stream::encode_all(&[][..], 1).unwrap());
        out
    }
    let bytes = legacy(&[0; 20]);
    let decoder =
        dbn::decode::DynDecoder::new_inferred(bytes.as_slice(), VersionUpgradePolicy::AsIs)
            .unwrap();
    assert_eq!(decoder.metadata().dataset, "GLBX.MDP3");
    for variable in [vec![0; 65537], vec![0; 1], {
        let mut v = vec![0; 20];
        v[4..8].copy_from_slice(&u32::MAX.to_le_bytes());
        v
    }] {
        let bytes = legacy(&variable);
        assert!(
            dbn::decode::DynDecoder::new_inferred(bytes.as_slice(), VersionUpgradePolicy::AsIs)
                .is_err()
        );
    }
    let mut header = 0x184D2A50_u32.to_le_bytes().to_vec();
    header.extend_from_slice(&u32::MAX.to_le_bytes());
    header.extend_from_slice(b"DBZ\x01");
    assert!(
        dbn::decode::DynDecoder::new_inferred(header.as_slice(), VersionUpgradePolicy::AsIs)
            .is_err()
    );
}
