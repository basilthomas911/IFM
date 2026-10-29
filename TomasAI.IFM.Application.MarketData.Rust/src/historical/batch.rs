//! Owned historical batch control and bounded downloads. Submission is deliberately
//! not automatically retried: the application persists provider job identity to
//! avoid charging twice. A streamed file is provisional until its final size/hash
//! match; durable staging, checkpointing and atomic commit remain application work.
use super::{Failure, History};
use crate::runtime::Deadline;
use databento::{
    dbn::Encoding,
    historical::{
        batch::{BatchFileDesc, BatchJob, ListJobsParams, SubmitJobParams},
        metadata::GetQueryParams,
    },
};
use sha2::{Digest, Sha256};
use tokio_util::sync::CancellationToken;
/// Evidence returned only after the entire download has passed validation.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Downloaded {
    /// Actual bytes, exactly equal to the provider's file descriptor.
    pub size: u64,
    /// Lowercase SHA-256 hexadecimal digest without algorithm prefix.
    pub sha256: String,
}
impl History {
    async fn batch_json(
        &self,
        method: hyper::Method,
        endpoint: &str,
        form: Vec<(&str, String)>,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<u8>, Failure> {
        self.request(method, endpoint, form, token, deadline, |response| async {
            self.body(response.into_body(), 8 * 1024 * 1024).await
        })
        .await
    }
    /// Submits exactly one charged request using the pinned SDK's full form.
    /// The caller must durably retain the returned job identity before retrying.
    pub async fn submit_batch(
        &self,
        params: &SubmitJobParams,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<BatchJob, Failure> {
        if params.date_time_range.end <= params.date_time_range.start {
            return Err(Failure::Input("Batch date range is empty or reversed"));
        }
        let query = GetQueryParams::builder()
            .dataset(params.dataset.clone())
            .schema(params.schema)
            .symbols(params.symbols.clone())
            .stype_in(params.stype_in)
            .date_time_range(params.date_time_range.clone())
            .maybe_limit(params.limit)
            .build();
        let mut form = Self::form(&query)?;
        form.extend([
            ("encoding", params.encoding.to_string()),
            ("compression", params.compression.to_string()),
            ("pretty_px", params.pretty_px.to_string()),
            ("pretty_ts", params.pretty_ts.to_string()),
            (
                "map_symbols",
                params
                    .map_symbols
                    .unwrap_or(params.encoding != Encoding::Dbn)
                    .to_string(),
            ),
            ("split_symbols", params.split_symbols.to_string()),
            ("delivery", params.delivery.to_string()),
            ("stype_out", params.stype_out.to_string()),
            ("split_duration", params.split_duration.to_string()),
        ]);
        if let Some(size) = params.split_size {
            form.push(("split_size", size.to_string()));
        }
        let body = self
            .batch_json(
                hyper::Method::POST,
                "batch.submit_job",
                form,
                token,
                deadline,
            )
            .await?;
        serde_json::from_slice(&body).map_err(|e| self.provider(e))
    }
    /// Lists bounded job metadata; the same original deadline includes JSON read.
    pub async fn list_batch_jobs(
        &self,
        params: &ListJobsParams,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<BatchJob>, Failure> {
        let mut form = Vec::new();
        if let Some(states) = &params.states {
            if states.len() > 16 {
                return Err(Failure::Input("Too many batch state filters"));
            }
            form.push((
                "states",
                states
                    .iter()
                    .map(|s| s.as_str())
                    .collect::<Vec<_>>()
                    .join(","),
            ));
        }
        if let Some(since) = params.since {
            form.push(("since", since.unix_timestamp_nanos().to_string()));
        }
        let body = self
            .batch_json(hyper::Method::GET, "batch.list_jobs", form, token, deadline)
            .await?;
        serde_json::from_slice(&body).map_err(|e| self.provider(e))
    }
    fn batch_id(job_id: &str) -> Result<Vec<(&'static str, String)>, Failure> {
        if job_id.is_empty() || job_id.len() > 256 || job_id.chars().any(char::is_control) {
            return Err(Failure::Input("Invalid provider batch identity"));
        }
        Ok(vec![("job_id", job_id.into())])
    }
    /// Reuses persisted job identity without submitting another paid request.
    pub async fn batch_details(
        &self,
        job_id: &str,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<BatchJob, Failure> {
        let body = self
            .batch_json(
                hyper::Method::GET,
                "batch.get_job_details",
                Self::batch_id(job_id)?,
                token,
                deadline,
            )
            .await?;
        serde_json::from_slice(&body).map_err(|e| self.provider(e))
    }
    /// Lists source file descriptors, retaining hashes, sizes and all provider URLs.
    pub async fn batch_files(
        &self,
        job_id: &str,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<BatchFileDesc>, Failure> {
        let body = self
            .batch_json(
                hyper::Method::GET,
                "batch.list_files",
                Self::batch_id(job_id)?,
                token,
                deadline,
            )
            .await?;
        serde_json::from_slice(&body).map_err(|e| self.provider(e))
    }
    /// Streams a file through one 65,536-byte scratch buffer. The consumer must
    /// perform bounded synchronous work and keep output provisional until success.
    /// No SDK filesystem task, detached HTTP driver or whole-file allocation runs.
    pub async fn download_batch<P>(
        &self,
        file: &BatchFileDesc,
        token: &CancellationToken,
        deadline: Deadline,
        mut consume: P,
    ) -> Result<Downloaded, Failure>
    where
        P: FnMut(&[u8]) -> Result<(), String>,
    {
        self.download_batch_async(file, token, deadline, |bytes| {
            std::future::ready(consume(bytes))
        })
        .await
    }
    /// Streams verified file bytes to an asynchronous bounded consumer. The
    /// returned future owns any copied chunk; borrowed scratch never escapes.
    /// Network, backpressure and checksum validation share the original deadline.
    pub async fn download_batch_async<P, F>(
        &self,
        file: &BatchFileDesc,
        token: &CancellationToken,
        deadline: Deadline,
        mut consume: P,
    ) -> Result<Downloaded, Failure>
    where
        P: FnMut(&[u8]) -> F,
        F: std::future::Future<Output = Result<(), String>>,
    {
        let url = file
            .urls
            .get("https")
            .ok_or(Failure::Input("Batch file has no HTTPS URL"))?;
        if url.len() > 16384 {
            return Err(Failure::Input("Batch URL exceeds capacity"));
        }
        let target = reqwest::Url::parse(url).map_err(|_| Failure::Input("Invalid batch URL"))?;
        let loopback = target.host_str().is_some_and(|h| {
            h.parse::<std::net::IpAddr>()
                .is_ok_and(|ip| ip.is_loopback())
        });
        if (target.scheme() != "https" && !(target.scheme() == "http" && loopback))
            || !target.username().is_empty()
            || target.password().is_some()
            || target.fragment().is_some()
        {
            return Err(Failure::Input("Unsupported batch URL policy"));
        }
        // Match the pinned SDK's get_with_path(url.path()): download using the
        // configured historical API origin, not the provider descriptor's host.
        // Authentication remains on that trusted origin only. Never forward a
        // key or signed query to an arbitrary descriptor/redirect destination.
        let mut trusted_target = self.base.clone();
        trusted_target.set_path(target.path());
        trusted_target.set_query(None);
        trusted_target.set_fragment(None);
        let target = trusted_target;
        if file.hash.len() > 2048 {
            return Err(Failure::Input("Batch checksum exceeds capacity"));
        }
        let (algorithm, expected) = file
            .hash
            .split_once(':')
            .ok_or(Failure::Input("Invalid batch checksum format"))?;
        let expected = if algorithm == "sha256" {
            if expected.len() != 64 || !expected.bytes().all(|b| b.is_ascii_hexdigit()) {
                return Err(Failure::Input("Invalid batch SHA-256"));
            }
            Some(expected)
        } else {
            // Preserve the pinned SDK's unsupported-algorithm behavior, with a
            // retained warning rather than silently skipping its checksum.
            let mut warnings = self
                .warnings
                .lock()
                .map_err(|_| Failure::Input("Warning owner interrupted"))?;
            if warnings.len() == 256 {
                return Err(Failure::Input("Warning capacity exhausted"));
            }
            warnings.push_back(super::Warning {
                endpoint: "batch.download".into(),
                status: 0,
                request_id: String::new(),
                text: format!(
                    "Skipping provider checksum with unsupported hash algorithm: {algorithm}"
                ),
            });
            None
        };
        self.request_at(
            hyper::Method::GET,
            "batch.download",
            target,
            vec![],
            token,
            deadline,
            |response| async move {
                use futures_util::TryStreamExt;
                use http_body_util::BodyExt;
                use tokio::io::AsyncReadExt;
                if let Some(length) = response.headers().get(hyper::header::CONTENT_LENGTH) {
                    let length = length
                        .to_str()
                        .ok()
                        .and_then(|v| v.parse::<u64>().ok())
                        .ok_or(Failure::Input("Invalid batch content length"))?;
                    if length != file.size {
                        return Err(self.provider("Batch content length differs from descriptor"));
                    }
                }
                let stream = response
                    .into_body()
                    .into_data_stream()
                    .map_err(std::io::Error::other);
                let mut reader = tokio_util::io::StreamReader::new(stream);
                // Keep the fixed buffer on the heap. Embedding 64 KiB in nested
                // async futures can overflow a small caller/executor stack when
                // those futures are moved before their first poll.
                let mut scratch = Vec::new();
                scratch
                    .try_reserve_exact(65536)
                    .map_err(|_| Failure::Input("Batch scratch allocation failed"))?;
                scratch.resize(65536, 0);
                let mut size = 0u64;
                let mut hash = Sha256::new();
                loop {
                    let n = reader
                        .read(&mut scratch)
                        .await
                        .map_err(|e| self.provider(e))?;
                    if n == 0 {
                        break;
                    }
                    size = size
                        .checked_add(n as u64)
                        .ok_or(Failure::Input("Batch byte count overflow"))?;
                    if size > file.size {
                        return Err(self.provider("Batch response exceeds descriptor size"));
                    }
                    hash.update(&scratch[..n]);
                    consume(&scratch[..n]).await.map_err(|error| {
                        Failure::Processing(error.replace(&self.key, "[REDACTED]"))
                    })?;
                    tokio::task::yield_now().await;
                }
                if size != file.size {
                    return Err(self.provider("Batch response is truncated"));
                }
                let actual = format!("{:x}", hash.finalize());
                if expected.is_some_and(|expected| !actual.eq_ignore_ascii_case(expected)) {
                    return Err(self.provider("Batch checksum mismatch"));
                }
                Ok(Downloaded {
                    size,
                    sha256: actual,
                })
            },
        )
        .await
    }
}
