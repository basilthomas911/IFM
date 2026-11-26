//! Exact resolved subscription/reference recipe; credentials and resource handles are deliberately not serialized.
use super::*;
use crate::checkpoint as c;
use rmpv::Value as V;
fn decimal(v: &crate::option_chain::qualification::Decimal) -> V {
    V::Array(v.bits.iter().map(|v| (*v).into()).collect())
}
fn read_decimal(v: &V) -> Result<crate::option_chain::qualification::Decimal, String> {
    let f = c::fields(v, 4)?;
    let mut bits = [0; 4];
    for (i, v) in f.iter().enumerate() {
        bits[i] = c::unsigned(v)?
            .try_into()
            .map_err(|_| "Recipe decimal overflow")?;
    }
    Ok(crate::option_chain::qualification::Decimal { bits })
}
pub(super) fn capture(intent: &Intent) -> Result<V, String> {
    let reviewed = &intent.reviewed;
    let subscriptions = intent
        .source
        .plan
        .subscriptions
        .iter()
        .map(|v| {
            V::Array(vec![
                V::Array(v.symbols.iter().cloned().map(Into::into).collect()),
                v.symbology.to_string().into(),
                v.schema.to_string().into(),
                v.replay_start_ns.map_or(V::Nil, Into::into),
            ])
        })
        .collect();
    let registrations = intent
        .source
        .registrations
        .iter()
        .map(|r| {
            V::Array(vec![
                r.instrument.into(),
                r.publisher.into(),
                r.contract.clone().into(),
                r.source.clone().into(),
                (if r.asset == Asset::Futures { 1 } else { 2 }).into(),
                r.tick.map_or(V::Nil, Into::into),
                r.vwap.into(),
                r.required.into(),
            ])
        })
        .collect();
    let definitions = reviewed
        .definitions
        .iter()
        .map(|d| {
            let b = &d.blueprint;
            let x = &b.conversion;
            V::Array(vec![
                d.instrument.into(),
                d.underlying.into(),
                decimal(&d.strike),
                d.call.into(),
                d.required.into(),
                b.contract.value(),
                b.calendar.value(),
                b.publications.checkpoint(),
                V::Array(vec![
                    x.source.clone().into(),
                    x.series.clone().into(),
                    x.convention.into(),
                    x.version.clone().into(),
                    x.evidence.clone().into(),
                ]),
                b.maximum_lead.into(),
            ])
        })
        .collect();
    let statistics = reviewed
        .statistics
        .iter()
        .map(|(id, s)| {
            V::Array(vec![
                (*id).into(),
                s.volume.map_or(V::Nil, Into::into),
                s.volume_date.map_or(V::Nil, c::day),
                s.open_interest.map_or(V::Nil, Into::into),
                s.open_interest_date.map_or(V::Nil, c::day),
            ])
        })
        .collect();
    let caches = intent
        .caches
        .iter()
        .map(|v| {
            V::Array(vec![
                v.scope.clone().into(),
                v.horizon.clone().into(),
                V::Array(v.contracts.iter().cloned().map(Into::into).collect()),
                v.ranking.as_ref().map_or(V::Nil, |r| {
                    V::Array(vec![
                        r.parameters.value(),
                        r.direction.into(),
                        c::day(r.date),
                    ])
                }),
            ])
        })
        .collect();
    Ok(V::Array(vec![
        1.into(),
        intent.scope.clone().into(),
        intent.revision.into(),
        c::day(intent.date),
        c::day(intent.definition),
        V::Array(subscriptions),
        V::Array(registrations),
        (match intent.source.evidence {
            Evidence::ControlOnly => 0,
            Evidence::ControlOnlyWithOptionalQuoteReplay => 1,
            Evidence::Observations => 2,
        })
        .into(),
        V::Array(definitions),
        reviewed.production.into(),
        reviewed.production_approved.into(),
        reviewed.review_evidence.clone().map_or(V::Nil, Into::into),
        reviewed.policy_version.clone().into(),
        reviewed.selection_ms.into(),
        reviewed.iv_ms.into(),
        reviewed.risk_ms.into(),
        (reviewed.maximum_contracts as u64).into(),
        (reviewed.batch as u64).into(),
        V::Array(statistics),
        V::Array(intent.monitored.iter().map(|v| (*v).into()).collect()),
        V::Array(caches),
    ]))
}
pub(super) fn restore(value: &V, configured: &Intent) -> Result<Arc<Intent>, String> {
    let f = c::fields(value, 21)?;
    if c::unsigned(&f[0])? != 1
        || c::string(&f[1], 1024)? != configured.scope
        || c::number(&f[2])? != configured.revision
        || c::date(&f[3])? != configured.date
    {
        return Err("Original resolved option recipe does not match scope/date/revision".into());
    }
    let policy = &configured.reviewed;
    if c::string(&f[12], 1024)? != policy.policy_version
        || c::unsigned(&f[13])? != u64::from(policy.selection_ms)
        || c::unsigned(&f[14])? != u64::from(policy.iv_ms)
        || c::unsigned(&f[15])? != u64::from(policy.risk_ms)
        || c::unsigned(&f[16])? != policy.maximum_contracts as u64
        || c::unsigned(&f[17])? != policy.batch as u64
        || c::boolean(&f[9])? != policy.production
        || c::boolean(&f[10])? != policy.production_approved
    {
        return Err("Original option review/cadence policy differs from configured owner".into());
    }
    let list = |index: usize, max: usize| -> Result<&Vec<V>, String> {
        f[index]
            .as_array()
            .filter(|v| v.len() <= max)
            .ok_or_else(|| "Option recipe list exceeded bound".into())
    };
    let subscriptions = list(5, 64)?
        .iter()
        .map(|v| {
            let p = c::fields(v, 4)?;
            let symbols = p[0]
                .as_array()
                .filter(|v| v.len() <= 65536)
                .ok_or("Recipe symbols exceeded")?
                .iter()
                .map(|v| c::string(v, 256))
                .collect::<Result<Vec<_>, _>>()?;
            Ok(super::super::transport::SubscriptionSpec {
                symbols,
                symbology: c::string(&p[1], 128)?
                    .parse()
                    .map_err(|_| "Recipe symbology invalid")?,
                schema: c::string(&p[2], 128)?
                    .parse()
                    .map_err(|_| "Recipe schema invalid")?,
                replay_start_ns: if p[3].is_nil() {
                    None
                } else {
                    Some(c::number(&p[3])?)
                },
            })
        })
        .collect::<Result<Vec<_>, String>>()?;
    let registrations = list(6, crate::option_chain::MAX_OPTION_REGISTRATIONS)?
        .iter()
        .map(|v| {
            let p = c::fields(v, 8)?;
            Ok(Registration {
                instrument: c::unsigned(&p[0])?
                    .try_into()
                    .map_err(|_| "Recipe instrument overflow")?,
                publisher: c::unsigned(&p[1])?
                    .try_into()
                    .map_err(|_| "Recipe publisher overflow")?,
                contract: c::string(&p[2], 256)?,
                source: c::string(&p[3], 256)?,
                asset: match c::unsigned(&p[4])? {
                    1 => Asset::Futures,
                    2 => Asset::Option,
                    _ => return Err("Recipe asset invalid".into()),
                },
                tick: if p[5].is_nil() {
                    None
                } else {
                    Some(c::number(&p[5])?)
                },
                vwap: c::boolean(&p[6])?,
                required: c::boolean(&p[7])?,
            })
        })
        .collect::<Result<Vec<_>, String>>()?;
    let definitions = list(8, crate::option_chain::MAX_LIVE_OPTIONS)?
        .iter()
        .map(|v| {
            let p = c::fields(v, 10)?;
            let x = c::fields(&p[8], 5)?;
            Ok(crate::option_chain::preparation::Definition {
                instrument: c::unsigned(&p[0])?
                    .try_into()
                    .map_err(|_| "Definition instrument overflow")?,
                underlying: c::unsigned(&p[1])?
                    .try_into()
                    .map_err(|_| "Definition underlying overflow")?,
                strike: read_decimal(&p[2])?,
                call: c::boolean(&p[3])?,
                required: c::boolean(&p[4])?,
                blueprint: crate::option_chain::preparation::Blueprint {
                    contract: crate::option_chain::convention::Convention::new(p[5].clone())
                        .map_err(str::to_owned)?,
                    calendar: crate::reference::calendar::Calendar::restore_checkpoint(&p[6])?,
                    publications:
                        crate::reference::treasury_policy::Publications::restore_checkpoint(&p[7])?,
                    conversion: crate::reference::treasury_policy::Conversion {
                        source: c::string(&x[0], 1024)?,
                        series: c::string(&x[1], 1024)?,
                        convention: c::number(&x[2])?
                            .try_into()
                            .map_err(|_| "Recipe convention overflow")?,
                        version: c::string(&x[3], 1024)?,
                        evidence: c::string(&x[4], 4096)?,
                    },
                    maximum_lead: c::number(&p[9])?
                        .try_into()
                        .map_err(|_| "Recipe lead overflow")?,
                },
            })
        })
        .collect::<Result<Vec<_>, String>>()?;
    let n = |v: &V| {
        if v.is_nil() {
            Ok(None)
        } else {
            c::number(v).map(Some)
        }
    };
    let d = |v: &V| {
        if v.is_nil() {
            Ok(None)
        } else {
            c::date(v).map(Some)
        }
    };
    let statistics = list(18, crate::option_chain::MAX_LIVE_OPTIONS)?
        .iter()
        .map(|v| {
            let p = c::fields(v, 5)?;
            Ok((
                c::unsigned(&p[0])?
                    .try_into()
                    .map_err(|_| "Recipe statistic instrument overflow")?,
                crate::option_chain::statistics::Seed {
                    volume: n(&p[1])?,
                    volume_date: d(&p[2])?,
                    open_interest: n(&p[3])?,
                    open_interest_date: d(&p[4])?,
                },
            ))
        })
        .collect::<Result<Vec<_>, String>>()?;
    let mut monitored = BTreeSet::new();
    for v in list(19, crate::option_chain::MAX_LIVE_OPTIONS)? {
        if !monitored.insert(
            c::unsigned(v)?
                .try_into()
                .map_err(|_| "Recipe monitored instrument overflow")?,
        ) {
            return Err("Duplicate recipe monitored instrument".into());
        }
    }
    let caches = list(20, 64)?
        .iter()
        .map(|v| {
            let p = c::fields(v, 4)?;
            let contracts = p[2]
                .as_array()
                .filter(|v| v.len() <= crate::option_chain::MAX_LIVE_OPTIONS)
                .ok_or("Recipe cache membership exceeded")?
                .iter()
                .map(|v| c::string(v, 256))
                .collect::<Result<BTreeSet<_>, _>>()?;
            let ranking = if p[3].is_nil() {
                None
            } else {
                let r = c::fields(&p[3], 3)?;
                Some(crate::option_chain::strategy::Ranking {
                    parameters: Arc::new(
                        crate::option_chain::strategy::Parameters::new(r[0].clone())
                            .map_err(str::to_owned)?,
                    ),
                    direction: c::unsigned(&r[1])?
                        .try_into()
                        .map_err(|_| "Ranking direction overflow")?,
                    date: c::date(&r[2])?,
                })
            };
            Ok(CacheScope {
                scope: c::string(&p[0], 1024)?,
                horizon: c::string(&p[1], 128)?,
                contracts,
                ranking,
            })
        })
        .collect::<Result<Vec<_>, String>>()?;
    let reviewed = Arc::new(ReviewedScope {
        manifests: configured.reviewed.manifests.clone(),
        references: configured.reviewed.references.clone(),
        definitions,
        production: c::boolean(&f[9])?,
        production_approved: c::boolean(&f[10])?,
        review_evidence: if f[11].is_nil() {
            None
        } else {
            Some(c::string(&f[11], 4096)?)
        },
        policy_version: c::string(&f[12], 1024)?,
        selection_ms: c::unsigned(&f[13])?
            .try_into()
            .map_err(|_| "Selection cadence overflow")?,
        iv_ms: c::unsigned(&f[14])?
            .try_into()
            .map_err(|_| "IV cadence overflow")?,
        risk_ms: c::unsigned(&f[15])?
            .try_into()
            .map_err(|_| "Risk cadence overflow")?,
        maximum_contracts: c::unsigned(&f[16])?
            .try_into()
            .map_err(|_| "Recipe capacity overflow")?,
        batch: c::unsigned(&f[17])?
            .try_into()
            .map_err(|_| "Recipe batch overflow")?,
        statistics,
    });
    let source = Start {
        plan: SessionPlan {
            lane: Lane::OptionsGlbx,
            subscriptions,
        },
        registrations,
        key: configured.source.key.clone(),
        gateway: configured.source.gateway.clone(),
        evidence: match c::unsigned(&f[7])? {
            0 => Evidence::ControlOnly,
            1 => Evidence::ControlOnlyWithOptionalQuoteReplay,
            2 => Evidence::Observations,
            _ => return Err("Recipe evidence policy invalid".into()),
        },
    };
    source.validate(crate::option_chain::MAX_OPTION_REGISTRATIONS)?;
    Ok(Arc::new(Intent {
        background: None,
        reference_bootstrap: None,
        date: c::date(&f[3])?,
        definition: c::date(&f[4])?,
        source,
        reviewed,
        monitored,
        caches,
        broker: configured.broker.clone(),
        scope: configured.scope.clone(),
        revision: configured.revision,
    }))
}

#[cfg(test)]
pub(super) mod tests {
    use super::*;
    pub(crate) fn original() -> Intent {
        let date = time::Date::from_calendar_date(2026, time::Month::October, 1).unwrap();
        let clock = Arc::new(Clock::new(17_000_000_000_000_000, false).unwrap());
        let resolver =
            Arc::new(crate::dns::Resolver::new(vec!["127.0.0.1:53".parse().unwrap()]).unwrap());
        let treasury = Arc::new(
            crate::reference::treasury_curve::Treasury::new(
                "https://example.invalid",
                resolver.clone(),
                clock.clone(),
            )
            .unwrap(),
        );
        let at=17_000_000_000_000_000i64;let mut convention=vec![V::Nil;29];for index in [0,3,4,10,11,14,24,25,26,28]{convention[index]=0.into();}convention[0]=3.into();convention[3]=1.into();convention[4]=7.into();convention[28]=1.into();for(index,text)in [(1,"ES20261218C5000"),(2,"ES20261218"),(5,"ES"),(6,"GLBX.MDP3"),(7,"CME"),(8,"USD"),(9,"European"),(15,"test-calendar/v1"),(16,"50"),(17,"0.25"),(18,"tick-policy/v1"),(19,"native-fixture"),(20,"verified"),(21,"pricing/v1"),(27,"5000")]{convention[index]=text.into();}for index in [12,13,22,23]{convention[index]=crate::messaging::codec::utc_offset(at+30*864_000_000_000).unwrap();}
        let definition=crate::option_chain::preparation::Definition{instrument:7,underlying:9,strike:crate::option_chain::qualification::Decimal{bits:[5000,0,0,0]},call:true,required:true,blueprint:crate::option_chain::preparation::Blueprint{contract:crate::option_chain::convention::Convention::new(V::Array(convention)).unwrap(),calendar:crate::reference::calendar::Calendar::new("test-calendar/v1".into(),"America/New_York",date,date,0,vec![date]).unwrap(),publications:crate::reference::treasury_policy::Publications::new("treasury-schedule/v1".into(),at-1,at+864_000_000_000,vec![crate::reference::treasury_policy::Publication{date,available:at-1}],"America/New_York").unwrap(),conversion:crate::reference::treasury_policy::Conversion::standard(),maximum_lead:0}};
        let reviewed = Arc::new(ReviewedScope {
            manifests: crate::option_chain::manifest::Budget::new(4096).unwrap(),
            references: Arc::new(crate::option_chain::preparation::References::new(
                treasury, clock,
            )),
            definitions: vec![definition],
            production: false,
            production_approved: false,
            review_evidence: Some("original-reviewed-policy".into()),
            policy_version: "test-policy/v1".into(),
            selection_ms: 100,
            iv_ms: 5000,
            risk_ms: 100,
            maximum_contracts: 4,
            batch: 1,
            statistics: vec![(
                7,
                crate::option_chain::statistics::Seed {
                    volume: Some(99),
                    volume_date: Some(date),
                    open_interest: Some(101),
                    open_interest_date: Some(date),
                },
            )],
        });
        Intent {
            background: None,
            reference_bootstrap: None,
            date,
            definition: date,
            source: Start {
                plan: SessionPlan {
                    lane: Lane::OptionsGlbx,
                    subscriptions: vec![super::super::super::transport::SubscriptionSpec {
                        symbols: vec!["7".into()],
                        symbology: databento::dbn::SType::InstrumentId,
                        schema: databento::dbn::Schema::Mbp1,
                        replay_start_ns: Some(123456789),
                    }],
                },
                registrations: vec![Registration {
                    instrument: 7,
                    publisher: 1,
                    contract: "ES20261218C5000".into(),
                    source: "7".into(),
                    asset: Asset::Option,
                    tick: Some(250_000_000),
                    vwap: false,
                    required: true,
                }],
                key: "NEVER-COPY-PROVIDER-CREDENTIAL".into(),
                gateway: super::super::super::lanes::Gateway {
                    host: "127.0.0.1".into(),
                    port: 1,
                    resolver,
                },
                evidence: Evidence::ControlOnly,
            },
            reviewed,
            monitored: BTreeSet::from([7]),
            caches: Vec::new(),
            broker: crate::messaging::publisher::BrokerSettings::new(
                "nats://127.0.0.1:1".into(),
                vec!["127.0.0.1:53".parse().unwrap()],
            )
            .unwrap(),
            scope: "isolated-test".into(),
            revision: 3,
        }
    }
    #[test]
    fn resolved_recipe_restores_exact_sources_without_provider_or_reference_acquisition() {
        let original = original();
        let value = capture(&original).unwrap();
        let encoded = crate::checkpoint::encode(&value).unwrap();
        assert!(
            !encoded
                .windows(original.source.key.len())
                .any(|v| v == original.source.key.as_bytes())
        );
        let cold = restore(&value, &original).unwrap();
        assert_eq!(capture(&cold).unwrap(), value);
        assert!(cold.background.is_none());
        assert!(cold.reference_bootstrap.is_none());
        assert_eq!(cold.source.plan, original.source.plan);
        assert!(cold.source.registrations == original.source.registrations);
        assert_eq!(cold.source.key, original.source.key);
        assert_eq!(cold.reviewed.statistics[0].1.volume, Some(99));
    }
    #[test]
    fn foreign_or_changed_review_policy_cannot_accept_original_recipe() {
        let original = original();
        let value = capture(&original).unwrap();
        let mut changed = original.clone();
        changed.revision += 1;
        assert!(restore(&value, &changed).is_err());
        let mut reviewed = (*original.reviewed).clone();
        reviewed.selection_ms += 1;
        changed = original.clone();
        changed.reviewed = Arc::new(reviewed);
        assert!(restore(&value, &changed).is_err());
    }
}

/// Runs the same complete typed recipe parser before staging. Resource constructors do no I/O;
/// these detached validation handles are never installed as a provider or as financial state.
pub(super) fn validate(value: &V, clock: Arc<Clock>) -> Result<(), String> {
    let f = c::fields(value, 21)?;
    let resolver = Arc::new(
        crate::dns::Resolver::new(vec![
            "127.0.0.1:53"
                .parse()
                .map_err(|_| "Validation endpoint invalid")?,
        ])
        .map_err(|_| "Validation resolver invalid")?,
    );
    let treasury = Arc::new(
        crate::reference::treasury_curve::Treasury::new(
            "https://127.0.0.1",
            resolver.clone(),
            clock.clone(),
        )
        .map_err(|_| "Validation reference constructor invalid")?,
    );
    let reviewed = Arc::new(ReviewedScope {
        manifests: crate::option_chain::manifest::Budget::new(16 * 1024 * 1024)
            .map_err(str::to_owned)?,
        references: Arc::new(crate::option_chain::preparation::References::new(
            treasury, clock,
        )),
        definitions: Vec::new(),
        production: c::boolean(&f[9])?,
        production_approved: c::boolean(&f[10])?,
        review_evidence: None,
        policy_version: c::string(&f[12], 1024)?,
        selection_ms: c::unsigned(&f[13])?
            .try_into()
            .map_err(|_| "Selection cadence overflow")?,
        iv_ms: c::unsigned(&f[14])?
            .try_into()
            .map_err(|_| "IV cadence overflow")?,
        risk_ms: c::unsigned(&f[15])?
            .try_into()
            .map_err(|_| "Risk cadence overflow")?,
        maximum_contracts: c::unsigned(&f[16])?
            .try_into()
            .map_err(|_| "Maximum contract overflow")?,
        batch: c::unsigned(&f[17])?
            .try_into()
            .map_err(|_| "Batch overflow")?,
        statistics: Vec::new(),
    });
    if !(10..=60000).contains(&reviewed.selection_ms)
        || !(10..=60000).contains(&reviewed.iv_ms)
        || !(10..=60000).contains(&reviewed.risk_ms)
        || !(1..=64).contains(&reviewed.batch)
        || reviewed.maximum_contracts > crate::option_chain::MAX_LIVE_OPTIONS
    {
        return Err("Captured option scheduling policy invalid".into());
    }
    let configured = Intent {
        background: None,
        reference_bootstrap: None,
        date: c::date(&f[3])?,
        definition: c::date(&f[4])?,
        source: Start {
            plan: SessionPlan {
                lane: Lane::OptionsGlbx,
                subscriptions: Vec::new(),
            },
            registrations: Vec::new(),
            key: String::new(),
            gateway: super::super::lanes::Gateway {
                host: "127.0.0.1".into(),
                port: 1,
                resolver,
            },
            evidence: Evidence::ControlOnly,
        },
        reviewed,
        monitored: BTreeSet::new(),
        caches: Vec::new(),
        broker: crate::messaging::publisher::BrokerSettings::new(
            "nats://127.0.0.1:1".into(),
            vec![
                "127.0.0.1:53"
                    .parse()
                    .map_err(|_| "Validation broker endpoint invalid")?,
            ],
        )
        .map_err(str::to_owned)?,
        scope: c::string(&f[1], 1024)?,
        revision: c::number(&f[2])?,
    };
    let parsed = restore(value, &configured)?;
    if parsed.scope.trim().is_empty() || parsed.revision <= 0 {
        return Err("Captured option identity invalid".into());
    }
    Ok(())
}
