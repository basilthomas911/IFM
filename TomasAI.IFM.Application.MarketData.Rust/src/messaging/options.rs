//! Bounded native option-observation assembly from the native pricing cache.
//! Reference and quote ownership remain native; only detached event bytes leave
//! this module. Deduplication retains fixed-size fingerprints, not object graphs.
use super::{
    codec,
    events::{Identity, Instrument},
    futures::Event,
    option_observation,
};
use crate::{
    dataset::{live::Registration, transport::Lane},
    latest::Asset,
    option_chain::coalesced::Coalesced,
};
use sha2::{Digest, Sha256};
use std::{
    collections::BTreeMap,
    sync::{
        Arc,
        atomic::{AtomicI64, Ordering},
    },
};
/// One shared logical-generation observation revision across scopes and refreshes.
#[derive(Default)]
pub struct Revisions(AtomicI64);
impl Revisions {
    fn next(&self) -> Result<i64, &'static str> {
        self.0
            .fetch_update(Ordering::Relaxed, Ordering::Relaxed, |n| n.checked_add(1))
            .map(|n| n + 1)
            .map_err(|_| "Native option observation revision exhausted")
    }
}
struct Entry {
    instrument: Instrument,
    physical: u128,
    fingerprint: Option<[u8; 32]>,
    raw: Option<(Vec<u8>, tokio::time::Instant)>,
}
/// Instrument-local fingerprints and shared logical-generation revisions.
pub struct Options {
    date: time::Date,
    entries: BTreeMap<u32, Entry>,
    revisions: Arc<Revisions>,
}
impl Options {
    /// Validates exact provider/canonical option identities before publication.
    /// Supply individually monitored option registrations only; background
    /// candidate pricing does not authorize an individual actor publication.
    /// Futures dependencies are deliberately absent from the option-event map.
    ///
    /// # Errors
    /// Invalid, duplicate or excessive registrations fail before state mutation.
    pub fn new(
        date: time::Date,
        definition: time::Date,
        registrations: Vec<Registration>,
        revisions: Arc<Revisions>,
    ) -> Result<Self, &'static str> {
        if registrations.len() > 2112 {
            return Err("Invalid native option publication capacity");
        }
        let mut seen = std::collections::BTreeSet::new();
        let mut entries = BTreeMap::new();
        for r in registrations {
            if !seen.insert(r.instrument) {
                return Err("Duplicate option publication registration");
            }
            if r.asset != Asset::Option {
                continue;
            }
            let id = r.instrument;
            let instrument = Instrument::new(r, Lane::OptionsGlbx, date, definition)?;
            entries.insert(
                id,
                Entry {
                    instrument,
                    physical: 0,
                    fingerprint: None,
                    raw: None,
                },
            );
            if entries.len() > 2048 {
                return Err("Native option publication capacity exhausted");
            }
        }
        Ok(Self {
            date,
            entries,
            revisions,
        })
    }
    /// Captures a current native risk observation and creates its existing actor
    /// event. Superseded or absent preparation cannot certify another generation.
    /// Repeated unchanged cache reads emit nothing and consume no new revision.
    /// Caller admits the returned event before calling again; failed admission
    /// requires dataset recovery, rather than continuing with a lost event.
    ///
    /// # Errors
    /// Unknown instrument, malformed frozen inputs or revision overflow must be
    /// reported to native recovery; no substitute quote or zero risk is invented.
    #[allow(clippy::too_many_arguments)]
    pub fn quote(
        &mut self,
        engine: &Coalesced,
        instrument: u32,
        physical: u128,
        at: i64,
        pricing_update: bool,
        identity: Identity,
    ) -> Result<Option<Event>, &'static str> {
        let entry = self
            .entries
            .get_mut(&instrument)
            .ok_or("Unknown native option publication instrument")?;
        if physical == 0 {
            return Err("Absent native option physical generation");
        }
        let Some(prepared) = engine.trade_context(instrument, physical)? else {
            return Ok(None);
        };
        let Some(context) = prepared.reference else {
            return Ok(None);
        };
        let Some(calculation) = engine.completed_risk(instrument, at)? else {
            return Ok(None);
        };
        if calculation.source_generation != physical
            || !engine.matches_completed_risk(instrument, physical, &context, &calculation)?
        {
            return Ok(None);
        }
        let Some(mut snapshot) = option_observation::snapshot(
            &context,
            &prepared.context,
            &calculation,
            self.date,
            at,
            1,
            pricing_update,
        )?
        else {
            return Ok(None);
        };
        let fingerprint: [u8; 32] = Sha256::digest(codec::encode(&snapshot)?).into();
        if entry.physical == physical && entry.fingerprint == Some(fingerprint) {
            return Ok(None);
        }
        let now = tokio::time::Instant::now();
        let raw = codec::encode(&rmpv::Value::Array(vec![
            codec::decimal_bits(calculation.option.bid)?,
            codec::decimal_bits(calculation.option.ask)?,
            codec::decimal_bits(calculation.option.bid_size)?,
            codec::decimal_bits(calculation.option.ask_size)?,
        ]))?;
        if !pricing_update
            && entry.physical == physical
            && entry.raw.as_ref().is_some_and(|(previous, at)| {
                *previous == raw && now.duration_since(*at) < std::time::Duration::from_secs(1)
            })
        {
            return Ok(None);
        }
        let revision = self.revisions.next()?;
        let rmpv::Value::Array(ref mut fields) = snapshot else {
            return Err("Invalid native option observation shape");
        };
        fields[22] = revision.into();
        let contract = context.convention().value();
        let underlying = contract
            .as_array()
            .and_then(|v| v.get(9))
            .and_then(rmpv::Value::as_str)
            .ok_or("Missing native underlying contract identity")?;
        let event =
            entry
                .instrument
                .option_quote(calculation.option, snapshot, underlying, identity)?;
        if !engine.matches_completed_risk(instrument, physical, &context, &calculation)? {
            return Ok(None);
        }
        if !pricing_update {
            entry.raw = Some((raw, now));
        }
        entry.physical = physical;
        entry.fingerprint = Some(fingerprint);
        Ok(Some(Event {
            kind: 7,
            value: event,
        }))
    }
    /// Bounded registered option IDs for fair cooperative native sweeps.
    pub fn instruments(&self) -> impl Iterator<Item = u32> + '_ {
        self.entries.keys().copied()
    }
    /// Invalidates old publication fingerprints after physical retirement.
    pub fn retire(&mut self) {
        for entry in self.entries.values_mut() {
            entry.physical = 0;
            entry.fingerprint = None;
            entry.raw = None;
        }
    }
}
