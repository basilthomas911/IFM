//! Re-exports of the fixed-buffer parser shared by live, historical and file reads.
use databento::dbn;
pub use databento::dbn::decode::dbn::streaming::{
    DEFAULT_RETAINED_BUDGET, INPUT_CAPACITY, Metrics, Policy, Retention, decode,
};
use tokio::io::{AsyncRead, AsyncReadExt};
/// Live readiness retains unresolved symbols and validates/discards arrays that
/// this consumer does not use. Historical defaults retain every field instead.
pub fn live_decoder(
    reader: &mut (dyn AsyncRead + Unpin + Send),
) -> std::pin::Pin<Box<dyn std::future::Future<Output = dbn::Result<dbn::Metadata>> + Send + '_>> {
    Box::pin(async move {
        decode(
            &mut reader.take(u64::MAX),
            Policy {
                retention: Retention::Readiness,
                ..Policy::default()
            },
        )
        .await
        .map(|(m, _)| m)
    })
}
