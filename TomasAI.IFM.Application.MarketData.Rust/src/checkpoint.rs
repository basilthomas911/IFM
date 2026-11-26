//! Versioned bounded binary checkpoint authority. It contains owned state, never live handles or credentials.
use rmpv::Value;
use sha2::{Digest,Sha256};
use std::{collections::BTreeMap,fs::{File,OpenOptions},io::{Read,Write},path::Path,sync::{Arc,Condvar,Mutex,OnceLock,Weak},time::{Duration,Instant}};

const MAGIC:&[u8;8]=b"IFMCKP01";
/// Absolute decoded checkpoint bound, checked before allocation or restoration.
pub const MAX_BYTES:usize=64*1024*1024;
struct Gate{state:Mutex<(u64,Option<u64>)>,changed:Condvar}
static TRANSACTIONS:Gate=Gate{state:Mutex::new((0,None)),changed:Condvar::new()};
static ENABLED:OnceLock<bool>=OnceLock::new();
static IMPORT_ONLY:OnceLock<bool>=OnceLock::new();
type Directory=BTreeMap<(String,String),Weak<dyn CheckpointOwner>>;
static OWNERS:OnceLock<Mutex<Directory>>=OnceLock::new();
struct Staged{digest:[u8;32],owners:BTreeMap<String,Value>,floor:Value,bytes:usize,source_verified:bool,applied:std::collections::BTreeSet<String>,restore_root:Option<std::path::PathBuf>,source:Option<Arc<crate::messaging::source_outbox::Sink>>}
static STAGED:OnceLock<Mutex<BTreeMap<String,Staged>>>=OnceLock::new();
pub(crate) trait CheckpointOwner:Send+Sync{
    fn open_original_source(&self)->Result<Arc<crate::messaging::source_outbox::Sink>,String>{Err("Original source owner is not installed".into())}
    fn prepare(&self)->Result<(),String>{Ok(())}
    fn checkpoint(&self)->Result<Value,String>;
    fn restore(&self,value:&Value)->Result<(),String>;
}
pub(crate) struct Registration{scope:String,key:String,owner:Arc<dyn CheckpointOwner>}
impl Registration{pub(crate) fn source_started(&self)->Result<(),String>{let mut staged=STAGED.get_or_init(Mutex::default).lock().map_err(|_|"Staged checkpoint owner directory interrupted")?;if let Some(image)=staged.get_mut(&self.scope){if image.owners.contains_key(&self.key){image.applied.insert(self.key.clone());}}Ok(())}}
pub(crate) fn register(scope:&str,key:&str,owner:Arc<dyn CheckpointOwner>)->Result<Registration,String>{
    let _transaction=transaction()?;
    if scope.is_empty()||scope.len()>128||key.is_empty()||key.len()>256{return Err("Invalid native checkpoint owner key".into());}
    let mut owners=OWNERS.get_or_init(Mutex::default).lock().map_err(|_|"Checkpoint registry interrupted")?;
    owners.retain(|_,owner|owner.strong_count()>0);
    if owners.len()>=512||owners.get(&(scope.into(),key.into())).and_then(Weak::upgrade).is_some(){return Err("Duplicate/overflow native checkpoint owner".into());}
    let original={let staged=STAGED.get_or_init(Mutex::default).lock().map_err(|_|"Staged checkpoint owner directory interrupted")?;staged.get(scope).filter(|image|!image.applied.contains(key)&&image.applied.len()<image.owners.len()).map(|image|(image.owners.get(key).cloned(),image.source_verified,image.floor.clone()))};
    if let Some((value,verified,floor))=original{
        let value=value.ok_or("New native owner cannot replace an unbound original checkpoint owner")?;
        let source=if !verified{let source=match crate::messaging::source_outbox::lookup(scope){Ok(Some(source))=>source,Ok(None) if replay_import_only()=>owner.open_original_source()?,Err("Native durable source owner is not ready") if replay_import_only()=>owner.open_original_source()?,Ok(None)=>return Err("Original native source floor not installed".into()),Err(error)=>return Err(error.into())};source.verify_checkpoint_floor(&floor)?;Some(source)}else{None};
        owner.restore(&value)?;
        if let Some(image)=STAGED.get_or_init(Mutex::default).lock().map_err(|_|"Staged checkpoint directory interrupted")?.get_mut(scope){image.source_verified=true;if source.is_some(){image.source=source;}}
    }
    owners.insert((scope.into(),key.into()),Arc::downgrade(&owner));Ok(Registration{scope:scope.into(),key:key.into(),owner})
}
impl Drop for Registration{fn drop(&mut self){if let Ok(_transaction)=transaction(){if let Ok(mut owners)=OWNERS.get_or_init(Mutex::default).lock(){let key=(self.scope.clone(),self.key.clone());if owners.get(&key).and_then(Weak::upgrade).is_some_and(|owner|Arc::ptr_eq(&owner,&self.owner)){owners.remove(&key);}}}}}
fn owners(scope:&str)->Result<Vec<(String,Arc<dyn CheckpointOwner>)>,String>{
    let owners=OWNERS.get_or_init(Mutex::default).lock().map_err(|_|"Checkpoint registry interrupted")?;
    let values=owners.iter().filter(|((owner_scope,_),_)|owner_scope==scope).map(|((_,key),owner)|owner.upgrade().map(|owner|(key.clone(),owner)).ok_or_else(||"Native checkpoint owner ended during capture".to_owned())).collect::<Result<Vec<_>,_>>()?;
    if values.is_empty(){return Err("No original native checkpoint owners are installed".into());}Ok(values)
}
pub(crate) fn staged_owner(scope:&str,key:&str)->Result<Option<Value>,String>{Ok(STAGED.get_or_init(Mutex::default).lock().map_err(|_|"Staged checkpoint directory interrupted")?.get(scope).filter(|image|!image.applied.contains(key)).and_then(|image|image.owners.get(key)).cloned())}
pub(crate) fn staged_futures_identity(scope:&str,lane:crate::dataset::transport::Lane)->Result<Option<([u8;16],[u8;16])>,String>{
    let Some(value)=staged_owner(scope,&format!("futures-{}",lane as u8))?else{return Ok(None);};let value=fields(&value,4)?;let mapper=fields(&value[2],11)?;let config=fields(&mapper[1],10)?;
    let read=|value:&Value|match value{Value::Binary(bytes) if bytes.len()==16=>Ok(bytes.as_slice().try_into().unwrap()),_=>Err("Staged original owner UUID invalid".to_owned())};Ok(Some((read(&config[3])?,read(&config[4])?)))
}
pub(crate) fn configure_restore_root(scope:&str,root:&Path)->Result<(),String>{
    if !root.is_absolute()||!root.is_dir(){return Err("Explicit existing isolated checkpoint restore root required".into());}let root=root.canonicalize().map_err(|error|error.to_string())?;
    let mut staged=STAGED.get_or_init(Mutex::default).lock().map_err(|_|"Staged checkpoint directory interrupted")?;let image=staged.get_mut(scope).ok_or("Stage original checkpoint before mapping restored roots")?;
    let source=fields(&image.floor,7)?;let mut originals=vec![std::path::PathBuf::from(string(&source[6],32768)?)];
    for(key,value)in &image.owners{if key.starts_with("futures-"){let owner=fields(value,4)?;let data=fields(&owner[1],14)?;if !data[13].is_nil(){let raw=fields(&data[13],10)?;originals.push(std::path::PathBuf::from(string(&raw[1],32768)?));}}}
    for original in originals{let original=original.canonicalize().unwrap_or(original);if root.starts_with(&original)||original.starts_with(&root){return Err("Isolated restore root overlaps an original live native owner".into());}}
    if image.source_verified||!image.applied.is_empty(){return Err("Native checkpoint restore roots cannot change after owner binding".into());}
    if image.restore_root.as_ref().is_some_and(|original|original!=&root){return Err("A different isolated restore root is already bound".into());}image.restore_root=Some(root);Ok(())
}
pub(crate) fn logical_namespace(scope:&str,actual:[u8;32])->[u8;32]{
    STAGED.get_or_init(Mutex::default).lock().ok().and_then(|staged|staged.get(scope).and_then(|image|image.floor.as_array()).and_then(|fields|fields.get(2)).and_then(|value|match value{Value::Binary(bytes) if bytes.len()==32=>Some(bytes.as_slice().try_into().unwrap()),_=>None})).unwrap_or(actual)
}
pub(crate) fn validate_restore_path(scope:&str,path:&Path)->Result<(),String>{
    let path=path.canonicalize().map_err(|error|error.to_string())?;let staged=STAGED.get_or_init(Mutex::default).lock().map_err(|_|"Staged checkpoint directory interrupted")?;
    if let Some(image)=staged.get(scope){let root=image.restore_root.as_ref().ok_or("Explicit original-to-isolated checkpoint root mapping is missing")?;if &path==root||!path.starts_with(root){return Err("Native restored owner path escapes its isolated certificate root".into());}}Ok(())
}
pub(crate) fn validate_raw_restore_path(path:&Path,original:&Value)->Result<(),String>{
    let path=path.canonicalize().map_err(|error|error.to_string())?;let fields=fields(original,10)?;let old=std::path::PathBuf::from(string(&fields[1],32768)?);
    if old.canonicalize().ok().as_ref()==Some(&path){return Err("Native restore cannot reopen the original raw source root".into());}
    let staged=STAGED.get_or_init(Mutex::default).lock().map_err(|_|"Staged checkpoint directory interrupted")?;
    if !staged.values().any(|image|image.restore_root.as_ref().is_some_and(|root|&path!=root&&path.starts_with(root))){return Err("Raw checkpoint target is outside the explicit isolated restore mapping".into());}Ok(())
}
pub(crate) fn export_scope(scope:&str,lease:u64,path:&Path)->Result<[u8;32],String>{
    if lease==0||crate::messaging::source_forwarding::ifm_market_data_source_forwarding_lease_v1()!=lease||crate::messaging::source_forwarding::ifm_market_data_source_forwarding_active_v1()!=0{return Err("Native checkpoint requires the original drained forwarding lease".into());}
    if !frozen_by(lease)?{return Err("Native checkpoint state lease is not held".into());}
    let value={
        let owners=owners(scope)?;let mut values=Vec::with_capacity(owners.len());
        for(key,owner)in owners{values.push(Value::Array(vec![key.into(),owner.checkpoint()?]));}
        let source=crate::messaging::source_outbox::lookup(scope).map_err(str::to_owned)?.ok_or("Native checkpoint requires durable source ownership")?;
        Value::Array(vec![1.into(),scope.into(),lease.into(),Value::Array(values),source.checkpoint_floor()?])
    };
    if crate::messaging::source_forwarding::ifm_market_data_source_forwarding_lease_v1()!=lease{return Err("Native checkpoint lease changed before persistence".into());}
    save(path,&value)
}
pub(crate) fn import_scope(scope:&str,path:&Path,expected:[u8;32])->Result<(),String>{
    let value=load(path,expected)?;let f=fields(&value,5)?;
    if unsigned(&f[0])?!=1||string(&f[1],128)?!=scope{return Err("Native checkpoint scope/version differs".into());}
    let _freeze=freeze()?;
    let values=f[3].as_array().filter(|values|!values.is_empty()&&values.len()<=512).ok_or("Native checkpoint owner set exceeds bounds")?;
    let mut restored=std::collections::BTreeSet::new();
    let floor=fields(&f[4],7)?;if unsigned(&floor[0])?!=1||string(&floor[1],128)?!=scope||!matches!(&floor[2],Value::Binary(bytes)if bytes.len()==32)||!std::path::Path::new(&string(&floor[6],32768)?).is_absolute(){return Err("Native checkpoint original source floor differs".into());}
    let mut pending=BTreeMap::new();
    for value in values{let pair=fields(value,2)?;let key=string(&pair[0],256)?;if !restored.insert(key.clone()){return Err("Native checkpoint repeats an owner".into());}
        if key.starts_with("futures-"){crate::messaging::futures_delivery::validate_checkpoint_image(&pair[1])?;let owner=fields(&pair[1],4)?;let book=fields(&owner[1],14)?;let mapper=fields(&owner[2],11)?;let config=fields(&mapper[1],10)?;if key!=format!("futures-{}",lane(&book[4])? as u8)||string(&config[0],128)?!=scope{return Err("Original futures checkpoint owner key/scope differs".into());}}
        else if key=="options-3"{crate::dataset::options_owner::validate_checkpoint_image_for_scope(&pair[1],scope)?;}
        else{return Err("Native checkpoint owner kind unsupported".into());}
        pending.insert(key,pair[1].clone());
    }
    let bytes=std::fs::metadata(path).map_err(|error|error.to_string())?.len() as usize;
    let mut staged=STAGED.get_or_init(Mutex::default).lock().map_err(|_|"Staged checkpoint directory interrupted")?;
    if let Some(original)=staged.get(scope){if original.digest==expected{return Ok(());}return Err("A different original checkpoint is already staged for this scope".into());}
    if staged.len()>=16||staged.values().map(|image|image.bytes).sum::<usize>()+bytes>128*1024*1024{return Err("Staged checkpoint image capacity exceeded".into());}
    if OWNERS.get_or_init(Mutex::default).lock().map_err(|_|"Checkpoint owner registry interrupted")?.keys().any(|(owner_scope,_)|owner_scope==scope){return Err("Native checkpoint staging requires an idle owner set".into());}
    staged.insert(scope.into(),Staged{digest:expected,owners:pending,floor:f[4].clone(),bytes,source_verified:false,applied:Default::default(),restore_root:None,source:None});Ok(())
}
pub(crate) fn begin_scope(scope:&str,lease:u64)->Result<(),String>{
    if lease==0||crate::messaging::source_forwarding::ifm_market_data_source_forwarding_lease_v1()!=lease||crate::messaging::source_forwarding::ifm_market_data_source_forwarding_active_v1()!=0{return Err("Native checkpoint requires original forwarding ownership".into());}
    hold(lease)?;
    let prepared=(||{for(_,owner)in owners(scope)?{owner.prepare()?;}
        if crate::messaging::source_production::ifm_market_data_source_production_lease_v1()==lease{crate::messaging::source_production::seal_prepared(lease)?;}Ok(())})();
    if prepared.is_err(){let _=end_scope(lease);}prepared
}
pub(crate) fn prepare_paused_production(scope:&str,lease:u64)->Result<(),String>{
    if crate::messaging::source_production::ifm_market_data_source_production_lease_v1()!=lease
        ||crate::messaging::source_production::ifm_market_data_source_production_active_v1()!=0{return Err("Native production is not quiescent".into());}
    hold(lease)?;
    let prepared=(||{for(_,owner)in owners(scope)?{owner.prepare()?;}crate::messaging::source_production::seal_prepared(lease)})();
    let released=end_scope(lease);prepared.and(released)
}
pub(crate) fn end_scope(lease:u64)->Result<(),String>{
    let mut state=TRANSACTIONS.state.lock().map_err(|_|"Native checkpoint gate interrupted")?;
    if lease==0||state.1!=Some(lease)||state.0!=0{return Err("Native checkpoint lease owner differs".into());}
    state.1=None;TRANSACTIONS.changed.notify_all();Ok(())
}
/// Adds the actual raw-journal prefixes to the existing source sidecar while
/// the same cross-owner state lease is held. Full prefix hashes are deliberately
/// calculated by the copy tool after releasing this short state freeze.
pub(crate) fn source_prefix_manifest(scope:&str,lease:u64)->Result<String,String>{
    let original=crate::messaging::source_outbox::capture_prefix_manifest(scope,lease)?;
    if !frozen_by(lease)?{return Ok(original);}
    let mut manifest:serde_json::Value=serde_json::from_str(&original).map_err(|error|error.to_string())?;
    let mut raw_sources=Vec::new();
    for(key,owner)in owners(scope)?{
        if !key.starts_with("futures-"){continue;}
        let image=owner.checkpoint()?;let owner=fields(&image,4)?;let data=fields(&owner[1],14)?;
        if data[13].is_nil(){continue;}
        let raw=fields(&data[13],10)?;let root=string(&raw[1],32768)?;
        let epoch=match &raw[2]{Value::Binary(bytes)if bytes.len()==16=>u128::from_le_bytes(bytes.as_slice().try_into().unwrap()),_=>return Err("Raw sidecar original epoch invalid".into())};
        let mut files=Vec::new();for row in raw[6].as_array().ok_or("Raw sidecar original files absent")?{let row=fields(row,3)?;let tail=match &row[2]{Value::Binary(bytes)if bytes.len()==32=>bytes.iter().map(|byte|format!("{byte:02x}")).collect::<String>(),_=>return Err("Raw sidecar tail digest invalid".into())};files.push(serde_json::json!({"RelativePath":string(&row[0],256)?,"Length":unsigned(&row[1])?,"TailSha256":tail}));}
        let owner_name=format!("{epoch:032x}.owner");let length=std::fs::metadata(std::path::Path::new(&root).join(&owner_name)).map_err(|error|error.to_string())?.len();if length!=0{return Err("Raw segment lock file has unexpected content".into());}
        files.push(serde_json::json!({"RelativePath":owner_name,"Length":length}));
        raw_sources.push(serde_json::json!({"OwnerKey":key,"SourceRoot":root,"Files":files,"CurrentEpoch":format!("{epoch:032x}"),"Ordinal":unsigned(&raw[3])?,"SequenceBase":number(&raw[4])?}));
    }
    manifest["RawSources"]=serde_json::Value::Array(raw_sources);manifest["CheckpointStateHeld"]=true.into();
    let encoded=serde_json::to_string(&manifest).map_err(|error|error.to_string())?;if encoded.len()>1024*1024{return Err("Combined native source prefix sidecar exceeds bound".into());}Ok(encoded)
}
fn frozen_by(lease:u64)->Result<bool,String>{let state=TRANSACTIONS.state.lock().map_err(|_|"Native checkpoint gate interrupted")?;Ok(lease!=0&&state.1==Some(lease)&&state.0==0)}
fn hold(lease:u64)->Result<(),String>{
    if !enabled(){return Err("Native checkpoint boundary is disabled".into());}
    let until=Instant::now()+Duration::from_secs(10);let mut state=TRANSACTIONS.state.lock().map_err(|_|"Native checkpoint gate interrupted")?;
    if state.1.is_some_and(|owner|owner!=lease){return Err("Another native checkpoint lease is held".into());}
    state.1=Some(lease);
    while state.0!=0{let remaining=until.saturating_duration_since(Instant::now());if remaining.is_zero(){state.1=None;TRANSACTIONS.changed.notify_all();return Err("Native checkpoint could not drain active source transactions".into());}state=TRANSACTIONS.changed.wait_timeout(state,remaining).map_err(|_|"Native checkpoint gate interrupted")?.0;}
    Ok(())
}
pub(crate) struct Transaction;
impl Drop for Transaction{fn drop(&mut self){if let Ok(mut state)=TRANSACTIONS.state.lock(){state.0=state.0.saturating_sub(1);TRANSACTIONS.changed.notify_all();}}}
pub(crate) struct Freeze(u64);
impl Drop for Freeze{fn drop(&mut self){let _=end_scope(self.0);}}
pub(crate) fn enabled()->bool{*ENABLED.get_or_init(||std::env::var("IFM_SESSION_CAPTURE_BOUNDARY_ENABLED").is_ok_and(|value|value=="1"))}
pub(crate) fn replay_import_only()->bool{enabled()&&*IMPORT_ONLY.get_or_init(||std::env::var("IFM_HISTORICAL_NATIVE_IMPORT_ONLY").is_ok_and(|value|value=="1"))}

/// Short synchronous state transaction. No caller may hold this through an await.
pub(crate) fn transaction()->Result<Option<Transaction>,String>{if !enabled(){return Ok(None);}let mut state=TRANSACTIONS.state.lock().map_err(|_|"Native checkpoint transaction interrupted")?;while state.1.is_some(){state=TRANSACTIONS.changed.wait(state).map_err(|_|"Native checkpoint transaction interrupted")?;}state.0=state.0.checked_add(1).ok_or("Native checkpoint transaction count overflow")?;Ok(Some(Transaction))}
/// Stops original state mutation while a complete set of owners is copied.
pub(crate) fn freeze()->Result<Freeze,String>{let lease=rand::random::<u64>().max(1);hold(lease)?;Ok(Freeze(lease))}

/// Encodes a typed numeric-key checkpoint, preserving every integer and opaque frozen frame byte.
pub(crate) fn encode(value:&Value)->Result<Vec<u8>,String>{
    struct Bounded(Vec<u8>);
    impl Write for Bounded{
        fn write(&mut self,bytes:&[u8])->std::io::Result<usize>{
            if bytes.len()>MAX_BYTES.saturating_sub(self.0.len()){return Err(std::io::Error::other("Native checkpoint exceeds bound"));}
            self.0.extend_from_slice(bytes);Ok(bytes.len())
        }
        fn flush(&mut self)->std::io::Result<()>{Ok(())}
    }
    let mut payload=Bounded(Vec::new());rmpv::encode::write_value(&mut payload,value).map_err(|error|error.to_string())?;
    let mut result=Vec::with_capacity(payload.0.len()+48);result.extend_from_slice(MAGIC);result.extend_from_slice(&(payload.0.len() as u64).to_le_bytes());
    result.extend_from_slice(&Sha256::digest(&payload.0));result.extend_from_slice(&payload.0);Ok(result)
}
/// Validates version, length and digest before decoding any state graph.
pub(crate) fn decode(bytes:&[u8])->Result<Value,String>{
    if bytes.len()<48||bytes.len()>MAX_BYTES+48||bytes.get(..8)!=Some(MAGIC.as_slice()){return Err("Native checkpoint version/length invalid".into());}
    let length=u64::from_le_bytes(bytes[8..16].try_into().unwrap());
    if length!=bytes.len() as u64-48||Sha256::digest(&bytes[48..]).as_slice()!=&bytes[16..48]{return Err("Native checkpoint checksum/length mismatch".into());}
    let mut input=std::io::Cursor::new(&bytes[48..]);let value=rmpv::decode::read_value_with_max_depth(&mut input,64).map_err(|error|error.to_string())?;
    if input.position()!=length{return Err("Native checkpoint contains trailing fields".into());}Ok(value)
}
/// Creates and syncs a new checkpoint file. Existing evidence is never overwritten.
pub(crate) fn save(path:&Path,value:&Value)->Result<[u8;32],String>{
    let bytes=encode(value)?;let digest=Sha256::digest(&bytes).into();
    let mut file=OpenOptions::new().write(true).create_new(true).open(path).map_err(|error|error.to_string())?;
    file.write_all(&bytes).and_then(|_|file.sync_all()).map_err(|error|error.to_string())?;Ok(digest)
}
/// Reads a checkpoint only under its externally bound full-file SHA256.
pub(crate) fn load(path:&Path,expected:[u8;32])->Result<Value,String>{
    let file=File::open(path).map_err(|error|error.to_string())?;let length=file.metadata().map_err(|error|error.to_string())?.len();
    if length>MAX_BYTES as u64+48{return Err("Native checkpoint file exceeds bound".into());}
    let mut bytes=Vec::with_capacity(length as usize);file.take(MAX_BYTES as u64+49).read_to_end(&mut bytes).map_err(|error|error.to_string())?;
    if bytes.len()>MAX_BYTES+48{return Err("Native checkpoint grew beyond its explicit bound".into());}
    if Sha256::digest(&bytes).as_slice()!=expected{return Err("Native checkpoint does not match original manifest digest".into());}decode(&bytes)
}
/// Exact fixed-shape record fields, with no defaulting of missing state.
pub(crate) fn fields(value:&Value,count:usize)->Result<&[Value],String>{value.as_array().filter(|values|values.len()==count).map(Vec::as_slice).ok_or_else(||"Native checkpoint record shape invalid".into())}
/// Exact bounded integer.
pub(crate) fn number(value:&Value)->Result<i64,String>{value.as_i64().ok_or_else(||"Native checkpoint integer invalid".into())}
/// Exact bounded unsigned integer.
pub(crate) fn unsigned(value:&Value)->Result<u64,String>{value.as_u64().ok_or_else(||"Native checkpoint unsigned integer invalid".into())}
/// Original opaque generation/UUID representation, never truncated through JSON numbers.
pub(crate) fn wide(value:u128)->Value{Value::Binary(value.to_le_bytes().to_vec())}
/// Restores an original opaque generation/UUID.
pub(crate) fn read_wide(value:&Value)->Result<u128,String>{match value{Value::Binary(bytes) if bytes.len()==16=>Ok(u128::from_le_bytes(bytes.as_slice().try_into().unwrap())),_=>Err("Native checkpoint generation invalid".into())}}
pub(crate) fn boolean(value:&Value)->Result<bool,String>{value.as_bool().ok_or_else(||"Native checkpoint boolean invalid".into())}
pub(crate) fn string(value:&Value,maximum:usize)->Result<String,String>{value.as_str().filter(|text|text.len()<=maximum).map(str::to_owned).ok_or_else(||"Native checkpoint string invalid".into())}
pub(crate) fn lane(value:&Value)->Result<crate::dataset::transport::Lane,String>{match unsigned(value)?{1=>Ok(crate::dataset::transport::Lane::FuturesGlbx),2=>Ok(crate::dataset::transport::Lane::FuturesXcbf),3=>Ok(crate::dataset::transport::Lane::OptionsGlbx),_=>Err("Native checkpoint lane invalid".into())}}
pub(crate) fn date(value:&Value)->Result<time::Date,String>{let day=i32::try_from(number(value)?).map_err(|_|"Native checkpoint date overflow")?;time::Date::from_julian_day(day.checked_add(1_721_426).ok_or("Native checkpoint date overflow")?).map_err(|_|"Native checkpoint date invalid".into())}
pub(crate) fn day(value:time::Date)->Value{(value.to_julian_day()-1_721_426).into()}
pub(crate) fn header(value:&crate::dataset::records::Header)->Value{Value::Array(vec![value.instrument.into(),value.publisher.into(),value.flags.into(),value.event_ns.into(),value.received_ns.into(),value.sequence.into(),value.schema.into()])}
pub(crate) fn read_header(value:&Value)->Result<crate::dataset::records::Header,String>{let f=fields(value,7)?;Ok(crate::dataset::records::Header{instrument:unsigned(&f[0])?.try_into().map_err(|_|"Invalid instrument")?,publisher:unsigned(&f[1])?.try_into().map_err(|_|"Invalid publisher")?,flags:unsigned(&f[2])?.try_into().map_err(|_|"Invalid flags")?,event_ns:number(&f[3])?,received_ns:number(&f[4])?,sequence:unsigned(&f[5])?.try_into().map_err(|_|"Invalid sequence")?,schema:unsigned(&f[6])?.try_into().map_err(|_|"Invalid schema")?})}
pub(crate) fn quote(value:&crate::dataset::records::Quote)->Value{Value::Array(vec![header(&value.header),value.bid.into(),value.ask.into(),value.bid_size.into(),value.ask_size.into(),value.bid_count.into(),value.ask_count.into()])}
pub(crate) fn read_quote(value:&Value)->Result<crate::dataset::records::Quote,String>{let f=fields(value,7)?;Ok(crate::dataset::records::Quote{header:read_header(&f[0])?,bid:number(&f[1])?,ask:number(&f[2])?,bid_size:unsigned(&f[3])?.try_into().map_err(|_|"Invalid bid size")?,ask_size:unsigned(&f[4])?.try_into().map_err(|_|"Invalid ask size")?,bid_count:unsigned(&f[5])?.try_into().map_err(|_|"Invalid bid count")?,ask_count:unsigned(&f[6])?.try_into().map_err(|_|"Invalid ask count")?})}

#[cfg(test)]mod tests{
    use super::*;
    #[tokio::test(flavor="multi_thread",worker_threads=2)]
    async fn checkpoint_begin_seals_accepted_drain_and_refuses_an_active_unsealed_owner(){
        if std::env::var("IFM_CHECKPOINT_PRODUCTION_SEAL_CHILD").as_deref()!=Ok("1"){
            let output=std::process::Command::new(std::env::current_exe().unwrap())
                .args(["--exact","checkpoint::tests::checkpoint_begin_seals_accepted_drain_and_refuses_an_active_unsealed_owner","--nocapture"])
                .env("IFM_CHECKPOINT_PRODUCTION_SEAL_CHILD","1").env("IFM_SESSION_CAPTURE_BOUNDARY_ENABLED","1")
                .env("IFM_REALTIME_SOURCE_CAPTURE_ENABLED","1").output().unwrap();
            assert!(output.status.success(),"{}",String::from_utf8_lossy(&output.stderr));
            assert!(String::from_utf8_lossy(&output.stdout).contains("1 passed"));return;
        }
        struct Original;
        impl CheckpointOwner for Original{
            fn checkpoint(&self)->Result<Value,String>{Ok(Value::Array(vec![1.into()]))}
            fn restore(&self,_:&Value)->Result<(),String>{Ok(())}
        }
        let owner=register("production-seal-regression","test",Arc::new(Original)).unwrap();
        let token=tokio_util::sync::CancellationToken::new();
        assert_eq!(crate::messaging::source_production::pause_for_test(872,Duration::from_secs(1)),0);
        let active=crate::messaging::source_production::enter_accepted(&token).await.unwrap();assert!(active.is_some());
        assert_eq!(crate::messaging::source_forwarding::ifm_market_data_source_forwarding_hold_v1(872,1000),0);
        assert!(begin_scope("production-seal-regression",872).is_err());assert!(!frozen_by(872).unwrap());
        drop(active);begin_scope("production-seal-regression",872).unwrap();end_scope(872).unwrap();
        let waiting=tokio::spawn(async move{crate::messaging::source_production::enter_accepted(&token).await});
        tokio::time::sleep(Duration::from_millis(20)).await;assert!(!waiting.is_finished());
        assert_eq!(crate::messaging::source_production::resume_for_test(873),4);
        assert_eq!(crate::messaging::source_production::resume_for_test(872),0);drop(waiting.await.unwrap().unwrap());
        assert_eq!(crate::messaging::source_forwarding::ifm_market_data_source_forwarding_release_v1(872),0);drop(owner);
    }
    #[test]fn exact_binary_version_digest_and_wide_generations(){let value=Value::Array(vec![1.into(),wide(u128::MAX),Value::Binary(vec![0,255,1])]);let bytes=encode(&value).unwrap();assert_eq!(decode(&bytes).unwrap(),value);assert_eq!(read_wide(&value.as_array().unwrap()[1]).unwrap(),u128::MAX);for index in [0,8,16,48]{let mut corrupt=bytes.clone();corrupt[index]^=1;assert!(decode(&corrupt).is_err());}assert!(decode(&bytes[..bytes.len()-1]).is_err());}
    #[test]fn file_is_original_create_only_and_external_digest_is_required(){let root=std::env::temp_dir().join(format!("ifm-native-checkpoint-{:032x}",rand::random::<u128>()));std::fs::create_dir(&root).unwrap();let path=root.join("state.bin");let value=Value::Array(vec![1.into()]);let digest=save(&path,&value).unwrap();assert_eq!(load(&path,digest).unwrap(),value);assert!(save(&path,&value).is_err());assert!(load(&path,[0;32]).is_err());std::fs::remove_dir_all(root).unwrap();}
    #[test]fn state_boundary_drains_actual_mutations_and_is_not_thread_affine(){
        if std::env::var("IFM_CHECKPOINT_GATE_CHILD").as_deref()!=Ok("1"){
            let status=std::process::Command::new(std::env::current_exe().unwrap()).args(["--exact","checkpoint::tests::state_boundary_drains_actual_mutations_and_is_not_thread_affine","--nocapture"]).env("IFM_CHECKPOINT_GATE_CHILD","1").env("IFM_SESSION_CAPTURE_BOUNDARY_ENABLED","1").status().unwrap();assert!(status.success());return;
        }
        let active_transaction=transaction().unwrap();assert!(active_transaction.is_some());
        let holder=std::thread::spawn(||hold(771));
        for _ in 0..100{if TRANSACTIONS.state.lock().unwrap().1==Some(771){break;}std::thread::sleep(Duration::from_millis(1));}
        assert!(!holder.is_finished());drop(active_transaction);holder.join().unwrap().unwrap();assert!(frozen_by(771).unwrap());
        let resumed=Arc::new(std::sync::atomic::AtomicBool::new(false));let observed=resumed.clone();
        let worker=std::thread::spawn(move||{let _transaction=transaction().unwrap();observed.store(true,std::sync::atomic::Ordering::Release);});
        std::thread::sleep(Duration::from_millis(10));assert!(!resumed.load(std::sync::atomic::Ordering::Acquire));assert!(end_scope(772).is_err());end_scope(771).unwrap();worker.join().unwrap();assert!(resumed.load(std::sync::atomic::Ordering::Acquire));
    }
}
