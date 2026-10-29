//! Copies a complete native option scope without exporting its physical owner.
//! Source generation and immutable reference identities are checked again after
//! the copy, so a concurrent reconnect/refresh cannot produce a mixed snapshot.
use super::{
    capture::{Failure, Page},
    model::Instrument,
};
use crate::{
    dataset::{live::LiveData, transport::Lane},
    messaging::option_evidence::Quote,
    option_chain::{
        coalesced::{Coalesced, underlying_quote},
        preparation::ReviewedScope,
        selection::Selection,
    },
};
fn failure(code: &str) -> Failure {
    Failure {
        code: code.into(),
        input: "Snapshot".into(),
    }
}
/// Borrowed immutable definition/policy fields only. No reference acquisition
/// service, provider socket, native child, lease or cancellation owner is present.
pub struct Metadata<'a> {
    /// Complete reviewed source membership for this numerical view.
    pub definitions: &'a [crate::option_chain::preparation::Definition],
    /// Reviewed selection provenance policy.
    pub policy_version: &'a str,
    /// Reviewed IV refresh cadence for provenance.
    pub iv_ms: u32,
}
/// Reads detached observations for all reviewed definitions, including currently
/// unquoted candidates. The caller applies strict/browsing qualification using
/// `capture`; absence never fabricates an option price or Greek.
///
/// # Errors
/// Generation/refresh races and incomplete native membership fail explicitly.
/// Nothing returned retains LiveData, a dataset child, task, lease or connection.
pub fn read(
    data: &LiveData,
    engine: &Coalesced,
    reviewed: &ReviewedScope,
    generation: [u8; 16],
    token: &str,
    at: i64,
) -> Result<Page, Failure> {
    let query = data
        .queries(Lane::OptionsGlbx)
        .map_err(|_| failure("Recovering"))?
        .ok_or_else(|| failure("Recovering"))?;
    read_queries(
        &query,
        engine,
        Metadata {
            definitions: &reviewed.definitions,
            policy_version: &reviewed.policy_version,
            iv_ms: reviewed.iv_ms,
        },
        generation,
        token,
        at,
    )
}
/// Copies coherent sources from numeric query data without retaining LiveData.
///
/// # Errors
/// Generation/reference races, unavailable metadata or incomplete membership.
pub fn read_queries(
    data: &crate::dataset::live::Queries,
    engine: &Coalesced,
    metadata: Metadata<'_>,
    generation: [u8; 16],
    token: &str,
    at: i64,
) -> Result<Page, Failure> {
    if generation == [0; 16]
        || token.trim().is_empty()
        || token.encode_utf16().count() > 128
        || metadata.definitions.len() > 2048
    {
        return Err(failure("InvalidSnapshotRequest"));
    }
    let physical = data
        .generation()
        .map_err(|_| failure("Recovering"))?
        .ok_or_else(|| failure("Recovering"))?;
    let identities = engine
        .context_identities()
        .map_err(|_| failure("Recovering"))?;
    let mut items = Vec::with_capacity(metadata.definitions.len());
    for definition in metadata.definitions {
        let retained = engine
            .trade_context(definition.instrument, physical)
            .map_err(|_| failure("Recovering"))?
            .ok_or_else(|| failure("IncompleteChain"))?;
        let context = retained
            .reference
            .ok_or_else(|| failure("ContractMetadataUnavailable"))?;
        if context.generation() != generation
            || identities.get(&definition.instrument) != Some(&retained.context.identity)
        {
            return Err(failure("Recovering"));
        }
        let copy_quote =
            |instrument| -> Result<Option<crate::option_chain::qualification::Quote>, Failure> {
                Ok(data
                    .raw_prices(instrument)
                    .map_err(|_| failure("Recovering"))?
                    .and_then(|prices| prices.quote)
                    .and_then(|tick| underlying_quote(tick, u128::from_le_bytes(generation))))
            };
        let raw = copy_quote(definition.instrument)?;
        let underlying = copy_quote(definition.underlying)?;
        let mut selection = None;
        if let Some(calculation) = engine
            .read(definition.instrument, false, at)
            .map_err(|_| failure("Recovering"))?
            && raw == Some(calculation.option)
            && underlying == Some(calculation.underlying)
            && engine
                .matches_observation(definition.instrument, physical, &context, &calculation)
                .map_err(|_| failure("Recovering"))?
        {
            selection = Selection::from_calculation(
                &context,
                &calculation,
                metadata.policy_version,
                metadata.iv_ms,
            )
            .map_err(|_| failure("SelectionCalculationUnavailable"))?;
        }
        let state = data
            .publication_state(definition.instrument)
            .map_err(|_| failure("Recovering"))?
            .filter(|(id, _)| *id == physical)
            .ok_or_else(|| failure("Recovering"))?
            .1
            .option_statistics;
        let day = |date: time::Date| date.to_julian_day() - 1721426;
        items.push(Instrument {
            contract: context.convention().contract().into(),
            quote: raw.map(|quote| Quote {
                contract: context.convention().contract().into(),
                quote,
            }),
            underlying: underlying.map(|quote| Quote {
                contract: context.convention().underlying().into(),
                quote,
            }),
            context: Some(context),
            strike: Some(definition.strike),
            call: Some(definition.call),
            future: None,
            selection,
            volume: state.and_then(|s| s.volume),
            interest: state.and_then(|s| s.open_interest),
            statistics_at: state.and_then(|s| s.statistics_at),
            volume_date: state.and_then(|s| s.volume_date).map(day),
            interest_date: state.and_then(|s| s.open_interest_date).map(day),
            unavailable: None,
            last_event: None,
            last_received: None,
        });
    }
    if data.generation().map_err(|_| failure("Recovering"))? != Some(physical)
        || engine
            .context_identities()
            .map_err(|_| failure("Recovering"))?
            != identities
    {
        return Err(failure("Recovering"));
    }
    Ok(Page {
        generation,
        token: token.into(),
        total: items.len(),
        instruments: items,
        continuation: None,
    })
}
