//! Pinned Databento SDK lifecycle adapter used by cancellation qualification.
//!
//! Provider semantics are based on the existing bridge's use of SDK 0.58.0.
//! `subscribe`/`start` are not cancellation safe, so the entire client lives inside
//! the cancellable future. No task keeps an external clone of its socket halves.
//! Production readiness must additionally verify subscription acknowledgements,
//! required observations and workload evidence; reaching `Reading` is not proof.

use crate::dns::{ResolveError, Resolver};
use crate::runtime::{Deadline, Interrupted};
use databento::{LiveClient, dbn::Schema, live::Subscription};
use std::net::SocketAddr;
use tokio::sync::mpsc;
use tokio_util::sync::CancellationToken;

/// Stage reached by an owner, exposed for lifecycle diagnostics and qualification.
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum Stage {
    /// Resolving the gateway using owned async DNS exchanges.
    Resolving,
    /// Connecting or authenticating to the selected gateway.
    Connecting,
    /// Sending the provider subscription request.
    Subscribing,
    /// Waiting for session metadata after the provider start command.
    Starting,
    /// Waiting for or processing provider records.
    Reading,
}

/// Completion of one entire owned provider session.
#[derive(Debug)]
pub enum SessionExit {
    /// Caller cancellation or the original operation deadline ended the owner.
    Interrupted(Interrupted),
    /// The gateway closed the session after all available records were consumed.
    Closed,
    /// The SDK reported a provider/protocol error; diagnostic text is not logged here.
    ProviderError(String),
    /// DNS failed without falling back to the system resolver.
    ResolutionError(ResolveError),
}

/// Gateway hostname and service port, resolved afresh for each session attempt.
pub struct NamedGateway<'a> {
    /// Provider DNS hostname, not a URL.
    pub host: &'a str,
    /// Numeric provider service port.
    pub port: u16,
    /// Explicit async resolver; must outlive the awaited session.
    pub resolver: &'a Resolver,
}

/// Qualifies the production-style hostname path without entering OS DNS.
/// DNS, address fallback, authentication and reads share one original deadline.
/// Resolution completion includes dropping all losing resolver sockets.
pub async fn qualify_named_session(
    gateway: NamedGateway<'_>,
    key: String,
    dataset: String,
    cancellation: CancellationToken,
    deadline: Deadline,
    stages: mpsc::Sender<Stage>,
) -> SessionExit {
    if key.len() != 32 || !key.is_ascii() {
        return SessionExit::ProviderError("Invalid provider credential format".into());
    }
    let _ = stages.try_send(Stage::Resolving);
    let addresses = match gateway
        .resolver
        .resolve(gateway.host, gateway.port, &cancellation, deadline)
        .await
    {
        Ok(addresses) => addresses,
        Err(ResolveError::Interrupted(reason)) => return SessionExit::Interrupted(reason),
        Err(error) => return SessionExit::ResolutionError(error),
    };
    qualify_addresses(addresses, key, dataset, cancellation, deadline, stages).await
}

/// Owns a qualification session until EOF, error, cancellation or deadline.
///
/// This is a lifecycle probe, not the production price/publication implementation.
/// The explicit gateway address avoids real network/provider credentials in tests.
/// Stage delivery is bounded and nonblocking so diagnostics cannot prevent cleanup.
/// The API key is never emitted in stage/completion messages.
///
/// All SDK resources are created within `session`, below. Dropping that future
/// closes both SDK-owned socket halves, including on partially completed auth,
/// subscription/start, or partial DBN decoding. Await this function before claiming
/// completion; signalling its token alone is not completion evidence.
pub async fn qualify_owned_session(
    gateway: SocketAddr,
    key: String,
    dataset: String,
    cancellation: CancellationToken,
    deadline: Deadline,
    stages: mpsc::Sender<Stage>,
) -> SessionExit {
    qualify_addresses(vec![gateway], key, dataset, cancellation, deadline, stages).await
}

async fn qualify_addresses(
    addresses: Vec<SocketAddr>,
    key: String,
    dataset: String,
    cancellation: CancellationToken,
    deadline: Deadline,
    stages: mpsc::Sender<Stage>,
) -> SessionExit {
    // SDK validation logs rejected key contents. Reject invalid formatting here
    // with a generic message before handing credentials to provider code.
    if key.len() != 32 || !key.is_ascii() {
        return SessionExit::ProviderError("Invalid provider credential format".into());
    }
    let session = async move {
        let _ = stages.try_send(Stage::Connecting);
        let mut client = LiveClient::builder()
            // Numeric slice ToSocketAddrs is immediate: no lookup_host hostname
            // branch and no spawn_blocking system resolver job can be reached.
            .addr(addresses.as_slice())
            .await?
            .key(key)?
            .dataset(dataset)
            // The owner deadline is authoritative. Do not allow SDK default
            // connect/auth timers to terminate qualification after 10/30 seconds.
            .timeout_conf(databento::live::TimeoutConf {
                connect: None,
                auth: None,
            })
            .build()
            .await?;
        let _ = stages.try_send(Stage::Subscribing);
        client
            .subscribe(
                Subscription::builder()
                    .symbols("ES.c.0")
                    .schema(Schema::Trades)
                    .stype_in(databento::dbn::SType::Continuous)
                    .build(),
            )
            .await?;
        let _ = stages.try_send(Stage::Starting);
        client
            .start_with_metadata_decoder(super::metadata::live_decoder)
            .await?;
        let _ = stages.try_send(Stage::Reading);
        while client.next_record().await?.is_some() {
            // The lifecycle probe does not publish data. Bounded decode iterations
            // must yield even when the local socket buffer is continuously ready.
            tokio::task::yield_now().await;
        }
        Ok::<_, databento::Error>(())
    };

    match deadline.run(&cancellation, session).await {
        Err(reason) => SessionExit::Interrupted(reason),
        Ok(Ok(())) => SessionExit::Closed,
        Ok(Err(error)) => SessionExit::ProviderError(error.to_string()),
    }
}
