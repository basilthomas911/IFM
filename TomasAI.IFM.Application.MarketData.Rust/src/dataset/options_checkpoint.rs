//! Optional exact options checkpoint owner. An in-flight asynchronous pass is never advertised as a quiet boundary.
use super::*;
use crate::checkpoint as c;
use rmpv::Value as V;
use std::sync::atomic::{AtomicBool, Ordering};
pub(super) struct RuntimeState {
    pub cache_version: i64,
    pub cache_dirty: bool,
    pub cadence: crate::option_chain::cadence::Cadence,
    pub cache_results: cache_delivery::Batch,
    pub versions: [u64; 4],
    pub cursor: usize,
    pub dirty: bool,
    pub threshold: u64,
    pub published: BTreeSet<u32>,
    pub qualified: Option<u64>,
}
impl RuntimeState {
    pub fn checkpoint(&self) -> Result<V, String> {
        Ok(V::Array(vec![
            1.into(),
            self.cache_version.into(),
            self.cache_dirty.into(),
            self.cadence.checkpoint(),
            V::Array(
                self.cache_results
                    .iter()
                    .map(|(scope, p)| Ok(V::Array(vec![scope.clone().into(), p.checkpoint()?])))
                    .collect::<Result<Vec<_>, String>>()?,
            ),
            (self.cursor as u64).into(),
            self.dirty.into(),
            self.threshold.into(),
            V::Array(self.published.iter().map(|v| (*v).into()).collect()),
            self.qualified.map_or(V::Nil, Into::into),
            V::Array(self.versions.iter().map(|v| (*v).into()).collect()),
        ]))
    }
    pub fn restore(value: &V) -> Result<Self, String> {
        let f = c::fields(value, 11)?;
        if c::unsigned(&f[0])? != 1 {
            return Err("Options runtime checkpoint version".into());
        }
        let mut cache_results = cache_delivery::Batch::new();
        for row in f[4]
            .as_array()
            .filter(|v| v.len() <= 64)
            .ok_or("Options cache count exceeded")?
        {
            let p = c::fields(row, 2)?;
            let scope = c::string(&p[0], 1024)?;
            let publication = CachePublication::restore_checkpoint(&p[1])?;
            if scope != publication.scope
                || cache_results.insert(scope, Arc::new(publication)).is_some()
            {
                return Err("Duplicate/mismatched options cache".into());
            }
        }
        let mut published = BTreeSet::new();
        for id in f[8]
            .as_array()
            .filter(|v| v.len() <= crate::option_chain::MAX_LIVE_OPTIONS)
            .ok_or("Options published membership exceeded")?
        {
            if !published.insert(
                c::unsigned(id)?
                    .try_into()
                    .map_err(|_| "Options member overflow")?,
            ) {
                return Err("Duplicate options member".into());
            }
        }
        crate::option_chain::cadence::Cadence::restore_checkpoint(&f[3])?;
        let version_fields = c::fields(&f[10], 4)?;
        let mut versions = [0; 4];
        for (i, v) in version_fields.iter().enumerate() {
            versions[i] = c::unsigned(v)?;
        }
        Ok(Self {
            versions,
            cache_version: c::number(&f[1])?,
            cache_dirty: c::boolean(&f[2])?,
            cadence: crate::option_chain::cadence::Cadence::restore_checkpoint(&f[3])?,
            cache_results,
            cursor: c::unsigned(&f[5])?
                .try_into()
                .map_err(|_| "Options cursor overflow")?,
            dirty: c::boolean(&f[6])?,
            threshold: c::unsigned(&f[7])?,
            published,
            qualified: if f[9].is_nil() {
                None
            } else {
                Some(c::unsigned(&f[9])?)
            },
        })
    }
}
pub(super) struct OptionsCheckpoint {
    pub clock: Arc<Clock>,
    pub data: Arc<LiveData>,
    pub engine: Arc<Coalesced>,
    pub mapper: Arc<Mutex<Options>>,
    pub ordered: Arc<Ordered>,
    pub outbox: Arc<Outbox>,
    pub configuration: Arc<Mutex<String>>,
    pub runtime: Arc<Mutex<RuntimeState>>,
    pub busy: Arc<AtomicBool>,
    pub statistics: Arc<Mutex<LogicalStatistics>>,
    pub retired: Arc<Mutex<crate::messaging::retired_evidence::Archive>>,
    pub recipe: Arc<Intent>,
}
impl c::CheckpointOwner for OptionsCheckpoint {
    fn open_original_source(&self)->Result<Arc<crate::messaging::source_outbox::Sink>,String>{
        crate::messaging::source_outbox::Sink::open(crate::messaging::source_outbox::location(&self.recipe.scope)?,self.recipe.broker.clone(),self.recipe.scope.clone())
    }

    fn prepare(&self) -> Result<(), String> {
        if self.busy.load(Ordering::Acquire) {
            return Err(
                "Original options pass remains in flight; settle it before checkpoint".into(),
            );
        }
        if crate::messaging::source_production::ifm_market_data_source_production_lease_v1() != 0
            && (self.outbox.retained_bytes().map_err(str::to_owned)? != 0
                || self.retired.lock().map_err(|_| "Retired evidence interrupted")?
                    .has_pending_receipts().map_err(str::to_owned)?)
        {
            return Err("Original option evidence receipts remain pending; drain accepted evidence before the held boundary".into());
        }
        if self.ordered.status().pending_since.is_some()
            && !self.ordered.checkpoint_publications_owned()?
        {
            return Err("Original option actor publications remain pending".into());
        }
        self.data.checkpoint_prepare_lane(Lane::OptionsGlbx, |_| {
            Err("Original option output remains pending; settle original owner then retry".into())
        })
    }
    fn checkpoint(&self) -> Result<V, String> {
        self.prepare()?;
        let statistics = self
            .statistics
            .lock()
            .map_err(|_| "Logical statistics interrupted")?;
        let bank = statistics
            .values
            .lock()
            .map_err(|_| "Statistics bank interrupted")?;
        let logical = V::Array(vec![
            statistics.date.map_or(V::Nil, c::day),
            V::Array(
                statistics
                    .identities
                    .iter()
                    .map(|(id, (publisher, contract))| {
                        V::Array(vec![
                            (*id).into(),
                            (*publisher).into(),
                            contract.clone().into(),
                        ])
                    })
                    .collect(),
            ),
            V::Array(
                bank.iter()
                    .map(|(id, value)| {
                        V::Array(vec![
                            (*id).into(),
                            super::super::book::statistics_checkpoint(value),
                        ])
                    })
                    .collect(),
            ),
        ]);
        drop(bank);
        drop(statistics);
        let runtime = self
            .runtime
            .lock()
            .map_err(|_| "Options runtime interrupted")?;
        let versions = [
            *self.data.updates().borrow(),
            *self.engine.updates().borrow(),
            *self.engine.reference_updates().borrow(),
            *self.clock.changes().borrow(),
        ];
        let mut runtime_image = runtime.checkpoint()?;
        if runtime.versions != versions {
            if let V::Array(fields) = &mut runtime_image {
                fields[2] = true.into();
                fields[6] = true.into();
            }
        }
        Ok(V::Array(vec![
            1.into(),
            self.ordered.checkpoint()?,
            self.data.checkpoint_lane(Lane::OptionsGlbx)?,
            self.engine.checkpoint()?,
            self.mapper
                .lock()
                .map_err(|_| "Option mapper interrupted")?
                .checkpoint(),
            self.outbox.checkpoint()?,
            runtime_image,
            self.configuration
                .lock()
                .map_err(|_| "Options configuration interrupted")?
                .clone()
                .into(),
            logical,
            self.retired
                .lock()
                .map_err(|_| "Retired evidence interrupted")?
                .checkpoint()?,
            recipe_checkpoint::capture(&self.recipe)?,
        ]))
    }
    fn restore(&self, value: &V) -> Result<(), String> {
        let f = c::fields(value, 11)?;
        if c::unsigned(&f[0])? != 1 {
            return Err("Options checkpoint version".into());
        }
        let recipe = recipe_checkpoint::restore(&f[10], &self.recipe)?;
        if recipe_checkpoint::capture(&recipe)? != recipe_checkpoint::capture(&self.recipe)? {
            return Err("Original option resolved recipe changed during import".into());
        }
        if !self.busy.load(Ordering::Acquire) {
            return Err("Options checkpoint import requires pre-activation owner".into());
        }
        let mapper = Options::restore_checkpoint(&f[4])?;
        let runtime = RuntimeState::restore(&f[6])?;
        let configuration = c::string(&f[7], 512)?;
        let retired = crate::messaging::retired_evidence::Archive::restore_checkpoint(&f[9])?;
        let l = c::fields(&f[8], 3)?;
        let mut identities = BTreeMap::new();
        for row in l[1]
            .as_array()
            .filter(|v| v.len() <= crate::option_chain::MAX_OPTION_REGISTRATIONS)
            .ok_or("Logical option identities exceeded")?
        {
            let p = c::fields(row, 3)?;
            let id = c::unsigned(&p[0])?
                .try_into()
                .map_err(|_| "Option identity overflow")?;
            let publisher = c::unsigned(&p[1])?
                .try_into()
                .map_err(|_| "Option publisher overflow")?;
            if identities
                .insert(id, (publisher, c::string(&p[2], 256)?))
                .is_some()
            {
                return Err("Duplicate logical option identity".into());
            }
        }
        let mut bank = BTreeMap::new();
        for row in l[2]
            .as_array()
            .filter(|v| v.len() <= identities.len())
            .ok_or("Logical statistics exceeded")?
        {
            let p = c::fields(row, 2)?;
            let id = c::unsigned(&p[0])?
                .try_into()
                .map_err(|_| "Option statistics identity overflow")?;
            if !identities.contains_key(&id)
                || bank
                    .insert(id, super::super::book::restore_statistics(&p[1])?)
                    .is_some()
            {
                return Err("Logical statistics identity mismatch".into());
            }
        }
        let date = if l[0].is_nil() {
            None
        } else {
            Some(c::date(&l[0])?)
        };
        // Validate all detached graphs before touching the actual pre-activation owner.
        Coalesced::from_checkpoint(&f[3])?;
        let scratch = Outbox::new(2048, 16 * 1024 * 1024).map_err(str::to_owned)?;
        scratch.restore_checkpoint(&f[5])?;
        self.engine.restore_checkpoint(&f[3])?;
        self.data.import_checkpoint_lane(Lane::OptionsGlbx, &f[2])?;
        self.ordered.restore_checkpoint(&f[1])?;
        self.outbox.restore_checkpoint(&f[5])?;
        *self
            .mapper
            .lock()
            .map_err(|_| "Options mapper interrupted")? = mapper;
        *self
            .runtime
            .lock()
            .map_err(|_| "Options runtime interrupted")? = runtime;
        *self
            .configuration
            .lock()
            .map_err(|_| "Options configuration interrupted")? = configuration;
        {
            let mut s = self
                .statistics
                .lock()
                .map_err(|_| "Logical statistics interrupted")?;
            *s.values
                .lock()
                .map_err(|_| "Logical statistics bank interrupted")? = bank;
            s.date = date;
            s.identities = identities;
        }
        *self
            .retired
            .lock()
            .map_err(|_| "Retired evidence interrupted")? = retired;
        Ok(())
    }
}

pub(super) fn original_header(value: &V) -> Result<Header, String> {
    let f = c::fields(value, 11)?;
    let h = c::fields(&f[1], 8)?;
    let guid = |v: &V| -> Result<[u8; 16], String> {
        v.as_slice()
            .filter(|v| v.len() == 16)
            .ok_or("Original options GUID invalid")?
            .try_into()
            .map_err(|_| "Original options GUID invalid".into())
    };
    Ok(Header {
        lane: c::lane(&h[1])?,
        value_date: c::number(&h[2])?
            .try_into()
            .map_err(|_| "Original options date overflow")?,
        worker: guid(&h[3])?,
        generation: guid(&h[4])?,
        revision: c::number(&h[5])?,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::checkpoint::CheckpointOwner;
    fn owner(recipe: Arc<Intent>, seed: bool) -> OptionsCheckpoint {
        let clock = Arc::new(Clock::new(17_000_000_000_000_000, false).unwrap());
        let generation = [17; 16];
        let bank = Arc::new(Mutex::new(BTreeMap::new()));
        let data = Arc::new(
            LiveData::new(
                recipe.date,
                u128::from_le_bytes(generation),
                crate::option_chain::MAX_OPTION_REGISTRATIONS,
                clock.clone(),
                Arc::new(crate::runtime::owner::Owner::default()),
            )
            .unwrap()
            .with_option_statistics(bank.clone())
            .unwrap(),
        );
        if seed {
            let mut book = super::super::super::book::Book::<crate::pricing::Values>::new(
                recipe.date,
                u128::from_le_bytes(generation),
                crate::option_chain::MAX_OPTION_REGISTRATIONS,
            )
            .unwrap();
            let reader = book
                .register(7, "ES20261218C5000", Asset::Option, 2)
                .unwrap();
            reader
                .update_quote(crate::latest::Tick {
                    sequence: 51,
                    event_ticks: 1234,
                    receive_ticks: 1235,
                    value: crate::latest::QuoteValue {
                        bid: Some(12_000_000_000),
                        ask: Some(13_000_000_000),
                        bid_size: 5,
                        ask_size: 6,
                        bid_count: 1,
                        ask_count: 2,
                        local_received_ticks: 1236,
                    },
                })
                .unwrap();
            book.bind_option_statistics(bank.clone()).unwrap();
            data.seed_checkpoint_book_for_test(Lane::OptionsGlbx, book, 1)
                .unwrap();
        }
        let engine = Arc::new(Coalesced::new(1, 1, 5000).unwrap());
        let strike = crate::option_chain::qualification::Decimal {
            bits: [5000, 0, 0, 0],
        };
        engine
            .register(
                7,
                17,
                crate::option_chain::coalesced::Context {
                    identity: 21,
                    underlying: 9,
                    expiration: 100_000,
                    days_in_year: 365,
                    valid_until: 90_000,
                    model: crate::pricing::Request {
                        forward: 0.,
                        strike: 5000.,
                        time: 0.,
                        rate: 0.04,
                        call: true,
                        american: false,
                        futures_style: false,
                    },
                    qualification: crate::option_chain::qualification::Request {
                        size: 304,
                        flags: 3 | 4 | 8 | 16,
                        generation_low: 1,
                        maximum_age: 5000,
                        maximum_skew: 2000,
                        strike,
                        definition_strike: strike,
                        ..Default::default()
                    },
                },
                true,
            )
            .unwrap();
        let mapper = Arc::new(Mutex::new(
            Options::new(
                recipe.date,
                recipe.definition,
                recipe.source.registrations.clone(),
                Arc::new(Revisions::default()),
            )
            .unwrap(),
        ));
        let ordered = Ordered::new(
            crate::messaging::publisher::Port::local_source_for_test(&recipe.scope, None),
            Header {
                lane: Lane::OptionsGlbx,
                value_date: recipe.date.to_julian_day() - 1721426,
                worker: [7; 16],
                generation,
                revision: recipe.revision,
            },
        )
        .unwrap();
        OptionsCheckpoint {
            clock,
            data,
            engine,
            mapper,
            ordered,
            outbox: Arc::new(Outbox::new(2048, 16 * 1024 * 1024).unwrap()),
            configuration: Arc::new(Mutex::new("original-configuration".into())),
            runtime: Arc::new(Mutex::new(RuntimeState {
                versions: [0; 4],
                cache_version: 0,
                cache_dirty: true,
                cadence: crate::option_chain::cadence::Cadence::new(),
                cache_results: cache_delivery::Batch::new(),
                cursor: 0,
                dirty: true,
                threshold: 0,
                published: BTreeSet::new(),
                qualified: None,
            })),
            busy: Arc::new(AtomicBool::new(false)),
            statistics: Arc::new(Mutex::new(LogicalStatistics {
                date: Some(recipe.date),
                identities: BTreeMap::from([(7, (1, "ES20261218C5000".into()))]),
                values: bank,
            })),
            retired: Arc::new(Mutex::new(
                crate::messaging::retired_evidence::Archive::default(),
            )),
            recipe,
        }
    }

    #[test]
    fn held_production_boundary_requires_active_and_retired_receipt_drain() {
        if std::env::var("IFM_OPTION_RECEIPT_PAUSE_CHILD").as_deref() != Ok("1") {
            let status=std::process::Command::new(std::env::current_exe().unwrap())
                .args(["--exact", "dataset::options_owner::checkpoint_state::tests::held_production_boundary_requires_active_and_retired_receipt_drain", "--nocapture"])
                .env("IFM_OPTION_RECEIPT_PAUSE_CHILD", "1").output().unwrap();
            assert!(status.status.success() && String::from_utf8_lossy(&status.stdout).contains("1 passed; 0 failed"),
                "{}\n{}",String::from_utf8_lossy(&status.stdout),String::from_utf8_lossy(&status.stderr)); return;
        }
        let source=owner(Arc::new(super::super::recipe_checkpoint::tests::original()),true);
        let seed=|outbox:&Outbox| {
            let mut image=outbox.checkpoint().unwrap();
            let V::Array(fields)=&mut image else {panic!("outbox image");};
            fields[3]=1.into();
            fields[4]=V::Array(vec![V::Array(vec!["ES20261218C5000".into(),739945.into(),V::Binary(vec![7;16]),
                "original-evidence".into(),"original-digest".into(),V::Binary(vec![1,2,3]),1.into(),0.into(),123.into(),true.into()])]);
            outbox.restore_checkpoint(&image).unwrap();
        };
        seed(&source.outbox);
        assert!(source.prepare().is_ok()); // Ordinary checkpoint retains exact pending evidence.
        assert_eq!(crate::messaging::source_production::pause_for_test(819,std::time::Duration::from_secs(1)),0);
        assert!(source.prepare().unwrap_err().contains("evidence receipts remain pending"));
        source.outbox.complete("original-evidence",1).unwrap();
        let retired=Arc::new(Outbox::new(4,4096).unwrap());seed(&retired);
        source.retired.lock().unwrap().retain(retired.clone()).unwrap();
        assert!(source.prepare().unwrap_err().contains("evidence receipts remain pending"));
        retired.complete("original-evidence",1).unwrap();
        assert!(source.prepare().is_ok());
        assert_eq!(crate::messaging::source_production::resume_for_test(819),0);
    }

    #[tokio::test]
    async fn replay_import_only_factory_publishes_original_queries_without_provider(){
        if std::env::var("IFM_NATIVE_OPTIONS_READONLY_CHILD").ok().as_deref()!=Some("1"){
            let root=std::env::temp_dir().join(format!("ifm-options-readonly-{:032x}",rand::random::<u128>()));std::fs::create_dir_all(root.join("isolated")).unwrap();
            let result=std::process::Command::new(std::env::current_exe().unwrap()).args(["--exact","dataset::options_owner::checkpoint_state::tests::replay_import_only_factory_publishes_original_queries_without_provider","--nocapture"]).env("IFM_NATIVE_OPTIONS_READONLY_CHILD","1").env("IFM_SESSION_CAPTURE_BOUNDARY_ENABLED","1").env("IFM_HISTORICAL_NATIVE_IMPORT_ONLY","1").env("IFM_REALTIME_SOURCE_CAPTURE_ENABLED","1").env("IFM_REALTIME_SOURCE_DIRECTORY",root.join("isolated").join("sources")).env("IFM_NATIVE_OPTIONS_READONLY_ROOT",&root).env_remove("IFM_NATIVE_COMPONENT_CHECKPOINT_FILE").output().unwrap();
            std::fs::remove_dir_all(&root).unwrap();assert!(result.status.success(),"{}\n{}",String::from_utf8_lossy(&result.stdout),String::from_utf8_lossy(&result.stderr));return;
        }
        let root=std::path::PathBuf::from(std::env::var_os("IFM_NATIVE_OPTIONS_READONLY_ROOT").unwrap());let recipe=Arc::new(super::super::recipe_checkpoint::tests::original());let original=owner(recipe.clone(),true);let image=original.checkpoint().unwrap();
        let original_sources=root.join("original");let sink=crate::messaging::source_outbox::Sink::open(original_sources.clone(),recipe.broker.clone(),recipe.scope.clone()).unwrap();let root_image=V::Array(vec![1.into(),recipe.scope.clone().into(),1.into(),V::Array(vec![V::Array(vec!["options-3".into(),image])]),sink.checkpoint_floor().unwrap()]);let file=root.join("original.checkpoint");let sha=c::save(&file,&root_image).unwrap();drop(sink);c::import_scope(&recipe.scope,&file,sha).unwrap();c::configure_restore_root(&recipe.scope,&root.join("isolated")).unwrap();
        let target=crate::messaging::source_outbox::location(&recipe.scope).unwrap();std::fs::create_dir_all(&target).unwrap();for entry in std::fs::read_dir(&original_sources).unwrap(){let entry=entry.unwrap();if entry.file_name()!="owner.lock"{std::fs::copy(entry.path(),target.join(entry.file_name())).unwrap();}}
        let clock=Arc::new(Clock::new(17_000_000_000_000_000,false).unwrap());let factory=super::super::OptionsFactory::new(clock);let root_owner=Arc::new(crate::runtime::owner::Owner::default());let parent=Arc::new(root_owner.child().unwrap());let mut resources=crate::dataset::supervisor::Factory::start(&factory,recipe,parent.clone(),Deadline::after(std::time::Duration::from_secs(2))).unwrap();
        let view=tokio::time::timeout(std::time::Duration::from_secs(2),async{loop{if let Some(view)=factory.view(){break view;}if let Some(error)=resources.changes().borrow().failure.clone(){panic!("{error}");}tokio::task::yield_now().await;}}).await.expect("Read-only original public view must not wait for provider connectivity");
        let raw=view.queries.raw_prices(7).unwrap().unwrap().quote.unwrap();assert_eq!(raw.sequence,51);assert_eq!(raw.event_ticks,1234);assert_eq!(raw.value.bid,Some(12_000_000_000));assert_eq!(view.queries.counters().unwrap()["Provider session attempts"],0);assert!(parent.provider_state().borrow().is_none());assert!(!resources.changes().borrow().setup_complete);assert!(resources.changes().borrow().proofs.is_empty());let before=view.publication.checkpoint().unwrap();assert!(view.publication.submit(5,vec![0xc0],[17;16],None).is_err());assert_eq!(before,view.publication.checkpoint().unwrap());
        resources.cancel(Deadline::after(std::time::Duration::from_secs(1)));resources.join(Deadline::after(std::time::Duration::from_secs(1))).await.unwrap();assert!(view.queries.raw_prices(7).unwrap().is_none());assert!(view.queries.generation().unwrap().is_none());
    }

    #[test]
    fn checkpoint_actual_options_owner_imports_original_book_before_any_provider_start() {
        let recipe = Arc::new(super::super::recipe_checkpoint::tests::original());
        let source = owner(recipe.clone(), true);
        let image = source.checkpoint().unwrap();
        validate_image(&image).unwrap();
        let mut malformed = image.clone();
        if let V::Array(fields) = &mut malformed {
            if let V::Array(recipe) = &mut fields[10] {
                recipe[5] = V::Array(Vec::new());
            }
        }
        assert!(validate_image(&malformed).is_err());
        if let Some(path)=std::env::var_os("IFM_NATIVE_COMPONENT_CHECKPOINT_FILE"){
            let path=std::path::PathBuf::from(path);if let Some(parent)=path.parent(){std::fs::create_dir_all(parent).unwrap();}
            let original_root=path.with_extension("original-sources");std::fs::create_dir_all(&original_root).unwrap();
            let sink=crate::messaging::source_outbox::Sink::open(original_root.clone(),source.recipe.broker.clone(),source.recipe.scope.clone()).unwrap();
            let root_image=V::Array(vec![1.into(),source.recipe.scope.clone().into(),1.into(),V::Array(vec![V::Array(vec!["options-3".into(),image.clone()])]),sink.checkpoint_floor().unwrap()]);
            let sha=crate::checkpoint::save(&path,&root_image).unwrap();let sha=sha.iter().map(|v|format!("{v:02x}")).collect::<String>();
            let files=std::fs::read_dir(&original_root).unwrap().map(|entry|{let entry=entry.unwrap();serde_json::json!({"RelativePath":entry.file_name().to_string_lossy(),"Length":entry.metadata().unwrap().len()})}).collect::<Vec<_>>();
            std::fs::write(path.with_extension("fixture.json"),serde_json::to_vec_pretty(&serde_json::json!({"Kind":"SyntheticPrivateOwnerFixture","Scope":source.recipe.scope,"CheckpointSha256":sha,"OriginalSourceRoot":original_root.canonicalize().unwrap(),"Files":files,"Qualified":false,"ProviderStarted":false})).unwrap()).unwrap();
        }
        let bytes = crate::checkpoint::encode(&image).unwrap();
        let restored = owner(recipe, false);
        restored.busy.store(true, Ordering::Release);
        restored
            .restore(&crate::checkpoint::decode(&bytes).unwrap())
            .unwrap();
        let queries = restored
            .data
            .queries(Lane::OptionsGlbx)
            .unwrap()
            .expect("Original restored book must be queryable without starting a provider");
        assert_eq!(
            queries.generation().unwrap(),
            Some(u128::from_le_bytes([17; 16]))
        );
        let quote = queries
            .raw_prices(7)
            .unwrap()
            .expect("Original raw book is preserved")
            .quote
            .expect("Original quote is preserved");
        assert_eq!(quote.sequence, 51);
        assert_eq!(quote.value.bid, Some(12_000_000_000));
        assert_eq!(quote.event_ticks, 1234);
        assert!(
            restored
                .data
                .lifecycle(Lane::OptionsGlbx)
                .unwrap()
                .is_none()
        );
        assert_eq!(
            source.engine.checkpoint().unwrap(),
            restored.engine.checkpoint().unwrap()
        );
        assert_eq!(
            source.mapper.lock().unwrap().checkpoint(),
            restored.mapper.lock().unwrap().checkpoint()
        );
        assert_eq!(
            source.ordered.checkpoint().unwrap(),
            restored.ordered.checkpoint().unwrap()
        );
        restored.busy.store(false, Ordering::Release);
        assert!(restored.restore(&image).is_err());
        source.busy.store(true, Ordering::Release);
        assert!(source.prepare().is_err());
    }
}

pub(super) fn validate_image(value: &V) -> Result<(), String> {
    let f = c::fields(value, 11)?;
    if c::unsigned(&f[0])? != 1 {
        return Err("Options checkpoint version".into());
    }
    let header = original_header(value)?;
    if header.lane != Lane::OptionsGlbx
        || header.revision <= 0
        || header.worker == [0; 16]
        || header.generation == [0; 16]
    {
        return Err("Original option header invalid".into());
    }
    let h = c::fields(&f[1], 8)?;
    let recipe_identity=c::fields(&f[10],21)?;
    if c::unsigned(&h[0])?!=1||c::date(&recipe_identity[3])?.to_julian_day()-1721426!=header.value_date||c::number(&recipe_identity[2])?!=header.revision{return Err("Original option recipe/header identity mismatch".into());}
    if c::number(&h[6])? < 0 {
        return Err("Original option publication cursor invalid".into());
    }
    let current_fields = c::fields(&h[7], 4)?;
    let current = Current::new(
        c::unsigned(&current_fields[1])?
            .try_into()
            .map_err(|_| "Current capacity overflow")?,
        c::unsigned(&current_fields[2])?
            .try_into()
            .map_err(|_| "Current byte capacity overflow")?,
    )
    .map_err(str::to_owned)?;
    current.import_checkpoint(&h[7])?;
    let native = c::fields(&f[2], 14)?;
    if c::unsigned(&native[0])? != 1
        || c::lane(&native[4])? != Lane::OptionsGlbx
        || c::date(&native[1])?.to_julian_day() - 1721426 != header.value_date
        || c::read_wide(&native[2])? != u128::from_le_bytes(header.generation)
    {
        return Err("Original native options owner does not match header".into());
    }
    let book = super::super::book::Book::<crate::pricing::Values>::restore_checkpoint(
        &native[10],
        CancellationToken::new(),
    )?;
    if book.generation() != c::read_wide(&native[6])? {
        return Err("Original option book generation mismatch".into());
    }
    c::unsigned(&native[3])?;
    c::unsigned(&native[5])?;
    c::boolean(&native[7])?;
    c::boolean(&native[8])?;
    c::boolean(&native[9])?;
    c::unsigned(&native[11])?;
    let clock_fields = c::fields(&native[12], 4)?;
    if c::unsigned(&clock_fields[0])? != 1 {
        return Err("Original options clock version invalid".into());
    }
    let clock = Arc::new(
        Clock::new(c::number(&clock_fields[1])?, false)
            .map_err(|_| "Original option clock invalid")?,
    );
    c::boolean(&clock_fields[2])?;
    c::unsigned(&clock_fields[3])?;
    Coalesced::from_checkpoint(&f[3])?;
    Options::restore_checkpoint(&f[4])?;
    let outbox = Outbox::new(2048, 16 * 1024 * 1024).map_err(str::to_owned)?;
    outbox.restore_checkpoint(&f[5])?;
    RuntimeState::restore(&f[6])?;
    c::string(&f[7], 512)?;
    crate::messaging::retired_evidence::Archive::restore_checkpoint(&f[9])?;
    recipe_checkpoint::validate(&f[10], clock)?;
    let logical = c::fields(&f[8], 3)?;
    if !logical[0].is_nil() {
        c::date(&logical[0])?;
    }
    let mut ids = BTreeSet::new();
    for row in logical[1]
        .as_array()
        .filter(|v| v.len() <= crate::option_chain::MAX_OPTION_REGISTRATIONS)
        .ok_or("Logical option membership exceeded")?
    {
        let p = c::fields(row, 3)?;
        let id = c::unsigned(&p[0])?;
        if id == 0 || !ids.insert(id) || c::unsigned(&p[1])? > u16::MAX as u64 {
            return Err("Logical option identity invalid".into());
        }
        c::string(&p[2], 256)?;
    }
    let mut statistics = BTreeSet::new();
    for row in logical[2]
        .as_array()
        .filter(|v| v.len() <= ids.len())
        .ok_or("Logical statistics exceeded")?
    {
        let p = c::fields(row, 2)?;
        let id = c::unsigned(&p[0])?;
        if !ids.contains(&id) || !statistics.insert(id) {
            return Err("Logical statistic identity invalid".into());
        }
        super::super::book::restore_statistics(&p[1])?;
    }
    Ok(())
}
