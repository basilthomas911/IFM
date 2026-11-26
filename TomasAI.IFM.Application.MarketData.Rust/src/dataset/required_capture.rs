//! A bounded durable low-volume source intent, preceding official/control mapping.
//! Original-only recovery is an explicit gap; frozen wire can be imported verbatim.
use sha2::{Digest, Sha256};
use std::{fs::{File, OpenOptions}, io::{Read, Write, Seek, SeekFrom}, path::Path};
const HEADER: &[u8; 8] = b"IFMREQ01";
const MAX_RECORD: usize = 64 * 1024 * 1024;
const MAX_JOURNAL: u64 = 192 * 1024 * 1024;

/// Exact pending source evidence and optionally frozen final publication bytes.
/// The containing encoder owns this file exclusively across preparation/capture.
pub(crate) struct RequiredCaptureIntent {
    file: File,
    original: Option<Vec<u8>>,
    frozen: Option<Vec<u8>>,
    poisoned: bool,
    bytes: u64,
}
impl RequiredCaptureIntent {
    pub(crate) fn checkpoint(&self)->Result<rmpv::Value,String>{
        if self.poisoned{return Err("Uncertain required source intent cannot produce a healthy checkpoint".into());}
        Ok(rmpv::Value::Array(vec![1.into(),self.original.clone().map_or(rmpv::Value::Nil,rmpv::Value::Binary),self.frozen.clone().map_or(rmpv::Value::Nil,rmpv::Value::Binary)]))
    }
    pub(crate) fn import_checkpoint(&mut self,value:&rmpv::Value)->Result<(),String>{
        let f=crate::checkpoint::fields(value,3)?;if crate::checkpoint::unsigned(&f[0])?!=1{return Err("Unknown required source checkpoint version".into());}
        let bytes=|value:&rmpv::Value|match value{rmpv::Value::Nil=>Ok(None),rmpv::Value::Binary(bytes) if !bytes.is_empty()&&bytes.len()<MAX_RECORD=>Ok(Some(bytes.clone())),_=>Err("Required checkpoint evidence exceeds bound".to_owned())};
        let original=bytes(&f[1])?;let frozen=bytes(&f[2])?;
        if frozen.is_some()&&original.is_none(){return Err("Required checkpoint frozen wire has no original".into());}
        if self.original.is_some()||self.frozen.is_some(){if self.original==original&&self.frozen==frozen{return Ok(());}return Err("Required checkpoint conflicts with current original intent".into());}
        if let Some(original)=original{self.begin(&original)?;if let Some(frozen)=frozen{self.freeze(&frozen)?;}}Ok(())
    }
    pub(crate) fn open(root: &Path, owner: &str) -> Result<Self, String> {
        if owner.is_empty() || owner.len() > 160 || !owner.bytes().all(|b| b.is_ascii_alphanumeric() || b == b'-') {
            return Err("Invalid required source intent owner".into());
        }
        std::fs::create_dir_all(root).map_err(|e| format!("Required source intent directory: {e}"))?;
        let mut file = OpenOptions::new().read(true).write(true).create(true).truncate(false)
            .open(root.join(format!("required-{owner}.intent"))).map_err(|e| e.to_string())?;
        file.try_lock().map_err(|e| format!("Required source intent already owned: {e}"))?;
        let bytes = file.metadata().map_err(|e| e.to_string())?.len();
        if bytes > MAX_JOURNAL { return Err("Required source intent exceeds durable bound".into()); }
        if bytes == 0 { file.write_all(HEADER).and_then(|_| file.sync_all()).map_err(|e| e.to_string())?; }
        file.seek(SeekFrom::Start(0)).map_err(|e| e.to_string())?;
        let mut all = Vec::new(); file.read_to_end(&mut all).map_err(|e| e.to_string())?;
        if all.get(..8) != Some(HEADER.as_slice()) { return Err("Required source intent header is damaged".into()); }
        let mut result = Self { file, original: None, frozen: None, poisoned: false, bytes: all.len() as u64 };
        let mut position = 8;
        while position < all.len() {
            let length = all.get(position..position + 4).ok_or("Required source intent contains a torn length")?;
            let length = u32::from_le_bytes(length.try_into().unwrap()) as usize;
            if !(1..=MAX_RECORD).contains(&length) { return Err("Required source intent record exceeds bound".into()); }
            position += 4;
            let body = all.get(position..position + length).ok_or("Required source intent contains a torn record")?;
            position += length;
            let hash = all.get(position..position + 32).ok_or("Required source intent contains a torn proof")?;
            if Sha256::digest(body).as_slice() != hash { return Err("Required source intent checksum mismatch".into()); }
            result.apply(body[0], &body[1..])?;
            position += 32;
        }
        Ok(result)
    }
    fn apply(&mut self, phase: u8, bytes: &[u8]) -> Result<(), String> {
        match phase {
            1 if self.original.is_none() && !bytes.is_empty() => { self.original = Some(bytes.to_vec()); self.frozen = None; }
            2 if self.original.is_some() && self.frozen.is_none() => self.frozen = Some(bytes.to_vec()),
            3 if self.frozen.is_some() && bytes.is_empty() => { self.original = None; self.frozen = None; }
            _ => return Err("Required source intent has an invalid phase transition".into()),
        }
        Ok(())
    }
    fn append(&mut self, phase: u8, bytes: &[u8]) -> Result<(), String> {
        if self.poisoned { return Err("Required source intent has an uncertain write; cold reconciliation is required".into()); }
        if bytes.len() + 1 > MAX_RECORD || self.bytes + bytes.len() as u64 + 37 > MAX_JOURNAL {
            return Err("Required source intent capacity exhausted; original source was not evicted".into());
        }
        let mut body = Vec::with_capacity(bytes.len() + 1); body.push(phase); body.extend_from_slice(bytes);
        let length = (body.len() as u32).to_le_bytes();
        let result = self.file.seek(SeekFrom::End(0)).and_then(|_| self.file.write_all(&length)).and_then(|_| self.file.write_all(&body))
            .and_then(|_| self.file.write_all(&Sha256::digest(&body))).and_then(|_| self.file.sync_all());
        if let Err(error) = result { self.poisoned = true; return Err(format!("Required source intent write is uncertain: {error}")); }
        self.bytes += body.len() as u64 + 36;
        self.apply(phase, bytes)
    }
    /// Called before mapping. An unresolved different input can never be overwritten.
    pub(crate) fn begin(&mut self, original: &[u8]) -> Result<(), String> {
        if self.poisoned { return Err("Required source intent write remains uncertain".into()); }
        if let Some(prior) = &self.original {
            return if prior == original { Ok(()) } else { Err("Prior required source intent remains unresolved".into()) };
        }
        if original.is_empty() { return Err("Required source intent needs authentic original evidence".into()); }
        self.append(1, original)
    }
    /// Saves exact wire, original origin metadata and IDs before source capture.
    pub(crate) fn freeze(&mut self, wire: &[u8]) -> Result<(), String> {
        if self.poisoned { return Err("Required source intent write remains uncertain".into()); }
        if let Some(prior) = &self.frozen {
            return if prior == wire { Ok(()) } else { Err("Required source frozen publication changed".into()) };
        }
        if self.original.is_none() { return Err("Required source must begin before freezing wire".into()); }
        self.append(2, wire)
    }
    /// Call only after existing source-capture ownership is durably confirmed.
    pub(crate) fn complete(&mut self) -> Result<(), String> {
        if self.poisoned { return Err("Required source intent write remains uncertain".into()); }
        if self.original.is_none() { return Ok(()); }
        if self.frozen.is_none() { return Err("Original-only required source intent needs authentic gap reconciliation".into()); }
        self.append(3, &[])?;
        if self.bytes > 8 * 1024 * 1024 {
            // A durable complete proof precedes reclamation; pending evidence
            // is never truncated. Interrupted header replacement fails closed.
            if let Err(error) = self.file.set_len(0).and_then(|_| self.file.seek(SeekFrom::Start(0))).and_then(|_| self.file.write_all(HEADER)).and_then(|_| self.file.sync_all()) {
                self.poisoned = true; return Err(format!("Completed required intent compaction uncertain: {error}"));
            }
            self.bytes = 8;
        }
        Ok(())
    }
    pub(crate) fn original(&self) -> Option<&[u8]> { self.original.as_deref() }
    pub(crate) fn frozen(&self) -> Option<&[u8]> { self.frozen.as_deref() }
}

/// Structured source-time evidence, preceding mapper mutation. A pre-map cold
/// interruption is an explicit gap, never fabricated replacement event IDs.
pub(crate) fn encode_original(outputs: &[super::live::Output]) -> Result<Vec<u8>, String> {
    use super::{live::OutputSource, records::Observation, book::Update};
    use rmpv::Value as V;
    fn a(items: Vec<V>) -> V { V::Array(items) }
    fn bin(value: u128) -> V { V::Binary(value.to_le_bytes().to_vec()) }
    fn snapshot(s: crate::aggregation::session::Snapshot) -> V { a(vec![s.value_date.to_julian_day().into(),s.open.into(),s.high.into(),s.low.into(),s.sequence.into(),s.event_ns.into(),s.volume.into(),(s.quality as u8).into()]) }
    fn header(h: super::records::Header) -> V { a(vec![h.instrument.into(),h.publisher.into(),h.flags.into(),h.event_ns.into(),h.received_ns.into(),h.sequence.into(),h.schema.into()]) }
    if outputs.is_empty() || outputs.len() > 16_385 { return Err("Original required output prefix exceeds bound".into()); }
    let mut rows = Vec::with_capacity(outputs.len());
    for o in outputs {
        if o.capture_serial == 0 || o.generation == 0 || o.trade.is_some() || o.state.option_statistics.is_some() { return Err("Required futures intent has unqualified identity or option-only inputs".into()); }
        let source = match o.source {
            OutputSource::ReplayComplete(id) => a(vec![0.into(),id.into()]),
            OutputSource::StatisticsReplayComplete(id) => a(vec![1.into(),id.into()]),
            OutputSource::Observation(Observation::Trade(t)) => a(vec![2.into(),header(t.header),t.price.into(),t.size.into(),t.action.into(),t.side.into(),t.dbn_flags.into(),t.depth.into(),t.delta_ns.into()]),
            OutputSource::Observation(Observation::Quote(q)) => a(vec![3.into(),header(q.header),q.bid.into(),q.ask.into(),q.bid_size.into(),q.ask_size.into(),q.bid_count.into(),q.ask_count.into()]),
            OutputSource::Observation(Observation::Statistic(s)) => a(vec![4.into(),header(s.header),s.price.into(),s.quantity.into(),s.reference_ns.into(),s.kind.into(),s.channel.into(),s.action.into(),s.flags.into()]),
            OutputSource::Observation(Observation::Order(s)) => a(vec![5.into(),header(s.header),s.order_id.into(),s.price.into(),s.size.into(),s.action.into(),s.side.into(),s.dbn_flags.into(),s.channel.into(),s.delta_ns.into()]),
        };
        let update = match o.state.update { None => V::Nil, Some(Update::Unchanged) => a(vec![0.into()]), Some(Update::Quote) => a(vec![1.into()]), Some(Update::Trade{price_changed}) => a(vec![2.into(),price_changed.into()]), Some(Update::Statistics(s)) => a(vec![3.into(),snapshot(s)]) };
        let market = o.state.market.map_or(V::Nil, |s| {
            let quote = s.quote.map_or(V::Nil, |q| a(vec![q.sequence.into(),q.event_ticks.into(),q.receive_ticks.into(),q.value.bid.map_or(V::Nil,V::from),q.value.ask.map_or(V::Nil,V::from),q.value.bid_size.into(),q.value.ask_size.into(),q.value.bid_count.into(),q.value.ask_count.into(),q.value.local_received_ticks.into()]));
            let trade = s.trade.map_or(V::Nil, |t| a(vec![t.sequence.into(),t.event_ticks.into(),t.receive_ticks.into(),t.value.value.price.into(),t.value.value.size.into(),t.value.action.into(),t.value.side.into(),t.value.conditions.into(),bin(t.value.epoch),t.value.ordinal.into()]));
            a(vec![s.date.to_julian_day().into(),quote,trade])
        });
        let vwap = o.state.vwap.map_or(V::Nil, |v| a(vec![bin(v.epoch),v.ordinal.into(),v.sequence.into(),v.at.into(),bin(v.price_volume.coefficient),v.price_volume.scale.into(),v.volume.into(),v.eligible.into(),v.rejected.into(),bin(v.last_price.coefficient),v.last_price.scale.into(),v.valid.into(),v.replay_complete.into(),bin(v.generation),(v.reason as u8).into()]));
        let raw = o.raw.map_or(V::Nil, |r| a(vec![V::Binary(r.identity.event.to_vec()),V::Binary(r.identity.command.to_vec()),r.identity.observed.into(),r.sequence.into()]));
        rows.push(a(vec![(o.lane as u8).into(),bin(o.generation),o.capture_serial.into(),o.observed.into(),source,update,market,o.state.session.map_or(V::Nil,snapshot),vwap,raw]));
    }
    let encoded = crate::messaging::codec::encode(&a(vec![1.into(),a(rows)]))?;
    if encoded.len() + 1 > MAX_RECORD { return Err("Required original evidence exceeds bounded record".into()); }
    Ok(encoded)
}

#[cfg(test)]
mod tests {
    use super::*;
    fn root() -> std::path::PathBuf {
        std::env::temp_dir().join(format!("ifm-required-intent-{:032x}", rand::random::<u128>()))
    }
    #[test]
    fn cold_original_only_intent_blocks_completion_and_input_replacement() {
        let root = root();
        { let mut intent = RequiredCaptureIntent::open(&root, "es-owner").unwrap(); intent.begin(b"original official input and frozen source-time state").unwrap(); }
        let mut cold = RequiredCaptureIntent::open(&root, "es-owner").unwrap();
        assert!(cold.original().is_some()); assert!(cold.frozen().is_none());
        assert!(cold.complete().is_err()); assert!(cold.begin(b"replacement current input").is_err());
        drop(cold); std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn cold_frozen_intent_reuses_original_wire_and_completion_is_durable() {
        let root = root(); let original = b"original generation/serial/observation"; let frozen = b"exact original wire IDs origin and ordering";
        { let mut intent = RequiredCaptureIntent::open(&root, "es-owner").unwrap(); intent.begin(original).unwrap(); intent.freeze(frozen).unwrap(); }
        { let mut cold = RequiredCaptureIntent::open(&root, "es-owner").unwrap();
            assert_eq!(cold.original(), Some(original.as_slice())); assert_eq!(cold.frozen(), Some(frozen.as_slice()));
            cold.begin(original).unwrap(); cold.freeze(frozen).unwrap(); assert!(cold.freeze(b"restamped").is_err()); cold.complete().unwrap(); }
        let cold = RequiredCaptureIntent::open(&root, "es-owner").unwrap(); assert!(cold.original().is_none());
        drop(cold); std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn completion_reclaims_only_completed_history_and_preserves_next_pending_intent() {
        let root = root();
        let large = vec![0x42; 9 * 1024 * 1024];
        { let mut intent = RequiredCaptureIntent::open(&root, "es-owner").unwrap();
            intent.begin(b"original official record").unwrap(); intent.freeze(&large).unwrap();
            assert!(intent.file.metadata().unwrap().len() > 8 * 1024 * 1024);
            intent.complete().unwrap(); assert_eq!(intent.file.metadata().unwrap().len(), 8);
            intent.begin(b"next pending original source").unwrap(); }
        let cold = RequiredCaptureIntent::open(&root, "es-owner").unwrap();
        assert_eq!(cold.original(), Some(b"next pending original source".as_slice()));
        drop(cold); std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn required_capture_child_process_writes_marker_and_exits() {
        let Some(root) = std::env::var_os("IFM_REQUIRED_INTENT_CHILD_ROOT") else { return; };
        let mut intent = RequiredCaptureIntent::open(Path::new(&root), "child-owner").unwrap();
        intent.begin(b"authentic official record and original source-time state").unwrap();
        if std::env::var_os("IFM_REQUIRED_INTENT_CHILD_FROZEN").is_some() { intent.freeze(b"original wire event-id origin and capture key").unwrap(); }
        std::process::exit(0); // No graceful shutdown, Drop or provider replay.
    }
    #[test]
    fn process_death_before_and_after_wire_freeze_preserves_gap_or_original_wire() {
        for frozen in [false, true] {
            let root = root();
            let mut child = std::process::Command::new(std::env::current_exe().unwrap());
            child.args(["--exact", "dataset::required_capture::tests::required_capture_child_process_writes_marker_and_exits", "--nocapture"])
                .env("IFM_REQUIRED_INTENT_CHILD_ROOT", &root);
            if frozen { child.env("IFM_REQUIRED_INTENT_CHILD_FROZEN", "1"); }
            assert!(child.status().unwrap().success());
            let mut cold = RequiredCaptureIntent::open(&root, "child-owner").unwrap();
            assert_eq!(cold.original(), Some(b"authentic official record and original source-time state".as_slice()));
            if frozen {
                assert_eq!(cold.frozen(), Some(b"original wire event-id origin and capture key".as_slice()));
                cold.complete().unwrap();
            } else { assert!(cold.frozen().is_none()); assert!(cold.complete().is_err()); }
            drop(cold); std::fs::remove_dir_all(root).unwrap();
        }
    }
    #[test]
    fn torn_or_corrupt_intent_never_becomes_cold_healthy() {
        let root = root(); { let mut intent = RequiredCaptureIntent::open(&root, "es-owner").unwrap(); intent.begin(b"received official input").unwrap(); }
        let mut file = OpenOptions::new().append(true).open(root.join("required-es-owner.intent")).unwrap();
        file.write_all(&100u32.to_le_bytes()).unwrap(); file.write_all(&[2, 3]).unwrap(); file.sync_all().unwrap(); drop(file);
        assert!(RequiredCaptureIntent::open(&root, "es-owner").is_err()); std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn sustained_completed_intents_reclaim_history_but_latest_frozen_evidence_survives_restart(){
        let root=root();let payload=vec![0x5a;64*1024];let mut intent=RequiredCaptureIntent::open(&root,"retention-owner").unwrap();
        let mut compacted=false;let mut previous=8;
        for sequence in 0u64..160{intent.begin(&sequence.to_le_bytes()).unwrap();intent.freeze(&payload).unwrap();intent.complete().unwrap();
            let size=intent.file.metadata().unwrap().len();if size<previous{compacted=true;}assert!(size<=8*1024*1024);previous=size;}
        assert!(compacted);let original=b"last pending original generation and serial";intent.begin(original).unwrap();intent.freeze(&payload).unwrap();drop(intent);
        let mut cold=RequiredCaptureIntent::open(&root,"retention-owner").unwrap();assert_eq!(cold.original(),Some(original.as_slice()));assert_eq!(cold.frozen(),Some(payload.as_slice()));
        assert!(cold.begin(b"replacement").is_err());cold.complete().unwrap();drop(cold);
        assert!(RequiredCaptureIntent::open(&root,"retention-owner").unwrap().original().is_none());std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn oversized_refusal_never_changes_original_or_freezes_replacement_and_can_retry(){
        let root=root();let mut intent=RequiredCaptureIntent::open(&root,"bounded-owner").unwrap();intent.begin(b"original authentic evidence").unwrap();let before=intent.file.metadata().unwrap().len();
        let oversized=vec![0;MAX_RECORD];assert!(intent.freeze(&oversized).is_err());assert_eq!(intent.file.metadata().unwrap().len(),before);assert_eq!(intent.original(),Some(b"original authentic evidence".as_slice()));assert!(intent.frozen().is_none());drop(oversized);
        intent.freeze(b"exact original frozen wire").unwrap();drop(intent);let mut cold=RequiredCaptureIntent::open(&root,"bounded-owner").unwrap();assert_eq!(cold.frozen(),Some(b"exact original frozen wire".as_slice()));cold.complete().unwrap();drop(cold);std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn checksum_corruption_of_pending_frozen_history_is_not_treated_as_completion(){
        let root=root();{let mut intent=RequiredCaptureIntent::open(&root,"corrupt-owner").unwrap();intent.begin(b"original").unwrap();intent.freeze(b"frozen original wire").unwrap();}
        let path=root.join("required-corrupt-owner.intent");let mut bytes=std::fs::read(&path).unwrap();let index=bytes.len()-1;bytes[index]^=1;std::fs::write(&path,bytes).unwrap();
        assert!(RequiredCaptureIntent::open(&root,"corrupt-owner").err().unwrap().contains("checksum"));std::fs::remove_dir_all(root).unwrap();
    }

}
