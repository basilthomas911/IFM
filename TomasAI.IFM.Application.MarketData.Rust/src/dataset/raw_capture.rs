//! Durable raw futures history, captured before replaceable application output.
//! File data is synced before the source processor can advance its hot book.
use super::{live::Registration, records::{Observation, Quote}, transport::Lane};
use crate::messaging::{codec, events::{Identity, Instrument}};
use sha2::{Digest, Sha256};
use std::{collections::BTreeMap, fs::{File, OpenOptions}, io::{Read, Seek, SeekFrom, Write}, path::{Path, PathBuf}};

const ROTATE_BYTES: u64 = 16 * 1024 * 1024;
const MAX_SPOOL_BYTES: u64 = 2 * 1024 * 1024 * 1024;
const MAX_PAYLOAD: usize = 16 * 1024;

/// Original journal identity reused by live trades, separate from the ITI book ordinal.
#[derive(Clone, Copy, Debug)]
pub struct Stamp {
    /// Persisted original event, command and source-observation time.
    pub identity: Identity,
    /// Persisted monotonic raw-row identity, independent of the ITI ordinal.
    pub sequence: i64,
}

/// One source-owned spool. Its files outlive physical generations and processes.
pub struct RawCapture {
    root: PathBuf,
    file: File,
    _owner: File,
    epoch: [u8; 16],
    ordinal: u64,
    sequence_base: i64,
    bytes: u64,
    instruments: BTreeMap<u32, Instrument>,
    quotes: Vec<Quote>,
    quote_generation: u128,
    quote_observed: i64,
}
impl RawCapture {
    pub(crate) fn restore_checkpoint(root:&Path,value:&rmpv::Value,lane:Lane,date:time::Date,definition:time::Date,registrations:&[Registration])->Result<Self,String>{
        use crate::checkpoint as c;let f=c::fields(value,10)?;if c::unsigned(&f[0])?!=1{return Err("Unknown raw checkpoint version".into());}
        let original=std::path::PathBuf::from(c::string(&f[1],32768)?);if original.canonicalize().ok()==root.canonicalize().ok(){return Err("Raw checkpoint import must use an explicitly relocated owned copy".into());}
        let epoch=match &f[2]{rmpv::Value::Binary(bytes) if bytes.len()==16=>bytes.as_slice().try_into().unwrap(),_=>return Err("Original raw checkpoint epoch invalid".into())};
        let ordinal=c::unsigned(&f[3])?;let sequence_base=c::number(&f[4])?;let bytes=c::unsigned(&f[5])?;
        if sequence_base<=0||ordinal>=1_000_000||bytes<24||bytes>ROTATE_BYTES+MAX_PAYLOAD as u64{return Err("Original raw checkpoint ID/file bounds invalid".into());}
        let files=f[6].as_array().filter(|files|files.len()<=1024).ok_or("Raw checkpoint prefix count exceeds bound")?;
        for row in files{let row=c::fields(row,3)?;let name=c::string(&row[0],256)?;if name.contains(['\\','/'])||name==".."{return Err("Raw checkpoint path traversal refused".into());}let path=root.join(name);let mut file=File::open(path).map_err(|error|error.to_string())?;let length=c::unsigned(&row[1])?;if file.metadata().map_err(|error|error.to_string())?.len()!=length{return Err("Original raw prefix length differs".into());}let count=length.min(64);file.seek(SeekFrom::End(-(count as i64))).map_err(|error|error.to_string())?;let mut tail=vec![0;count as usize];file.read_exact(&mut tail).map_err(|error|error.to_string())?;if row[2]!=rmpv::Value::Binary(Sha256::digest(&tail).to_vec()){return Err("Original raw prefix proof differs".into());}}
        let name=format!("{:032x}",u128::from_le_bytes(epoch));let owner=OpenOptions::new().read(true).write(true).open(root.join(format!("{name}.owner"))).map_err(|error|error.to_string())?;owner.try_lock().map_err(|error|format!("Original raw checkpoint owner active: {error}"))?;
        let mut file=OpenOptions::new().read(true).write(true).open(root.join(format!("{name}.wal"))).map_err(|error|error.to_string())?;
        if file.metadata().map_err(|error|error.to_string())?.len()!=bytes{return Err("Original current raw segment differs".into());}file.seek(SeekFrom::End(0)).map_err(|error|error.to_string())?;
        let mut instruments=BTreeMap::new();for registration in registrations{if instruments.insert(registration.instrument,Instrument::new(registration.clone(),lane,date,definition)?).is_some(){return Err("Duplicate raw checkpoint registration".into());}}
        if f[9]!=rmpv::Value::Array(instruments.values().map(Instrument::capture_identity).collect()){return Err("Original raw checkpoint registrations differ".into());}
        Ok(Self{root:root.into(),file,_owner:owner,epoch,ordinal,sequence_base,bytes,instruments,quotes:Vec::with_capacity(64),quote_generation:c::read_wide(&f[7])?,quote_observed:c::number(&f[8])?})
    }
    pub(crate) fn checkpoint(&self)->Result<rmpv::Value,String>{
        use rmpv::Value as V;
        if !self.quotes.is_empty(){return Err("Original raw quote prefix must be flushed before checkpoint".into());}
        let mut files=Vec::new();
        for entry in std::fs::read_dir(&self.root).map_err(|error|error.to_string())?{
            let entry=entry.map_err(|error|error.to_string())?;let path=entry.path();
            if path.extension().is_some_and(|extension|extension=="wal")||path.file_name().is_some_and(|name|name=="sequence.bin"){
                let mut file=File::open(&path).map_err(|error|error.to_string())?;let length=file.metadata().map_err(|error|error.to_string())?.len();let count=length.min(64);file.seek(SeekFrom::End(-(count as i64))).map_err(|error|error.to_string())?;let mut tail=vec![0;count as usize];file.read_exact(&mut tail).map_err(|error|error.to_string())?;
                files.push(V::Array(vec![path.file_name().and_then(|name|name.to_str()).ok_or("Invalid raw checkpoint filename")?.into(),length.into(),V::Binary(Sha256::digest(&tail).to_vec())]));
            }
        }
        files.sort_by(|left,right|left.as_array().unwrap()[0].as_str().cmp(&right.as_array().unwrap()[0].as_str()));
        if files.len()>1024{return Err("Raw checkpoint file prefix count exceeds bound".into());}
        Ok(V::Array(vec![1.into(),self.root.canonicalize().map_err(|error|error.to_string())?.to_str().ok_or("Raw checkpoint path is not UTF8")?.into(),V::Binary(self.epoch.to_vec()),self.ordinal.into(),self.sequence_base.into(),self.bytes.into(),V::Array(files),V::Binary(self.quote_generation.to_le_bytes().to_vec()),self.quote_observed.into(),V::Array(self.instruments.values().map(Instrument::capture_identity).collect())]))
    }
    /// Creates an exclusively owned segment; existing segments are never overwritten.
    pub fn open(root: &Path, lane: Lane, date: time::Date, definition: time::Date,
        registrations: &[Registration]) -> Result<Self, String> {
        std::fs::create_dir_all(root).map_err(|e| format!("Raw storage directory: {e}"))?;
        let (file, owner, epoch, sequence_base) = Self::segment(root)?;
        let mut instruments = BTreeMap::new();
        for registration in registrations {
            instruments.insert(registration.instrument, Instrument::new(registration.clone(), lane, date, definition)?);
        }
        Ok(Self { root: root.into(), file, _owner: owner, epoch, ordinal: 0, sequence_base, bytes: 24, instruments,
            quotes: Vec::with_capacity(64), quote_generation: 0, quote_observed: 0 })
    }
    fn segment(root: &Path) -> Result<(File, File, [u8; 16], i64), String> {
        let pending = std::fs::read_dir(root).map_err(|e| e.to_string())?
            .try_fold(0u64, |sum, entry| -> Result<u64, String> {
                let entry = entry.map_err(|e| e.to_string())?;
                match entry.metadata() {
                    Ok(metadata) => Ok(sum.saturating_add(metadata.len())),
                    Err(error) if error.kind() == std::io::ErrorKind::NotFound => Ok(sum),
                    Err(error) => Err(error.to_string()),
                }
            })?;
        if pending >= MAX_SPOOL_BYTES { return Err("Raw futures spool capacity exhausted; history was not evicted".into()); }
        // Reserve a durable monotonic ID range before creating a segment. Each segment
        // holds far fewer than one million records; IDs remain ordered across restarts.
        let mut ids = OpenOptions::new().read(true).write(true).create(true).truncate(false)
            .open(root.join("sequence.bin")).map_err(|e| e.to_string())?;
        ids.lock().map_err(|e| e.to_string())?;
        let mut previous = [0u8; 40];
        let end = match ids.metadata().map_err(|e| e.to_string())?.len() {
            0 => 0,
            40 => {
                ids.read_exact(&mut previous).map_err(|e| e.to_string())?;
                if Sha256::digest(&previous[..8])[..] != previous[8..] { return Err("Raw source ID checksum mismatch".into()); }
                i64::from_le_bytes(previous[..8].try_into().unwrap())
            },
            _ => return Err("Raw source ID reservation is damaged".into()),
        };
        let now = std::time::SystemTime::now().duration_since(std::time::UNIX_EPOCH).map_err(|e| e.to_string())?.as_nanos() / 100;
        let sequence_base = end.checked_add(1).ok_or("Raw source IDs exhausted")?.max(i64::try_from(now).map_err(|e| e.to_string())?);
        let reserved = sequence_base.checked_add(1_000_000).ok_or("Raw source IDs exhausted")?;
        let mut reservation = Vec::from(reserved.to_le_bytes()); reservation.extend_from_slice(&Sha256::digest(reserved.to_le_bytes()));
        ids.seek(SeekFrom::Start(0)).and_then(|_| ids.write_all(&reservation)).and_then(|()| ids.sync_all()).map_err(|e| e.to_string())?;
        drop(ids);
        let epoch = rand::random::<u128>().max(1).to_le_bytes();
        let name = format!("{:032x}", u128::from_le_bytes(epoch));
        let owner = OpenOptions::new().read(true).write(true).create_new(true).open(root.join(format!("{name}.owner")))
            .map_err(|e| e.to_string())?;
        owner.try_lock().map_err(|e| format!("Raw segment ownership: {e}"))?;
        let mut file = OpenOptions::new().read(true).write(true).create_new(true).open(root.join(format!("{name}.wal")))
            .map_err(|e| e.to_string())?;
        file.write_all(b"IFMRAW01").and_then(|()| file.write_all(&epoch)).and_then(|()| file.sync_all())
            .map_err(|e| format!("Raw segment header: {e}"))?;
        Ok((file, owner, epoch, sequence_base))
    }
    /// Captures original fields and identities before any queue admission or generation fence.
    pub fn append(&mut self, physical: u128, observed: i64, source: Observation) -> Result<Option<Stamp>, String> {
        let header = match source { Observation::Trade(t) => t.header, Observation::Quote(q) => q.header, _ => return Ok(None) };
        if header.flags & 2 != 0 { return Ok(None); } // Provider warm-up replay is not a new live observation.
        if !self.instruments.contains_key(&header.instrument) { return Ok(None); }
        if self.bytes >= ROTATE_BYTES {
            self.flush_quotes()?;
            self.seal()?;
            let (file, owner, epoch, base) = Self::segment(&self.root)?;
            self.file = file; self._owner = owner; self.epoch = epoch; self.ordinal = 0; self.bytes = 24; self.sequence_base = base;
        }
        if let Observation::Quote(quote) = source {
            if self.quotes.first().is_some_and(|prior| prior.header.instrument != quote.header.instrument
                || prior.header.publisher != quote.header.publisher || self.quote_generation != physical) {
                self.flush_quotes()?;
            }
            if self.quotes.is_empty() { self.quote_generation = physical; self.quote_observed = observed; }
            self.quotes.push(quote);
            if self.quotes.len() == 64 { self.flush_quotes()?; }
            return Ok(None);
        }
        self.flush_quotes()?;
        let ordinal = self.ordinal.checked_add(1).ok_or("Raw acceptance ordinal exhausted")?;
        if ordinal >= 1_000_000 { return Err("Raw segment ID reservation exhausted".into()); }
        let sequence = self.sequence_base + ordinal as i64;
        let instrument = &self.instruments[&header.instrument];
        let identity = Identity::fresh(observed);
        let (kind, value) = match source {
            Observation::Trade(t) => (1u8, instrument.trade(t, identity, sequence)?),
            _ => unreachable!(),
        };
        self.write_record(physical, kind, ordinal, value)?;
        Ok(Some(Stamp { identity, sequence }))
    }
    fn flush_quotes(&mut self) -> Result<(), String> {
        let Some(first) = self.quotes.first() else { return Ok(()); };
        let ordinal = self.ordinal.checked_add(1).ok_or("Raw quote ordinal exhausted")?;
        let sequence = self.sequence_base + ordinal as i64;
        let mut value = self.instruments[&first.header.instrument].quotes(&self.quotes, 3, Identity::fresh(self.quote_observed), sequence)?;
        // A CFE definition may admit either publisher. Keep each stored segment's actual publisher.
        if let rmpv::Value::Array(fields) = &mut value { fields[13] = first.header.publisher.into(); }
        self.write_record(self.quote_generation, 2, ordinal, value)?;
        self.quotes.clear();
        Ok(())
    }
    fn write_record(&mut self, physical: u128, kind: u8, ordinal: u64, value: rmpv::Value) -> Result<(), String> {
        let payload = codec::encode(&value)?;
        if payload.len() > MAX_PAYLOAD { return Err("Raw observation exceeds bounded payload".into()); }
        let mut record = Vec::with_capacity(25 + payload.len());
        record.push(kind); record.extend_from_slice(&physical.to_le_bytes());
        record.extend_from_slice(&ordinal.to_le_bytes()); record.extend_from_slice(&payload);
        let hash = Sha256::digest(&record);
        self.file.write_all(&(record.len() as u32).to_le_bytes())
            .and_then(|()| self.file.write_all(&record)).and_then(|()| self.file.write_all(&hash))
            .map_err(|e| format!("Raw observation append failed: {e}"))?;
        self.bytes += 4 + record.len() as u64 + 32; self.ordinal = ordinal;
        Ok(())
    }
    fn seal(&mut self) -> Result<(), String> {
        self.file.write_all(&0u32.to_le_bytes()).and_then(|()| self.file.sync_data()).map_err(|e| e.to_string())
    }
    /// Completes the group commit before buffered observations enter the hot book.
    pub fn flush(&mut self) -> Result<(), String> {
        self.flush_quotes()?;
        self.file.sync_data().map_err(|e| format!("Raw observation group commit failed: {e}"))
    }
}

impl Drop for RawCapture {
    fn drop(&mut self) {
        if let Err(error) = self.flush() { eprintln!("Raw futures shutdown commit failed: {error}"); }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn checkpoint_resumes_original_raw_segment_and_reserved_row_range_without_renumbering(){
        let root=std::env::temp_dir().join(format!("ifm-raw-checkpoint-{:032x}",rand::random::<u128>()));
        let date=time::Date::from_calendar_date(2026,time::Month::November,23).unwrap();let registration=Registration{instrument:42,publisher:1,contract:"ES20261218".into(),source:"ESZ6".into(),asset:Asset::Futures,tick:Some(250_000_000),vwap:false,required:true};
        let trade=Trade{header:Header{instrument:42,publisher:1,flags:0,event_ns:1795467599000000000,received_ns:1795467599000000100,sequence:1,schema:1},price:6000000000000,size:3,action:b'T',side:b'B',dbn_flags:0,depth:0,delta_ns:100};
        let mut capture=RawCapture::open(&root,Lane::FuturesGlbx,date,date,std::slice::from_ref(&registration)).unwrap();let first=capture.append(55,17954675990000000,Observation::Trade(trade)).unwrap().unwrap();capture.flush().unwrap();let image=capture.checkpoint().unwrap();let epoch=capture.epoch;let base=capture.sequence_base;
        assert!(RawCapture::restore_checkpoint(&root,&image,Lane::FuturesGlbx,date,date,std::slice::from_ref(&registration)).is_err());let relocated=root.with_extension("restored");std::fs::create_dir(&relocated).unwrap();for entry in std::fs::read_dir(&root).unwrap(){let path=entry.unwrap().path();std::fs::copy(&path,relocated.join(path.file_name().unwrap())).unwrap();}
        let mut restored=RawCapture::restore_checkpoint(&relocated,&image,Lane::FuturesGlbx,date,date,std::slice::from_ref(&registration)).unwrap();assert_eq!(restored.epoch,epoch);assert_eq!(restored.sequence_base,base);let mut relocated_image=restored.checkpoint().unwrap();if let rmpv::Value::Array(fields)=&mut relocated_image{fields[1]=image.as_array().unwrap()[1].clone();}assert_eq!(relocated_image,image);
        let mut next=trade;next.header.sequence=2;let second=restored.append(55,17954675990000001,Observation::Trade(next)).unwrap().unwrap();assert_eq!(second.sequence,first.sequence+1);restored.flush().unwrap();drop(restored);
        assert!(RawCapture::restore_checkpoint(&relocated,&image,Lane::FuturesGlbx,date,date,&[registration]).is_err());drop(capture);std::fs::remove_dir_all(root).unwrap();std::fs::remove_dir_all(relocated).unwrap();
    }
    use crate::{dataset::records::{Header, Trade, Quote}, latest::Asset};
    #[test]
    fn quote_bursts_keep_the_existing_bounded_array_storage_shape() {
        let root = std::env::temp_dir().join(format!("ifm-raw-quotes-{:032x}", rand::random::<u128>()));
        let date = time::Date::from_calendar_date(2026, time::Month::November, 23).unwrap();
        let registration = Registration { instrument: 42, publisher: 1, contract: "ES20261218".into(), source: "ESZ6".into(),
            asset: Asset::Futures, tick: Some(250_000_000), vwap: false, required: true };
        let mut capture = RawCapture::open(&root, Lane::FuturesGlbx, date, date, &[registration]).unwrap();
        for sequence in 1..=64 {
            capture.append(1, 17954675990000000, Observation::Quote(Quote {
                header: Header { instrument: 42, publisher: 1, flags: 0, event_ns: 1795467599000000000 + sequence as i64,
                    received_ns: 1795467599000000100 + sequence as i64, sequence, schema: 1 },
                bid: 6000000000000, ask: 6000250000000, bid_size: sequence, ask_size: sequence + 1, bid_count: 1, ask_count: 2,
            })).unwrap();
        }
        capture.flush().unwrap(); drop(capture);
        let path = std::fs::read_dir(&root).unwrap().map(|e|e.unwrap().path()).find(|p|p.extension().is_some_and(|e|e=="wal")).unwrap();
        let bytes = std::fs::read(path).unwrap(); let length = u32::from_le_bytes(bytes[24..28].try_into().unwrap()) as usize;
        assert_eq!(bytes.len(), 28 + length + 32);
        let value = rmpv::decode::read_value(&mut std::io::Cursor::new(&bytes[53..28+length])).unwrap();
        let fields = value.as_array().unwrap(); assert_eq!(fields[16].as_u64(), Some(64));
        let quotes = fields[17].as_array().unwrap(); assert_eq!(quotes.len(), 64);
        for (index, quote) in quotes.iter().enumerate() {
            assert_eq!(quote.as_array().unwrap()[0].as_u64(), Some((index + 1) as u64));
            assert_eq!(quote.as_array().unwrap()[1].as_i64(), Some(1795467599000000001 + index as i64));
        }
        assert!(root.starts_with(std::env::temp_dir())); std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn live_trade_reuses_the_journaled_identity_timestamp_and_row_key() {
        let date = time::Date::from_calendar_date(2026, time::Month::November, 23).unwrap();
        let registration = Registration { instrument: 42, publisher: 1, contract: "ES20261218".into(), source: "ESZ6".into(),
            asset: Asset::Futures, tick: Some(250_000_000), vwap: false, required: true };
        let trade = Trade { header: Header { instrument: 42, publisher: 1, flags: 0,
            event_ns: 1795467599000000000, received_ns: 1795467599000000100, sequence: 7, schema: 1 },
            price: 6000000000000, size: 3, action: b'T', side: b'B', dbn_flags: 0, depth: 0, delta_ns: 100 };
        let identity = Identity::fresh(17954675990000000);
        let original = Instrument::new(registration.clone(), Lane::FuturesGlbx, date, date).unwrap().trade(trade, identity, 9999).unwrap();
        let mut encoder = crate::messaging::futures::Futures::new(Lane::FuturesGlbx, date, date, vec![registration], 64).unwrap();
        let output = super::super::live::Output {
            capture_serial: 0, observed: identity.observed + 100000, lane: Lane::FuturesGlbx, generation: 1,
            source: super::super::live::OutputSource::Observation(Observation::Trade(trade)),
            state: super::super::book::PublicationState { update: Some(super::super::book::Update::Trade { price_changed: true }), ..Default::default() },
            trade: None, raw: Some(Stamp { identity, sequence: 9999 }),
        };
        let frames = encoder.process(&output).unwrap();
        let published = frames.iter().find(|frame| frame.kind == 3).unwrap();
        assert_eq!(&published.value.as_array().unwrap()[12], &original);
    }
    #[test]
    #[ignore = "Child process helper; invoked explicitly by the restart test"]
    fn process_exit_child() {
        let root = std::env::var_os("IFM_RAW_CHILD_ROOT").unwrap();
        let date = time::Date::from_calendar_date(2026, time::Month::November, 23).unwrap();
        let registration = Registration { instrument: 42, publisher: 1, contract: "ES20261218".into(), source: "ESZ6".into(),
            asset: Asset::Futures, tick: Some(250_000_000), vwap: false, required: true };
        let mut capture = RawCapture::open(Path::new(&root), Lane::FuturesGlbx, date, date, &[registration]).unwrap();
        let header = Header { instrument: 42, publisher: 1, flags: 0, event_ns: 1795467599000000000,
            received_ns: 1795467599000000100, sequence: 1, schema: 1 };
        capture.append(1, 17954675990000000, Observation::Trade(Trade { header, price: 6000000000000, size: 1,
            action: b'T', side: b'B', dbn_flags: 0, depth: 0, delta_ns: 100 })).unwrap();
        capture.flush().unwrap();
        std::process::exit(0); // No Rust destructors, source shutdown or orderly segment seal.
    }
    #[test]
    fn committed_source_survives_process_exit_without_destructors() {
        let root = std::env::temp_dir().join(format!("ifm-raw-child-{:032x}", rand::random::<u128>()));
        let status = std::process::Command::new(std::env::current_exe().unwrap())
            .args(["--exact", "dataset::raw_capture::tests::process_exit_child", "--ignored"])
            .env("IFM_RAW_CHILD_ROOT", &root).status().unwrap();
        assert!(status.success());
        let files: Vec<_> = std::fs::read_dir(&root).unwrap().map(|e|e.unwrap().path()).filter(|p|p.extension().is_some_and(|e|e=="wal")).collect();
        assert_eq!(files.len(), 1);
        let bytes = std::fs::read(&files[0]).unwrap();
        let length = u32::from_le_bytes(bytes[24..28].try_into().unwrap()) as usize;
        assert_eq!(bytes.len(), 28 + length + 32);
        assert_eq!(&Sha256::digest(&bytes[28..28+length])[..], &bytes[28+length..]);
        std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn all_raw_records_survive_queue_sized_bursts_generation_changes_and_owner_restart() {
        let root = std::env::temp_dir().join(format!("ifm-raw-{:032x}", rand::random::<u128>()));
        let registration = Registration { instrument: 42, publisher: 1, contract: "ES20261218".into(), source: "ESZ6".into(),
            asset: Asset::Futures, tick: Some(250_000_000), vwap: false, required: true };
        let date = time::Date::from_calendar_date(2026, time::Month::November, 23).unwrap();
        let mut capture = RawCapture::open(&root, Lane::FuturesGlbx, date, date, &[registration.clone()]).unwrap();
        let mut queue = super::super::output::LaneOutput::new();
        let mut rejected = 0;
        for seq in 1..=20_000 {
            let header = Header { instrument: 42, publisher: 1, flags: 0, event_ns: 1795467599000000000 + seq as i64,
                received_ns: 1795467599000000100 + seq as i64, sequence: seq, schema: 1 };
            let source = if seq % 2 == 0 { Observation::Trade(Trade { header, price: 6000000000000, size: seq,
                action: b'T', side: b'B', dbn_flags: 0, depth: 0, delta_ns: 100 }) }
                else { Observation::Quote(Quote { header, bid: 6000000000000, ask: 6000250000000, bid_size: seq,
                    ask_size: seq + 1, bid_count: 1, ask_count: 2 }) };
            capture.append(if seq < 10000 { 1 } else { 2 }, 17954675990000000, source).unwrap();
            if seq % 64 == 0 { capture.flush().unwrap(); }
            if queue.push_back(super::super::live::Output {
                capture_serial: 0, observed: 17954675990000000, lane: Lane::FuturesGlbx, generation: 1,
                source: super::super::live::OutputSource::Observation(source),
                state: super::super::book::PublicationState::default(), trade: None, raw: None,
            }).is_err() { rejected += 1; }
        }
        assert!(rejected > 0); // The downstream queue really overflowed; raw history was captured first.
        queue.clear(); // A generation can retire all display output without deleting storage history.
        capture.flush().unwrap();
        drop(capture); // No orderly seal: simulate source-owner restart, without deleting history.
        let next = RawCapture::open(&root, Lane::FuturesGlbx, date, date, &[registration]).unwrap();
        drop(next);
        let mut count = 0;
        for entry in std::fs::read_dir(&root).unwrap() {
            let path = entry.unwrap().path();
            if path.extension().is_none_or(|e| e != "wal") { continue; }
            let bytes = std::fs::read(path).unwrap(); assert_eq!(&bytes[..8], b"IFMRAW01");
            let mut offset = 24;
            while offset < bytes.len() {
                let length = u32::from_le_bytes(bytes[offset..offset+4].try_into().unwrap()) as usize; offset += 4;
                if length == 0 { break; }
                let record = &bytes[offset..offset+length]; offset += length;
                assert_eq!(&Sha256::digest(record)[..], &bytes[offset..offset+32]); offset += 32; count += 1;
            }
        }
        assert_eq!(count, 20_000);
        if let Some(destination) = std::env::var_os("IFM_RAW_CAPTURE_TEST_EXPORT") {
            std::fs::create_dir_all(&destination).unwrap();
            for entry in std::fs::read_dir(&root).unwrap() {
                let entry = entry.unwrap();
                if entry.path().extension().is_some_and(|e| e == "wal" || e == "owner") {
                    std::fs::copy(entry.path(), Path::new(&destination).join(entry.file_name())).unwrap();
                }
            }
        }
        std::fs::remove_dir_all(root).unwrap();
    }
}
