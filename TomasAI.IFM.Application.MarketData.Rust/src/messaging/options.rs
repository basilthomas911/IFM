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
        if registrations.len() > crate::option_chain::MAX_OPTION_REGISTRATIONS {
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
            if entries.len() > crate::option_chain::MAX_LIVE_OPTIONS {
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

impl Options{
    pub(crate) fn checkpoint(&self)->rmpv::Value{
        use rmpv::Value as V;V::Array(vec![1.into(),crate::checkpoint::day(self.date),self.revisions.0.load(Ordering::Acquire).into(),V::Array(self.entries.iter().map(|(id,e)|V::Array(vec![(*id).into(),e.instrument.capture_identity(),crate::checkpoint::wide(e.physical),e.fingerprint.map_or(V::Nil,|v|V::Binary(v.to_vec())),e.raw.as_ref().map_or(V::Nil,|(bytes,at)|V::Array(vec![V::Binary(bytes.clone()),(at.elapsed().as_nanos().min(u64::MAX as u128) as u64).into()]))])).collect())])
    }
    pub(crate) fn restore_checkpoint(value:&rmpv::Value)->Result<Self,String>{
        use crate::checkpoint as c;let f=c::fields(value,4)?;if c::unsigned(&f[0])?!=1{return Err("Option mapper checkpoint version".into());}let mut entries=BTreeMap::new();let rows=f[3].as_array().filter(|v|v.len()<=crate::option_chain::MAX_LIVE_OPTIONS).ok_or("Option mapper count exceeded")?;
        for row in rows{let e=c::fields(row,5)?;let id=c::unsigned(&e[0])?.try_into().map_err(|_|"Option instrument overflow")?;let instrument=Instrument::restore_identity(&e[1])?;let fingerprint=if e[3].is_nil(){None}else{match &e[3]{rmpv::Value::Binary(b) if b.len()==32=>Some(b.as_slice().try_into().unwrap()),_=>return Err("Option fingerprint invalid".into())}};
            let raw=if e[4].is_nil(){None}else{let r=c::fields(&e[4],2)?;let bytes=r[0].as_slice().filter(|b|b.len()<=1024*1024).ok_or("Option retained raw bytes invalid")?.to_vec();let at=tokio::time::Instant::now().checked_sub(std::time::Duration::from_nanos(c::unsigned(&r[1])?)).ok_or("Option raw age invalid")?;Some((bytes,at))};
            if entries.insert(id,Entry{instrument,physical:c::read_wide(&e[2])?,fingerprint,raw}).is_some(){return Err("Duplicate option mapper identity".into());}
        }
        let revision=c::number(&f[2])?;if revision<0{return Err("Negative option revision".into());}Ok(Self{date:c::date(&f[1])?,entries,revisions:Arc::new(Revisions(AtomicI64::new(revision)))})
    }
}
