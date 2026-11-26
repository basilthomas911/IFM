//! Event-driven native futures-to-NATS delivery beneath a dataset task owner.
//! Provider output, event construction and broker writes never enter the FFI.
//! Broker retry uses one incident deadline; source reset receives that same
//! deadline and never waits for a managed receiver or database acknowledgement.

use super::{
    codec,
    current::Current,
    futures::Futures,
    publisher::{BrokerSettings, PreparedFrame, Publisher},
    wire::Frame,
};
use crate::{
    dataset::{
        live::{LiveData, Output},
        transport::Lane,
    },
    runtime::{Deadline, owner::Child},
};
use std::{
    sync::{
        Arc, Mutex,
        atomic::{AtomicBool, Ordering},
    },
    time::Duration,
};
use tokio::{sync::watch, task::JoinHandle};
use tokio_util::sync::CancellationToken;

/// Native publication availability, distinct from provider source readiness.
#[derive(Clone, Debug, Default)]
pub struct Status {
    /// Broker transport is connected, independently of a first market frame.
    pub connected: bool,
    /// A current native frame has completed a broker protocol flush.
    pub running: bool,
    /// One original setup/recovery deadline is active.
    pub recovering: bool,
    /// Original deadline expired; query remains available while NATS is down.
    pub terminal: bool,
    /// Earliest original setup/recovery deadline, cleared only on full readiness.
    pub deadline: Option<Deadline>,
    /// First publication failure in the current episode, without credentials.
    pub root_error: Option<String>,
    /// Latest sanitized failure detail.
    pub error: Option<String>,
}
/// Complete imported broker/envelope identity. Contains no managed callbacks.
#[derive(Clone)]
pub struct Configuration {
    /// Shared validated broker settings, including explicit DNS/TLS policy.
    pub broker: BrokerSettings,
    /// Existing deployment subject scope.
    pub scope: String,
    /// Exact logical dataset role.
    pub lane: Lane,
    /// API ValueDate as CLR day number.
    pub value_date: i32,
    /// Logical owner Guid bytes in CLR order.
    pub worker: [u8; 16],
    /// Logical generation Guid bytes in CLR order.
    pub generation: [u8; 16],
    /// Positive desired-subscription revision.
    pub revision: i64,
    /// Explicit API-session quiet/closed eligibility. Broker protocol readiness
    /// may suffice without inventing a fresh trade or market-price observation.
    pub control_only: bool,
    /// Bounded queued/in-flight frames.
    pub queue: usize,
    /// Bounded queued/in-flight encoded bytes.
    pub bytes: usize,
}
/// Owned native publication task and detached latest-value query storage.
pub type PublicationDiagnostics = Arc<Mutex<Option<super::publisher::Port>>>;

/// Owned native publication task and detached latest-value query storage.
pub struct Delivery {
    checkpoint:Option<crate::checkpoint::Registration>,
    diagnostics: PublicationDiagnostics,
    stop: CancellationToken,
    task: Option<JoinHandle<()>>,
    status: watch::Receiver<Status>,
    current: Arc<Current>,
    retirement: Arc<Mutex<Option<Deadline>>>,
    source_ready: Arc<AtomicBool>,
    source_changed: Arc<tokio::sync::Notify>,
}
struct FrozenOutput {
    key: (u128, u64),
    frames: Vec<PreparedFrame>,
    offset: usize,
    retained: Vec<(String, Frame)>,
}
struct DurableEncoder {
    events: Futures,
    config: Configuration,
    sequence: i64,
    port: super::publisher::Port,
    current: Arc<Current>,
    submitted: u64,
    first_current: Option<(u128, u64)>,
    completed: std::collections::BTreeMap<u128, u64>,
    pending: std::collections::VecDeque<FrozenOutput>,
    pending_keys: std::collections::BTreeSet<(u128, u64)>,
    required: crate::dataset::required_capture::RequiredCaptureIntent,
    required_last: Option<(u128, u64)>,
    mapping_failed: bool,
}
pub(crate) struct ImportedFutures{
    pub(crate) current:Arc<Current>,
    _data:Arc<LiveData>,
    _encoder:Arc<Mutex<DurableEncoder>>,
    _registration:crate::checkpoint::Registration,
}
impl ImportedFutures{
    pub(crate) fn retire(&self){
        if let Ok(encoder)=self._encoder.lock(){let _=self._data.cancel_lane(encoder.config.lane);}
    }
}
pub(crate) fn import_read_only(data:Arc<LiveData>,events:Futures,config:Configuration,metadata:rmpv::Value)->Result<ImportedFutures,String>{
    if !crate::checkpoint::replay_import_only(){return Err("Original futures readonly binding requires explicit replay-import-only mode".into());}
    let raw=data.raw_capture_root().ok_or("Original futures readonly binding requires an isolated raw owner root")?;
    crate::checkpoint::validate_restore_path(&config.scope,raw)?;
    let required=crate::dataset::required_capture::RequiredCaptureIntent::open(raw,&format!("{}-{}",config.scope,config.lane as u8))?;
    let current=Arc::new(Current::new(128,16*1024*1024).map_err(str::to_owned)?);
    let encoder=Arc::new(Mutex::new(DurableEncoder{events,sequence:0,port:super::publisher::Port::imported_read_only(&config.scope),current:current.clone(),submitted:0,first_current:None,completed:Default::default(),pending:Default::default(),pending_keys:Default::default(),required,required_last:None,mapping_failed:false,config:config.clone()}));
    let registration=crate::checkpoint::register(&config.scope,&format!("futures-{}",config.lane as u8),Arc::new(FuturesCheckpointOwner{data:Arc::downgrade(&data),encoder:Arc::downgrade(&encoder),lane:config.lane,metadata}))?;
    Ok(ImportedFutures{current,_data:data,_encoder:encoder,_registration:registration})
}
struct FuturesCheckpointOwner{data:std::sync::Weak<LiveData>,encoder:std::sync::Weak<Mutex<DurableEncoder>>,lane:Lane,metadata:rmpv::Value}
pub(crate) fn validate_checkpoint_image(value:&rmpv::Value)->Result<(),String>{
    use crate::checkpoint as c;let owner=c::fields(value,4)?;if c::unsigned(&owner[0])?!=1{return Err("Unknown futures checkpoint owner image".into());}
    let data=c::fields(&owner[1],14)?;let book=crate::dataset::book::Book::<crate::pricing::Values>::restore_checkpoint(&data[10],CancellationToken::new())?;
    if c::unsigned(&data[0])?!=1||book.generation()!=c::read_wide(&data[6])?{return Err("Original checkpoint source/book generation differs".into());}
    let mapper=c::fields(&owner[2],11)?;let config=c::fields(&mapper[1],10)?;
    if c::unsigned(&mapper[0])?!=1||c::lane(&config[1])?!=c::lane(&data[4])?||c::number(&config[2])?!=c::number(&data[1])?||c::number(&mapper[3])?<0{return Err("Original checkpoint mapper/source identity differs".into());}
    let events=Futures::restore_checkpoint(&mapper[2])?;
    let recipe=crate::dataset::futures_recipe_checkpoint::decode(&owner[3],c::lane(&config[1])?)?;
    if recipe.date!=c::date(&data[1])?{return Err("Original futures catalog date differs from book".into());}
    let book_fields=c::fields(&data[10],7)?;let rows=book_fields[5].as_array().ok_or("Original checkpoint book registrations absent")?;
    let bindings=events.capture_identity();let bindings=bindings.as_array().ok_or("Original mapper registrations absent")?;
    if rows.len()!=bindings.len(){return Err("Original source and mapper registration counts differ".into());}
    if recipe.registrations.len()!=bindings.len()||recipe.registrations.iter().any(|registration|!bindings.iter().any(|binding|binding.as_array().is_some_and(|binding|binding[0]==rmpv::Value::from(registration.instrument)&&binding[1]==rmpv::Value::from(registration.publisher)&&binding[2]==rmpv::Value::from(registration.contract.clone())&&binding[3]==rmpv::Value::from(registration.source.clone())))){return Err("Original futures recipe and mapper registrations differ".into());}
    for row in rows{let row=c::fields(row,11)?;if !bindings.iter().any(|binding|binding.as_array().is_some_and(|binding|binding[0]==row[0]&&binding[2]==row[1]&&binding[4]==row[2])){return Err("Original source and mapper registrations differ".into());}}
    let current=Current::new(128,16*1024*1024).map_err(str::to_owned)?;current.import_checkpoint(&mapper[10])?;
    let required=c::fields(&mapper[8],3)?;if c::unsigned(&required[0])?!=1||required[1].is_nil()&&!required[2].is_nil(){return Err("Original required intent phase invalid".into());}
    for index in [1,2]{if !required[index].is_nil()&&!matches!(&required[index],rmpv::Value::Binary(bytes) if !bytes.is_empty()&&bytes.len()<64*1024*1024){return Err("Original required intent exceeds bound".into());}}
    let pending=mapper[7].as_array().filter(|values|values.len()<=16385).ok_or("Original pending source count exceeds bound")?;
    for value in pending{let p=c::fields(value,5)?;let offset=usize::try_from(c::unsigned(&p[2])?).map_err(|_|"Original frame offset overflow")?;let frames=p[3].as_array().filter(|values|values.len()<=16385&&offset<=values.len()).ok_or("Original pending frame offset invalid")?;for(index,value)in frames.iter().enumerate(){let frame=PreparedFrame::restore_intent(value)?;if index>=offset&&frame.bytes.is_empty(){return Err("Original unpublished frame bytes absent".into());}}for row in p[4].as_array().ok_or("Original retained frames absent")?{let p=c::fields(row,2)?;Frame::restore_checkpoint(&p[1])?;}}
    Ok(())
}
impl crate::checkpoint::CheckpointOwner for FuturesCheckpointOwner{
    fn open_original_source(&self)->Result<Arc<super::source_outbox::Sink>,String>{let encoder=self.encoder.upgrade().ok_or("Original checkpoint mapper owner ended")?;let encoder=encoder.lock().map_err(|_|"Checkpoint mapper owner interrupted")?;super::source_outbox::Sink::open(super::source_outbox::location(&encoder.config.scope)?,encoder.config.broker.clone(),encoder.config.scope.clone())}
    fn prepare(&self)->Result<(),String>{
        let data=self.data.upgrade().ok_or("Original checkpoint source owner ended")?;let encoder=self.encoder.upgrade().ok_or("Original checkpoint mapper owner ended")?;
        data.checkpoint_prepare_lane(self.lane,|outputs|encoder.lock().map_err(|_|"Checkpoint mapper owner interrupted")?.capture(outputs))
    }
    fn checkpoint(&self)->Result<rmpv::Value,String>{
        let data=self.data.upgrade().ok_or("Original checkpoint source owner ended")?;let encoder=self.encoder.upgrade().ok_or("Original checkpoint mapper owner ended")?;
        if self.metadata.is_nil(){return Err("Original resolved futures factory catalog was not captured".into());}
        Ok(rmpv::Value::Array(vec![1.into(),data.checkpoint_lane(self.lane)?,encoder.lock().map_err(|_|"Checkpoint mapper owner interrupted")?.checkpoint()?,self.metadata.clone()]))
    }
    fn restore(&self,value:&rmpv::Value)->Result<(),String>{
        let f=crate::checkpoint::fields(value,4)?;if crate::checkpoint::unsigned(&f[0])?!=1||f[3]!=self.metadata{return Err("Unknown or changed original futures checkpoint catalog".into());}
        let data=self.data.upgrade().ok_or("Original checkpoint source owner ended")?;let encoder=self.encoder.upgrade().ok_or("Original checkpoint mapper owner ended")?;
        let original=crate::checkpoint::fields(&f[1],14)?;
        if !original[13].is_nil(){
            let raw=data.raw_capture_root().ok_or("Original raw checkpoint requires its isolated copied owner root")?;
            crate::checkpoint::validate_raw_restore_path(raw,&original[13])?;
            let recipe=crate::dataset::futures_recipe_checkpoint::decode(&f[3],self.lane)?;
            let verified=crate::dataset::raw_capture::RawCapture::restore_checkpoint(raw,&original[13],self.lane,recipe.date,recipe.definition,&recipe.registrations)?;
            drop(verified);
        }
        data.import_checkpoint_lane(self.lane,&f[1])?;encoder.lock().map_err(|_|"Checkpoint mapper owner interrupted")?.import_checkpoint(&f[2])
    }
}
impl DurableEncoder {
    fn checkpoint_config(&self)->rmpv::Value{
        use rmpv::Value as V;V::Array(vec![self.config.scope.clone().into(),(self.config.lane as u8).into(),self.config.value_date.into(),V::Binary(self.config.worker.to_vec()),V::Binary(self.config.generation.to_vec()),self.config.revision.into(),self.config.control_only.into(),(self.config.queue as u64).into(),(self.config.bytes as u64).into(),V::Binary(crate::checkpoint::logical_namespace(&self.config.scope,self.config.broker.source_namespace_digest()).to_vec())])
    }
    fn checkpoint(&self)->Result<rmpv::Value,String>{
        use rmpv::Value as V;use crate::checkpoint::wide;
        if self.mapping_failed{return Err("Failed original mapper cannot export a healthy checkpoint".into());}
        let pair=|value:Option<(u128,u64)>|value.map_or(V::Nil,|(physical,serial)|V::Array(vec![wide(physical),serial.into()]));
        Ok(V::Array(vec![1.into(),self.checkpoint_config(),self.events.checkpoint(),self.sequence.into(),self.submitted.into(),pair(self.first_current),V::Array(self.completed.iter().map(|(physical,serial)|V::Array(vec![wide(*physical),(*serial).into()])).collect()),V::Array(self.pending.iter().map(|pending|V::Array(vec![wide(pending.key.0),pending.key.1.into(),(pending.offset as u64).into(),V::Array(pending.frames.iter().map(PreparedFrame::intent_value).collect()),V::Array(pending.retained.iter().map(|(contract,frame)|V::Array(vec![contract.clone().into(),frame.checkpoint()])).collect())])).collect()),self.required.checkpoint()?,pair(self.required_last),self.current.checkpoint()?]))
    }
    fn import_checkpoint(&mut self,value:&rmpv::Value)->Result<(),String>{
        use crate::checkpoint as c;let f=c::fields(value,11)?;
        if c::unsigned(&f[0])?!=1||f[1]!=self.checkpoint_config(){return Err("Native mapper checkpoint owner/configuration differs".into());}
        if self.sequence!=0||self.submitted!=0||!self.pending.is_empty()||!self.completed.is_empty(){return Err("Native checkpoint cannot overwrite an active mapper".into());}
        let events=Futures::restore_checkpoint(&f[2])?;
        if events.capture_identity()!=self.events.capture_identity(){return Err("Native checkpoint original registrations differ".into());}
        let sequence=c::number(&f[3])?;if sequence<0{return Err("Native checkpoint publication sequence invalid".into());}
        let _original_submitted=c::unsigned(&f[4])?;
        let pair=|value:&rmpv::Value|->Result<Option<(u128,u64)>,String>{if value.is_nil(){return Ok(None);}let p=c::fields(value,2)?;Ok(Some((c::read_wide(&p[0])?,c::unsigned(&p[1])?)))};
        let _original_first=pair(&f[5])?;
        let mut completed=std::collections::BTreeMap::new();
        for row in f[6].as_array().filter(|rows|rows.len()<=16385).ok_or("Checkpoint completed generations exceed bound")?{let p=c::fields(row,2)?;if completed.insert(c::read_wide(&p[0])?,c::unsigned(&p[1])?).is_some(){return Err("Duplicate completed checkpoint generation".into());}}
        let mut pending=std::collections::VecDeque::new();let mut keys=std::collections::BTreeSet::new();
        for row in f[7].as_array().filter(|rows|rows.len()<=16385).ok_or("Checkpoint pending outputs exceed bound")?{
            let p=c::fields(row,5)?;let key=(c::read_wide(&p[0])?,c::unsigned(&p[1])?);let offset=usize::try_from(c::unsigned(&p[2])?).map_err(|_|"Checkpoint frame offset overflow")?;
            let frames=p[3].as_array().filter(|frames|frames.len()<=16385).ok_or("Checkpoint pending frames exceed bound")?.iter().map(PreparedFrame::restore_intent).collect::<Result<Vec<_>,_>>()?;
            if offset>frames.len()||frames[offset..].iter().any(|frame|frame.bytes.is_empty())||!keys.insert(key)||completed.get(&key.0).is_some_and(|serial|*serial>=key.1){return Err("Checkpoint pending original order/offset differs".into());}
            let retained=p[4].as_array().filter(|frames|frames.len()<=16385).ok_or("Checkpoint retained frames exceed bound")?.iter().map(|row|{let r=c::fields(row,2)?;Ok((c::string(&r[0],1024)?,Frame::restore_checkpoint(&r[1])?))}).collect::<Result<Vec<_>,String>>()?;
            pending.push_back(FrozenOutput{key,frames,offset,retained});
        }
        let required_last=pair(&f[9])?;
        self.current.import_checkpoint(&f[10])?;
        self.required.import_checkpoint(&f[8])?;
        self.events=events;self.sequence=sequence;self.completed=completed;self.pending=pending;self.pending_keys=keys;self.required_last=required_last;
        // A new physical transport must establish its own ACK progress. Old
        // submitted/flushed counters are evidence, never a readiness substitute.
        self.submitted=0;self.first_current=None;self.mapping_failed=false;Ok(())
    }
    fn prepare(&mut self, outputs: &[Output]) -> Result<(), String> {
        if self.mapping_failed {
            return Err("Original source mapper failed before frozen publication; authentic intent reconstruction is required".into());
        }
        for output in outputs {
            if output.capture_serial == 0 {
                return Err("Original output capture serial is absent".into());
            }
            let key = (output.generation, output.capture_serial);
            if self
                .completed
                .get(&key.0)
                .is_some_and(|serial| *serial >= key.1)
                || self.pending_keys.contains(&key)
            {
                continue;
            }
            let prepared = (|| -> Result<FrozenOutput, String> {
                let events = self.events.process(output).map_err(str::to_owned)?;
                let mut frames = Vec::with_capacity(events.len());
                let mut retained = Vec::new();
                for event in events {
                    self.sequence = self
                        .sequence
                        .checked_add(1)
                        .ok_or("Native publication sequence exhausted")?;
                    let contract = if matches!(event.kind, 3 | 4) {
                        event
                            .value
                            .as_array()
                            .and_then(|values| values.get(2))
                            .and_then(ValueContract::contract)
                            .map(str::to_owned)
                    } else {
                        None
                    };
                    let frame = Frame {
                        lane: self.config.lane,
                        value_date: self.config.value_date,
                        worker: self.config.worker,
                        generation: self.config.generation,
                        manifest_revision: self.config.revision,
                        sequence: self.sequence,
                        kind: event.kind,
                        payload: codec::encode(&event.value).map_err(str::to_owned)?,
                        origin_ticks: Some(output.observed),
                    };
                    frames.push(self.port.prepare_frame(&frame).map_err(str::to_owned)?);
                    if let Some(contract) = contract {
                        retained.push((contract, frame));
                    }
                }
                Ok(FrozenOutput {
                    key,
                    frames,
                    offset: 0,
                    retained,
                })
            })();
            let prepared = match prepared {
                Ok(value) => value,
                Err(error) => {
                    self.mapping_failed = true;
                    return Err(error);
                }
            };
            self.pending.push_back(prepared);
            self.pending_keys.insert(key);
        }
        Ok(())
    }
    fn capture_required(&mut self, outputs: &[Output]) -> Result<(), String> {
        let last = outputs.last().ok_or("Required source prefix is empty")?;
        let key = (last.generation, last.capture_serial);
        if self.required.original().is_none() {
            let original=codec::encode(&rmpv::Value::Array(vec![1.into(),self.config.scope.clone().into(),(self.config.lane as u8).into(),self.config.value_date.into(),super::wire::guid(self.config.worker).into(),super::wire::guid(self.config.generation).into(),self.config.revision.into(),self.sequence.into(),rmpv::Value::Binary(self.config.broker.source_namespace_digest().to_vec()),self.events.capture_identity(),rmpv::Value::Binary(crate::dataset::required_capture::encode_original(outputs)?)] )).map_err(str::to_owned)?;
            self.required.begin(&original)?;
            self.required_last = Some(key);
        } else if self.required_last != Some(key) {
            return Err("An earlier required source intent remains unresolved".into());
        }
        self.prepare(outputs)?;
        if self.required.frozen().is_none() {
            let frames = self
                .pending
                .iter()
                .flat_map(|pending| {
                    pending.frames[pending.offset..]
                        .iter()
                        .map(PreparedFrame::intent_value)
                })
                .collect();
            let wire = codec::encode(&rmpv::Value::Array(vec![
                1.into(),
                rmpv::Value::Array(frames),
            ]))
            .map_err(str::to_owned)?;
            self.required.freeze(&wire)?;
        }
        self.capture(outputs)
    }
    fn capture(&mut self, outputs: &[Output]) -> Result<(), String> {
        if self.required.original().is_some() && self.required.frozen().is_none() {
            return Err("Required source mapping has no frozen wire; authentic intent reconciliation is required".into());
        }
        self.prepare(outputs)?;
        for output in outputs {
            let key = (output.generation, output.capture_serial);
            if self
                .completed
                .get(&key.0)
                .is_some_and(|serial| *serial >= key.1)
            {
                continue;
            }
            let pending = self
                .pending
                .front_mut()
                .ok_or("Original source preparation is absent")?;
            if pending.key != key {
                return Err("Required original output was overtaken before durable capture".into());
            }
            while pending.offset < pending.frames.len() {
                let mut count = 0;
                let mut bytes = 0;
                for frame in &pending.frames[pending.offset..] {
                    if count == 64 || count > 0 && bytes + frame.bytes.len() > self.config.bytes {
                        break;
                    }
                    if frame.bytes.len() > self.config.bytes {
                        return Err(
                            "Original source frame exceeds native publication byte capacity".into(),
                        );
                    }
                    count += 1;
                    bytes += frame.bytes.len();
                }
                let mut group = pending.frames[pending.offset..pending.offset + count]
                    .iter_mut()
                    .collect::<Vec<_>>();
                self.port
                    .try_prepared_batch(&mut group)
                    .map_err(str::to_owned)?;
                pending.offset += count;
                self.submitted = self.submitted.saturating_add(count as u64);
                if self
                    .first_current
                    .is_none_or(|(physical, _)| physical != key.0)
                {
                    self.first_current = Some((key.0, self.submitted));
                }
            }
            for (contract, frame) in &pending.retained {
                self.current
                    .retain(contract, frame)
                    .map_err(str::to_owned)?;
            }
            self.completed.insert(key.0, key.1);
            self.pending_keys.remove(&key);
            self.pending.pop_front();
        }
        if self.required_last.is_some_and(|(physical, serial)| {
            self.completed
                .get(&physical)
                .is_some_and(|done| *done >= serial)
        }) {
            self.required.complete()?;
            self.required_last = None;
        }
        Ok(())
    }
    fn rebind(&mut self, port: super::publisher::Port) {
        self.port = port;
        self.submitted = 0;
        self.first_current = None;
    }
}

struct Exit {
    status: watch::Sender<Status>,
    clean: bool,
    data: Arc<LiveData>,
    lane: Lane,
}
impl Drop for Exit {
    fn drop(&mut self) {
        if !self.clean {
            let _ = self.data.cancel_lane(self.lane);
        }
        self.status.send_modify(|status| {
            status.connected = false;
            status.running = false;
            if !self.clean {
                status.terminal = true;
                status.recovering = false;
                status.error = Some("Native publication owner exited before joined cleanup".into());
            }
        });
    }
}
impl Delivery {
    /// Starts beneath the same native dataset parent as the provider resources.
    /// There is exactly one exclusive output consumer for this lane.
    ///
    /// # Errors
    /// Rejects invalid identity/capacity or stopped/exhausted native admission.
    pub fn start(
        data: Arc<LiveData>,
        events: Futures,
        config: Configuration,
        parent: Arc<Child>,
        deadline: Deadline,
    ) -> Result<Self, &'static str> {
        Self::start_with_metadata(data,events,config,parent,deadline,rmpv::Value::Nil)
    }
    pub(crate) fn start_with_metadata(
        data:Arc<LiveData>,events:Futures,config:Configuration,parent:Arc<Child>,deadline:Deadline,metadata:rmpv::Value
    )->Result<Self,&'static str>{
        if config.lane == Lane::OptionsGlbx
            || !data.belongs_to(&parent)
            || !(1..=4096).contains(&config.queue)
            || !(1..=64 * 1024 * 1024).contains(&config.bytes)
        {
            return Err("Futures delivery requires a futures lane");
        }
        Frame {
            lane: config.lane,
            value_date: config.value_date,
            worker: config.worker,
            generation: config.generation,
            manifest_revision: config.revision,
            sequence: 1,
            kind: 3,
            payload: vec![0xc0],
            origin_ticks: None,
        }
        .subject(&config.scope)?;
        let current = Arc::new(Current::new(128, 16 * 1024 * 1024)?);
        let diagnostics = Arc::new(std::sync::Mutex::new(None));
        let writer_diagnostics = diagnostics.clone();
        let retained = current.clone();
        let stop = parent.cancellation().child_token();
        let stopping = stop.clone();
        let retirement = Arc::new(Mutex::new(None));
        let retiring = retirement.clone();
        let (status, observed) = watch::channel(Status {
            recovering: true,
            deadline: Some(deadline),
            ..Status::default()
        });
        let owned = parent.clone();
        let exit = Exit {
            status: status.clone(),
            clean: false,
            data: data.clone(),
            lane: config.lane,
        };
        let source_ready = Arc::new(AtomicBool::new(
            data.lifecycle(config.lane).ok().flatten().is_some(),
        ));
        let source_changed = Arc::new(tokio::sync::Notify::new());
        let ready = source_ready.clone();
        let changed = source_changed.clone();
        let capture_mode = super::source_outbox::enabled();
        let mut initial_publisher = if capture_mode {
            Some(Publisher::start_owned(
                config.broker.clone(),
                config.scope.clone(),
                config.queue,
                config.bytes,
                deadline,
                &parent,
            )?)
        } else {
            None
        };
        let (shared, mut events) = if let Some(publisher) = initial_publisher.as_ref() {
            let raw = data
                .raw_capture_root()
                .ok_or("Durable futures source capture requires raw trade/quote storage")?;
            let required = crate::dataset::required_capture::RequiredCaptureIntent::open(
                raw,
                &format!("{}-{}", config.scope, config.lane as u8),
            )
            .map_err(|_| "Required source intent could not open")?;
            if required.original().is_some() {
                return Err("Required source intent must be reconciled before provider startup");
            }
            let encoder = Arc::new(Mutex::new(DurableEncoder {
                events,
                config: config.clone(),
                sequence: 0,
                port: publisher.port(),
                current: current.clone(),
                submitted: 0,
                first_current: None,
                completed: Default::default(),
                pending: Default::default(),
                pending_keys: Default::default(),
                required,
                required_last: None,
                mapping_failed: false,
            }));
            let capture = encoder.clone();
            data.bind_required_output_capture(
                config.lane,
                Arc::new(move |outputs| {
                    capture
                        .lock()
                        .map_err(|_| "Native source encoder interrupted")?
                        .capture_required(outputs)
                }),
            )
            .map_err(|_| "Required native source hook binding failed")?;
            (Some(encoder), None)
        } else {
            (None, Some(events))
        };
        let checkpoint=if crate::checkpoint::enabled(){
            let encoder=shared.as_ref().ok_or("Native checkpoints require durable original source capture")?;
            Some(crate::checkpoint::register(&config.scope,&format!("futures-{}",config.lane as u8),Arc::new(FuturesCheckpointOwner{data:Arc::downgrade(&data),encoder:Arc::downgrade(encoder),lane:config.lane,metadata})).map_err(|_|"Original native checkpoint owner registration failed")?)
        }else{None};
        let task = parent
            .spawn_joined(move |_| async move {
                let mut exit = exit;
                let mut episode = Some(deadline);
                let mut fault_reported = false;
                let mut sequence = 0i64;
                while !ready.load(Ordering::Acquire){tokio::select!{biased;()=stopping.cancelled()=>{exit.clean=true;return;},()=changed.notified()=>{}}}
                loop {
                    if stopping.is_cancelled() {
                        break;
                    }
                    let budget = episode.unwrap_or_else(Deadline::recovery);
                    let first=initial_publisher.is_some();
                    let publisher=initial_publisher.take().map_or_else(||Publisher::start_owned(config.broker.clone(),config.scope.clone(),config.queue,config.bytes,budget,&owned),Ok);
                    let mut publisher = match publisher {
                        Ok(p) => p,
                        Err(error) => {
                            let _ = data.cancel_lane(config.lane);
                            status.send_modify(|s| {
                                s.error = Some(error.into());
                                s.terminal = true;
                                s.running = false;
                                s.recovering = false;
                            });
                            break;
                        }
                    };
                    if let Ok(mut diagnostics) = writer_diagnostics.lock() {
                        *diagnostics = Some(publisher.port());
                    }
                    if !first&&let Some(encoder)=&shared{encoder.lock().map_err(|_|"Native encoder interrupted").map(|mut encoder|encoder.rebind(publisher.port())).ok();}
                    let failure=if let Some(encoder)=&shared{Self::pump_durable(&data,&config,encoder,&mut publisher,&stopping,&mut episode,&status).await}else{Self::pump(
                        &data,
                        events.as_mut().expect("legacy encoder"),
                        &config,
                        &mut sequence,
                        &retained,
                        &mut publisher,
                        &stopping,
                        &mut episode,
                        &status,
                    ).await};
                    if stopping.is_cancelled() {
                        let cleanup = retiring
                            .lock()
                            .ok()
                            .and_then(|d| *d)
                            .unwrap_or_else(Deadline::recovery);
                        let _ = publisher.shutdown(cleanup).await;
                        break;
                    }
                    let error =
                        failure.unwrap_or_else(|| "Native futures publication stopped".into());
                    if episode.is_none() {
                        fault_reported = false;
                    }
                    let budget = *episode.get_or_insert_with(Deadline::recovery);
                    status.send_modify(|s| {
                        s.connected = false;
                        s.running = false;
                        s.recovering = true;
                        s.deadline = Some(budget);
                        s.root_error.get_or_insert(error.clone());
                        s.error = Some(error.clone());
                    });
                    // Join the old broker workers independently of any server ACK.
                    // No new publisher starts while old transport ownership remains.
                    let joined = publisher.shutdown(budget).await;
                    if !fault_reported && !budget.remaining().is_zero() {
                        fault_reported = true;
                        if let Err(fault) = data
                            .report_fault(config.lane, &error, &stopping, budget)
                            .await
                        {
                            status.send_modify(|s| s.error = Some(fault));
                        }
                    }
                    if let Some(events)=events.as_mut(){events.retire();}
                    if joined.is_err() || budget.remaining().is_zero() {
                        let _ = data.cancel_lane(config.lane);
                        status.send_modify(|s| {
                            s.terminal = true;
                            s.recovering = false;
                            s.error = Some(
                                "Native publication original two-minute deadline expired".into(),
                            );
                        });
                        break;
                    }
                    if budget
                        .run(&stopping, tokio::time::sleep(Duration::from_millis(200)))
                        .await
                        .is_err()
                    {
                        if !stopping.is_cancelled() {
                            let _ = data.cancel_lane(config.lane);
                            status.send_modify(|s| {
                                s.terminal = true;
                                s.recovering = false;
                            });
                        }
                        break;
                    }
                }
                status.send_modify(|s| s.running = false);
                exit.clean = true;
            })
            .map_err(|_| "Native futures delivery parent admission failed")?;
        Ok(Self {
            checkpoint,
            diagnostics,
            stop,
            task: Some(task),
            status: observed,
            current,
            retirement,
            source_ready,
            source_changed,
        })
    }
    /// Releases the prepared mapper only after native provider ownership has been installed.
    pub fn source_started(&self) {
        if let Some(checkpoint)=&self.checkpoint{if checkpoint.source_started().is_err(){self.stop.cancel();return;}}
        self.source_ready.store(true, Ordering::Release);
        self.source_changed.notify_one();
    }
    #[allow(clippy::too_many_arguments)]
    async fn pump_durable(
        data: &LiveData,
        config: &Configuration,
        encoder: &Arc<Mutex<DurableEncoder>>,
        publisher: &mut Publisher,
        stop: &CancellationToken,
        episode: &mut Option<Deadline>,
        status: &watch::Sender<Status>,
    ) -> Option<String> {
        let mut source = data.updates();
        let mut broker = publisher.changes();
        let mut lifecycle = match data.lifecycle(config.lane) {
            Ok(Some(value)) => value,
            Ok(None) => return Some("Native source owner absent".into()),
            Err(error) => return Some(error),
        };
        loop {
            if stop.is_cancelled() {
                return None;
            }
            // Queue -> mapper is the one lock order, including failed lifecycle recovery.
            let captured = match data.capture_output_prefix(config.lane, 64, |outputs| {
                encoder
                    .lock()
                    .map_err(|_| "Native source encoder interrupted")?
                    .capture(outputs)?;
                Ok(outputs.len())
            }) {
                Ok(value) => value.unwrap_or(0),
                Err(error) => return Some(error),
            };
            let provider = lifecycle.borrow().clone();
            let health = publisher.status();
            let first = match encoder.lock() {
                Ok(encoder) => encoder.first_current,
                Err(_) => return Some("Native encoder interrupted".into()),
            };
            let physical = data.generation(config.lane).ok().flatten();
            let fresh = first.is_some_and(|(generation, threshold)| {
                Some(generation) == physical && health.flushed >= threshold
            }) || config.control_only && health.connected;
            let quiet = provider.phase == crate::dataset::lanes::Phase::Running
                && health.connected
                && first.is_none()
                && health.pending_since.is_none();
            if provider.phase == crate::dataset::lanes::Phase::Running && (fresh || quiet) {
                *episode = None;
            }
            status.send_modify(|state| {
                state.connected = health.connected;
                state.running = provider.phase == crate::dataset::lanes::Phase::Running && fresh;
                if state.running || quiet {
                    state.recovering = false;
                    state.deadline = None;
                    state.error = None;
                    state.root_error = None;
                }
            });
            if let Some(original) = provider.deadline {
                *episode = Some(episode.map_or(original, |old| old.earliest(original)));
            }
            if let Some(error) = health.failure {
                return Some(health.detail.unwrap_or_else(|| error.into()));
            }
            if health.stopped {
                return Some("Native broker transport stopped".into());
            }
            if matches!(
                provider.phase,
                crate::dataset::lanes::Phase::Failed | crate::dataset::lanes::Phase::Stopped
            ) {
                return Some(
                    provider
                        .error
                        .unwrap_or_else(|| "Native source owner stopped".into()),
                );
            }
            if captured > 0 {
                tokio::task::yield_now().await;
                continue;
            }
            let pending = health.pending_since.map(Deadline::recovery_from);
            let deadline = match (*episode, pending) {
                (Some(a), Some(b)) => Some(a.earliest(b)),
                (a, b) => a.or(b),
            };
            tokio::select! {biased;()=stop.cancelled()=>return None,
                ()=async{if let Some(deadline)=deadline{let _=deadline.run(stop,std::future::pending::<()>()).await;}else{std::future::pending::<()>().await;}}=>return Some("Native publication original two-minute deadline expired".into()),
                changed=source.changed()=>if changed.is_err(){return Some("Native source observation ended".into());},
                changed=broker.changed()=>if changed.is_err(){return Some("Native broker observation ended".into());},
                changed=lifecycle.changed()=>if changed.is_err(){return Some("Native lifecycle observation ended".into());},
            }
        }
    }

    #[allow(clippy::too_many_arguments)] // One cold native task context, not an FFI surface.
    async fn pump(
        data: &LiveData,
        events: &mut Futures,
        config: &Configuration,
        sequence: &mut i64,
        current: &Current,
        publisher: &mut Publisher,
        stop: &CancellationToken,
        episode: &mut Option<Deadline>,
        status: &watch::Sender<Status>,
    ) -> Option<String> {
        let durable = publisher.has_source_capture();
        let mut source = data.updates();
        let mut broker = publisher.changes();
        let mut lifecycle = match data.lifecycle(config.lane) {
            Ok(Some(status)) => status,
            Ok(None) => return Some("Native source owner absent".into()),
            Err(error) => return Some(error),
        };
        let mut submitted = 0u64;
        // At most one source record is assembled ahead of broker admission.
        // A full broker queue pauses this consumer, never the provider reader.
        let mut waiting = std::collections::VecDeque::<(PreparedFrame, u128)>::new();
        let mut first_current: Option<(u128, u64)> = None;
        loop {
            if stop.is_cancelled() {
                return None;
            }
            let provider = lifecycle.borrow().clone();
            if let Some(original) = provider.deadline {
                *episode = Some(episode.map_or(original, |prior| prior.earliest(original)));
            }
            match provider.phase {
                crate::dataset::lanes::Phase::Failed | crate::dataset::lanes::Phase::Stopped => {
                    return Some(
                        provider
                            .error
                            .unwrap_or_else(|| "Native source owner stopped".into()),
                    );
                }
                crate::dataset::lanes::Phase::Starting
                | crate::dataset::lanes::Phase::Recovering => status.send_modify(|s| {
                    s.running = false;
                    s.recovering = true;
                    s.deadline = *episode;
                    if let Some(error) = &provider.error {
                        s.root_error.get_or_insert(error.clone());
                        s.error = Some(error.clone());
                    }
                }),
                crate::dataset::lanes::Phase::Running => {}
            }
            let health = publisher.status();
            status.send_if_modified(|s| {
                if s.connected == health.connected {
                    return false;
                }
                s.connected = health.connected;
                true
            });
            let pending = health.pending_since.map(Deadline::recovery_from);
            if let Some(error) = health.failure {
                if let Some(pending) = pending {
                    episode.get_or_insert(pending);
                }
                return Some(
                    health
                        .detail
                        .map_or_else(|| error.into(), |detail| format!("{error}: {detail}")),
                );
            }
            if health.stopped {
                if let Some(pending) = pending {
                    episode.get_or_insert(pending);
                }
                return Some("Native broker transport stopped".into());
            }
            let physical = data.generation(config.lane).ok().flatten();
            let fresh = first_current.is_some_and(|(generation, threshold)| {
                Some(generation) == physical && health.flushed >= threshold
            }) || config.control_only && health.connected;
            // An acknowledged source and connected publisher can legitimately
            // have no market frame. Finish setup without certifying publication
            // or retaining a timer that resets a heartbeat-only quiet feed.
            if provider.phase == crate::dataset::lanes::Phase::Running
                && health.connected
                && first_current.is_none()
                && health.pending_since.is_none()
            {
                *episode = None;
                status.send_if_modified(|s| {
                    if !s.recovering && s.deadline.is_none() {
                        return false;
                    }
                    s.recovering = false;
                    s.deadline = None;
                    true
                });
            }
            if provider.phase == crate::dataset::lanes::Phase::Running
                && fresh
                && !status.borrow().running
            {
                *episode = None;
                status.send_modify(|s| {
                    s.running = true;
                    s.recovering = false;
                    s.deadline = None;
                    s.root_error = None;
                    s.error = None;
                });
            }
            let mut drained = 0;
            if health.connected {
                'drain: for _ in 0..64 {
                    // Retry the detached record before consuming another. The
                    // upstream quote FIFO stays bounded by evicting old quotes;
                    // executions and replay checkpoints are never discarded.
                    while !durable && let Some((frame, physical)) = waiting.front_mut() {
                        if data.generation(config.lane).ok().flatten() != Some(*physical) {
                            waiting.pop_front();
                            continue;
                        }
                        match publisher.try_prepared_frame(frame) {
                            Ok(()) => {
                                submitted = submitted.saturating_add(1);
                                if first_current.is_none_or(|(old, _)| old != *physical) {
                                    first_current = Some((*physical, submitted));
                                }
                                waiting.pop_front();
                            }
                            Err(
                                error @ ("Native publication queue unavailable"
                                | "Native publication byte capacity exceeded"),
                            ) => {
                                if frame.bytes.len() <= config.bytes {
                                    break 'drain;
                                }
                                return Some(error.into());
                            }
                            Err(error) => return Some(error.into()),
                        }
                    }
                    let next =
                        match data.consume_output(config.lane, |value| Ok(Some(value.clone()))) {
                            Ok(value) => value,
                            Err(error) => return Some(error),
                        };
                    let Some(next) = next else {
                        break;
                    };
                    drained += 1;
                    if data.generation(config.lane).ok().flatten() != Some(next.generation) {
                        continue;
                    }
                    let batch = match events.process(&next) {
                        Ok(batch) => batch,
                        Err(error) => return Some(error.into()),
                    };
                    for event in batch {
                        let Some(next_sequence) = sequence.checked_add(1) else {
                            return Some("Native publication sequence exhausted".into());
                        };
                        *sequence = next_sequence;
                        let contract = if matches!(event.kind, 3 | 4) {
                            event
                                .value
                                .as_array()
                                .and_then(|v| v.get(2))
                                .and_then(ValueContract::contract)
                        } else {
                            None
                        };
                        let payload = match codec::encode(&event.value) {
                            Ok(payload) => payload,
                            Err(error) => return Some(error.into()),
                        };
                        let frame = Frame {
                            lane: config.lane,
                            value_date: config.value_date,
                            worker: config.worker,
                            generation: config.generation,
                            manifest_revision: config.revision,
                            sequence: *sequence,
                            kind: event.kind,
                            payload,
                            origin_ticks: Some(next.observed),
                        };
                        if let Some(contract) = contract
                            && let Err(error) = current.retain(contract, &frame)
                        {
                            return Some(error.into());
                        }
                        let prepared = match publisher.prepare_frame(&frame) {
                            Ok(prepared) => prepared,
                            Err(error) => return Some(error.into()),
                        };
                        waiting.push_back((prepared, next.generation));
                    }
                    if durable
                        && (waiting.len() >= 64
                            || waiting.iter().map(|(f, _)| f.bytes.len()).sum::<usize>()
                                >= config.bytes)
                    {
                        break;
                    }
                }
                if durable {
                    while !waiting.is_empty() {
                        let count = waiting.len().min(64);
                        let physical = waiting[0].1;
                        let mut frames = waiting
                            .iter_mut()
                            .take(count)
                            .map(|(frame, _)| frame)
                            .collect::<Vec<_>>();
                        if let Err(error) = publisher.try_prepared_batch(&mut frames) {
                            return Some(error.into());
                        }
                        submitted = submitted.saturating_add(count as u64);
                        if first_current.is_none_or(|(old, _)| old != physical) {
                            first_current = Some((physical, submitted));
                        }
                        for _ in 0..count {
                            waiting.pop_front();
                        }
                    }
                }
            }
            if drained == 64 || drained != 0 && !waiting.is_empty() {
                tokio::task::yield_now().await;
                continue;
            }
            // Notification only, never a managed read/drain loop or timer poll.
            let deadline = match (*episode, pending) {
                (Some(a), Some(b)) => Some(a.earliest(b)),
                (a, b) => a.or(b),
            };
            tokio::select! {biased; ()=stop.cancelled()=>return None,
            ()=async{match deadline{Some(d)=>{let _=d.run(stop,std::future::pending::<()>()).await;},None=>std::future::pending().await}}=>{
                // Preserve the first pending batch's deadline. The retry owner
                // must report failure now, rather than start another 120 seconds.
                if let Some(deadline)=deadline {*episode=Some(deadline);}
                return Some("Native publication original two-minute deadline expired".into());
            },
            changed=broker.changed()=>if changed.is_err(){return Some("Native broker observation ended".into());},
            changed=source.changed()=>if changed.is_err(){return Some("Native source observation ended".into());},
            changed=lifecycle.changed()=>if changed.is_err(){
                let provider=lifecycle.borrow().clone();
                if let Some(original)=provider.deadline {*episode=Some(episode.map_or(original,|prior|prior.earliest(original)));}
                return Some(provider.error.unwrap_or_else(||"Native lifecycle observation ended".into()));
            }}
        }
    }
    /// Non-consuming native availability status even when the broker is down.
    pub fn status(&self) -> Status {
        self.status.borrow().clone()
    }
    /// Event-driven availability observations, independent of publication capacity.
    pub fn changes(&self) -> watch::Receiver<Status> {
        self.status.clone()
    }
    /// Detached latest-value query owner; retaining it cannot retain a dataset.
    pub fn current(&self) -> Arc<Current> {
        self.current.clone()
    }
    /// Detached publication observation; it owns no task or provider resource.
    pub fn diagnostics(&self) -> PublicationDiagnostics {
        self.diagnostics.clone()
    }
    /// Signals cancellation and proves producer/socket worker exit under the
    /// caller's existing deadline. Repeated calls never extend that deadline.
    ///
    /// # Errors
    /// An expired or failed join is never presented as successful cleanup.
    pub async fn shutdown(&mut self, deadline: Deadline) -> Result<(), &'static str> {
        let deadline = {
            let mut saved = self
                .retirement
                .lock()
                .map_err(|_| "Native publication retirement interrupted")?;
            let d = saved.map_or(deadline, |prior| prior.earliest(deadline));
            *saved = Some(d);
            d
        };
        self.stop.cancel();
        if let Some(task) = self.task.as_mut() {
            let joined = deadline
                .run(&CancellationToken::new(), task)
                .await
                .map_err(|_| "Native publication join deadline expired")?;
            self.task.take();
            joined.map_err(|_| "Native publication task failed")?;
        }
        Ok(())
    }
}
struct ValueContract;
impl ValueContract {
    fn contract(value: &rmpv::Value) -> Option<&str> {
        value.as_array()?.first()?.as_str()
    }
}
impl Drop for Delivery {
    fn drop(&mut self) {
        self.stop.cancel();
    }
}

#[cfg(test)]
mod durable_mapper_tests {
    use super::*;
    use crate::dataset::{book::{PublicationState,Update},live::{OutputSource,Registration},records::{Header,Observation,Statistic}};
    use crate::aggregation::session::{Snapshot,VolumeQuality};
    fn date()->time::Date{time::Date::from_calendar_date(2026,time::Month::November,23).unwrap()}
    fn root()->std::path::PathBuf{std::env::temp_dir().join(format!("ifm-mapper-source-{:032x}",rand::random::<u128>()))}
    fn broker()->BrokerSettings{BrokerSettings::new("nats://127.0.0.1:4222".into(),vec!["1.1.1.1:53".parse().unwrap()]).unwrap()}
    fn official()->Output{
        let stats=Snapshot{value_date:date(),open:6_000_000_000_000,high:6_025_000_000_000,low:5_975_000_000_000,sequence:1,event_ns:1_795_467_599_000_000_000,volume:1234567,quality:VolumeQuality::OfficialFinal};
        Output{capture_serial:1,observed:17_954_675_990_000_000,lane:Lane::FuturesGlbx,generation:55,
            source:OutputSource::Observation(Observation::Statistic(Statistic{header:Header{instrument:42,publisher:1,flags:0,event_ns:stats.event_ns,received_ns:stats.event_ns+100,sequence:1,schema:9},price:0,quantity:stats.volume,reference_ns:stats.event_ns,kind:6,channel:2,action:1,flags:0})),state:PublicationState{update:Some(Update::Statistics(stats)),session:Some(stats),..Default::default()},trade:None,raw:None}
    }
    fn encoder(raw:&std::path::Path,source:Option<&Arc<super::super::source_outbox::Sink>>)->DurableEncoder{
        let registration=Registration{instrument:42,publisher:1,contract:"ES20261218".into(),source:"ESZ6".into(),asset:crate::latest::Asset::Futures,tick:Some(250000000),vwap:true,required:true};
        DurableEncoder{events:Futures::new(Lane::FuturesGlbx,date(),date(),vec![registration],64).unwrap(),config:Configuration{broker:broker(),scope:"test".into(),lane:Lane::FuturesGlbx,value_date:date().to_julian_day()-1721426,worker:[1;16],generation:[2;16],revision:1,control_only:false,queue:256,bytes:4*1024*1024},sequence:0,port:super::super::publisher::Port::local_source_for_test("test",source),current:Arc::new(Current::new(128,16*1024*1024).unwrap()),submitted:0,first_current:None,completed:Default::default(),pending:Default::default(),pending_keys:Default::default(),required:crate::dataset::required_capture::RequiredCaptureIntent::open(raw,"test-1").unwrap(),required_last:None,mapping_failed:false}
    }
    #[test]
    fn mapped_official_wire_and_ids_stay_frozen_through_refusal_and_cold_source_import(){
        let root=root();let raw=root.join("raw");let mut encoder=encoder(&raw,None);let value=official();assert!(encoder.capture_required(std::slice::from_ref(&value)).is_err());
        let frozen=encoder.pending.front().unwrap().frames[0].bytes.clone();assert!(encoder.required.frozen().is_some());let sequence=encoder.sequence;
        assert!(encoder.capture_required(std::slice::from_ref(&value)).is_err());assert_eq!(encoder.sequence,sequence);assert_eq!(encoder.pending.front().unwrap().frames[0].bytes,frozen);drop(encoder);
        let sink=super::super::source_outbox::Sink::open(root.join("source"),broker(),"test".into()).unwrap();sink.reconcile_required_from(Lane::FuturesGlbx,&raw).unwrap();assert_eq!(sink.pending_wire_for_test().unwrap(),vec![frozen]);
        let marker=crate::dataset::required_capture::RequiredCaptureIntent::open(&raw,"test-1").unwrap();assert!(marker.original().is_none());drop(marker);drop(sink);std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn original_only_mapper_failure_never_remints_or_cold_reports_success(){
        let root=root();let raw=root.join("raw");let mut encoder=encoder(&raw,None);let mut value=official();if let OutputSource::Observation(Observation::Statistic(ref mut statistic))=value.source{statistic.header.instrument=999;}
        assert!(encoder.capture_required(std::slice::from_ref(&value)).is_err());assert!(encoder.mapping_failed);let sequence=encoder.sequence;assert!(encoder.capture_required(std::slice::from_ref(&value)).is_err());assert_eq!(encoder.sequence,sequence);assert!(encoder.required.original().is_some());assert!(encoder.required.frozen().is_none());drop(encoder);
        let sink=super::super::source_outbox::Sink::open(root.join("source"),broker(),"test".into()).unwrap();assert!(sink.reconcile_required_from(Lane::FuturesGlbx,&raw).is_err());assert!(sink.pending_wire_for_test().unwrap().is_empty());drop(sink);std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn checkpoint_keeps_original_prepared_ids_intent_and_sequence_across_cold_retry(){
        let root=root();let mut original=encoder(&root.join("raw-a"),None);let value=official();
        assert!(original.capture_required(std::slice::from_ref(&value)).is_err());
        let frozen=original.pending.front().unwrap().frames[0].bytes.clone();let sequence=original.sequence;
        let image=original.checkpoint().unwrap();let bytes=crate::checkpoint::encode(&image).unwrap();let decoded=crate::checkpoint::decode(&bytes).unwrap();
        let mut cold=encoder(&root.join("raw-b"),None);cold.import_checkpoint(&decoded).unwrap();
        assert_eq!(cold.sequence,sequence);assert_eq!(cold.events.checkpoint(),original.events.checkpoint());
        assert_eq!(cold.required.original(),original.required.original());assert_eq!(cold.required.frozen(),original.required.frozen());
        assert!(cold.capture_required(std::slice::from_ref(&value)).is_err());assert_eq!(cold.pending.front().unwrap().frames[0].bytes,frozen);assert_eq!(cold.sequence,sequence);
        let sink=super::super::source_outbox::Sink::open(root.join("source"),broker(),"test".into()).unwrap();
        cold.rebind(super::super::publisher::Port::local_source_for_test("test",Some(&sink)));
        cold.capture_required(std::slice::from_ref(&value)).unwrap();assert!(cold.pending.is_empty());assert!(cold.required.original().is_none());assert_eq!(sink.pending_wire_for_test().unwrap(),vec![frozen]);
        drop(cold);drop(original);drop(sink);std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn checkpoint_restore_refuses_changed_original_owner_or_active_mapper(){
        let root=root();let original=encoder(&root.join("raw-a"),None);let image=original.checkpoint().unwrap();
        let mut wrong=encoder(&root.join("raw-b"),None);wrong.config.revision=2;assert!(wrong.import_checkpoint(&image).is_err());
        let mut active=encoder(&root.join("raw-c"),None);active.sequence=1;assert!(active.import_checkpoint(&image).is_err());
        drop(original);drop(wrong);drop(active);std::fs::remove_dir_all(root).unwrap();
    }
    #[test]
    fn complete_futures_owner_restores_original_catalog_book_and_mapper_without_provider(){
        use crate::checkpoint::CheckpointOwner;use rmpv::Value as V;
        let root=root();let original=Arc::new(Mutex::new(encoder(&root.join("raw-a"),None)));
        let config=original.lock().unwrap().config.clone();let clock=Arc::new(crate::runtime::clock::Clock::new(17_000_000_000_000_000,false).unwrap());
        let data=Arc::new(LiveData::new(date(),u128::from_le_bytes(config.generation),2048,clock.clone(),Arc::new(crate::runtime::owner::Owner::default())).unwrap());
        let mut book=crate::dataset::book::Book::<crate::pricing::Values>::new(date(),55,2048).unwrap();
        book.register(42,"ES20261218",crate::latest::Asset::Futures,1).unwrap().update_quote(crate::latest::Tick{sequence:77,event_ticks:1234,receive_ticks:1235,value:crate::latest::QuoteValue{bid:Some(6_000_000_000_000),ask:Some(6_000_250_000_000),bid_size:5,ask_size:6,bid_count:1,ask_count:1,local_received_ticks:1236}}).unwrap();
        data.seed_checkpoint_book_for_test(Lane::FuturesGlbx,book,3).unwrap();
        let metadata=V::Array(vec![1.into(),crate::checkpoint::day(date()),crate::checkpoint::day(date()),V::Array(vec![V::Array(vec![V::Array(vec!["ESZ6".into()]),"raw_symbol".into(),"trades".into(),V::Nil])]),V::Array(vec![V::Array(vec![42.into(),1.into(),"ES20261218".into(),"ESZ6".into(),1.into(),250000000.into(),true.into(),true.into()])]),2.into(),V::Array(vec![V::Array(vec!["original-contract-dto".into()])]),V::Array(vec![V::Array(vec!["original-public-model".into()])])]);
        let owner=FuturesCheckpointOwner{data:Arc::downgrade(&data),encoder:Arc::downgrade(&original),lane:Lane::FuturesGlbx,metadata:metadata.clone()};
        let image=owner.checkpoint().unwrap();validate_checkpoint_image(&image).unwrap();
        let restored_data=Arc::new(LiveData::new(date(),u128::from_le_bytes(config.generation),2048,clock,Arc::new(crate::runtime::owner::Owner::default())).unwrap());let restored_encoder=Arc::new(Mutex::new(encoder(&root.join("raw-b"),None)));
        let restored=FuturesCheckpointOwner{data:Arc::downgrade(&restored_data),encoder:Arc::downgrade(&restored_encoder),lane:Lane::FuturesGlbx,metadata:metadata.clone()};
        restored.restore(&crate::checkpoint::decode(&crate::checkpoint::encode(&image).unwrap()).unwrap()).unwrap();
        assert!(restored_data.lifecycle(Lane::FuturesGlbx).unwrap().is_none());let query=restored_data.queries(Lane::FuturesGlbx).unwrap().unwrap();let quote=query.raw_prices(42).unwrap().unwrap().quote.unwrap();assert_eq!(quote.sequence,77);assert_eq!(quote.event_ticks,1234);assert_eq!(quote.value.bid,Some(6_000_000_000_000));assert_eq!(restored_encoder.lock().unwrap().events.checkpoint(),original.lock().unwrap().events.checkpoint());
        let recipe=crate::dataset::futures_recipe_checkpoint::decode(&metadata,Lane::FuturesGlbx).unwrap();assert_eq!(recipe.contracts,vec![V::Array(vec!["original-contract-dto".into()])]);assert_eq!(recipe.public_models,vec![V::Array(vec!["original-public-model".into()])]);
        let mut wrong=image.clone();if let V::Array(owner)=&mut wrong{if let V::Array(recipe)=&mut owner[3]{if let V::Array(rows)=&mut recipe[4]{if let V::Array(registration)=&mut rows[0]{registration[2]="ES20270319".into();}}}}assert!(validate_checkpoint_image(&wrong).is_err());
        drop(restored);drop(restored_encoder);drop(original);std::fs::remove_dir_all(root).unwrap();
    }
}
