//! Durable native-to-API source ownership. Dataset replacement cannot erase an
//! accepted publication. Only an exact API capture receipt retires its record.
use super::{
    publisher::{BrokerSettings, Publisher, Status},
    wire::Frame,
};
use crate::runtime::{Deadline, owner::Child};
use tokio_util::sync::CancellationToken;
use sha2::{Digest, Sha256};
use std::{
    collections::{BTreeMap, BTreeSet},
    fs::{File, OpenOptions},
    io::{Read, Seek, SeekFrom, Write},
    path::PathBuf,
    sync::{
        Arc, Mutex, OnceLock, Weak,
        atomic::{AtomicU64, Ordering},
    },
    time::Duration,
};
use tokio::sync::{Notify, watch};

const MAGIC: &[u8; 8] = b"IFMSRC02";
const MAX_BYTES: u64 = 2 * 1024 * 1024 * 1024;
const SEGMENT_BYTES: u64 = 16 * 1024 * 1024;
const MAX_RECORD: usize = 2 * 1024 * 1024;
const MAX_PENDING: usize = 262_144;
static REGISTRY: OnceLock<Mutex<BTreeMap<String, Weak<Sink>>>> = OnceLock::new();
static TRACKER_ID: AtomicU64 = AtomicU64::new(1);

/// Staged native producer protocol; disabled until the coordinated cutover.
pub(crate) fn enabled() -> bool {
    std::env::var("IFM_REALTIME_SOURCE_CAPTURE_ENABLED")
        .is_ok_and(|v| v == "1" || v.eq_ignore_ascii_case("true"))
}
/// Root location is independent of dataset generations and provider sessions.
pub(crate) fn location(scope: &str) -> Result<PathBuf, String> {
    if scope.is_empty()
        || scope.len() > 128
        || !scope
            .bytes()
            .all(|c| c.is_ascii_alphanumeric() || c == b'-')
    {
        return Err("Invalid source capture scope".into());
    }
    let base = std::env::var_os("IFM_REALTIME_SOURCE_DIRECTORY")
        .map(PathBuf::from)
        .or_else(|| {
            std::env::var_os("LOCALAPPDATA")
                .map(|v| PathBuf::from(v).join("IFM").join("RealtimeSourceCapture"))
        })
        .or_else(|| {
            std::env::var_os("HOME")
                .map(|v| PathBuf::from(v).join(".local/share/IFM/RealtimeSourceCapture"))
        })
        .ok_or("Native source capture directory unavailable")?;
    Ok(base.join(scope))
}
/// Finds the runtime-owned source journal; enabled mode never silently falls back to Core.
pub(crate) fn lookup(scope: &str) -> Result<Option<Arc<Sink>>, &'static str> {
    if !enabled() {
        return Ok(None);
    }
    REGISTRY
        .get_or_init(Mutex::default)
        .lock()
        .map_err(|_| "Source capture registry interrupted")?
        .get(scope)
        .and_then(Weak::upgrade)
        .map(Some)
        .ok_or("Native durable source owner is not ready")
}
/// Captures immutable file-prefix boundaries, never a claim that moving mapper
/// state or required-intent files form a native restart checkpoint.
pub(crate) fn capture_prefix_manifest(scope:&str,lease:u64)->Result<String,String>{
    fn held(lease:u64)->bool { lease!=0&&super::source_forwarding::ifm_market_data_source_forwarding_lease_v1()==lease&&super::source_forwarding::ifm_market_data_source_forwarding_active_v1()==0 }
    if !held(lease){return Err("Native source prefix requires the original drained forwarding lease".into());}
    let sink=lookup(scope)?.ok_or("Native durable source capture is disabled")?;
    let manifest=sink.prefix_manifest(lease)?;
    if !held(lease){return Err("Native source forwarding lease changed during prefix capture".into());}
    Ok(manifest)
}
/// Copied publication progress only; no provider, lease or dataset task is retained.
#[derive(Clone)]
pub(crate) struct Tracker {
    id: u64,
    status: watch::Sender<Status>,
    pending: Arc<Mutex<BTreeMap<u64, tokio::time::Instant>>>,
}
impl Tracker {
    /// Creates one progress witness for a publication port.
    pub(crate) fn new(status: watch::Sender<Status>) -> Self {
        Self {
            id: TRACKER_ID.fetch_add(1, Ordering::Relaxed),
            status,
            pending: Arc::new(Mutex::new(BTreeMap::new())),
        }
    }
    fn admit(&self, id: u64) {
        if let Ok(mut p) = self.pending.lock() {
            p.entry(id).or_insert_with(tokio::time::Instant::now);
            let oldest = p.values().copied().min();
            self.status.send_modify(|s| s.pending_since = oldest);
        }
    }
    fn complete(&self, id: u64) {
        if let Ok(mut p) = self.pending.lock() {
            if p.remove(&id).is_some() {
                let oldest = p.values().copied().min();
                self.status.send_modify(|s| {
                    s.flushed = s.flushed.saturating_add(1);
                    s.pending_since = oldest;
                });
            }
        }
    }
}
#[derive(Clone)]
struct Meta {
    key: [u8; 32],
    path: PathBuf,
    offset: u64,
    length: usize,
    group: String,
    trackers: Vec<Tracker>,
}
/// Original encoded source packet, loaded under explicit byte bounds.
pub(crate) struct Record {
    ordinal: u64,
    pub(crate) subject: String,
    pub(crate) origin: Option<String>,
    pub(crate) bytes: Vec<u8>,
    key: [u8; 32],
}
impl Record {
    fn encode(&self) -> Result<Vec<u8>, String> {
        if self.subject.len() > 512
            || self.origin.as_ref().is_some_and(|o| o.len() > 32768)
            || self.bytes.len() > MAX_RECORD
        {
            return Err("Source frame exceeds bounds".into());
        }
        let mut out = Vec::with_capacity(self.bytes.len() + 160);
        out.extend_from_slice(&self.ordinal.to_le_bytes());
        out.extend_from_slice(&self.key);
        field(&mut out, self.subject.as_bytes());
        field(&mut out, self.origin.as_deref().unwrap_or("").as_bytes());
        field(&mut out, &self.bytes);
        if out.len() > MAX_RECORD {
            return Err("Source frame exceeds bounds".into());
        }
        Ok(out)
    }
    fn decode(input: &[u8]) -> Result<Self, String> {
        if input.len() < 52 || input.len() > MAX_RECORD {
            return Err("Invalid source capture frame".into());
        }
        let ordinal = u64::from_le_bytes(input[..8].try_into().unwrap());
        let key = input[8..40].try_into().unwrap();
        let mut at = 40;
        let subject = String::from_utf8(take(input, &mut at, 512)?.to_vec())
            .map_err(|_| "Invalid source subject")?;
        let origin = String::from_utf8(take(input, &mut at, 32768)?.to_vec())
            .map_err(|_| "Invalid source provenance")?;
        let bytes = take(input, &mut at, MAX_RECORD)?.to_vec();
        if at != input.len() || ordinal == 0 {
            return Err("Invalid source capture tail".into());
        }
        Ok(Self {
            ordinal,
            key,
            subject,
            origin: (!origin.is_empty()).then_some(origin),
            bytes,
        })
    }
    /// Validate the exact original source receipt, including full wire digest.
    pub(crate) fn accepts(&self, reply: &[u8]) -> Result<(), String> {
        if reply.len() > 8192 {
            return Err("Source capture reply exceeds bound".into());
        }
        let value = rmpv::decode::read_value(&mut std::io::Cursor::new(reply))
            .map_err(|_| "Invalid source capture receipt")?;
        let values = value
            .as_array()
            .filter(|v| v.len() == 7)
            .ok_or("Invalid source receipt shape")?;
        let envelope = super::codec::decode_compressed(&self.bytes).map_err(str::to_owned)?;
        let e = envelope
            .as_array()
            .filter(|v| v.len() == 8)
            .ok_or("Invalid retained source envelope")?;
        let digest = hex(&Sha256::digest(&self.bytes));
        if values[0].as_i64() != Some(1)
            || values[1].as_str() != e[2].as_str()
            || values[2].as_str() != e[3].as_str()
            || values[3].as_i64() != e[5].as_i64()
            || values[4].as_str() != Some(digest.as_str())
            || values[5].as_bool() != Some(true)
            || values[6].as_str() != Some("")
        {
            return Err("Source capture was not confirmed for the original bytes".into());
        }
        Ok(())
    }
}
fn field(out: &mut Vec<u8>, value: &[u8]) {
    out.extend_from_slice(&(value.len() as u32).to_le_bytes());
    out.extend_from_slice(value);
}
fn take<'a>(input: &'a [u8], at: &mut usize, max: usize) -> Result<&'a [u8], String> {
    if input.len().saturating_sub(*at) < 4 {
        return Err("Truncated source capture field".into());
    }
    let len = u32::from_le_bytes(input[*at..*at + 4].try_into().unwrap()) as usize;
    *at += 4;
    if len > max || len > input.len().saturating_sub(*at) {
        return Err("Invalid source capture field bound".into());
    }
    let result = &input[*at..*at + len];
    *at += len;
    Ok(result)
}
fn hex(bytes: &[u8]) -> String {
    const H: &[u8; 16] = b"0123456789ABCDEF";
    let mut s = String::with_capacity(bytes.len() * 2);
    for b in bytes {
        s.push(H[(b >> 4) as usize] as char);
        s.push(H[(b & 15) as usize] as char);
    }
    s
}
fn group(subject: &str, bytes: &[u8]) -> Result<String, String> {
    let value = super::codec::decode_compressed(bytes).map_err(str::to_owned)?;
    let e = value
        .as_array()
        .filter(|e| e.len() == 8)
        .ok_or("Invalid native source envelope")?;
    let role = if subject.contains(".publications.Options.") {
        "Options"
    } else {
        "Futures"
    };
    Ok(format!(
        "{role}.{}",
        e[0].as_str().ok_or("Missing source dataset")?
    ))
}
struct State {
    flushing: bool,
    failed: bool,
    file: File,
    path: PathBuf,
    bytes: u64,
    next: u64,
    pending: BTreeMap<u64, Meta>,
    index: BTreeMap<[u8; 32], u64>,
    acked: BTreeSet<u64>,
    segments: BTreeMap<PathBuf, Vec<u64>>,
}
#[derive(Clone, Copy)]
struct Limits { bytes: u64, pending: usize, segment: u64 }
impl Default for Limits { fn default()->Self { Self { bytes:MAX_BYTES,pending:MAX_PENDING,segment:SEGMENT_BYTES } } }
/// A single runtime owns this journal across every dataset generation.
pub(crate) struct Sink {
    limits: Limits,
    commit: Mutex<()>,
    state: Mutex<State>,
    root: PathBuf,
    _lease: File,
    notify: Notify,
    _scope: String,
}
impl Sink {
    pub(crate) fn checkpoint_floor(&self)->Result<rmpv::Value,String>{
        use rmpv::Value as V;
        let _commit=self.commit.lock().map_err(|_|"Native checkpoint source commit interrupted")?;
        let state=self.state.lock().map_err(|_|"Native checkpoint source owner interrupted")?;
        if state.failed||state.flushing{return Err("Native source checkpoint has uncertain ownership".into());}
        let namespace=std::fs::read(self.root.join("namespace.sha256")).map_err(|error|error.to_string())?;
        if namespace.len()!=32{return Err("Native checkpoint broker namespace invalid".into());}
        let mut files=Vec::new();
        for path in state.segments.keys().cloned().chain([self.root.join("confirmed.bin")]){
            let mut file=match File::open(&path){Ok(file)=>file,Err(error)if error.kind()==std::io::ErrorKind::NotFound&&path.ends_with("confirmed.bin")=>continue,Err(error)=>return Err(error.to_string())};
            let length=file.metadata().map_err(|error|error.to_string())?.len();
            if path.extension().is_some_and(|extension|extension=="pubwal")&&length==8{continue;}
            let tail=length.min(64);file.seek(SeekFrom::End(-(tail as i64))).map_err(|error|error.to_string())?;let mut bytes=vec![0;tail as usize];file.read_exact(&mut bytes).map_err(|error|error.to_string())?;
            files.push(V::Array(vec![path.file_name().and_then(|name|name.to_str()).ok_or("Checkpoint source filename invalid")?.into(),length.into(),V::Binary(Sha256::digest(&bytes).to_vec())]));
        }
        files.sort_by(|left,right|left.as_array().unwrap()[0].as_str().cmp(&right.as_array().unwrap()[0].as_str()));
        let pending=state.pending.iter().map(|(ordinal,meta)|V::Array(vec![(*ordinal).into(),V::Binary(meta.key.to_vec()),meta.path.file_name().and_then(|name|name.to_str()).unwrap_or("").into(),meta.offset.into(),(meta.length as u64).into(),meta.group.clone().into()])).collect();
        Ok(V::Array(vec![1.into(),self._scope.clone().into(),V::Binary(namespace),state.next.into(),V::Array(files),V::Array(pending),self.root.canonicalize().map_err(|error|error.to_string())?.to_str().ok_or("Native source checkpoint root is not UTF8")?.into()]))
    }
    pub(crate) fn verify_checkpoint_floor(&self,value:&rmpv::Value)->Result<(),String>{
        let actual=self.checkpoint_floor()?;let original=crate::checkpoint::fields(value,7)?;let mut relocated=original.to_vec();relocated[6]=actual.as_array().unwrap()[6].clone();
        if actual!=rmpv::Value::Array(relocated){return Err("Original native checkpoint source floor differs; restore exact original prefixes first".into());}Ok(())
    }
    pub(crate) fn open(root: PathBuf, broker: BrokerSettings, scope: String) -> Result<Arc<Self>, String> {
        Self::open_with_limits(root,broker,scope,Limits::default())
    }
    fn open_with_limits(root:PathBuf,broker:BrokerSettings,scope:String,limits:Limits)->Result<Arc<Self>,String>{
        if limits.bytes<64||limits.pending==0||limits.segment<8{return Err("Invalid native source capacity limits".into());}
        std::fs::create_dir_all(&root).map_err(|e| e.to_string())?;
        let lease = OpenOptions::new()
            .read(true)
            .write(true)
            .create(true)
            .truncate(false)
            .open(root.join("owner.lock"))
            .map_err(|e| e.to_string())?;
        lease
            .try_lock()
            .map_err(|_| "Another runtime owns native source publications")?;
        crate::checkpoint::validate_restore_path(&scope,&root)?;
        let identity = crate::checkpoint::logical_namespace(&scope,broker.source_namespace_digest());
        let identity_path = root.join("namespace.sha256");
        match std::fs::read(&identity_path) {
            Ok(saved) if saved != identity => {
                return Err("Native source journal belongs to another broker namespace".into());
            }
            Ok(_) => {}
            Err(e) if e.kind() == std::io::ErrorKind::NotFound => {
                let mut file = OpenOptions::new()
                    .write(true)
                    .create_new(true)
                    .open(&identity_path)
                    .map_err(|e| e.to_string())?;
                file.write_all(&identity)
                    .and_then(|()| file.sync_all())
                    .map_err(|e| e.to_string())?;
            }
            Err(e) => return Err(e.to_string()),
        }

        let mut pending = BTreeMap::new();
        let mut index = BTreeMap::new();
        let mut segments = BTreeMap::new();
        let mut next = 1u64;
        let mut total = 0u64;
        let mut acked = BTreeSet::new();
        if let Ok(mut ack) = File::open(root.join("confirmed.bin")) {
            let mut data = Vec::new();
            ack.read_to_end(&mut data).map_err(|e| e.to_string())?;
            if data.len() % 40 != 0 {
                return Err("Partial native source receipt journal".into());
            }
            for row in data.chunks_exact(40) {
                if Sha256::digest(&row[..8])[..] != row[8..] {
                    return Err("Native source receipt checksum mismatch".into());
                }
                let id = u64::from_le_bytes(row[..8].try_into().unwrap());
                acked.insert(id);
                next = next.max(id.checked_add(1).ok_or("Source ordinal exhausted")?);
            }
        }
        for entry in std::fs::read_dir(&root).map_err(|e| e.to_string())? {
            let entry = entry.map_err(|e| e.to_string())?;
            if entry.path().extension().is_none_or(|s| s != "pubwal") {
                continue;
            }
            let path = entry.path();
            let mut file = File::open(&path).map_err(|e| e.to_string())?;
            total = total
                .checked_add(file.metadata().map_err(|e| e.to_string())?.len())
                .ok_or("Source spool byte overflow")?;
            let mut magic = [0u8; 8];
            file.read_exact(&mut magic)
                .map_err(|_| "Partial source segment header")?;
            if &magic != MAGIC {
                return Err("Unknown source segment version".into());
            }
            let mut ids = Vec::new();
            loop {
                let offset = file.stream_position().map_err(|e| e.to_string())?;
                let mut len = [0u8; 4];
                let got = file.read(&mut len).map_err(|e| e.to_string())?;
                if got == 0 {
                    break;
                }
                if got != 4 {
                    return Err(
                        "Partial native source capture length; authentic recovery required".into(),
                    );
                }
                let length = u32::from_le_bytes(len) as usize;
                if length > MAX_RECORD || length < 52 {
                    return Err("Invalid source capture length".into());
                }
                let mut body = vec![0u8; length];
                let mut digest = [0u8; 32];
                file.read_exact(&mut body)
                    .and_then(|()| file.read_exact(&mut digest))
                    .map_err(|_| "Partial native source capture; authentic recovery required")?;
                if Sha256::digest(&body)[..] != digest {
                    return Err("Native source capture checksum mismatch".into());
                }
                let record = Record::decode(&body)?;
                next = next.max(
                    record
                        .ordinal
                        .checked_add(1)
                        .ok_or("Source ordinal exhausted")?,
                );
                ids.push(record.ordinal);
                if !acked.contains(&record.ordinal) {
                    if pending.len() >= limits.pending
                        || index.insert(record.key, record.ordinal).is_some()
                    {
                        return Err("Native source capacity or duplicate pending identity".into());
                    }
                    let g = group(&record.subject, &record.bytes)?;
                    pending.insert(
                        record.ordinal,
                        Meta {
                            key: record.key,
                            path: path.clone(),
                            offset,
                            length,
                            group: g,
                            trackers: vec![],
                        },
                    );
                }
            }
            if ids.is_empty(){
                // An empty segment owns no publication or receipt. Reclaim only
                // these provably empty restart headers before reserving another.
                total=total.saturating_sub(file.metadata().map_err(|e|e.to_string())?.len());
                drop(file);std::fs::remove_file(&path).map_err(|e|e.to_string())?;
            }else{segments.insert(path, ids);}
        }
        if total.saturating_add(8) > limits.bytes {
            return Err("Native source spool capacity exceeded; no history evicted".into());
        }
        let path = root.join(format!("{:032x}.pubwal", rand::random::<u128>().max(1)));
        let mut file = OpenOptions::new()
            .read(true)
            .write(true)
            .create_new(true)
            .open(&path)
            .map_err(|e| e.to_string())?;
        file.write_all(MAGIC)
            .and_then(|()| file.sync_all())
            .map_err(|e| e.to_string())?;
        segments.insert(path.clone(), vec![]);
        Ok(Arc::new(Self {
            limits,
            commit: Mutex::new(()),
            state: Mutex::new(State {
                flushing: false,
                failed: false,
                file,
                path,
                bytes: total + 8,
                next,
                pending,
                index,
                acked,
                segments,
            }),
            root,
            _lease: lease,
            notify: Notify::new(),
            _scope: scope,
        }))
    }
    fn prefix_manifest(&self,lease:u64)->Result<String,String>{
        let _commit=self.commit.lock().map_err(|_|"Native source commit interrupted")?;
        let state=self.state.lock().map_err(|_|"Native source state interrupted")?;
        if state.failed||state.flushing{return Err("Native source prefix has an uncertain write".into());}
        let mut files=Vec::with_capacity(state.segments.len()+2);
        for path in state.segments.keys().cloned().chain([self.root.join("namespace.sha256"),self.root.join("confirmed.bin")]){
            match std::fs::metadata(&path){Ok(metadata)=>files.push(serde_json::json!({"RelativePath":path.file_name().and_then(|value|value.to_str()).ok_or("Invalid source prefix filename")?,"Length":metadata.len()})),Err(error)if error.kind()==std::io::ErrorKind::NotFound&&path.ends_with("confirmed.bin")=>{},Err(error)=>return Err(error.to_string())}
        }
        let mut seen=BTreeSet::new();let mut heads=Vec::new();
        for(ordinal,meta)in &state.pending{
            if !seen.insert(meta.group.clone()){continue;}
            let source=Self::read(meta)?;let decoded=super::codec::decode_compressed(&source.bytes).map_err(str::to_owned)?;let fields=decoded.as_array().ok_or("Invalid original source prefix")?;
            heads.push(serde_json::json!({"Group":meta.group,"Ordinal":ordinal,"Dataset":fields[0].as_str(),"ValueDate":fields[1].as_i64(),"WorkerInstanceId":fields[2].as_str(),"GenerationId":fields[3].as_str(),"ManifestRevision":fields[4].as_i64(),"Sequence":fields[5].as_i64(),"WireSha256":hex(&Sha256::digest(&source.bytes))}));
        }
        serde_json::to_string(&serde_json::json!({"Version":1,"Scope":self._scope,"ForwardingLease":lease,"SourceRoot":self.root,"NextOrdinal":state.next,"PendingCount":state.pending.len(),"Files":files,"QueuedHeads":heads,"MapperCheckpoint":false,"RequiredIntentCheckpoint":false})).map_err(|error|error.to_string())
    }
    /// Captures complete original wire bytes before publication admission advances.
    pub(crate) fn capture(
        &self,
        subject: &str,
        bytes: &[u8],
        origin: Option<&str>,
        key: [u8; 32],
        tracker: Tracker,
    ) -> Result<(), String> {
        self.capture_batch(&[(subject,bytes,origin,key)],tracker)
    }
    /// Commits one finite group. No relay can observe any newly appended record before the shared fsync succeeds.
    pub(crate) fn capture_batch(
        &self,
        frames: &[(&str, &[u8], Option<&str>, [u8; 32])],
        tracker: Tracker,
    ) -> Result<(), String> {
        if frames.is_empty() || frames.len() > 64 {
            return Err("Native source commit group exceeds bound".into());
        }
        let _commit = self
            .commit
            .lock()
            .map_err(|_| "Native source commit interrupted")?;
        {
            let mut state = self
                .state
                .lock()
                .map_err(|_| "Native source state interrupted")?;
            if state.failed {
                return Err("Native source commit previously failed; recovery is required".into());
            }
            self.preflight(&state,frames)?;
            state.flushing = true;
        }
        let result = (|| {
            for (subject, bytes, origin, key) in frames {
                self.capture_inner(subject, bytes, *origin, *key, tracker.clone(), false)?;
            }
            let state = self
                .state
                .lock()
                .map_err(|_| "Native source state interrupted")?;
            state.file.sync_data().map_err(|e| e.to_string())
        })();
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Native source state interrupted")?;
        if result.is_ok() {
            state.flushing = false;
        } else {
            state.failed = true;
        }
        drop(state);
        self.notify.notify_one();
        result
    }
    // Definite refusal must not poison the relay: all identities and capacity are
    // checked before any group write. Only an uncertain IO failure after this
    // boundary requires cold reconciliation.
    fn preflight(&self,state:&State,frames:&[(&str,&[u8],Option<&str>,[u8;32])])->Result<(),String>{
        let mut additions=BTreeMap::<[u8;32],(String,Vec<u8>,Option<String>)>::new();
        let mut bytes=state.bytes;let mut segment=state.file.metadata().map_err(|e|e.to_string())?.len();
        let mut next=state.next;
        for(subject,wire,origin,key)in frames {
            if let Some(id)=state.index.get(key){
                let original=Self::read(state.pending.get(id).ok_or("Missing native source owner")?)?;
                if original.subject!=*subject||original.bytes!=*wire||original.origin.as_deref()!=*origin{return Err("Native source identity reused with different bytes".into());}
                continue;
            }
            if let Some((prior_subject,prior_wire,prior_origin))=additions.get(key){
                if prior_subject!=subject||prior_wire.as_slice()!=*wire||prior_origin.as_deref()!=*origin{return Err("Native source group identity reused with different bytes".into());}
                continue;
            }
            if state.pending.len()+additions.len()>=self.limits.pending{return Err("Native source pending capacity exhausted; source retained".into());}
            let _=group(subject,wire)?;
            let record=Record{ordinal:next,key:*key,subject:(*subject).into(),origin:origin.map(str::to_owned),bytes:wire.to_vec()};
            let size=record.encode()?.len()as u64+36;
            if segment>=self.limits.segment {bytes=bytes.checked_add(8).ok_or("Source spool byte overflow")?;segment=8;}
            bytes=bytes.checked_add(size).ok_or("Source spool byte overflow")?;
            if bytes>self.limits.bytes.saturating_sub(8){return Err("Native source disk capacity exhausted; restart header headroom and pending history retained".into());}
            segment+=size;next=next.checked_add(1).ok_or("Source ordinal exhausted")?;
            additions.insert(*key,((*subject).into(),wire.to_vec(),origin.map(str::to_owned)));
        }
        Ok(())
    }
    fn capture_inner(
        &self,
        subject: &str,
        bytes: &[u8],
        origin: Option<&str>,
        key: [u8; 32],
        tracker: Tracker,
        sync: bool,
    ) -> Result<(), String> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Native source journal interrupted")?;
        if state.failed {
            return Err("Native source commit previously failed; recovery is required".into());
        }
        if let Some(id) = state.index.get(&key).copied() {
            let meta = state
                .pending
                .get_mut(&id)
                .ok_or("Missing native source owner")?;
            let record = Self::read(meta)?;
            if record.bytes != bytes
                || record.subject != subject
                || record.origin.as_deref() != origin
            {
                return Err("Native source identity reused with different bytes".into());
            }
            if !meta.trackers.iter().any(|t| t.id == tracker.id) {
                tracker.admit(id);
                meta.trackers.push(tracker);
            }
            self.notify.notify_one();
            return Ok(());
        }
        if state.pending.len() >= self.limits.pending {
            return Err("Native source pending capacity exhausted; source retained".into());
        }
        let ordinal = state.next;
        let record = Record {
            ordinal,
            key,
            subject: subject.into(),
            origin: origin.map(str::to_owned),
            bytes: bytes.to_vec(),
        };
        let group = group(subject, bytes)?;
        let body = record.encode()?;
        let size = 4 + body.len() as u64 + 32;
        let rotate=state.file.metadata().map_err(|e|e.to_string())?.len()>=self.limits.segment;
        if size.saturating_add(if rotate {8}else{0}) > self.limits.bytes.saturating_sub(8).saturating_sub(state.bytes) {
            return Err("Native source disk capacity exhausted; no pending history evicted".into());
        }
        if rotate {
            state.file.sync_all().map_err(|e| e.to_string())?;
            let path = self
                .root
                .join(format!("{:032x}.pubwal", rand::random::<u128>().max(1)));
            let mut file = OpenOptions::new()
                .read(true)
                .write(true)
                .create_new(true)
                .open(&path)
                .map_err(|e| e.to_string())?;
            file.write_all(MAGIC)
                .and_then(|()| file.sync_all())
                .map_err(|e| e.to_string())?;
            state.file = file;
            state.path = path.clone();
            state.segments.insert(path, vec![]);
            state.bytes += 8;
        }
        let offset = state.file.stream_position().map_err(|e| e.to_string())?;
        state
            .file
            .write_all(&(body.len() as u32).to_le_bytes())
            .and_then(|()| state.file.write_all(&body))
            .and_then(|()| state.file.write_all(&Sha256::digest(&body)))
            .and_then(|()| if sync { state.file.sync_data() } else { Ok(()) })
            .map_err(|e| format!("Native source durable capture failed: {e}"))?;
        let path = state.path.clone();
        state.segments.get_mut(&path).unwrap().push(ordinal);
        state.next = ordinal.checked_add(1).ok_or("Source ordinal exhausted")?;
        state.bytes += size;
        tracker.admit(ordinal);
        state.pending.insert(
            ordinal,
            Meta {
                key,
                path,
                offset,
                length: body.len(),
                group,
                trackers: vec![tracker],
            },
        );
        state.index.insert(key, ordinal);
        drop(state);
        self.notify.notify_one();
        Ok(())
    }
    fn reconcile_required(&self, lane: crate::dataset::transport::Lane) -> Result<(), String> {
        let Some(raw) = std::env::var_os("IFM_RAW_FUTURES_STORAGE_ROOT")
            .filter(|root| !root.is_empty())
            .map(PathBuf::from)
        else {
            return Ok(());
        };
        self.reconcile_required_from(lane,&raw)
    }
    pub(crate) fn reconcile_required_from(&self,lane:crate::dataset::transport::Lane,raw:&std::path::Path)->Result<(),String>{
        let mut intent = crate::dataset::required_capture::RequiredCaptureIntent::open(
            raw,
            &format!("{}-{}", self._scope, lane as u8),
        )?;
        if intent.original().is_none() {
            return Ok(());
        }
        let frozen=intent.frozen().ok_or("Authentic required source intent exists without frozen mapped wire; reconcile the original statistical/control evidence before provider replacement")?;
        let mut input = std::io::Cursor::new(frozen);
        let value = rmpv::decode::read_value(&mut input)
            .map_err(|_| "Invalid required source frozen wire")?;
        if input.position() != frozen.len() as u64 {
            return Err("Required source frozen wire has trailing bytes".into());
        }
        let parts = value
            .as_array()
            .filter(|parts| parts.len() == 2 && parts[0].as_u64() == Some(1))
            .ok_or("Invalid required source frozen version")?;
        let records = parts[1]
            .as_array()
            .filter(|records| records.len() <= 65536)
            .ok_or("Required frozen source count exceeds bound")?;
        let expected = format!("ifm.{}.marketdata.v1.publications.Futures.", self._scope);
        let mut sources = Vec::with_capacity(records.len());
        for record in records {
            let fields = record
                .as_array()
                .filter(|fields| fields.len() == 4)
                .ok_or("Invalid frozen source record")?;
            let subject = fields[0]
                .as_str()
                .filter(|subject| subject.starts_with(&expected) && subject.len() <= 512)
                .ok_or("Frozen source namespace differs")?;
            let bytes = fields[1]
                .as_slice()
                .filter(|bytes| !bytes.is_empty() && bytes.len() <= MAX_RECORD)
                .ok_or("Frozen source bytes exceed bound")?;
            let origin = if fields[2].is_nil() {
                None
            } else {
                Some(
                    fields[2]
                        .as_str()
                        .filter(|origin| origin.len() <= 2048)
                        .ok_or("Invalid frozen source origin")?,
                )
            };
            let key: [u8; 32] = fields[3]
                .as_slice()
                .ok_or("Missing frozen source key")?
                .try_into()
                .map_err(|_| "Invalid frozen source key")?;
            sources.push((subject, bytes, origin, key));
        }
        let tracker = Tracker::new(watch::channel(Status::default()).0);
        for chunk in sources.chunks(64) {
            self.capture_batch(chunk, tracker.clone())?;
        }
        intent.complete()
    }
    /// Generation changes wait until the previous source's required ownership is confirmed.
    pub(crate) fn ensure_drained(
        &self,
        lane: crate::dataset::transport::Lane,
    ) -> Result<(), String> {
        if lane != crate::dataset::transport::Lane::OptionsGlbx {
            self.reconcile_required(lane)?;
        }
        let role = if lane == crate::dataset::transport::Lane::OptionsGlbx {
            "Options"
        } else {
            "Futures"
        };
        let group = format!("{role}.{}", lane.dataset());
        let state = self
            .state
            .lock()
            .map_err(|_| "Native source journal interrupted")?;
        if state.pending.values().any(|entry| entry.group == group) {
            return Err(format!(
                "Native durable source generation transition is waiting for required ownership: {group}"
            ));
        }
        Ok(())
    }
    /// Waits for the original retained ownership within the already accepted
    /// start budget. No replacement generation or provider is started here.
    pub(crate) async fn wait_drained(&self,lane:crate::dataset::transport::Lane,stop:&CancellationToken,deadline:crate::runtime::Deadline)->Result<(),String>{
        if lane!=crate::dataset::transport::Lane::OptionsGlbx{self.reconcile_required(lane)?;}
        let role=if lane==crate::dataset::transport::Lane::OptionsGlbx{"Options"}else{"Futures"};let group=format!("{role}.{}",lane.dataset());
        deadline.run(stop,async{
            loop{
                {
                    let state=self.state.lock().map_err(|_|"Original source journal interrupted")?;
                    if state.failed{return Err("Original source journal has an uncertain write; startup remains blocked".into());}
                    if !state.pending.values().any(|entry|entry.group==group){return Ok(());}
                }
                tokio::time::sleep(Duration::from_millis(10)).await;
            }
        }).await.map_err(|_|"Original accepted native startup deadline expired while waiting for retained source ownership".to_owned())?
    }
    fn read(meta: &Meta) -> Result<Record, String> {
        let mut file = File::open(&meta.path).map_err(|e| e.to_string())?;
        file.seek(SeekFrom::Start(meta.offset + 4))
            .map_err(|e| e.to_string())?;
        let mut body = vec![0u8; meta.length];
        let mut digest = [0u8; 32];
        file.read_exact(&mut body)
            .and_then(|()| file.read_exact(&mut digest))
            .map_err(|e| e.to_string())?;
        if Sha256::digest(&body)[..] != digest {
            return Err("Pending source checksum changed".into());
        }
        Record::decode(&body)
    }
    #[cfg(test)]
    pub(crate) fn pending_wire_for_test(&self)->Result<Vec<Vec<u8>>,String>{Ok(self.heads()?.into_iter().map(|record|record.bytes).collect())}
    #[cfg(test)]
    pub(crate) fn confirm_original_for_test(&self,original:&Frame)->Result<(),String>{
        let bytes=original.encode().map_err(str::to_owned)?;
        let record=self.heads()?.into_iter().find(|record|record.bytes==bytes).ok_or("Original test frame is not pending")?;
        let envelope=super::codec::decode_compressed(&record.bytes).map_err(str::to_owned)?;
        let fields=envelope.as_array().ok_or("Invalid original test frame")?;
        let receipt=super::codec::encode(&rmpv::Value::Array(vec![1.into(),fields[2].clone(),fields[3].clone(),fields[5].clone(),hex(&Sha256::digest(&record.bytes)).into(),true.into(),"".into()])).map_err(str::to_owned)?;
        record.accepts(&receipt)?;self.confirm(&record)
    }
    fn heads(&self) -> Result<Vec<Record>, String> {
        let state = self
            .state
            .lock()
            .map_err(|_| "Native source journal interrupted")?;
        if state.flushing {
            return Ok(vec![]);
        }
        let mut groups = BTreeSet::new();
        state
            .pending
            .values()
            .filter(|m| groups.insert(m.group.clone()))
            .take(3)
            .map(Self::read)
            .collect()
    }
    fn confirm(&self, record: &Record) -> Result<(), String> {
        self.confirm_batch(std::slice::from_ref(record))
    }
    fn confirm_batch(&self, records: &[Record]) -> Result<(), String> {
        if records.is_empty() || records.len() > 64 {
            return Err("Native receipt group exceeds bound".into());
        }
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Native source journal interrupted")?;
        let mut bytes = Vec::with_capacity(records.len() * 40);
        for record in records {
            let meta = state
                .pending
                .get(&record.ordinal)
                .ok_or("Missing pending source")?;
            if meta.key != record.key {
                return Err("Native source receipt owner changed".into());
            }
            let id = record.ordinal.to_le_bytes();
            bytes.extend_from_slice(&id);
            bytes.extend_from_slice(&Sha256::digest(id));
        }
        let mut ack = OpenOptions::new()
            .append(true)
            .create(true)
            .open(self.root.join("confirmed.bin"))
            .map_err(|e| e.to_string())?;
        ack.write_all(&bytes)
            .and_then(|()| ack.sync_data())
            .map_err(|e| e.to_string())?;
        for record in records {
            state.acked.insert(record.ordinal);
            let meta = state.pending.remove(&record.ordinal).unwrap();
            state.index.remove(&record.key);
            for tracker in meta.trackers {
                tracker.complete(record.ordinal);
            }
        }
        self.cleanup(&mut state)
    }
    fn batches(&self) -> Result<Vec<Vec<Record>>, String> {
        let state = self
            .state
            .lock()
            .map_err(|_| "Native source journal interrupted")?;
        if state.flushing {
            return Ok(vec![]);
        }
        let groups = state
            .pending
            .values()
            .map(|m| m.group.clone())
            .collect::<BTreeSet<_>>();
        let mut batches = Vec::new();
        for group in groups.into_iter().take(3) {
            let mut records = Vec::new();
            let mut identity = None;
            let mut bytes = 40;
            let mut decoded=0;
            for meta in state
                .pending
                .values()
                .filter(|meta| meta.group == group)
                .take(64)
            {
                let record = Self::read(meta)?;
                let expanded=super::codec::expanded_length(&record.bytes).map_err(str::to_owned)?;
                if expanded>576*1024{if records.is_empty(){records.push(record);}break;}
                if !records.is_empty()&&decoded+expanded>576*1024{break;}
                let envelope =
                    super::codec::decode_compressed(&record.bytes).map_err(str::to_owned)?;
                let fields = envelope
                    .as_array()
                    .filter(|e| e.len() == 8)
                    .ok_or("Invalid retained source identity")?;
                let actual = fields[1..5].to_vec();
                if identity.as_ref().is_some_and(|prior| *prior != actual) {
                    break;
                }
                let charge =
                    record.bytes.len() + record.origin.as_ref().map_or(0, String::len) + 16;
                if !records.is_empty() && bytes + charge > 512 * 1024 {
                    break;
                }
                bytes += charge;decoded+=expanded;
                identity = Some(actual);
                records.push(record);
            }
            if !records.is_empty() {
                batches.push(records);
            }
        }
        Ok(batches)
    }
    fn cleanup(&self, state: &mut State) -> Result<(), String> {
        let retired = state
            .segments
            .iter()
            .filter(|(p, ids)| **p != state.path && ids.iter().all(|id| state.acked.contains(id)))
            .map(|(p, _)| p.clone())
            .collect::<Vec<_>>();
        if retired.is_empty() {
            return Ok(());
        }
        for path in retired {
            let size = std::fs::metadata(&path).map_err(|e| e.to_string())?.len();
            std::fs::remove_file(&path).map_err(|e| e.to_string())?;
            for id in state.segments.remove(&path).unwrap() {
                state.acked.remove(&id);
            }
            state.bytes = state.bytes.saturating_sub(size);
        }
        let temporary = self.root.join("confirmed.pending");
        let mut ack = File::create(&temporary).map_err(|e| e.to_string())?;
        for id in &state.acked {
            let bytes = id.to_le_bytes();
            ack.write_all(&bytes)
                .and_then(|()| ack.write_all(&Sha256::digest(bytes)))
                .map_err(|e| e.to_string())?;
        }
        ack.sync_all().map_err(|e| e.to_string())?;
        drop(ack);
        std::fs::rename(temporary, self.root.join("confirmed.bin")).map_err(|e| e.to_string())?;
        Ok(())
    }
}

fn batch_wire(records: &[Record]) -> Result<Vec<u8>, String> {
    if records.is_empty() || records.len() > 64 {
        return Err("Native source request group exceeds bound".into());
    }
    let values = records
        .iter()
        .map(|record| {
            rmpv::Value::Array(vec![
                rmpv::Value::Binary(record.bytes.clone()),
                record.origin.clone().map_or(rmpv::Value::Nil, Into::into),
            ])
        })
        .collect();
    let wire = super::codec::encode(&rmpv::Value::Array(vec![
        2.into(),
        rmpv::Value::Array(values),
    ]))
    .map_err(str::to_owned)?;
    if wire.len() > 512 * 1024 {
        return Err("Native source request group exceeds broker wire bound".into());
    }
    Ok(wire)
}
fn accepts_batch(records: &[Record], reply: &[u8]) -> Result<(), String> {
    if reply.len() > 128 * 1024 {
        return Err("Native source group receipt exceeds bound".into());
    }
    let mut input = std::io::Cursor::new(reply);
    let value = rmpv::decode::read_value(&mut input).map_err(|_| "Invalid source group receipt")?;
    if input.position() != reply.len() as u64 {
        return Err("Source group receipt has trailing bytes".into());
    }
    let parts = value
        .as_array()
        .filter(|parts| parts.len() == 2)
        .ok_or("Invalid source group receipt shape")?;
    let receipts = parts[1]
        .as_array()
        .filter(|receipts| receipts.len() == records.len())
        .ok_or("Source group receipt identity count differs")?;
    if parts[0].as_u64() != Some(2) {
        return Err("Unsupported source group receipt version".into());
    }
    for (record, receipt) in records.iter().zip(receipts) {
        record.accepts(&super::codec::encode(receipt).map_err(str::to_owned)?)?;
    }
    Ok(())
}

async fn negotiate_source(publisher:&Publisher,receipts:&mut tokio::sync::mpsc::Receiver<Vec<u8>>,scope:&str,stop:&tokio_util::sync::CancellationToken)->Result<(),String>{
    let full=format!("ifm.{scope}.marketdata.v1");let body=super::codec::encode(&rmpv::Value::Array(vec![2.into(),full.clone().into()])).map_err(str::to_owned)?;
    publisher.try_source_capture(&format!("{full}.source-capture.hello"),&body,None).map_err(str::to_owned)?;
    let reply=tokio::select!{biased;()=stop.cancelled()=>return Err("Source capability negotiation cancelled".into()),reply=tokio::time::timeout(Duration::from_secs(2),receipts.recv())=>reply.map_err(|_|"Source capability negotiation timed out")?.ok_or("Source capability response closed")?};
    let value=rmpv::decode::read_value(&mut std::io::Cursor::new(reply)).map_err(|_|"Invalid source capability response")?;
    let fields=value.as_array().filter(|fields|fields.len()==3).ok_or("Invalid source capability response shape")?;
    if fields[0].as_u64()!=Some(2)||fields[1].as_str()!=Some(full.as_str())||fields[2].as_bool()!=Some(true){return Err("API does not confirm required source protocol2".into());}Ok(())
}

/// Starts beneath the non-replaceable notification owner. Existing pending work is replayed immediately.
pub(crate) fn start(
    parent: Arc<Child>,
    broker: BrokerSettings,
    scope: String,
) -> Result<Arc<Sink>, String> {
    let sink = Sink::open(location(&scope)?, broker.clone(), scope.clone())?;
    for lane in [
        crate::dataset::transport::Lane::FuturesGlbx,
        crate::dataset::transport::Lane::FuturesXcbf,
    ] {
        sink.reconcile_required(lane)?;
    }
    let mut registry = REGISTRY
        .get_or_init(Mutex::default)
        .lock()
        .map_err(|_| "Source capture registry interrupted")?;
    registry.retain(|_, v| v.strong_count() > 0);
    if registry.contains_key(&scope) {
        return Err("Native source scope already has a runtime owner".into());
    }
    registry.insert(scope.clone(), Arc::downgrade(&sink));
    drop(registry);
    let running = sink.clone();
    let child = parent.clone();
    parent.spawn_joined(move|stop|async move{
        while !stop.is_cancelled(){
            let deadline=Deadline::recovery();let exchange=Publisher::start_evidence_exchange(broker.clone(),scope.clone(),16,4*1024*1024,deadline,&child);
            let Ok((mut publisher,mut receipts))=exchange else{tokio::select!{()=stop.cancelled()=>break,()=tokio::time::sleep(Duration::from_millis(100))=>continue}};
            let mut progress=publisher.changes();
            if negotiate_source(&publisher,&mut receipts,&scope,&stop).await.is_err(){let _=publisher.shutdown(Deadline::recovery()).await;tokio::select!{()=stop.cancelled()=>break,()=tokio::time::sleep(Duration::from_millis(100))=>continue};}
            'connected:loop{
                if stop.is_cancelled(){break;}
                let heads=match running.batches(){Ok(h)=>h,Err(e)=>{eprintln!("NativeSourceCaptureBlocked: {e}");break;}};
                if heads.is_empty(){tokio::select!{()=stop.cancelled()=>break,()=running.notify.notified()=>continue};}
                let mut failed=false;
                for records in heads{
                    let Some(_forwarding)=super::source_forwarding::try_begin()else{failed=true;break;};
                    let record=&records[0];
                    let role=if record.subject.contains(".publications.Options."){"Options"}else{"Futures"};
                    let grouped=records.len()>1;
                    let endpoint=if grouped{format!("ifm.{scope}.marketdata.v1.source-capture.batch.{role}")}else{format!("ifm.{scope}.marketdata.v1.source-capture.{role}")};
                    let wire=if grouped{match batch_wire(&records){Ok(wire)=>wire,Err(error)=>{eprintln!("NativeSourceBatchBlocked: {error}");failed=true;continue;}}}else{record.bytes.clone()};
                    if publisher.try_source_capture(&endpoint,&wire,if grouped{None}else{record.origin.as_deref()}).is_err(){failed=true;break;}
                    let wait=tokio::time::sleep(Duration::from_secs(2));tokio::pin!(wait);
                    loop{tokio::select!{biased;()=stop.cancelled()=>break 'connected,()=&mut wait=>{failed=true;break;},changed=progress.changed()=>{
                        if changed.is_err()||progress.borrow().failure.is_some()||progress.borrow().stopped{failed=true;break;}
                    },reply=receipts.recv()=>{match reply{Some(reply)=>{if (if grouped{accepts_batch(&records,&reply)}else{record.accepts(&reply)}).is_ok(){if let Err(e)=running.confirm_batch(&records){eprintln!("NativeSourceCaptureConfirmationBlocked: {e}");failed=true;}break;}failed=true;break;},None=>{failed=true;break;}}}}}
                    // One blocked source group does not prevent another dataset's capture.
                }
                if failed{tokio::select!{()=stop.cancelled()=>break,()=tokio::time::sleep(Duration::from_millis(100))=>{}}}
                if publisher.status().failure.is_some()||publisher.status().stopped{break;}
            }
            let _=publisher.shutdown(Deadline::recovery()).await;
        }
    }).map_err(|e|format!("Native source relay admission failed: {e:?}"))?;
    Ok(sink)
}

/// Stable source identity is independent from compression, application clocks and retry tasks.
pub(crate) fn key(frame: &Frame, subject: &str) -> [u8; 32] {
    let mut h = Sha256::new();
    h.update(subject.as_bytes());
    h.update(frame.worker);
    h.update(frame.generation);
    h.update(frame.sequence.to_le_bytes());
    h.finalize().into()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::dataset::transport::Lane;
    fn broker() -> BrokerSettings {
        BrokerSettings::new(
            "nats://127.0.0.1:4222".into(),
            vec!["1.1.1.1:53".parse().unwrap()],
        )
        .unwrap()
    }
    fn root() -> PathBuf {
        std::env::temp_dir().join(format!("ifm-source-test-{:032x}", rand::random::<u128>()))
    }
    fn frame(sequence: i64, generation: u8) -> Frame {
        Frame {
            lane: Lane::FuturesGlbx,
            value_date: 739945,
            worker: [1; 16],
            generation: [generation; 16],
            manifest_revision: 1,
            sequence,
            kind: 4,
            payload: vec![0x91, 0x01],
            origin_ticks: None,
        }
    }
    fn tracker() -> Tracker {
        Tracker::new(watch::channel(Status::default()).0)
    }
    fn capture(sink: &Sink, frame: &Frame) {
        let subject = frame.subject("test").unwrap();
        sink.capture(
            &subject,
            &frame.encode().unwrap(),
            None,
            key(frame, &subject),
            tracker(),
        )
        .unwrap();
    }
    fn receipt(record: &Record, captured: bool) -> Vec<u8> {
        let e = super::super::codec::decode_compressed(&record.bytes).unwrap();
        let e = e.as_array().unwrap();
        super::super::codec::encode(&rmpv::Value::Array(vec![
            1.into(),
            e[2].clone(),
            e[3].clone(),
            e[5].clone(),
            hex(&Sha256::digest(&record.bytes)).into(),
            captured.into(),
            "".into(),
        ]))
        .unwrap()
    }
    #[tokio::test]
    async fn accepted_start_waits_for_original_receipt_without_replacing_ids_or_budget(){
        let root=root();let sink=Sink::open(root.clone(),broker(),"test".into()).unwrap();let original=frame(1,2);capture(&sink,&original);
        let waiting=sink.clone();let stop=CancellationToken::new();let token=stop.clone();let deadline=Deadline::after(Duration::from_secs(1));
        let task=tokio::spawn(async move{waiting.wait_drained(Lane::FuturesGlbx,&token,deadline).await});
        tokio::time::sleep(Duration::from_millis(30)).await;assert!(!task.is_finished());
        let originals=sink.heads().unwrap();assert_eq!(originals.len(),1);assert_eq!(originals[0].bytes,original.encode().unwrap());
        originals[0].accepts(&receipt(&originals[0],true)).unwrap();sink.confirm(&originals[0]).unwrap();
        task.await.unwrap().unwrap();assert!(sink.heads().unwrap().is_empty());drop(sink);
        let restored=Sink::open(root.clone(),broker(),"test".into()).unwrap();assert!(restored.heads().unwrap().is_empty());drop(restored);std::fs::remove_dir_all(root).unwrap();
    }
    #[tokio::test]
    async fn accepted_start_deadline_and_cancellation_leave_original_retained_ownership_intact(){
        let root=root();let sink=Sink::open(root.clone(),broker(),"test".into()).unwrap();capture(&sink,&frame(1,2));
        let before=sink.heads().unwrap()[0].bytes.clone();let deadline=Deadline::after(Duration::from_millis(25));let token=CancellationToken::new();
        assert!(sink.wait_drained(Lane::FuturesGlbx,&token,deadline).await.is_err());assert!(deadline.remaining().is_zero());
        let started=std::time::Instant::now();assert!(sink.wait_drained(Lane::FuturesGlbx,&token,deadline).await.is_err());assert!(started.elapsed()<Duration::from_millis(100));
        let cancelled=CancellationToken::new();cancelled.cancel();assert!(sink.wait_drained(Lane::FuturesGlbx,&cancelled,Deadline::after(Duration::from_secs(1))).await.is_err());
        assert_eq!(sink.heads().unwrap()[0].bytes,before);drop(sink);let restored=Sink::open(root.clone(),broker(),"test".into()).unwrap();assert_eq!(restored.heads().unwrap()[0].bytes,before);drop(restored);std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn restart_retains_original_generation_body_and_only_exact_receipt_retires_source() {
        let root = root();
        let sink = Sink::open(root.clone(), broker(), "test".into()).unwrap();
        let first = frame(1, 2);
        capture(&sink, &first);
        capture(&sink, &first);
        assert_eq!(sink.state.lock().unwrap().pending.len(), 1);
        drop(sink);
        let sink = Sink::open(root.clone(), broker(), "test".into()).unwrap();
        let records = sink.heads().unwrap();
        assert_eq!(records.len(), 1);
        assert_eq!(records[0].bytes, first.encode().unwrap());
        assert!(records[0].accepts(&receipt(&records[0], false)).is_err());
        assert!(records[0].accepts(&receipt(&records[0], true)).is_ok());
        let mut wrong = receipt(&records[0], true);
        *wrong.last_mut().unwrap() = 1;
        assert!(records[0].accepts(&wrong).is_err());
        sink.confirm(&records[0]).unwrap();
        drop(sink);
        let sink = Sink::open(root.clone(), broker(), "test".into()).unwrap();
        assert!(sink.heads().unwrap().is_empty());
        drop(sink);
        std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn binary_checkpoint_source_floor_matches_cold_exact_prefixes_and_refuses_new_or_retired_originals(){
        let root=root();let sink=Sink::open(root.clone(),broker(),"test".into()).unwrap();capture(&sink,&frame(1,2));capture(&sink,&frame(2,2));
        let floor=sink.checkpoint_floor().unwrap();let binary=crate::checkpoint::encode(&floor).unwrap();drop(sink);
        let cold=Sink::open(root.clone(),broker(),"test".into()).unwrap();cold.verify_checkpoint_floor(&crate::checkpoint::decode(&binary).unwrap()).unwrap();
        capture(&cold,&frame(3,2));assert!(cold.verify_checkpoint_floor(&floor).is_err());let next=cold.checkpoint_floor().unwrap();
        let head=cold.heads().unwrap().remove(0);cold.confirm(&head).unwrap();assert!(cold.verify_checkpoint_floor(&next).is_err());drop(cold);std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn pending_generations_keep_dataset_order_and_other_dataset_progresses() {
        let root = root();
        let sink = Sink::open(root.clone(), broker(), "test".into()).unwrap();
        capture(&sink, &frame(1, 2));
        capture(&sink, &frame(1, 3));
        let mut vx = frame(1, 4);
        vx.lane = Lane::FuturesXcbf;
        capture(&sink, &vx);
        let heads = sink.heads().unwrap();
        assert_eq!(heads.len(), 2);
        assert_eq!(heads[0].bytes, frame(1, 2).encode().unwrap());
        sink.confirm(&heads[0]).unwrap();
        assert_eq!(
            sink.heads().unwrap()[0].bytes,
            frame(1, 3).encode().unwrap()
        );
        drop(sink);
        std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn changed_immutable_source_and_corrupt_tail_fail_closed() {
        let root = root();
        let sink = Sink::open(root.clone(), broker(), "test".into()).unwrap();
        let original = frame(1, 2);
        capture(&sink, &original);
        let mut changed = original.clone();
        changed.payload = vec![0x91, 0x02];
        let subject = original.subject("test").unwrap();
        assert!(
            sink.capture(
                &subject,
                &changed.encode().unwrap(),
                None,
                key(&original, &subject),
                tracker()
            )
            .is_err()
        );
        let path = sink.state.lock().unwrap().path.clone();
        drop(sink);
        let mut file = OpenOptions::new().append(true).open(path).unwrap();
        file.write_all(&[1, 2]).unwrap();
        file.sync_all().unwrap();
        drop(file);
        assert!(Sink::open(root.clone(), broker(), "test".into()).is_err());
        std::fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn every_required_family_survives_restart_in_original_order_and_capacity_never_evicts() {
        let root = root();
        let sink = Sink::open(root.clone(), broker(), "test".into()).unwrap();
        for kind in 1..=7 {
            let mut value = frame(i64::from(kind), 2);
            value.kind = kind;
            capture(&sink, &value);
        }
        {
            let mut state = sink.state.lock().unwrap();
            state.bytes = MAX_BYTES;
        }
        let value = frame(8, 2);
        let subject = value.subject("test").unwrap();
        assert!(
            sink.capture(
                &subject,
                &value.encode().unwrap(),
                None,
                key(&value, &subject),
                tracker()
            )
            .is_err()
        );
        assert_eq!(sink.state.lock().unwrap().pending.len(), 7);
        drop(sink);
        let sink = Sink::open(root.clone(), broker(), "test".into()).unwrap();
        for kind in 1..=7 {
            let records = sink.heads().unwrap();
            assert_eq!(records.len(), 1);
            let value = super::super::codec::decode_compressed(&records[0].bytes).unwrap();
            assert_eq!(value.as_array().unwrap()[6].as_u64(), Some(kind));
            sink.confirm(&records[0]).unwrap();
        }
        assert!(sink.heads().unwrap().is_empty());
        drop(sink);
        std::fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn invalid_group_is_refused_before_any_write_without_poisoning_relay() {
        let root = root();
        let sink = Sink::open(root.clone(), broker(), "test".into()).unwrap();
        let value = frame(1, 2);
        let subject = value.subject("test").unwrap();
        let wire = value.encode().unwrap();
        let bad = frame(2, 2);
        let bad_subject = bad.subject("test").unwrap();
        assert!(
            sink.capture_batch(
                &[
                    (&subject, &wire, None, key(&value, &subject)),
                    (&bad_subject, &[0xc1], None, key(&bad, &bad_subject))
                ],
                tracker()
            )
            .is_err()
        );
        assert!(sink.heads().unwrap().is_empty());
        assert!(!sink.state.lock().unwrap().failed);
        assert!(sink.state.lock().unwrap().pending.is_empty());
        capture(&sink,&value);
        assert_eq!(sink.heads().unwrap().len(),1);
        drop(sink);
        std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    #[ignore = "Manual actual local fsync benchmark; serialize with BenchmarkDotNet runs"]
    fn measure_actual_source_capture_io() {
        const COUNT: usize = 4096;
        let inputs = (1..=COUNT)
            .map(|sequence| {
                let mut value = frame(sequence as i64, 2);
                value.kind = 1;
                value.payload = (0..512).map(|_| rand::random::<u8>()).collect();
                let subject = value.subject("test").unwrap();
                let key = key(&value, &subject);
                (subject, value.encode().unwrap(), key)
            })
            .collect::<Vec<_>>();
        for batch in [1, 8, 64] {
            let mut times = Vec::new();
            let mut disk = 0;
            for _ in 0..3 {
                let root = root();
                let sink = Sink::open(root.clone(), broker(), "test".into()).unwrap();
                let tracker = tracker();
                let started = std::time::Instant::now();
                for chunk in inputs.chunks(batch) {
                    if batch == 1 {
                        let (subject, wire, key) = &chunk[0];
                        sink.capture(subject, wire, None, *key, tracker.clone())
                            .unwrap();
                    } else {
                        let group = chunk
                            .iter()
                            .map(|(s, b, k)| (s.as_str(), b.as_slice(), None, *k))
                            .collect::<Vec<_>>();
                        sink.capture_batch(&group, tracker.clone()).unwrap();
                    }
                }
                times.push(started.elapsed().as_secs_f64());
                let state = sink.state.lock().unwrap();
                assert_eq!(state.pending.len(), COUNT);
                disk = state.bytes;
                drop(state);
                drop(sink);
                std::fs::remove_dir_all(root).unwrap();
            }
            times.sort_by(f64::total_cmp);
            let seconds = times[1];
            println!(
                "ACTUAL_NATIVE_SOURCE_IO batch={batch} records={COUNT} median_ms={:.3} records_per_second={:.0} bytes={disk} wire_payload=512_entropy_bytes rounds=3",
                seconds * 1000.,
                COUNT as f64 / seconds
            );
        }
    }

    #[test]
    fn transport_groups_keep_identity_order_exact_receipts_and_wire_bounds() {
        let root = root();
        let sink = Sink::open(root.clone(), broker(), "test".into()).unwrap();
        for sequence in 1..=70 {
            capture(&sink, &frame(sequence, 2));
        }
        let batches = sink.batches().unwrap();
        assert_eq!(batches.len(), 1);
        assert_eq!(batches[0].len(), 64);
        let wire = batch_wire(&batches[0]).unwrap();
        assert!(wire.len() <= 512 * 1024);
        let receipt_values = batches[0]
            .iter()
            .map(|record| {
                rmpv::decode::read_value(&mut std::io::Cursor::new(receipt(record, true))).unwrap()
            })
            .collect::<Vec<_>>();
        let answer = super::super::codec::encode(&rmpv::Value::Array(vec![
            2.into(),
            rmpv::Value::Array(receipt_values.clone()),
        ]))
        .unwrap();
        assert!(accepts_batch(&batches[0], &answer).is_ok());
        let mut reversed = receipt_values;
        reversed.reverse();
        let wrong = super::super::codec::encode(&rmpv::Value::Array(vec![
            2.into(),
            rmpv::Value::Array(reversed),
        ]))
        .unwrap();
        assert!(accepts_batch(&batches[0], &wrong).is_err());
        assert_eq!(sink.state.lock().unwrap().pending.len(), 70);
        sink.confirm_batch(&batches[0]).unwrap();
        assert_eq!(sink.batches().unwrap()[0].len(), 6);
        drop(sink);
        let sink = Sink::open(root.clone(), broker(), "test".into()).unwrap();
        assert_eq!(sink.batches().unwrap()[0].len(), 6);
        drop(sink);
        std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn oversized_transport_group_splits_without_mixing_generations() {
        let root = root();
        let sink = Sink::open(root.clone(), broker(), "test".into()).unwrap();
        for sequence in 1..=20 {
            let mut value = frame(sequence, 2);
            value.payload = (0..40000).map(|_| rand::random::<u8>()).collect();
            capture(&sink, &value);
        }
        capture(&sink, &frame(1, 3));
        let groups = sink.batches().unwrap();
        assert!(groups[0].len() < 20);
        assert!(batch_wire(&groups[0]).unwrap().len() <= 512 * 1024);
        while sink.state.lock().unwrap().pending.len() > 1 {
            let batch = sink.batches().unwrap();
            assert!(batch[0].iter().all(|r| {
                super::super::codec::decode_compressed(&r.bytes)
                    .unwrap()
                    .as_array()
                    .unwrap()[3]
                    .as_str()
                    == Some(super::super::wire::guid([2; 16]).as_str())
            }));
            sink.confirm_batch(&batch[0]).unwrap();
        }
        assert_eq!(sink.batches().unwrap()[0].len(), 1);
        drop(sink);
        std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn highly_compressed_sources_split_at_aggregate_expansion_bound_without_retiring_originals() {
        let root=root(); let sink=Sink::open(root.clone(),broker(),"test".into()).unwrap();
        for sequence in 1..=64 { let mut value=frame(sequence,2);value.payload=vec![0;16*1024];capture(&sink,&value); }
        let mut seen=0;
        while seen<64 {
            let groups=sink.batches().unwrap();let batch=&groups[0];
            assert!(batch_wire(batch).unwrap().len()<=512*1024);
            let expanded:usize=batch.iter().map(|r|super::super::codec::expanded_length(&r.bytes).unwrap()).sum();
            assert!(expanded<=576*1024);assert!(batch.len()<64);
            assert_eq!(sink.state.lock().unwrap().pending.len(),64-seen);
            seen+=batch.len();sink.confirm_batch(batch).unwrap();
        }
        assert!(sink.heads().unwrap().is_empty());drop(sink);std::fs::remove_dir_all(root).unwrap();
    }

    fn grouped_capture(sink:&Sink,values:&[Frame])->Result<(),String>{
        let wires=values.iter().map(|value|{let subject=value.subject("test").unwrap();let k=key(value,&subject);(subject,value.encode().unwrap(),k)}).collect::<Vec<_>>();
        let refs=wires.iter().map(|(subject,wire,key)|(subject.as_str(),wire.as_slice(),None,*key)).collect::<Vec<_>>();
        sink.capture_batch(&refs,tracker())
    }
    fn all_pending(sink:&Sink)->Vec<Record>{let state=sink.state.lock().unwrap();state.pending.values().map(Sink::read).collect::<Result<_,_>>().unwrap()}
    #[test]
    fn finite_pending_saturation_refuses_atomically_then_drain_restores_capture_without_restart(){
        let root=root();let limits=Limits{bytes:1024*1024,pending:64,segment:4096};
        let sink=Sink::open_with_limits(root.clone(),broker(),"test".into(),limits).unwrap();
        let initial=(1..=64).map(|n|frame(n,2)).collect::<Vec<_>>();grouped_capture(&sink,&initial).unwrap();
        let before=sink.state.lock().unwrap().bytes;
        assert!(grouped_capture(&sink,&[frame(65,2)]).unwrap_err().contains("pending capacity"));
        {let state=sink.state.lock().unwrap();assert_eq!(state.pending.len(),64);assert_eq!(state.bytes,before);assert!(!state.failed);assert!(!state.flushing);}
        let first=all_pending(&sink);sink.confirm_batch(&first[..32]).unwrap();
        grouped_capture(&sink,&(65..=96).map(|n|frame(n,2)).collect::<Vec<_>>()).unwrap();
        drop(sink);let sink=Sink::open_with_limits(root.clone(),broker(),"test".into(),limits).unwrap();
        let records=all_pending(&sink);assert_eq!(records.len(),64);
        let sequences=records.iter().map(|record|super::super::codec::decode_compressed(&record.bytes).unwrap().as_array().unwrap()[5].as_i64().unwrap()).collect::<Vec<_>>();
        assert_eq!(sequences,(33..=96).collect::<Vec<_>>());
        drop(sink);std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn finite_disk_saturation_keeps_originals_and_capture_resumes_after_proven_segment_reclaim(){
        let root=root();let limits=Limits{bytes:4096,pending:128,segment:1024};
        let sink=Sink::open_with_limits(root.clone(),broker(),"test".into(),limits).unwrap();let mut next=1;
        loop{let mut value=frame(next,2);value.payload=(0..256).map(|_|rand::random::<u8>()).collect();
            if grouped_capture(&sink,&[value]).is_err(){break;}next+=1;assert!(next<128);}
        assert!(next>3);let originals=all_pending(&sink);assert_eq!(originals.len(),(next-1)as usize);
        {let state=sink.state.lock().unwrap();assert!(!state.failed);assert!(!state.flushing);assert!(state.bytes<=limits.bytes);}
        sink.confirm_batch(&originals).unwrap();assert!(all_pending(&sink).is_empty());
        grouped_capture(&sink,&[frame(next,2)]).unwrap();drop(sink);
        let sink=Sink::open_with_limits(root.clone(),broker(),"test".into(),limits).unwrap();assert_eq!(all_pending(&sink).len(),1);
        drop(sink);std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn sustained_eight_thousand_sources_compact_only_proven_segments_and_keep_pinned_oldest_on_restart(){
        let root=root();let limits=Limits{bytes:16*1024*1024,pending:2048,segment:64*1024};
        let sink=Sink::open_with_limits(root.clone(),broker(),"test".into(),limits).unwrap();
        let original=frame(1,2);grouped_capture(&sink,std::slice::from_ref(&original)).unwrap();
        for cycle in 0..8 {
            for chunk in (0..1024).collect::<Vec<_>>().chunks(64){let values=chunk.iter().map(|index|{let mut value=frame(2+cycle*1024+i64::from(*index),2);value.payload=(0..512).map(|_|rand::random::<u8>()).collect();value}).collect::<Vec<_>>();grouped_capture(&sink,&values).unwrap();}
            let records=all_pending(&sink);assert_eq!(records.len(),1025);
            for chunk in records[1..].chunks(64){sink.confirm_batch(chunk).unwrap();}
            let state=sink.state.lock().unwrap();assert_eq!(state.pending.len(),1);assert!(state.segments.len()<=2);assert!(state.bytes<3*limits.segment);
        }
        assert!(std::fs::metadata(root.join("confirmed.bin")).unwrap().len()<32*1024);
        drop(sink);let sink=Sink::open_with_limits(root.clone(),broker(),"test".into(),limits).unwrap();
        let records=all_pending(&sink);assert_eq!(records.len(),1);assert_eq!(records[0].bytes,original.encode().unwrap());
        sink.confirm_batch(&records).unwrap();drop(sink);let sink=Sink::open_with_limits(root.clone(),broker(),"test".into(),limits).unwrap();assert!(all_pending(&sink).is_empty());
        drop(sink);std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn receipt_checksum_and_uncertain_actual_write_never_prune_pending_sources(){
        let root=root();let sink=Sink::open(root.clone(),broker(),"test".into()).unwrap();capture(&sink,&frame(1,2));
        let path=sink.state.lock().unwrap().path.clone();sink.state.lock().unwrap().file=File::open(path).unwrap();
        assert!(grouped_capture(&sink,&[frame(2,2)]).is_err());assert!(sink.state.lock().unwrap().failed);assert_eq!(sink.state.lock().unwrap().pending.len(),1);
        drop(sink);let sink=Sink::open(root.clone(),broker(),"test".into()).unwrap();capture(&sink,&frame(3,2));let records=all_pending(&sink);assert_eq!(records.len(),2);sink.confirm_batch(&records).unwrap();drop(sink);
        let path=root.join("confirmed.bin");let mut bytes=std::fs::read(&path).unwrap();assert!(!bytes.is_empty());bytes[8]^=1;std::fs::write(path,bytes).unwrap();
        assert!(Sink::open(root.clone(),broker(),"test".into()).is_err());std::fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn source_manifest_fixed_prefixes_are_immutable_while_capture_continues_without_claiming_mapper_checkpoint(){
        let root=root();let sink=Sink::open(root.clone(),broker(),"test".into()).unwrap();capture(&sink,&frame(1,2));
        let manifest:serde_json::Value=serde_json::from_str(&sink.prefix_manifest(777).unwrap()).unwrap();assert_eq!(manifest["NextOrdinal"],2);assert_eq!(manifest["PendingCount"],1);assert_eq!(manifest["MapperCheckpoint"],false);assert_eq!(manifest["QueuedHeads"][0]["Sequence"],1);
        let snapshots=manifest["Files"].as_array().unwrap().iter().map(|entry|{let path=root.join(entry["RelativePath"].as_str().unwrap());let length=entry["Length"].as_u64().unwrap()as usize;let bytes=std::fs::read(&path).unwrap();(path,bytes[..length].to_vec())}).collect::<Vec<_>>();
        capture(&sink,&frame(2,2));for(path,prefix)in snapshots{assert_eq!(std::fs::read(path).unwrap()[..prefix.len()],prefix);}
        assert_eq!(all_pending(&sink).len(),2);drop(sink);std::fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn exact_disk_bound_reserves_cold_open_headroom_and_repeated_restarts_never_evict_pending(){
        let root=root();let value=frame(1,2);let subject=value.subject("test").unwrap();let wire=value.encode().unwrap();
        let size=Record{ordinal:1,key:key(&value,&subject),subject,origin:None,bytes:wire.clone()}.encode().unwrap().len()as u64+36;
        let limits=Limits{bytes:8+size+8,pending:64,segment:1024};
        {let sink=Sink::open_with_limits(root.clone(),broker(),"test".into(),limits).unwrap();grouped_capture(&sink,std::slice::from_ref(&value)).unwrap();assert!(grouped_capture(&sink,&[frame(2,2)]).is_err());}
        for _ in 0..4{let sink=Sink::open_with_limits(root.clone(),broker(),"test".into(),limits).unwrap();assert_eq!(all_pending(&sink)[0].bytes,wire);assert!(sink.state.lock().unwrap().bytes<=limits.bytes);}
        {let sink=Sink::open_with_limits(root.clone(),broker(),"test".into(),limits).unwrap();let originals=all_pending(&sink);sink.confirm_batch(&originals).unwrap();grouped_capture(&sink,&[frame(2,2)]).unwrap();}
        std::fs::remove_dir_all(root).unwrap();
    }

}
