//! Native authoritative option-trade ingress. Ordered source contributions are
//! consumed into copied persistence evidence without a managed delivery loop.
//! Quote/statistics/replay processing stays with the native book; it never
//! fabricates an actual trade or overwrites an accepted evidence attempt.
use super::{
    codec,
    evidence_outbox::{Outbox, Record},
    option_pricing,
    option_source::Source,
};
use crate::{
    dataset::{
        live::{Output, OutputSource, Registration},
        records::Observation,
        transport::Lane,
    },
    latest::Asset,
    reference::decimal,
    runtime::Deadline,
};
use std::{collections::BTreeMap, sync::Arc};
use tokio_util::sync::CancellationToken;

/// Immutable identity mappings for one native logical options generation.
/// This assembler stores no connection, lease, task handle or foreign callback.
pub struct Ingress {
    date: time::Date,
    generation: [u8; 16],
    registrations: BTreeMap<u32, Registration>,
}
impl Ingress {
    /// Validates the complete provider/canonical identity mapping before ingress.
    ///
    /// # Errors
    /// Rejects unbounded, duplicate or unqualified identity and empty generation.
    pub fn new(
        date: time::Date,
        generation: [u8; 16],
        registrations: Vec<Registration>,
    ) -> Result<Self, &'static str> {
        if !(1..=9999).contains(&date.year())
            || generation == [0; 16]
            || registrations.is_empty()
            || registrations.len() > 2112
        {
            return Err("Invalid native option ingress identity");
        }
        let mut mappings = BTreeMap::new();
        for r in registrations {
            if r.instrument == 0
                || r.publisher == 0
                || r.contract.trim().is_empty()
                || r.contract.len() > 1024
                || r.source.is_empty()
                || r.source.len() > 4096
            {
                return Err("Unqualified native option ingress registration");
            }
            if mappings.insert(r.instrument, r).is_some() {
                return Err("Duplicate native option ingress registration");
            }
        }
        Ok(Self {
            date,
            generation,
            registrations: mappings,
        })
    }
    /// Prices and retains one actual source-time trade. Returns None for other
    /// source contributions and trade replay, which already updated native books.
    /// The returned record owns bytes only; external storage cannot pin a feed.
    /// No current context, quote or API wall clock is substituted after ingestion.
    ///
    /// # Errors
    /// Missing frozen metadata, source mismatch, arithmetic, cancellation or
    /// evidence capacity failure must be reported to dataset recovery by caller.
    pub async fn retain(
        &self,
        output: &Output,
        outbox: &Outbox,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Option<Arc<Record>>, &'static str> {
        if output.lane != Lane::OptionsGlbx {
            return Err("Native option ingress received another dataset lane");
        }
        let OutputSource::Observation(Observation::Trade(trade)) = output.source else {
            return Ok(None);
        };
        let header = trade.header;
        let registration = self
            .registrations
            .get(&header.instrument)
            .ok_or("Unregistered native option source")?;
        if registration.asset != Asset::Option {
            return Ok(None);
        }
        if header.flags & 2 != 0 {
            return Ok(None);
        }
        if header.publisher != registration.publisher
            || output.generation == 0
            || header.event_ns < 0
            || header.received_ns < 0
        {
            return Err("Frozen option trade identity does not match source record");
        }
        let captured = output
            .trade
            .as_ref()
            .ok_or("Actual option trade has no captured reference metadata")?;
        let context = captured
            .prepared
            .reference
            .as_ref()
            .ok_or("Actual option trade has no captured native reference metadata")?;
        let contract = context.convention();
        if contract.dataset() != "GLBX.MDP3"
            || contract.publisher() != header.publisher
            || contract.instrument() != header.instrument
            || contract.contract() != registration.contract
            || context.generation() != self.generation
        {
            return Err("Frozen option trade identity does not match source reference");
        }
        // Decimal parsing is of our own bounded exact nanounit formatting, never
        // a provider floating point conversion or untrusted length prefix.
        let value = codec::price(trade.price);
        let price = decimal::parse(value.as_str().ok_or("Invalid native trade decimal")?)
            .ok_or("Invalid native trade price")?;
        let source = Source {
            dataset: "GLBX.MDP3".into(),
            publisher: header.publisher,
            instrument: header.instrument,
            contract: registration.contract.clone(),
            date: self.date,
            price,
            size: trade.size,
            sequence: i64::from(header.sequence),
            event: header.event_ns,
            received: header.received_ns,
            generation: self.generation,
            symbol: contract.raw_symbol().into(),
        };
        let evidence = option_pricing::calculate(source, captured, stop, deadline).await?;
        Ok(Some(outbox.retain(&evidence)?))
    }
}
