//! Command/query-only ABI for the persistent native market-data owner. Accepted
//! business intent is retained in Rust; no streaming record or managed lease
//! callback is required to keep a dataset healthy or rebuild it.
#[path = "market_owner/browser.rs"]
mod browser_action;
#[path = "market_owner/business.rs"]
mod business_action;
#[path = "market_owner/scope.rs"]
mod scope_command;

use super::runtime::{Status, boundary, instance, valid};
use crate::{
    api::owner::MarketData,
    dataset::{
        futures_owner::Intent as FutureIntent,
        option_claims::Scope,
        options_owner::{CacheScope, Intent as OptionIntent},
        transport::Lane,
    },
    messaging::publisher::BrokerSettings,
    runtime::{Deadline, recovery::Phase},
    subscriptions::leases::Lifetime,
};
use serde_json::Value;
use std::{collections::BTreeSet, sync::Arc, time::Duration};

/// Compiled native command/query capabilities: discovery, strategy captures,
/// complete prices/Greeks, paged raw IO and owned reference bootstrap. This
/// reports kernel availability, not completion of Host operation acceptance.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_owner_features_v1() -> u64 {
    0x1ffff
}

fn owner(handle: u64) -> Result<Arc<MarketData>, Status> {
    instance(handle)?
        .market_owner
        .lock()
        .map_err(|_| Status::InternalFailure)?
        .clone()
        .ok_or(Status::InvalidInput)
}
unsafe fn json(input: *const u8, length: u32) -> Result<Value, Status> {
    if input.is_null() || length == 0 || length > 2 * 1024 * 1024 {
        return Err(Status::InvalidInput);
    }
    // SAFETY: Each export requires length readable bytes; the hard cap is checked
    // before borrowing and all retained configuration is copied into Rust values.
    serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
        .map_err(|_| Status::InvalidInput)
}
fn text<'a>(value: &'a Value, key: &str, maximum: usize) -> Result<&'a str, Status> {
    value[key]
        .as_str()
        .filter(|s| !s.trim().is_empty() && s.len() <= maximum)
        .ok_or(Status::InvalidInput)
}
fn number(value: &Value, key: &str) -> Result<u32, Status> {
    u32::try_from(value[key].as_u64().ok_or(Status::InvalidInput)?)
        .map_err(|_| Status::InvalidInput)
}
fn market_date(value: &Value, key: &str) -> Result<time::Date, Status> {
    let date = super::reference::date(number(value, key)?)?;
    if date.to_julian_day() == 1721426 {
        return Err(Status::InvalidInput);
    }
    Ok(date)
}
fn broker(value: &Value) -> Result<BrokerSettings, Status> {
    let endpoint = text(value, "endpoint", 65536)?;
    let dns = value["dns"]
        .as_array()
        .filter(|a| !a.is_empty() && a.len() <= 4)
        .ok_or(Status::InvalidInput)?;
    let dns = dns
        .iter()
        .map(|v| {
            v.as_str()
                .ok_or(Status::InvalidInput)?
                .parse()
                .map_err(|_| Status::InvalidInput)
        })
        .collect::<Result<Vec<_>, _>>()?;
    BrokerSettings::new(endpoint.into(), dns).map_err(|_| Status::InvalidInput)
}
fn scope(value: &Value) -> Result<String, Status> {
    let scope = text(value, "scope", 128)?;
    if !scope
        .bytes()
        .all(|c| c.is_ascii_alphanumeric() || c == b'-')
    {
        return Err(Status::InvalidInput);
    }
    Ok(scope.into())
}
fn instruments(value: &Value, maximum: usize) -> Result<BTreeSet<u32>, Status> {
    let items = value
        .as_array()
        .filter(|a| a.len() <= maximum)
        .ok_or(Status::InvalidInput)?;
    let output = items
        .iter()
        .map(|v| {
            u32::try_from(v.as_u64().filter(|n| *n > 0).ok_or(Status::InvalidInput)?)
                .map_err(|_| Status::InvalidInput)
        })
        .collect::<Result<BTreeSet<_>, _>>()?;
    if output.len() != items.len() {
        return Err(Status::InvalidInput);
    }
    Ok(output)
}
fn strings(value: &Value, maximum: usize, length: usize) -> Result<Vec<String>, Status> {
    let items = value
        .as_array()
        .filter(|a| a.len() <= maximum)
        .ok_or(Status::InvalidInput)?;
    items
        .iter()
        .map(|v| {
            v.as_str()
                .filter(|s| !s.trim().is_empty() && s.len() <= length)
                .map(str::to_owned)
                .ok_or(Status::InvalidInput)
        })
        .collect()
}
fn business(value: &Value) -> Result<u128, Status> {
    let words = value["owner"]
        .as_array()
        .filter(|a| a.len() == 2)
        .ok_or(Status::InvalidInput)?;
    let id = u128::from(words[0].as_u64().ok_or(Status::InvalidInput)?)
        | (u128::from(words[1].as_u64().ok_or(Status::InvalidInput)?) << 64);
    if id == 0 {
        return Err(Status::InvalidInput);
    }
    Ok(id)
}
/// Length-prefixed hashing prevents ambiguous workflow/leg tuples. No physical
/// generation participates: a rebuild must preserve the same business claim.
fn monitored_identity(value: &Value) -> Result<(u128, String), Status> {
    use sha2::{Digest, Sha256};
    let mut owner = Sha256::new();
    owner.update(b"ifm.monitored-option.v1");
    for key in ["workflow_type", "workflow_id", "leg_id", "contract"] {
        let part = text(value, key, 1024)?;
        owner.update((part.len() as u32).to_le_bytes());
        owner.update(part.as_bytes());
    }
    let digest = owner.finalize();
    let id = u128::from_le_bytes(
        digest[..16]
            .try_into()
            .map_err(|_| Status::InternalFailure)?,
    )
    .max(1);
    let contract = Sha256::digest(text(value, "contract", 1024)?.as_bytes());
    let scope = format!(
        "monitor-{}",
        contract
            .iter()
            .map(|b| format!("{b:02x}"))
            .collect::<String>()
    );
    Ok((id, scope))
}
/// Creates native idle control after the authoritative API clock is configured.
/// No option catalog, Treasury request or provider connection is needed to start
/// futures. Legacy prototype live ownership is mutually exclusive in this runtime.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_owner_create_v1(handle: u64) -> i32 {
    boundary(|| {
        let instance = instance(handle)?;
        let mut slot = instance
            .market_owner
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if slot.is_some()
            || instance
                .live
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .is_some()
        {
            return Err(Status::Busy);
        }
        let clock = instance.reference_clock()?;
        let native = instance
            .enter(|| MarketData::new(instance.owner.clone(), clock, None))
            .map_err(|_| Status::InternalFailure)?;
        *slot = Some(Arc::new(native));
        Ok(())
    })
}
/// Materializes only reviewed source identities. Physical plans, underlying
/// dependencies and exact pricing blueprints are constructed in Rust.
fn raw_option_source(
    provider: &Value,
    models: &[crate::reference::option_contract::OptionContract],
) -> Result<Value, Status> {
    let plan =
        crate::dataset::raw_options::Plan::from_models(models).map_err(|_| Status::InvalidInput)?;
    let symbols = plan
        .registrations
        .iter()
        .map(|r| r.source.clone())
        .collect::<Vec<_>>();
    let registrations=plan.registrations.into_iter().map(|row|serde_json::json!({
        "instrument":row.instrument,"publisher":row.publisher,"contract":row.contract,"source":row.source,
        "asset":if row.asset==crate::latest::Asset::Option {"option"} else {"futures"},
        "tick":row.tick,"vwap":row.vwap,"required":row.required
    })).collect();
    let mut source = provider.clone();
    let fields = source.as_object_mut().ok_or(Status::InvalidInput)?;
    fields.insert("evidence".into(), "observations".into());
    fields.insert("registrations".into(), Value::Array(registrations));
    fields.insert(
        "subscriptions".into(),
        serde_json::json!([
        {"symbols":symbols,"schema":"trades","symbology":"instrument_id"},
        {"symbols":symbols,"schema":"mbp-1","symbology":"instrument_id"}]),
    );
    Ok(source)
}
/// Imports a complete raw native option catalog, source plan and broker settings.
/// This command owns all copied inputs and starts no provider until claimed.
/// Replacements preserve native business claims and trigger native reconstruction.
///
/// # Safety
/// input is readable for length bytes (1..2 MiB), borrowed only for this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_catalog_v1(
    handle: u64,
    input: *const u8,
    length: u32,
) -> i32 {
    boundary(|| {
        // SAFETY: Export caller supplies bounded readable input for this call.
        let value = unsafe { json(input, length) }?;
        let instance = instance(handle)?;
        let native = owner(handle)?;
        let raw_only = match value.get("raw_only") {
            None => false,
            Some(v) => v.as_bool().ok_or(Status::InvalidInput)?,
        };
        let models = if value["raw_contracts"].is_null() {
            vec![]
        } else {
            value["raw_contracts"]
                .as_array()
                .filter(|v| v.len() <= 2048)
                .ok_or(Status::InvalidInput)?
                .iter()
                .map(|model| {
                    crate::reference::option_contract::OptionContract::from_command(model)
                        .map_err(|_| Status::InvalidInput)
                })
                .collect::<Result<Vec<_>, _>>()?
        };
        let date = market_date(&value, "date")?;
        let canonical = if value["canonical_monitor"].is_null() {
            None
        } else {
            if !raw_only
                || !models.is_empty()
                || !value["monitor"].is_null()
                || !value["claim"].is_null()
                || !value["background"].is_null()
            {
                return Err(Status::InvalidInput);
            }
            let (owner, scope) = monitored_identity(&value["canonical_monitor"])?;
            Some(crate::dataset::background::Monitor {
                owner,
                scope,
                contract: text(&value["canonical_monitor"], "contract", 1024)?.to_owned(),
            })
        };
        let discovery_scope = if value["canonical_scope"].is_null() {
            None
        } else {
            if !raw_only
                || !models.is_empty()
                || canonical.is_some()
                || !value["monitor"].is_null()
                || !value["claim"].is_null()
                || !value["background"].is_null()
            {
                return Err(Status::InvalidInput);
            }
            Some(
                crate::dataset::background::ScopeDemand::from_command(&value["canonical_scope"])
                    .and_then(|demand| {
                        demand.arm_preparation_lifetime(
                            &value["canonical_scope"],
                            native.clock_ticks()?,
                            tokio::time::Instant::now(),
                        )
                    })
                    .map_err(|_| Status::InvalidInput)?,
            )
        };
        let background_settings = if canonical.is_some() || discovery_scope.is_some() {
            Some((
                if value["reviewed"]["production"].as_bool() == Some(true) {
                    "Production"
                } else {
                    "Development"
                }
                .into(),
                false,
            ))
        } else if value["background"].is_null() {
            None
        } else {
            if !raw_only || !value["monitor"].is_null() || !value["claim"].is_null() {
                return Err(Status::InvalidInput);
            }
            let environment = text(&value["background"], "environment", 32)?.to_owned();
            if !matches!(environment.as_str(), "Development" | "Paper" | "Production") {
                return Err(Status::InvalidInput);
            }
            Some((
                environment,
                value["background"]["quiet"]
                    .as_bool()
                    .ok_or(Status::InvalidInput)?,
            ))
        };
        let empty_background = background_settings.is_some() && models.is_empty();
        let source_input = if empty_background {
            let mut input = value["source"].clone();
            input["registrations"] = serde_json::json!([]);
            input["subscriptions"] = serde_json::json!([]);
            input["evidence"] = if background_settings
                .as_ref()
                .is_some_and(|(_, quiet)| *quiet)
            {
                "control"
            } else {
                "observations"
            }
            .into();
            input
        } else if raw_only {
            raw_option_source(&value["source"], &models)?
        } else {
            value["source"].clone()
        };
        let source = super::live::parse_template(
            &serde_json::to_vec(&source_input).map_err(|_| Status::InvalidInput)?,
            Lane::OptionsGlbx,
            instance.dns_slots.clone(),
            empty_background,
        )?;
        let (background_template, background_policy) = if background_settings.is_some() {
            let (template, policy) =
                super::reviewed::parse_background(&value["reviewed"], &instance, date)?;
            (Some(template), Some(policy))
        } else {
            (None, None)
        };
        let reviewed = if empty_background {
            background_template.ok_or(Status::InvalidInput)?
        } else if raw_only {
            super::reviewed::parse_raw(&value["reviewed"], &instance, date, &models)?
        } else {
            super::reviewed::parse(&value["reviewed"], &instance)?
        };
        let background = background_settings
            .map(|(environment, _)| {
                let configuration = crate::dataset::background::Configuration::new(
                    Arc::new(crate::option_chain::discovery::Discovery {
                        broker: broker(&value["broker"])?,
                        scope: scope(&value)?,
                        environment,
                    }),
                    background_policy.ok_or(Status::InvalidInput)?,
                    Arc::downgrade(&native),
                );
                let configuration = if let Some(monitor) = canonical.clone() {
                    configuration
                        .monitored_only()
                        .with_monitor(monitor)
                        .map_err(|_| Status::InvalidInput)?
                } else if let Some(demand) = discovery_scope.clone() {
                    configuration
                        .monitored_only()
                        .with_scope(demand)
                        .map_err(|_| Status::InvalidInput)?
                } else {
                    configuration
                };
                let configuration = match value["background"].get("committed") {
                    None => configuration,
                    Some(flag) if flag.as_bool() == Some(false) => configuration,
                    Some(flag) if flag.as_bool() == Some(true) => configuration
                        .with_committed()
                        .map_err(|_| Status::InvalidInput)?,
                    _ => return Err(Status::InvalidInput),
                };
                Ok::<_, Status>(Arc::new(configuration))
            })
            .transpose()?;
        let use_store = raw_only
            || if value["reference_store"].is_null() {
                false
            } else {
                value["reference_store"]
                    .as_bool()
                    .ok_or(Status::InvalidInput)?
            };
        let reference_bootstrap = if use_store {
            Some(Arc::new(
                crate::option_chain::reference_bootstrap::Bootstrap::new(
                    broker(&value["broker"])?,
                    scope(&value)?,
                )
                .map_err(|_| Status::InvalidInput)?,
            ))
        } else {
            None
        };
        let monitor = if value["monitor"].is_null() {
            None
        } else {
            let (id, scope) = monitored_identity(&value["monitor"])?;
            let contract = text(&value["monitor"], "contract", 1024)?;
            let instrument = models
                .iter()
                .filter_map(|m| m.convention().ok())
                .find(|c| c.contract() == contract)
                .ok_or(Status::InvalidInput)?
                .instrument();
            Some((id, scope, instrument))
        };
        let claim = if value["claim"].is_null() {
            None
        } else {
            if !raw_only || monitor.is_some() {
                return Err(Status::InvalidInput);
            }
            Some(scope_command::parse(
                &value["claim"],
                &reviewed,
                native.clock_ticks().map_err(|_| Status::InternalFailure)?,
            )?)
        };
        use sha2::{Digest, Sha256};
        let raw_memo = if raw_only {
            let mut global = value.clone();
            let fields = global.as_object_mut().ok_or(Status::InvalidInput)?;
            fields.remove("raw_contracts");
            fields.remove("monitor");
            fields.remove("claim");
            if let Some(origin) = fields
                .get_mut("canonical_scope")
                .and_then(|scope| scope.get_mut("origin"))
                .and_then(Value::as_object_mut)
            {
                origin.remove("until");
                origin.remove("until_offset");
            }
            let mut hash = Sha256::new();
            hash.update(serde_json::to_vec(&global).map_err(|_| Status::InvalidInput)?);
            if let Some(treasury) = instance
                .treasury
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .as_ref()
            {
                hash.update((Arc::as_ptr(treasury) as usize).to_le_bytes());
            }
            let keys = models
                .iter()
                .map(|m| {
                    Ok((
                        m.convention()
                            .map_err(|_| Status::InvalidInput)?
                            .contract()
                            .to_owned(),
                        Sha256::digest(
                            crate::messaging::codec::encode(&m.value())
                                .map_err(|_| Status::InvalidInput)?,
                        )
                        .into(),
                    ))
                })
                .collect::<Result<Vec<_>, Status>>()?;
            Some((<[u8; 32]>::from(hash.finalize()), keys))
        } else {
            None
        };
        if !models.is_empty() {
            let bootstrap = reference_bootstrap.as_ref().ok_or(Status::InvalidInput)?;
            for model in models {
                bootstrap
                    .register_option_model(model)
                    .map_err(|_| Status::InvalidInput)?;
            }
        }
        let intent = Arc::new(OptionIntent {
            background,
            reference_bootstrap,
            date: market_date(&value, "date")?,
            definition: market_date(&value, "definition")?,
            source,
            reviewed,
            monitored: BTreeSet::new(),
            caches: vec![],
            broker: broker(&value["broker"])?,
            scope: scope(&value)?,
            revision: 1,
        });
        let merge = match value.get("merge") {
            None => false,
            Some(v) => v.as_bool().ok_or(Status::InvalidInput)?,
        };
        instance
            .enter(|| {
                if let Some(demand) = discovery_scope.as_ref()
                    && native.accept_empty_discovery(demand, date)?
                {
                    return Ok::<(), String>(());
                }
                if let Some((configuration, keys)) = raw_memo {
                    native.merge_raw_catalog_once(intent, configuration, keys)?;
                } else if merge {
                    native.merge_options_catalog(intent)?;
                } else {
                    native.configure_options_catalog(intent)?;
                }
                if let Some(demand) = discovery_scope.as_ref() {
                    native.forget_empty_discovery(demand.owner)?;
                }
                if let Some((id, scope, instrument)) = monitor {
                    native
                        .option_claims()
                        .ok_or("Native option claims unavailable")?
                        .monitor(id, &scope, instrument)?;
                }
                if let Some(claim) = claim {
                    native
                        .option_claims()
                        .ok_or("Native option claims unavailable")?
                        .configure_and_acquire(
                            claim.owner,
                            &claim.id,
                            claim.scope,
                            claim.lifetime,
                        )?;
                }
                Ok::<(), String>(())
            })
            .map_err(|_| Status::InvalidInput)
    })
}
fn futures_intent(
    instance: &super::runtime::Instance,
    lane: Lane,
    value: &Value,
) -> Result<Arc<FutureIntent>, Status> {
    if lane == Lane::OptionsGlbx {
        return Err(Status::InvalidInput);
    }
    let bootstrap = if value["discover"].is_null() {
        None
    } else {
        let rows = value["discover"]["roots"]
            .as_array()
            .filter(|v| !v.is_empty() && v.len() <= 16)
            .ok_or(Status::InvalidInput)?;
        let roots = rows
            .iter()
            .map(|row| {
                Ok(crate::dataset::futures_bootstrap::Root {
                    symbol: text(row, "symbol", 60)?.into(),
                    count: number(row, "count")? as usize,
                    fallback: if row["fallback"].is_null() {
                        None
                    } else {
                        Some(text(row, "fallback", 32)?.into())
                    },
                    vwap: row["vwap"].as_bool().ok_or(Status::InvalidInput)?,
                })
            })
            .collect::<Result<Vec<_>, Status>>()?;
        let history = instance
            .history
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .clone()
            .ok_or(Status::InvalidInput)?;
        let discovery = crate::dataset::futures_bootstrap::Discovery {
            history,
            roots,
            fixed: vec![],
            session_replay: value["discover"]["session_replay"]
                .as_bool()
                .ok_or(Status::InvalidInput)?,
        };
        discovery.validate(lane).map_err(|_| Status::InvalidInput)?;
        Some(Arc::new(discovery))
    };
    let mut source_input = value["source"].clone();
    if let Some(discovery) = bootstrap.as_ref()
        && source_input["subscriptions"]
            .as_array()
            .is_some_and(Vec::is_empty)
    {
        // Root demand is sufficient input. This placeholder is validation-only;
        // discovery replaces it with exact selected instrument subscriptions
        // before any live socket is opened. C# constructs no physical plan.
        let symbols = discovery
            .roots
            .iter()
            .map(|r| format!("{}.FUT", r.symbol))
            .collect::<Vec<_>>();
        source_input["subscriptions"] = serde_json::json!([
            {"symbols":symbols,"schema":"trades","symbology":"raw_symbol"},
            {"symbols":symbols,"schema":"mbp-1","symbology":"raw_symbol"},
            {"symbols":symbols,"schema":"statistics","symbology":"raw_symbol"}
        ]);
    }
    let source = super::live::parse_template(
        &serde_json::to_vec(&source_input).map_err(|_| Status::InvalidInput)?,
        lane,
        instance.dns_slots.clone(),
        bootstrap.is_some(),
    )?;
    let intent = Arc::new(FutureIntent {
        bootstrap,
        date: market_date(value, "date")?,
        definition: market_date(value, "definition")?,
        source,
        broker: broker(&value["broker"])?,
        scope: scope(value)?,
        revision: i64::from(number(value, "revision")?),
    });
    Ok(intent)
}
/// Accepts complete futures intent directly into the native supervisor. Returning
/// success means accepted intent; `owner_health` supplies actual readiness proof.
/// Cancellation of a caller waiting for readiness cannot withdraw accepted work.
///
/// # Safety
/// input is readable for length bytes (1..2 MiB), borrowed only for this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_futures_v1(
    handle: u64,
    selected: u32,
    input: *const u8,
    length: u32,
) -> i32 {
    boundary(|| {
        let lane = super::live::lane(selected)?;
        if lane == Lane::OptionsGlbx {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Export caller supplies bounded readable input for this call.
        let value = unsafe { json(input, length) }?;
        let instance = instance(handle)?;
        let intent = futures_intent(&instance, lane, &value)?;
        let native = owner(handle)?;
        instance
            .enter(|| native.start_futures(intent))
            .map(|_| ())
            .map_err(|_| Status::InvalidInput)
    })
}
/// Accepts the complete configured futures set in one copied command. Every
/// input is parsed and validated before any dataset's desired state is changed.
/// Native supervisors perform setup independently under their original budgets.
///
/// # Safety
/// input supplies length readable bytes (1..2 MiB), borrowed only for this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_start_v1(
    handle: u64,
    input: *const u8,
    length: u32,
) -> i32 {
    boundary(|| {
        // SAFETY: Caller supplies bounded readable bytes for this call.
        let value = unsafe { json(input, length) }?;
        let rows = value
            .as_array()
            .filter(|v| !v.is_empty() && v.len() <= 2)
            .ok_or(Status::InvalidInput)?;
        let instance = instance(handle)?;
        let mut seen = BTreeSet::new();
        let mut intents = Vec::new();
        let mut date = None;
        for row in rows {
            let lane = super::live::lane(number(row, "lane")?)?;
            if !seen.insert(lane) {
                return Err(Status::InvalidInput);
            }
            let intent = futures_intent(&instance, lane, row)?;
            if date.is_some_and(|d| d != intent.date) {
                return Err(Status::InvalidInput);
            }
            date = Some(intent.date);
            use sha2::{Digest, Sha256};
            let mut hash = Sha256::new();
            hash.update(serde_json::to_vec(row).map_err(|_| Status::InvalidInput)?);
            if let Some(discovery) = &intent.bootstrap {
                hash.update((Arc::as_ptr(&discovery.history) as usize).to_le_bytes());
            }
            intents.push((intent, <[u8; 32]>::from(hash.finalize())));
        }
        let native = owner(handle)?;
        instance
            .enter(|| {
                for (intent, key) in intents {
                    native.start_futures_once(intent, key)?;
                }
                Ok::<(), String>(())
            })
            .map_err(|_| Status::InvalidInput)
    })
}
/// Native business commands: configure, enable (durable unless ttl_ms supplied),
/// disable by stable business-owner identity, clear options, stop futures, or stop
/// all desired feeds. C# retains no native lease and runs no renewal timer.
///
/// # Safety
/// input is readable for length bytes (1..2 MiB), borrowed only for this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_command_v1(
    handle: u64,
    input: *const u8,
    length: u32,
) -> i32 {
    boundary(|| {
        // SAFETY: Export caller supplies bounded readable input for this call.
        let value = unsafe { json(input, length) }?;
        let native = owner(handle)?;
        match text(&value, "command", 64)? {
            "scope-alias" => {
                let input = value["generation"]
                    .as_array()
                    .filter(|v| v.len() == 16)
                    .ok_or(Status::InvalidInput)?;
                let mut generation = [0u8; 16];
                for (out, input) in generation.iter_mut().zip(input) {
                    *out = u8::try_from(input.as_u64().ok_or(Status::InvalidInput)?)
                        .map_err(|_| Status::InvalidInput)?;
                }
                native
                    .alias_option_scope(
                        business(&value)?,
                        generation,
                        text(&value, "expected", 128)?,
                        text(&value, "replacement", 64)?,
                    )
                    .map_err(|_| Status::InvalidInput)
            }
            "committed-refresh" => native
                .option_claims()
                .ok_or(Status::InvalidInput)?
                .refresh_committed()
                .map_err(|_| Status::InvalidInput),
            "stop" => native.stop_feeds().map_err(|_| Status::InternalFailure),
            "stop-futures" => native
                .stop_futures(super::live::lane(number(&value, "lane")?)?)
                .map(|_| ())
                .map_err(|_| Status::InvalidInput),
            command => {
                let claims = native.option_claims().ok_or(Status::InvalidInput)?;
                match command {
                    "configure" | "claim-scope" => {
                        let id = text(&value, "id", 512)?;
                        let options = instruments(&value["options"], 2048)?;
                        let monitored = instruments(&value["monitored"], 2048)?;
                        let cache_values = value["caches"]
                            .as_array()
                            .filter(|a| a.len() <= 64)
                            .ok_or(Status::InvalidInput)?;
                        let mut caches = Vec::with_capacity(cache_values.len());
                        for cache in cache_values {
                            let contracts = strings(&cache["contracts"], 2048, 1024)?;
                            let count = contracts.len();
                            let contracts = contracts.into_iter().collect::<BTreeSet<_>>();
                            if count != contracts.len() {
                                return Err(Status::InvalidInput);
                            }
                            let ranking = if cache["ranking"].is_null() {
                                None
                            } else {
                                let parameters = crate::option_chain::strategy::from_command(
                                    &cache["ranking"]["parameters"],
                                )
                                .map_err(|_| Status::InvalidInput)?;
                                let direction =
                                    u8::try_from(number(&cache["ranking"], "direction")?)
                                        .map_err(|_| Status::InvalidInput)?;
                                let date =
                                    super::reference::date(number(&cache["ranking"], "date")?)?;
                                let ranking = crate::option_chain::strategy::Ranking {
                                    parameters: Arc::new(parameters),
                                    direction,
                                    date,
                                };
                                ranking.bias().map_err(|_| Status::InvalidInput)?;
                                Some(ranking)
                            };
                            caches.push(CacheScope {
                                ranking,
                                scope: text(cache, "scope", 512)?.into(),
                                horizon: text(cache, "horizon", 16)?.into(),
                                contracts,
                            });
                        }
                        let scope = Scope {
                            options,
                            monitored,
                            caches,
                        };
                        if command == "claim-scope" {
                            let owner = business(&value)?;
                            let lifetime = if !value["expires_ticks"].is_null() {
                                if value["expires_offset"].as_f64() != Some(0.0) {
                                    return Err(Status::InvalidInput);
                                }
                                let until = value["expires_ticks"]
                                    .as_i64()
                                    .ok_or(Status::InvalidInput)?;
                                let now =
                                    native.clock_ticks().map_err(|_| Status::InternalFailure)?;
                                let ticks = until
                                    .checked_sub(now)
                                    .filter(|ticks| *ticks > 0 && *ticks <= 1_200_000_000)
                                    .ok_or(Status::InvalidInput)?;
                                Lifetime::Ephemeral(Duration::from_nanos(
                                    u64::try_from(ticks).map_err(|_| Status::InvalidInput)? * 100,
                                ))
                            } else if value["ttl_ms"].is_null() {
                                Lifetime::Durable
                            } else {
                                let ttl = number(&value, "ttl_ms")?;
                                if ttl == 0 || ttl > 120000 {
                                    return Err(Status::InvalidInput);
                                }
                                Lifetime::Ephemeral(Duration::from_millis(u64::from(ttl)))
                            };
                            claims
                                .configure_and_acquire(owner, id, scope, lifetime)
                                .map(|_| ())
                                .map_err(|_| Status::InvalidInput)
                        } else {
                            claims
                                .configure(id, scope)
                                .map_err(|_| Status::InvalidInput)
                        }
                    }
                    "enable" => {
                        let id = business(&value)?;
                        let scopes = strings(&value["scopes"], 64, 512)?;
                        let lifetime = if value["ttl_ms"].is_null() {
                            Lifetime::Durable
                        } else {
                            let ttl = number(&value, "ttl_ms")?;
                            if ttl == 0 || ttl > 120000 {
                                return Err(Status::InvalidInput);
                            }
                            Lifetime::Ephemeral(Duration::from_millis(u64::from(ttl)))
                        };
                        claims
                            .acquire(id, &scopes, lifetime)
                            .map(|_| ())
                            .map_err(|_| Status::InvalidInput)
                    }
                    "unmonitor" => claims
                        .disable(monitored_identity(&value)?.0)
                        .map_err(|_| Status::InternalFailure),
                    "disable" => claims
                        .disable(business(&value)?)
                        .map_err(|_| Status::InternalFailure),
                    "clear-options" => claims.clear().map_err(|_| Status::InternalFailure),
                    _ => Err(Status::InvalidInput),
                }
            }
        }
    })
}
/// Copies the complete existing backend-status DTO from native authority.
/// Sizing never consumes state; output is bounded to 64 KiB and no IO is run.
///
/// # Safety
/// required is aligned writable u32; output provides capacity writable bytes,
/// with null permitted only when capacity is zero.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_backend_status_v1(
    handle: u64,
    ready: u32,
    output: *mut u8,
    capacity: u32,
    required: *mut u32,
) -> i32 {
    boundary(|| {
        if ready > 1 || capacity > 65536 || !valid(required) || (capacity > 0 && output.is_null()) {
            return Err(Status::InvalidInput);
        }
        let value = owner(handle)?
            .backend_status(ready != 0)
            .map_err(|_| Status::InternalFailure)?;
        let bytes = crate::messaging::codec::encode(&value).map_err(|_| Status::InternalFailure)?;
        if bytes.len() > 65536 {
            return Err(Status::Capacity);
        }
        // SAFETY: Caller owns the validated scalar, independent of buffer sizing.
        unsafe {
            required.write(bytes.len() as u32);
        }
        if (capacity as usize) < bytes.len() {
            return Err(Status::Capacity);
        }
        // SAFETY: Complete writable capacity was checked before any byte copy.
        unsafe {
            std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
        }
        Ok(())
    })
}
/// Copies a complete existing backend startup/reconnect DTO. It performs no
/// provider IO or stream drain and never returns a partial complete snapshot.
///
/// # Safety
/// required is aligned writable u32; output supplies capacity writable bytes,
/// with null allowed only for zero-capacity sizing. Capacity is at most 16 MiB.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_read_snapshot_v1(
    handle: u64,
    ready: u32,
    output: *mut u8,
    capacity: u32,
    required: *mut u32,
) -> i32 {
    boundary(|| {
        if ready > 1
            || capacity > 16 * 1024 * 1024
            || !valid(required)
            || (capacity > 0 && output.is_null())
        {
            return Err(Status::InvalidInput);
        }
        let result = owner(handle)?
            .read_snapshot(ready != 0)
            .map_err(|_| Status::InternalFailure)?;
        let bytes =
            crate::messaging::codec::encode(&result).map_err(|_| Status::InternalFailure)?;
        if bytes.len() > 16 * 1024 * 1024 {
            return Err(Status::Capacity);
        }
        // SAFETY: Caller supplies validated writable scalar and complete output capacity.
        unsafe {
            required.write(bytes.len() as u32);
        }
        if (capacity as usize) < bytes.len() {
            return Err(Status::Capacity);
        }
        // SAFETY: Writable output length was checked before copying any byte.
        unsafe {
            std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
        }
        Ok(())
    })
}
/// Forty-byte copied retention/loss accounting; never a dataset handle.
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct NativeRetiredEvidence {
    /// Exactly forty bytes.
    pub size: u32,
    /// Always zero.
    pub reserved: u32,
    /// Number of retained old outboxes, at most four.
    pub retained: u64,
    /// Cumulative evicted old outboxes.
    pub generations: u64,
    /// Exact market-data records evicted at retirement retention limits.
    pub records: u64,
    /// Exact encoded bytes evicted at retirement retention limits.
    pub bytes: u64,
}
/// Copies retention/loss accounting even while source or persistence is stalled.
/// No broker, database, old outbox drain or recovery wait is performed.
///
/// # Safety
/// output supplies aligned exclusive writable storage for one forty-byte record.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_retired_evidence_v1(
    handle: u64,
    size: u32,
    output: *mut NativeRetiredEvidence,
) -> i32 {
    boundary(|| {
        if !valid(output) {
            return Err(Status::InvalidInput);
        }
        if size != 40 {
            return Err(Status::AbiMismatch);
        }
        let (retained, loss) = owner(handle)?
            .retired_evidence_status()
            .map_err(|_| Status::InternalFailure)?;
        let value = NativeRetiredEvidence {
            size: 40,
            reserved: 0,
            retained: retained as u64,
            generations: loss.generations,
            records: loss.records,
            bytes: loss.bytes,
        };
        // SAFETY: Caller owns the aligned fixed-size output validated above.
        unsafe {
            output.write(value);
        }
        Ok(())
    })
}
/// Fixed copied native dataset health. This is control evidence; no PID,
/// operating-system thread, provider handle or connection crosses this ABI.
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct NativeOwnerHealth {
    /// Exactly 64 bytes.
    pub size: u32,
    /// Explicit native lane 1..3.
    pub lane: u32,
    /// stopped=1, starting=2, running=3, recovering=4, failed=5.
    pub phase: u32,
    /// Bitset of missing native readiness Proof values.
    pub missing: u32,
    /// Opaque episode/revision/generation ticket counters.
    pub ticket: [u64; 3],
    /// Remaining original recovery allowance, zero when healthy/expired.
    pub remaining_ms: u64,
    /// Retained terminal notifications awaiting UI acknowledgment, at most 16.
    pub pending_failures: u64,
    /// Always zero.
    pub reserved: u64,
}
/// Copied actual publication identity, available independently of healthy prices.
#[repr(C)]
#[derive(Default)]
pub struct NativeOwnerIdentity {
    /// Exactly 56 bytes.
    pub size: u32,
    /// Native dataset lane 1..3.
    pub lane: u32,
    /// Original API ValueDate day number, meaningful only when flags bit 1 is set.
    pub value_date: i32,
    /// Bit 1 means an actual publication owner exists; bit 2 means non-idle
    /// desired work is accepted. A healthy idle controller is not a live feed.
    pub flags: u32,
    /// Worker Guid words in CLR byte order; never a fabricated process ID.
    pub worker: [u64; 2],
    /// Logical publication-generation Guid words in CLR byte order.
    pub generation: [u64; 2],
    /// Positive native desired manifest revision for this publication owner.
    pub revision: i64,
}
/// Reads actual retained publication identity, not a readiness claim. Consumers
/// query health separately and must never treat stopped identity as live data.
///
/// # Safety
/// output is aligned exclusive writable storage for one 56-byte record.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_identity_v1(
    handle: u64,
    selected: u32,
    size: u32,
    output: *mut NativeOwnerIdentity,
) -> i32 {
    boundary(|| {
        if !valid(output) {
            return Err(Status::InvalidInput);
        }
        if size != 56 {
            return Err(Status::AbiMismatch);
        }
        let lane = super::live::lane(selected)?;
        let native = owner(handle)?;
        let mut value = NativeOwnerIdentity {
            size: 56,
            lane: selected,
            ..Default::default()
        };
        if native.has_desired(lane) {
            value.flags |= 2;
        }
        if let Some(identity) = native.publication_identity(lane) {
            let words = |bytes: [u8; 16]| {
                [
                    u64::from_le_bytes(bytes[..8].try_into().expect("fixed Guid prefix")),
                    u64::from_le_bytes(bytes[8..].try_into().expect("fixed Guid suffix")),
                ]
            };
            value.value_date = identity.value_date;
            value.flags |= 1;
            value.worker = words(identity.worker);
            value.generation = words(identity.generation);
            value.revision = identity.revision;
        }
        // SAFETY: The caller supplies validated aligned output of exactly this size.
        unsafe {
            output.write(value);
        }
        Ok(())
    })
}
/// Copies current dataset health even when a broker or C# consumer is stalled.
///
/// # Safety
/// output is aligned writable exclusive storage; size must be exactly 64.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_health_v1(
    handle: u64,
    selected: u32,
    size: u32,
    output: *mut NativeOwnerHealth,
) -> i32 {
    boundary(|| {
        if !valid(output) {
            return Err(Status::InvalidInput);
        }
        if size != 64 {
            return Err(Status::AbiMismatch);
        }
        let lane = super::live::lane(selected)?;
        let native = owner(handle)?;
        let recovery = native.health(lane);
        let health = recovery.status();
        let value = NativeOwnerHealth {
            size: 64,
            lane: selected,
            phase: match health.phase {
                Phase::Stopped => 1,
                Phase::Starting => 2,
                Phase::Running => 3,
                Phase::Recovering => 4,
                Phase::Failed => 5,
            },
            missing: health
                .missing
                .iter()
                .fold(0, |bits, proof| bits | u32::from(*proof as u8)),
            ticket: health.ticket.parts(),
            remaining_ms: health.deadline.map_or(0, |d| {
                d.remaining().as_millis().min(u128::from(u64::MAX)) as u64
            }),
            pending_failures: recovery.failures(0, 16).len() as u64,
            reserved: 0,
        };
        // SAFETY: Export contract guarantees exclusive output after size/alignment validation.
        unsafe {
            output.write(value);
        }
        Ok(())
    })
}

/// Copies one current numeric snapshot using the already-qualified 656-byte
/// state layout. It is an explicit non-consuming query; streaming publications
/// continue directly from Rust to NATS and never enter this function.
///
/// # Safety
/// output is aligned exclusive writable storage for one 656-byte publication.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_state_v1(
    handle: u64,
    selected: u32,
    instrument: u32,
    publisher: u32,
    size: u32,
    output: *mut super::publication::NativePublication,
) -> i32 {
    boundary(|| {
        if !valid(output) || instrument == 0 || publisher > u32::from(u16::MAX) {
            return Err(Status::InvalidInput);
        }
        if size != 656 {
            return Err(Status::AbiMismatch);
        }
        let lane = super::live::lane(selected)?;
        let mut source = super::live::NativeMarketEvent {
            size: 128,
            lane: selected,
            instrument,
            ..Default::default()
        };
        source.fields[4] = i64::from(publisher);
        let state = owner(handle)?
            .publication_state(lane, instrument, publisher as u16)
            .map_err(|_| Status::InternalFailure)?;
        let value = if let Some((generation, state)) = state {
            source.generation_low = generation as u64;
            source.generation_high = (generation >> 64) as u64;
            super::publication::NativePublication::from_state(source, state)
        } else {
            super::publication::NativePublication::from_state(source, Default::default())
        };
        // SAFETY: Size/alignment checked; the caller owns the fixed output record.
        unsafe {
            output.write(value);
        }
        Ok(())
    })
}
/// Copies a complete current prepared-cache publication on explicit request.
/// No streaming consumer or managed lease is involved. Missing, expired and
/// recovering scopes return zero length; sizing never consumes a publication.
///
/// # Safety
/// scope names scope_length readable UTF-8 bytes; required is aligned writable
/// u32; output supplies capacity writable bytes (null only when capacity is zero).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_cache_v1(
    handle: u64,
    scope: *const u8,
    scope_length: u32,
    output: *mut u8,
    capacity: u32,
    required: *mut u32,
) -> i32 {
    boundary(|| {
        if scope.is_null()
            || scope_length == 0
            || scope_length > 512
            || !valid(required)
            || capacity > 4 * 1024 * 1024 + 4096
            || (capacity > 0 && output.is_null())
        {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller supplies scope_length readable bytes, bounded above.
        let scope = std::str::from_utf8(unsafe {
            std::slice::from_raw_parts(scope, scope_length as usize)
        })
        .map_err(|_| Status::InvalidInput)?;
        if scope.trim().is_empty() || scope.encode_utf16().count() > 128 {
            return Err(Status::InvalidInput);
        }
        let at = instance(handle)?
            .reference_clock()?
            .utc_ticks()
            .map_err(|_| Status::InternalFailure)?;
        let cache = owner(handle)?
            .option_cache(scope, at)
            .map_err(|_| Status::InternalFailure)?;
        let bytes = cache.as_ref().map_or(&[][..], |cache| cache.content());
        // SAFETY: The caller owns this validated writable scalar.
        unsafe {
            required.write(bytes.len() as u32);
        }
        if (capacity as usize) < bytes.len() {
            return Err(Status::Capacity);
        }
        if !bytes.is_empty() {
            // SAFETY: Caller guarantees capacity bytes; the length is checked.
            unsafe {
                std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
            }
        }
        Ok(())
    })
}

/// Submits a non-owning readiness waiter through the existing native completion
/// queue. No provider handle is returned and cancellation never withdraws demand.
/// Dataset recovery retains its original 120-second deadline.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_owner_wait_v1(
    handle: u64,
    operation: u64,
    selected: u32,
    require_active: u32,
) -> i32 {
    boundary(|| {
        if require_active > 1 {
            return Err(Status::InvalidInput);
        }
        let lane = if matches!(selected, 0 | 4) {
            None
        } else {
            Some(super::live::lane(selected)?)
        };
        if matches!(selected, 0 | 4) && require_active != 0 {
            return Err(Status::InvalidInput);
        }
        let instance = instance(handle)?;
        let native = owner(handle)?;
        let mut reservation = instance.reserve_control(operation)?;
        let work = async move {
            reservation.result = super::live::NativeControlResult::finished(match lane {
                Some(lane) => native.wait_connected(lane, require_active != 0).await,
                None if selected == 4 => native.wait_feeds_stopped().await,
                None => tokio::try_join!(
                    native.wait_connected(Lane::FuturesGlbx, false),
                    native.wait_connected(Lane::FuturesXcbf, false)
                )
                .map(|_| ()),
            });
            drop(reservation);
            Ok(0.0)
        };
        if let Err(status) = instance.submit_observer(operation, work) {
            instance
                .control
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .remove(&operation);
            return Err(status);
        }
        Ok(())
    })
}

/// Copies an explicit current-publication query page. It neither consumes data
/// nor starts an FFI stream; live messages are posted directly by Rust to NATS.
///
/// # Safety
/// required is aligned writable u32 storage; output is writable for capacity
/// bytes, with null permitted only for a zero-capacity sizing call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_current_v1(
    handle: u64,
    selected: u32,
    after: i64,
    maximum: u32,
    output: *mut u8,
    capacity: u32,
    required: *mut u32,
) -> i32 {
    boundary(|| {
        if after < 0
            || maximum == 0
            || maximum > 256
            || capacity > 4 * 1024 * 1024
            || !valid(required)
            || capacity > 0 && output.is_null()
        {
            return Err(Status::InvalidInput);
        }
        let value = owner(handle)?
            .current_frames(super::live::lane(selected)?, after, maximum as usize)
            .map_err(|_| Status::InternalFailure)?;
        let bytes = value
            .as_ref()
            .map(crate::messaging::codec::encode)
            .transpose()
            .map_err(|_| Status::InternalFailure)?
            .unwrap_or_default();
        if bytes.len() > 4 * 1024 * 1024 {
            return Err(Status::Capacity);
        }
        // SAFETY: Caller supplies exclusive writable scalar storage, validated above.
        unsafe {
            required.write(bytes.len() as u32);
        }
        if (capacity as usize) < bytes.len() {
            return Err(Status::Capacity);
        }
        if !bytes.is_empty() {
            // SAFETY: Caller byte capacity was checked before copying.
            unsafe {
                std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
            }
        }
        Ok(())
    })
}

/// Copies a bounded full quote-Greeks result. This query does no financial work
/// in C# and cannot acquire, renew or retain a provider lease. Zero bytes means
/// absent current coherent risk; native raw price queries remain independent.
///
/// # Safety
/// required is aligned writable u32; output supplies capacity writable bytes,
/// with null allowed only for a zero-capacity sizing call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_quote_greeks_v1(
    handle: u64,
    instrument: u32,
    publisher: u32,
    output: *mut u8,
    capacity: u32,
    required: *mut u32,
) -> i32 {
    boundary(|| {
        if instrument == 0
            || publisher > u32::from(u16::MAX)
            || !valid(required)
            || capacity > 16 * 1024
            || (capacity > 0 && output.is_null())
        {
            return Err(Status::InvalidInput);
        }
        let at = instance(handle)?
            .reference_clock()?
            .utc_ticks()
            .map_err(|_| Status::InternalFailure)?;
        let result = owner(handle)?
            .quote_greeks(instrument, publisher as u16, at)
            .map_err(|_| Status::InternalFailure)?;
        let bytes = result
            .as_ref()
            .map(crate::messaging::codec::encode)
            .transpose()
            .map_err(|_| Status::InternalFailure)?
            .unwrap_or_default();
        if bytes.len() > 16 * 1024 {
            return Err(Status::Capacity);
        }
        // SAFETY: Caller owns validated writable scalar storage.
        unsafe {
            required.write(bytes.len() as u32);
        }
        if (capacity as usize) < bytes.len() {
            return Err(Status::Capacity);
        }
        if !bytes.is_empty() {
            // SAFETY: Caller guarantees writable capacity, checked above.
            unsafe {
                std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
            }
        }
        Ok(())
    })
}

/// Copies a bounded full trade-Greeks result. This query does no financial work
/// in C# and cannot acquire, renew or retain a provider lease. Zero bytes means
/// absent current coherent risk; native raw price queries remain independent.
///
/// # Safety
/// required is aligned writable u32; output supplies capacity writable bytes,
/// with null allowed only for a zero-capacity sizing call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_trade_greeks_v1(
    handle: u64,
    instrument: u32,
    publisher: u32,
    output: *mut u8,
    capacity: u32,
    required: *mut u32,
) -> i32 {
    boundary(|| {
        if instrument == 0
            || publisher > u32::from(u16::MAX)
            || !valid(required)
            || capacity > 16 * 1024
            || (capacity > 0 && output.is_null())
        {
            return Err(Status::InvalidInput);
        }
        let result = owner(handle)?
            .trade_greeks(instrument, publisher as u16)
            .map_err(|_| Status::InternalFailure)?;
        let bytes = result
            .as_ref()
            .map(crate::messaging::codec::encode)
            .transpose()
            .map_err(|_| Status::InternalFailure)?
            .unwrap_or_default();
        if bytes.len() > 16 * 1024 {
            return Err(Status::Capacity);
        }
        // SAFETY: Caller owns validated writable scalar storage.
        unsafe {
            required.write(bytes.len() as u32);
        }
        if (capacity as usize) < bytes.len() {
            return Err(Status::Capacity);
        }
        if !bytes.is_empty() {
            // SAFETY: Caller guarantees writable capacity, checked above.
            unsafe {
                std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
            }
        }
        Ok(())
    })
}

/// Copies complete canonical raw-price query DTOs. Streaming remains direct NATS;
/// decimal conversion and snapshot construction occur entirely in native code.
///
/// # Safety
/// contract holds contract_length readable UTF-8 bytes; output holds capacity
/// writable bytes; required is aligned exclusive writable u32 storage.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_raw_prices_v1(
    handle: u64,
    selected: u32,
    contract: *const u8,
    contract_length: u32,
    date: u32,
    output: *mut u8,
    capacity: u32,
    required: *mut u32,
) -> i32 {
    boundary(|| {
        if contract.is_null()
            || contract_length == 0
            || contract_length > 1024
            || date > 3652058
            || !valid(required)
            || capacity > 4096
            || (capacity > 0 && output.is_null())
        {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller supplies bounded readable contract text.
        let contract = std::str::from_utf8(unsafe {
            std::slice::from_raw_parts(contract, contract_length as usize)
        })
        .map_err(|_| Status::InvalidInput)?;
        let result = owner(handle)?
            .raw_prices(super::live::lane(selected)?, contract, date as i32)
            .map_err(|_| Status::InternalFailure)?;
        let bytes = result
            .as_ref()
            .map(crate::messaging::codec::encode)
            .transpose()
            .map_err(|_| Status::InternalFailure)?
            .unwrap_or_default();
        if bytes.len() > 4096 {
            return Err(Status::Capacity);
        }
        // SAFETY: Required scalar output is validated above.
        unsafe {
            required.write(bytes.len() as u32);
        }
        if (capacity as usize) < bytes.len() {
            return Err(Status::Capacity);
        }
        if !bytes.is_empty() {
            // SAFETY: Output byte capacity is sufficient and caller-owned.
            unsafe {
                std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
            }
        }
        Ok(())
    })
}

/// Copies bounded terminal evidence as explicit query JSON. No polling timer or
/// streaming FFI consumer is created. An undersized buffer only reports required
/// capacity and does not consume/acknowledge any notification.
///
/// # Safety
/// required is aligned writable u32. output is writable for capacity bytes when
/// non-null; null is permitted only for capacity zero sizing calls.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_failures_v1(
    handle: u64,
    output: *mut u8,
    capacity: u32,
    required: *mut u32,
) -> i32 {
    boundary(|| {
        if !valid(required) || capacity > 4 * 1024 * 1024 || (capacity > 0 && output.is_null()) {
            return Err(Status::InvalidInput);
        }
        let records=owner(handle)?.failures().into_iter().map(|(lane,record)|serde_json::json!({"lane":lane as u32,"ordinal":record.ordinal,
            "ticket":record.status.ticket.parts(),"root":record.status.root_error,"detail":record.status.latest_error,"replaced":record.replaced})).collect::<Vec<_>>();
        let bytes = serde_json::to_vec(&records).map_err(|_| Status::InternalFailure)?;
        if bytes.len() > 4 * 1024 * 1024 {
            return Err(Status::Capacity);
        }
        // SAFETY: Required pointer is validated; output capacity is checked before copying.
        unsafe {
            required.write(bytes.len() as u32);
        }
        if capacity < (bytes.len() as u32) {
            return Err(Status::Capacity);
        }
        if !bytes.is_empty() {
            // SAFETY: Export contract supplies capacity writable bytes; no caller storage is retained.
            unsafe {
                std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
            }
        }
        Ok(())
    })
}
/// Acknowledges only explicitly delivered terminal notifications. Call this after
/// UI delivery confirmation, never merely because a broker flush completed.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_owner_failure_ack_v1(
    handle: u64,
    selected: u32,
    ordinal: u64,
) -> i32 {
    boundary(|| {
        let lane = super::live::lane(selected)?;
        if !owner(handle)?.health(lane).acknowledge_failures(ordinal) {
            return Err(Status::InvalidInput);
        }
        Ok(())
    })
}
/// Reports an external explicit reset/test command to the native dataset owner.
/// Internal provider/pricing/broker faults already report themselves in Rust.
/// The two-minute allowance is owned by the native supervisor and cannot be
/// extended by repeat calls during the same incident.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_owner_recover_v1(handle: u64, selected: u32) -> i32 {
    boundary(|| {
        owner(handle)?
            .report_fault(
                super::live::lane(selected)?,
                "Explicit native dataset recovery command",
                Deadline::recovery(),
            )
            .map_err(|_| Status::Closed)
    })
}

/// Accepts a complete public recovery request synchronously into native
/// authority, then returns its bounded result through the existing copied-result
/// operation protocol. Cancelling this observer never withdraws native recovery.
///
/// # Safety
/// input supplies length readable bytes, borrowed only for this call (1..16 KiB).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_recovery_request_v1(
    handle: u64,
    operation: u64,
    input: *const u8,
    length: u32,
) -> i32 {
    boundary(|| {
        if length > 16384 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller provides length readable bytes for this bounded call.
        let value = unsafe { json(input, length) }?;
        let lane = match (text(&value, "role", 16)?, text(&value, "dataset", 64)?) {
            ("Futures", "GLBX.MDP3") => Lane::FuturesGlbx,
            ("Futures", "XCBF.PITCH") => Lane::FuturesXcbf,
            ("Options", "GLBX.MDP3") => Lane::OptionsGlbx,
            _ => return Err(Status::InvalidInput),
        };
        let guid = |key: &str| -> Result<[u8; 16], Status> {
            let values = value[key]
                .as_array()
                .filter(|v| v.len() == 16)
                .ok_or(Status::InvalidInput)?;
            let mut output = [0u8; 16];
            for (target, source) in output.iter_mut().zip(values) {
                *target = u8::try_from(source.as_u64().ok_or(Status::InvalidInput)?)
                    .map_err(|_| Status::InvalidInput)?;
            }
            Ok(output)
        };
        let request = crate::api::owner::RecoveryRequest {
            lane,
            operation: guid("operation")?,
            worker: guid("worker")?,
            generation: guid("generation")?,
            date: market_date(&value, "date")?,
        };
        if request.operation == [0; 16] {
            return Err(Status::InvalidInput);
        }
        let instance = instance(handle)?;
        let native = owner(handle)?;
        let mut retained = super::retained::Pending::reserve(&instance, operation)?;
        let mut reservation = instance.reserve_control(operation)?;
        let rejected = match instance.enter(|| native.begin_recovery(&request)) {
            Ok(value) => value,
            Err(_) => {
                drop(reservation);
                instance
                    .control
                    .lock()
                    .map_err(|_| Status::InternalFailure)?
                    .remove(&operation);
                return Err(Status::InvalidInput);
            }
        };
        let work = async move {
            let result = async {
                let result = native.recovery_result(&request, rejected).await?;
                retained.commit(crate::messaging::codec::encode(&result)?, 8192)?;
                Ok::<(), String>(())
            }
            .await;
            reservation.result = super::live::NativeControlResult::finished(result);
            drop(retained);
            drop(reservation);
            Ok(0.0)
        };
        if let Err(status) = instance.submit_observer(operation, work) {
            instance
                .control
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .remove(&operation);
            return Err(status);
        }
        Ok(())
    })
}
/// Captures an existing public composition result wholly in native code. Input
/// metadata is copied; provider resources and managed pricing callbacks never
/// participate. Results use the bounded copied-result operation protocol.
///
/// # Safety
/// input supplies length readable bytes, borrowed only for this call (<=2 MiB).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_capture_v1(
    handle: u64,
    operation: u64,
    input: *const u8,
    length: u32,
) -> i32 {
    boundary(|| {
        // SAFETY: Caller supplies bounded readable input for the synchronous copy.
        let value = unsafe { json(input, length) }?;
        let instance = instance(handle)?;
        let native = owner(handle)?;
        if value["kind"].as_str() == Some("browser") {
            return browser_action::submit(instance, native, operation, value);
        }
        if matches!(
            value["kind"].as_str(),
            Some("committed-reconcile" | "composition-release")
        ) {
            return business_action::submit(instance, native, operation, value);
        }
        let parsed = if value["kind"].as_str() == Some("monitored-risk") {
            let contract = text(&value, "contract", 1024)?;
            native.monitored_risk_command(contract, market_date(&value, "date")?)
        } else {
            crate::api::owner::CaptureCommand::from_value(&value)
        };
        let mut retained = super::retained::Pending::reserve(&instance, operation)?;
        let mut reservation = instance.reserve_control(operation)?;
        let work = async move {
            let result = async {
                let outcome = match parsed {
                    Err(failure) => Err(failure),
                    Ok(command) => {
                        let now = native
                            .clock_ticks()
                            .map_err(|_| "Native capture clock unavailable")?;
                        let remaining = (i128::from(command.request.until) - i128::from(now))
                            .clamp(0, 1_200_000_000) as u64;
                        let deadline = Deadline::after(Duration::from_nanos(remaining * 100));
                        let stop = tokio_util::sync::CancellationToken::new();
                        if command.request.options {
                            if command.wait {
                                native
                                    .capture_options_ready(command.request, &stop, deadline)
                                    .await
                            } else {
                                native
                                    .capture_options(command.request, &stop, deadline)
                                    .await
                            }
                        } else if command.wait {
                            native
                                .capture_futures_ready(
                                    command.request,
                                    command.futures,
                                    command.non_utc_definition,
                                    &stop,
                                    deadline,
                                )
                                .await
                        } else {
                            native
                                .capture_futures(
                                    command.request,
                                    command.futures,
                                    command.non_utc_definition,
                                    &stop,
                                    deadline,
                                )
                                .await
                        }
                    }
                };
                let value = match outcome {
                    Ok(snapshot) => rmpv::Value::Array(vec![snapshot.value()?, rmpv::Value::Nil]),
                    Err(failure) => rmpv::Value::Array(vec![
                        rmpv::Value::Nil,
                        rmpv::Value::Array(vec![
                            failure.code.into(),
                            failure.input.into(),
                            "".into(),
                            "Complete coherent composition evidence is unavailable.".into(),
                            false.into(),
                        ]),
                    ]),
                };
                retained.commit(
                    crate::messaging::codec::encode(&value)?,
                    4 * 1024 * 1024 + 4096,
                )?;
                Ok::<(), String>(())
            }
            .await;
            reservation.result = super::live::NativeControlResult::finished(result);
            drop(retained);
            drop(reservation);
            Ok(0.0)
        };
        if let Err(status) = instance.submit_observer(operation, work) {
            instance
                .control
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .remove(&operation);
            return Err(status);
        }
        Ok(())
    })
}
/// Copies the exact thirty-two-key contract DTOs resolved by native bootstrap.
/// Zero bytes means no current healthy bootstrap result. This query starts no IO.
///
/// # Safety
/// `required` is aligned writable u32; output has capacity writable bytes and may
/// be null only for a zero-capacity sizing request.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_contracts_v1(
    handle: u64,
    selected: u32,
    output: *mut u8,
    capacity: u32,
    required: *mut u32,
) -> i32 {
    boundary(|| {
        let lane = super::live::lane(selected)?;
        if lane == Lane::OptionsGlbx
            || capacity > 1024 * 1024
            || !valid(required)
            || capacity > 0 && output.is_null()
        {
            return Err(Status::InvalidInput);
        }
        let value = owner(handle)?.futures_contracts(lane);
        let bytes = value
            .as_ref()
            .map(crate::messaging::codec::encode)
            .transpose()
            .map_err(|_| Status::InternalFailure)?
            .unwrap_or_default();
        if bytes.len() > 1024 * 1024 {
            return Err(Status::Capacity);
        }
        // SAFETY: Scalar pointer validated before any output mutation.
        unsafe {
            required.write(bytes.len() as u32);
        }
        if (capacity as usize) < bytes.len() {
            return Err(Status::Capacity);
        }
        if !bytes.is_empty() {
            // SAFETY: Complete output capacity checked before copying.
            unsafe {
                std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
            }
        }
        Ok(())
    })
}

/// Performs bounded native prepared-cache request validation and copies its DTO.
/// This is an explicit query and cannot create or renew provider demand.
///
/// # Safety
/// Input has length readable bytes. required is aligned writable u32. Output has
/// capacity writable bytes; null is valid only for zero-capacity sizing calls.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_prepared_cache_v1(
    handle: u64,
    input: *const u8,
    length: u32,
    output: *mut u8,
    capacity: u32,
    required: *mut u32,
) -> i32 {
    boundary(|| {
        if length > 65536
            || capacity > 4 * 1024 * 1024 + 4096
            || !valid(required)
            || capacity > 0 && output.is_null()
        {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller supplies readable input; bounded before parsing/copying.
        let request = unsafe { json(input, length) }?;
        let native = owner(handle)?;
        let value = native
            .prepared_cache_query(&request)
            .map_err(|_| Status::InvalidInput)?;
        let bytes = crate::messaging::codec::encode(&value).map_err(|_| Status::InternalFailure)?;
        if bytes.len() > 4 * 1024 * 1024 + 4096 {
            return Err(Status::Capacity);
        }
        // SAFETY: Required was validated before mutation; capacity checked before byte copy.
        unsafe {
            required.write(bytes.len() as u32);
        }
        if (capacity as usize) < bytes.len() {
            return Err(Status::Capacity);
        }
        if !bytes.is_empty() {
            // SAFETY: Complete output capacity checked above.
            unsafe {
                std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
            }
        }
        if request["kind"].as_str() == Some("scope-discovery") {
            let values = request["owner"]
                .as_array()
                .filter(|values| values.len() == 16)
                .ok_or(Status::InvalidInput)?;
            let mut owner = [0; 16];
            for (target, value) in owner.iter_mut().zip(values) {
                *target = u8::try_from(value.as_u64().ok_or(Status::InvalidInput)?)
                    .map_err(|_| Status::InvalidInput)?;
            }
            if let Err(error) = native.complete_rejected_discovery(owner) {
                let _ = native.report_fault(Lane::OptionsGlbx, &error, Deadline::recovery());
                return Err(Status::InternalFailure);
            }
        }
        Ok(())
    })
}

/// Executes a small canonical binding or mark query. Input is copied command
/// data; streaming remains native direct NATS. The result never owns a resource.
///
/// # Safety
/// input has length readable bytes (1..4096), required is aligned writable u32,
/// output has capacity writable bytes (0..4096), null only for zero capacity.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_query_v1(
    handle: u64,
    input: *const u8,
    length: u32,
    output: *mut u8,
    capacity: u32,
    required: *mut u32,
) -> i32 {
    boundary(|| {
        if length > 4096
            || capacity > 4096
            || !valid(required)
            || (capacity != 0 && output.is_null())
        {
            return Err(Status::InvalidInput);
        }
        // SAFETY: The caller supplies bounded readable input for this call.
        let request = unsafe { json(input, length) }?;
        let native = owner(handle)?;
        let contract = text(&request, "contract", 1024)?;
        let options = request["options"].as_bool().ok_or(Status::InvalidInput)?;
        let value = match text(&request, "kind", 16)? {
            "ticker" => {
                if !options {
                    return Err(Status::InvalidInput);
                }
                native
                    .option_ticker(contract)
                    .map_err(|_| Status::InternalFailure)?
            }
            "active" => native
                .stream_active(contract)
                .map_err(|_| Status::InternalFailure)?
                .into(),
            "market" | "statistics" => {
                if options {
                    return Err(Status::InvalidInput);
                }
                native
                    .current_snapshot(contract, request["kind"].as_str() == Some("statistics"))
                    .map_err(|_| Status::InternalFailure)?
            }
            "binding" => native
                .contract_binding(contract, options)
                .map_err(|_| Status::InternalFailure)?,
            "quote-risk" | "trade-risk" => {
                if !options {
                    return Err(Status::InvalidInput);
                }
                let day = request["date"]
                    .as_i64()
                    .filter(|d| (0..=3652058).contains(d))
                    .ok_or(Status::InvalidInput)?;
                native
                    .canonical_risk(
                        contract,
                        day as i32,
                        request["kind"].as_str() == Some("trade-risk"),
                    )
                    .map_err(|_| Status::InternalFailure)?
            }
            "price" => {
                let maximum = request["maximum_age_ticks"]
                    .as_i64()
                    .filter(|age| *age > 0)
                    .ok_or(Status::InvalidInput)?;
                match native.contract_price(contract, options, maximum) {
                    Ok((found, price)) => rmpv::Value::Array(vec![
                        found.into(),
                        price
                            .map(crate::messaging::codec::decimal_bits)
                            .transpose()
                            .map_err(|_| Status::InternalFailure)?
                            .unwrap_or(rmpv::Value::Nil),
                        rmpv::Value::Nil,
                    ]),
                    Err(error) if error == "InvalidFuturesOptionQuote" => {
                        rmpv::Value::Array(vec![true.into(), rmpv::Value::Nil, error.into()])
                    }
                    Err(_) => return Err(Status::InternalFailure),
                }
            }
            _ => return Err(Status::InvalidInput),
        };
        let bytes = crate::messaging::codec::encode(&value).map_err(|_| Status::InternalFailure)?;
        if bytes.len() > 4096 {
            return Err(Status::Capacity);
        }
        // SAFETY: The validated scalar belongs to the caller, independently of sizing.
        unsafe {
            required.write(bytes.len() as u32);
        }
        if (capacity as usize) < bytes.len() {
            return Err(Status::Capacity);
        }
        // SAFETY: Full caller output capacity was validated before copying.
        unsafe {
            std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
        }
        Ok(())
    })
}

/// Changes one copied transient stream-owner identity in native memory.
/// Physical datasets remain independently owned and cancellable.
///
/// # Safety
/// Input supplies length readable bytes (1..4096); changed is aligned writable
/// one-byte storage. Invalid output is rejected before any ownership mutation.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_owner_route_v1(
    handle: u64,
    input: *const u8,
    length: u32,
    changed: *mut u8,
) -> i32 {
    boundary(|| {
        if length > 4096 || !valid(changed) {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Bounded caller-readable command, copied before any native work.
        let value = unsafe { json(input, length) }?;
        let options = value["options"].as_bool().ok_or(Status::InvalidInput)?;
        let acquire = value["acquire"].as_bool().ok_or(Status::InvalidInput)?;
        let (owner, _) = monitored_identity(&value)?;
        let result = owner_instance_route(
            handle,
            owner,
            text(&value, "contract", 1024)?,
            options,
            acquire,
        )?;
        // SAFETY: Output was validated before mutation and remains caller-owned.
        unsafe {
            changed.write(u8::from(result));
        }
        Ok(())
    })
}
fn owner_instance_route(
    handle: u64,
    identity: u128,
    contract: &str,
    options: bool,
    acquire: bool,
) -> Result<bool, Status> {
    owner(handle)?
        .change_stream_route(identity, contract, options, acquire)
        .map_err(|_| Status::InvalidInput)
}

#[cfg(test)]
mod monitored_identity_tests {
    use super::*;
    #[test]
    fn business_identity_is_stable_unambiguous_and_contract_scoped() {
        let input = serde_json::json!({"workflow_type":"a","workflow_id":"bc","leg_id":"leg","contract":"ES-C"});
        let first = monitored_identity(&input).unwrap();
        assert_ne!(first.0, 0);
        assert!(first.1.len() <= 128);
        assert_eq!(first, monitored_identity(&input).unwrap());
        let mut other = input.clone();
        other["workflow_type"] = "ab".into();
        other["workflow_id"] = "c".into();
        let second = monitored_identity(&other).unwrap();
        assert_ne!(first.0, second.0);
        assert_eq!(first.1, second.1);
        other["contract"] = "ES-P".into();
        let third = monitored_identity(&other).unwrap();
        assert_ne!(first.0, third.0);
        assert_ne!(first.1, third.1);
    }
    #[test]
    fn incomplete_or_oversized_identity_is_rejected_before_mutation() {
        for key in ["workflow_type", "workflow_id", "leg_id", "contract"] {
            for invalid in [
                Value::Null,
                Value::String(" ".into()),
                Value::String("x".repeat(1025)),
                serde_json::json!(7),
            ] {
                let mut input = serde_json::json!({"workflow_type":"a","workflow_id":"b","leg_id":"c","contract":"d"});
                input[key] = invalid;
                assert!(monitored_identity(&input).is_err());
            }
        }
    }
}
