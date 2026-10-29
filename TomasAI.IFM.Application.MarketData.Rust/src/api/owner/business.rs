//! Native command semantics for committed handoff and explicit logical release.
//! Resource ownership never leaves dataset supervisors; observer cancellation
//! cannot roll back an accepted command or retain a connection/SDK task.
use super::*;
use serde_json::Value;
/// Copied logical release contract, unrelated to physical resource lifetime.
pub(crate) struct Release {
    scope: String,
    owner: u128,
    generation: [u8; 16],
    ownership: Option<Value>,
}
fn bytes(value: &Value) -> Result<[u8; 16], String> {
    let row = value
        .as_array()
        .filter(|row| row.len() == 16)
        .ok_or("OwnershipInvalid")?;
    let mut bytes = [0; 16];
    for (out, value) in bytes.iter_mut().zip(row) {
        *out = u8::try_from(value.as_u64().ok_or("OwnershipInvalid")?)
            .map_err(|_| "OwnershipInvalid")?;
    }
    Ok(bytes)
}
impl Release {
    pub(crate) fn parse(value: &Value) -> Result<Self, String> {
        if value["dataset"].as_str() != Some("GLBX.MDP3") {
            return Err("InvalidDataset".into());
        }
        let scope = value["scope"]
            .as_str()
            .filter(|scope| !scope.trim().is_empty() && scope.encode_utf16().count() <= 128)
            .ok_or("OwnershipInvalid")?
            .to_owned();
        let owner = u128::from_le_bytes(bytes(&value["lease"])?);
        let generation = bytes(&value["generation"])?;
        let ownership = (!value["ownership"].is_null()).then(|| value["ownership"].clone());
        if (owner == 0) != ownership.is_some() {
            return Err("OwnershipInvalid".into());
        }
        Ok(Self {
            scope,
            owner,
            generation,
            ownership,
        })
    }
}
pub(crate) fn result(active: bool, failure: Option<&str>) -> rmpv::Value {
    rmpv::Value::Array(vec![
        active.into(),
        failure.map_or(rmpv::Value::Nil, |code| {
            rmpv::Value::Array(vec![
                code.into(),
                "CompositionOwnership".into(),
                "".into(),
                "Native committed ownership is unavailable or changed.".into(),
                false.into(),
            ])
        }),
        rmpv::Value::Nil,
    ])
}
impl MarketData {
    /// Accepts one complete committed refresh before any observer wait.
    ///
    /// # Errors
    /// Missing/closed configured business source or exhausted native control.
    pub fn reconcile_business(&self) -> Result<bool, String> {
        if !self.options.control.has_intent() {
            return Ok(false);
        }
        let Some(claims) = self.option_claims() else {
            return Ok(false);
        };
        claims.refresh_committed()?;
        Ok(true)
    }
    /// Waits on native fact/health notifications until conditional realization
    /// completes. Observation cannot retain a provider resource or extend recovery.
    ///
    /// # Errors
    /// Closed/failed dataset, original deadline expiry or invalid copied facts.
    pub async fn wait_committed_state(&self) -> Result<rmpv::Value, String> {
        let health = self.health(Lane::OptionsGlbx);
        let mut changes = health.subscribe();
        let mut facts = None;
        let deadline = health.status().deadline.unwrap_or_else(Deadline::recovery);
        loop {
            let status = health.status();
            if status.phase == Phase::Failed {
                return Err(status
                    .latest_error
                    .or(status.root_error)
                    .unwrap_or("Native committed recovery failed".into()));
            }
            if status.phase == Phase::Stopped {
                return Err("Native committed owner stopped".into());
            }
            let Some(background) = self
                .options
                .control
                .intent()
                .and_then(|intent| intent.background.clone())
            else {
                return Ok(rmpv::Value::Nil);
            };
            if facts.is_none() {
                facts = Some(background.committed_changes()?);
            }
            if background.committed_current()? && status.phase == Phase::Running {
                return self.committed_state();
            }
            deadline.run(&tokio_util::sync::CancellationToken::new(),async{tokio::select!{biased;result=changes.changed()=>result.map_err(|_|"Native committed health ended"),result=async{match &mut facts{Some(changes)=>changes.changed().await,None=>std::future::pending().await}}=>result.map_err(|_|"Native committed source ended")}}).await.map_err(|_|"Native committed original deadline expired")??;
        }
    }
    /// Returns a detached typed rejection immediately, or accepts logical cleanup
    /// and leaves physical reconstruction to native dataset control.
    pub(crate) fn begin_business_release(
        &self,
        release: &Release,
    ) -> Result<Option<rmpv::Value>, String> {
        let Some(claims) = self.option_claims() else {
            return Ok(Some(result(false, None)));
        };
        let background = self
            .options
            .control
            .intent()
            .and_then(|intent| intent.background.clone());
        if let Some(ownership) = &release.ownership {
            let Some(background) = background else {
                return Ok(Some(result(false, Some("OwnershipNotReady"))));
            };
            let Some(book) = background.committed_book()? else {
                return Ok(Some(result(false, Some("OwnershipNotReady"))));
            };
            if !background.committed_current()?
                || self.health(Lane::OptionsGlbx).status().phase != Phase::Running
                || self
                    .publication_identity(Lane::OptionsGlbx)
                    .is_none_or(|header| header.generation != release.generation)
            {
                return Ok(Some(result(false, Some("Recovering"))));
            }
            return Ok(Some(
                match book.validate_ownership(&release.scope, ownership) {
                    Ok(active) => result(active, None),
                    Err(code) => result(
                        false,
                        Some(if code == "OwnershipConflict" {
                            "OwnershipConflict"
                        } else {
                            "OwnershipInvalid"
                        }),
                    ),
                },
            ));
        }
        let owns = claims.owned_scope(release.owner, &release.scope)?.is_some()
            || background.as_ref().map_or(Ok(false), |background| {
                background.owns_scope_name(release.owner, &release.scope)
            })?;
        if !owns {
            return Ok(Some(self.finish_business_release(release)?));
        }
        claims.disable(release.owner)?;
        Ok(None)
    }
    pub(crate) fn finish_business_release(&self, release: &Release) -> Result<rmpv::Value, String> {
        let explicit = self
            .option_claims()
            .map(|claims| claims.active_scopes())
            .transpose()?
            .unwrap_or_default()
            .iter()
            .any(|(scope, _)| scope == &release.scope);
        let deferred = self
            .options
            .control
            .intent()
            .and_then(|intent| intent.background.clone())
            .map(|background| background.resolved_scope(&release.scope, None))
            .transpose()?
            .flatten()
            .is_some();
        Ok(result(explicit || deferred, None))
    }
}
