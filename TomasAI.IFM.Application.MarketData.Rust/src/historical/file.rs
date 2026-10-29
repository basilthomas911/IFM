//! File decoding from an owned asynchronous byte source. The managed storage
//! adapter supplies bounded chunks; Rust never opens a blocking filesystem task.
use super::{Failure, Summary};
use crate::runtime::Deadline;
use databento::dbn::{self, RecordRef, VersionUpgradePolicy, decode::DbnMetadata};
use tokio::io::{AsyncRead, AsyncReadExt};
use tokio_util::sync::CancellationToken;
/// Decodes raw/compressed DBN or legacy DBZ with the same bounded metadata paths
/// as live/history acquisition. The callback's future owns copied values and may
/// apply bounded asynchronous backpressure. Decoder and source die together on
/// cancellation or the original deadline; no disk thread/task is spawned.
#[expect(
    deprecated,
    reason = "Preserve the current historical provider's legacy DBZ file compatibility"
)]
pub async fn for_each<R, P, F>(
    mut reader: R,
    token: &CancellationToken,
    deadline: Deadline,
    mut process: P,
) -> Result<Summary, Failure>
where
    R: AsyncRead + Unpin,
    P: FnMut(Option<&str>, RecordRef<'_>) -> F,
    F: std::future::Future<Output = Result<(), String>>,
{
    deadline
        .run(token, async {
            let mut prefix = [0u8; 12];
            reader
                .read_exact(&mut prefix)
                .await
                .map_err(|e| Failure::Provider(format!("File prefix: {e}")))?;
            if dbn::decode::dbz::starts_with_prefix(&prefix) {
                // Legacy compressed metadata has an independent 64 KiB cap in the
                // isolated DBZ decoder. Synchronous work below reads only this bounded
                // in-memory frame, never the file/socket or an arbitrary wire length.
                let length =
                    u32::from_le_bytes(prefix[4..8].try_into().expect("fixed prefix")) as usize;
                if !(96..=65536).contains(&length) {
                    return Err(Failure::Input(
                        "Legacy DBZ metadata exceeds bounded capacity",
                    ));
                }
                let mut frame = vec![0u8; length + 8];
                frame[..12].copy_from_slice(&prefix);
                reader
                    .read_exact(&mut frame[12..])
                    .await
                    .map_err(|e| Failure::Provider(format!("DBZ metadata: {e}")))?;
                let metadata = dbn::decode::dbz::Decoder::with_upgrade_policy(
                    std::io::Cursor::new(frame),
                    VersionUpgradePolicy::AsIs,
                )
                .map_err(|e| Failure::Provider(e.to_string()))?
                .metadata()
                .clone();
                let input = dbn::decode::AsyncDynReader::new(reader, dbn::Compression::Zstd);
                let mut decoder = dbn::decode::dbn::AsyncRecordDecoder::with_version(
                    input,
                    1,
                    VersionUpgradePolicy::AsIs,
                    metadata.ts_out,
                )
                .map_err(|e| Failure::Provider(e.to_string()))?;
                let mut records = 0u64;
                while let Some(record) = decoder
                    .decode_ref()
                    .await
                    .map_err(|e| Failure::Provider(e.to_string()))?
                {
                    process(metadata.symbols.first().map(String::as_str), record)
                        .await
                        .map_err(Failure::Processing)?;
                    records = records
                        .checked_add(1)
                        .ok_or(Failure::Input("File record count overflow"))?;
                    tokio::task::yield_now().await;
                }
                Ok(Summary { metadata, records })
            } else {
                let source = std::io::Cursor::new(prefix).chain(reader);
                let input = dbn::decode::AsyncDynReader::new_inferred(source)
                    .await
                    .map_err(|e| Failure::Provider(e.to_string()))?;
                let mut decoder = dbn::decode::dbn::AsyncDecoder::with_upgrade_policy(
                    input,
                    VersionUpgradePolicy::AsIs,
                )
                .await
                .map_err(|e| Failure::Provider(e.to_string()))?;
                let mut records = 0u64;
                // Keep metadata owned by its decoder; cloning a large mapping
                // table for every row would violate the bounded-memory intent.
                // into_parts is safe only after all records have been consumed.
                let symbol = decoder.metadata().symbols.first().cloned();
                while let Some(record) = decoder
                    .decode_record_ref()
                    .await
                    .map_err(|e| Failure::Provider(e.to_string()))?
                {
                    process(symbol.as_deref(), record)
                        .await
                        .map_err(Failure::Processing)?;
                    records = records
                        .checked_add(1)
                        .ok_or(Failure::Input("File record count overflow"))?;
                    tokio::task::yield_now().await;
                }
                let (metadata, _) = decoder.into_parts();
                Ok(Summary { metadata, records })
            }
        })
        .await
        .map_err(Failure::Interrupted)?
}
