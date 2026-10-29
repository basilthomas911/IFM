//! Copied status and explicit reconnect queries. No publication queue, resource
//! ownership or recovery timer is created by observing the native dataset owner.
use super::*;
impl MarketData {
    /// Copies authoritative backend status without managed epoch/admission state.
    /// Idle health is separate from live demand; no process identity is invented.
    /// Ready is the copied Host RPC subscription state, not a dataset proof.
    ///
    /// # Errors
    /// Interrupted native state, invalid clock or overflowing protocol counters.
    pub fn backend_status(&self, ready: bool) -> Result<rmpv::Value, String> {
        use crate::messaging::{codec, wire::guid};
        use rmpv::Value;
        let now = self
            .clock
            .utc_ticks()
            .map_err(|_| "Native status clock unavailable")?;
        let mut rows = Vec::new();
        let mut signature = vec![Value::from(ready)];
        let mut date = None;
        for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf, Lane::OptionsGlbx] {
            let health = self.health(lane).status();
            let desired = if lane == Lane::OptionsGlbx {
                self.options.control.intent().map(|i| i.date)
            } else {
                self.futures
                    .get(&lane)
                    .and_then(|f| f.control.intent())
                    .map(|i| i.date)
            };
            if let Some(desired) = desired {
                if date.is_some_and(|d| d != desired) {
                    return Err("Native active dataset ValueDates disagree".into());
                }
                date = Some(desired);
            }
            let identity = self.publication_identity(lane);
            let (worker, generation, revision) =
                identity.as_ref().map_or(([0; 16], [0; 16], 1), |h| {
                    (h.worker, h.generation, h.revision)
                });
            let connected = self.health(lane).connection_alive();
            let healthy = health.phase == Phase::Running || connected;
            let lifecycle = match health.phase {
                Phase::Stopped => "Stopped",
                Phase::Starting if connected => "Healthy",
                Phase::Starting => "Starting",
                Phase::Recovering => "Recovering",
                Phase::Failed => "StoppedError",
                Phase::Running if desired.is_some() => "Healthy",
                Phase::Running => "Idle",
            };
            let error = health.latest_error.clone().or(health.root_error.clone());
            let deadline = if matches!(health.phase, Phase::Starting | Phase::Recovering) {
                health
                    .deadline
                    .and_then(|d| i64::try_from(d.remaining().as_nanos() / 100).ok())
                    .and_then(|left| now.checked_add(left))
                    .map(codec::utc_offset)
                    .transpose()?
            } else {
                None
            };
            let row = Value::Array(vec![
                lane.role().into(),
                lane.dataset().into(),
                guid(worker).into(),
                guid(generation).into(),
                std::process::id().into(),
                (desired.is_some() && healthy).into(),
                healthy.into(),
                lifecycle.into(),
                error.clone().map_or(Value::Nil, Into::into),
                desired.map_or(Value::Nil, |d| Value::from(d.to_julian_day() - 1721426)),
                guid([0; 16]).into(),
                revision.into(),
                codec::utc_offset(now)?,
                deadline.unwrap_or(Value::Nil),
            ]);
            signature.push(Value::Array(vec![
                (lane as u32).into(),
                desired.map_or(Value::Nil, |d| Value::from(d.to_julian_day())),
                lifecycle.into(),
                guid(worker).into(),
                guid(generation).into(),
                revision.into(),
                Value::Array(health.ticket.parts().into_iter().map(Into::into).collect()),
                error.map_or(Value::Nil, Into::into),
            ]));
            rows.push(row);
        }
        let encoded = codec::encode(&Value::Array(signature))?;
        let revision = {
            let mut prior = self
                .status_sequence
                .lock()
                .map_err(|_| "Native status observation interrupted")?;
            if prior.0 != encoded {
                prior.1 = prior
                    .1
                    .checked_add(1)
                    .ok_or("Native status revision overflow")?;
                prior.0 = encoded;
            }
            prior.1
        };
        Ok(Value::Array(vec![
            ready.into(),
            date.map_or(Value::Nil, |d| Value::from(d.to_julian_day() - 1721426)),
            Value::Array(rows),
            guid(self.host_identity).into(),
            revision.into(),
            Value::Nil,
            Value::Nil,
            Value::Nil,
            1.into(),
            self.audit
                .lock()
                .ok()
                .and_then(|audit| audit.as_ref().map(|sink| sink.snapshot()))
                .unwrap_or(Value::Nil),
        ]))
    }
    /// Complete bounded startup/reconnect query in the existing public DTO shape.
    /// Rust owns membership, catalog mapping, current-generation fencing and
    /// completeness. This explicit query creates no managed streaming consumer.
    ///
    /// # Errors
    /// Interrupted state, concurrent replacement, malformed native metadata or
    /// complete copied result exceeding 16 MiB; partial snapshots are not returned.
    pub fn read_snapshot(&self, ready: bool) -> Result<rmpv::Value, String> {
        use crate::messaging::{
            codec,
            wire::{Frame, guid},
        };
        use rmpv::Value;
        let lanes = [Lane::FuturesGlbx, Lane::FuturesXcbf, Lane::OptionsGlbx];
        let stamp = |lane| {
            let health = self.health(lane).status();
            (
                health.ticket,
                health.phase,
                self.has_desired(lane),
                self.publication_identity(lane)
                    .map(|h| (h.worker, h.generation, h.revision)),
            )
        };
        let tickets = lanes.map(stamp);
        let status = self.backend_status(ready)?;
        let mut copied_bytes = codec::encode(&status)?.len() + 4096;
        let reserve = |used: &mut usize, additional: usize| -> Result<(), String> {
            *used = used
                .checked_add(additional)
                .ok_or("Native complete snapshot size overflow")?;
            if *used > 16 * 1024 * 1024 {
                return Err("Complete native backend catch-up capacity exceeded".into());
            }
            Ok(())
        };
        let mut manifests = Vec::new();
        let mut contracts = Vec::new();
        let mut current = Vec::new();
        for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf] {
            if self.health(lane).status().phase != Phase::Running || !self.has_desired(lane) {
                continue;
            }
            let factory = &self
                .futures
                .get(&lane)
                .ok_or("Unknown native futures lane")?
                .factory;
            let (Some(models), Some(identity), Some(values)) = (
                factory.public_models(),
                factory.identity(),
                factory.current(),
            ) else {
                continue;
            };
            if models.is_empty() || models.len() > 16 {
                return Err("Public manifest complete contract capacity exceeded".into());
            }
            reserve(
                &mut copied_bytes,
                codec::encode(&Value::Array(models.as_ref().clone()))?.len() + models.len() * 512,
            )?;
            let mut rows = Vec::new();
            for model in models.iter() {
                let fields = model
                    .as_array()
                    .filter(|v| v.len() == 32)
                    .ok_or("Invalid native public futures model")?;
                rows.push(Value::Array(vec![
                    fields[0].clone(),
                    fields[3].clone(),
                    1.into(),
                    fields[2].clone(),
                    lane.dataset().into(),
                    fields[9].clone(),
                    fields[10].clone(),
                ]));
            }
            rows.sort_by(|a, b| {
                a.as_array().unwrap()[0]
                    .as_str()
                    .cmp(&b.as_array().unwrap()[0].as_str())
            });
            manifests.push(Value::Array(vec![
                lane.dataset().into(),
                identity.value_date.into(),
                identity.revision.into(),
                Value::Array(rows),
            ]));
            contracts.extend(models.iter().cloned());
            let frames = values.capture_all(16 * 1024 * 1024 - copied_bytes)?;
            reserve(
                &mut copied_bytes,
                frames.iter().map(|f| f.payload.len() + 256).sum(),
            )?;
            current.extend(
                frames
                    .iter()
                    .map(Frame::value)
                    .collect::<Result<Vec<_>, _>>()?,
            );
        }
        let now = self
            .clock
            .utc_ticks()
            .map_err(|_| "Native snapshot clock unavailable")?;
        let options = self.options_current();
        let mut caches = Vec::new();
        let mut observations = Vec::new();
        if let Some(view) = options.as_ref() {
            // Match LatestOptionObservations: catch-up contains quote/risk marks
            // only. Durable trade evidence (kind 6) has a different DTO and must
            // never be replayed through the quote-only restoration contract.
            let frames = view
                .current
                .capture_kind(7, 16 * 1024 * 1024 - copied_bytes)?;
            reserve(
                &mut copied_bytes,
                frames.iter().map(|f| f.payload.len() + 256).sum(),
            )?;
            observations = frames
                .iter()
                .map(Frame::value)
                .collect::<Result<Vec<_>, _>>()?;
            let published = view
                .caches
                .lock()
                .map_err(|_| "Native cache catch-up interrupted")?;
            for (_, publication) in published.iter().filter(|(_, p)| p.until > now) {
                reserve(&mut copied_bytes, publication.len() + 256)?;
                caches.push(
                    rmpv::decode::read_value(&mut publication.content())
                        .map_err(|_| "Invalid native retained cache publication")?,
                );
            }
        }
        let scopes = self
            .option_claims()
            .map(|c| c.active_scopes())
            .transpose()?
            .unwrap_or_default();
        let health = self.health(Lane::OptionsGlbx).status();
        let identity = self.publication_identity(Lane::OptionsGlbx);
        // Resource proof belongs to the native supervisor, independently of
        // whether current financial inputs produce an executable candidate.
        // Loaded-trade risk/plan checks remain at the application boundary.
        let resources_ready = self.has_desired(Lane::OptionsGlbx) && health.phase == Phase::Running;
        let mut owned_scopes = scopes
            .iter()
            .map(|(id, _)| (id.clone(), resources_ready && options.is_some()))
            .collect::<std::collections::BTreeMap<_, _>>();
        if let Some(intent) = self.options.control.intent()
            && let Some(background) = &intent.background
        {
            for demand in background.scope_demands()? {
                owned_scopes
                    .entry(demand.scope)
                    .or_insert(resources_ready && options.is_some());
            }
            for monitor in background.monitors() {
                owned_scopes
                    .entry(monitor.scope)
                    .or_insert(resources_ready && options.is_some());
            }
        }
        if let Some(view) = options
            .as_ref()
            .filter(|_| self.has_desired(Lane::OptionsGlbx))
        {
            for cache in view.cache_scopes.iter() {
                owned_scopes
                    .entry(cache.scope.clone())
                    .or_insert(resources_ready);
            }
        }
        if owned_scopes.len() > 256 {
            return Err("Complete native workload scope capacity exceeded".into());
        }
        let all_scopes = owned_scopes.keys().cloned().map(Value::from).collect();
        let ready_scopes = owned_scopes
            .iter()
            .filter(|(_, ready)| **ready)
            .map(|(id, _)| Value::from(id.clone()))
            .collect();
        // OptionScopes means every option scope, including prepared background
        // lookups. It is not the subset with individually monitored trade legs.
        let option_scopes = owned_scopes
            .keys()
            .filter(|id| !id.starts_with("underlying-"))
            .cloned()
            .map(Value::from)
            .collect();
        let workload = Value::Array(vec![
            guid(identity.map_or([0; 16], |i| i.generation)).into(),
            codec::utc_offset(now)?,
            (self.has_desired(Lane::OptionsGlbx) && health.phase != Phase::Running).into(),
            Value::Array(all_scopes),
            Value::Array(ready_scopes),
            Value::Array(option_scopes),
            1.into(),
            resources_ready.into(),
        ]);
        if lanes
            .into_iter()
            .zip(tickets)
            .any(|(lane, ticket)| stamp(lane) != ticket)
        {
            return Err("Native catch-up owner changed during capture".into());
        }
        let result = Value::Array(vec![
            status,
            Value::Array(manifests),
            Value::Array(contracts),
            Value::Array(current),
            Value::Array(caches),
            Value::Array(vec![]),
            Value::Array(observations),
            workload,
        ]);
        if codec::encode(&result)?.len() > 16 * 1024 * 1024 {
            return Err("Complete native backend catch-up capacity exceeded".into());
        }
        Ok(result)
    }
}
