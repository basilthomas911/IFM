//! Native public recovery command authority. Request identity, stale-owner
//! fencing, idempotence and the original deadline live outside managed code.
use super::*;
use crate::messaging::{codec, wire::guid};
use rmpv::Value;
/// Complete copied recovery command; it owns no managed callback or resource.
#[derive(Clone)]
pub struct Request {
    /// Explicit role/dataset lane.
    pub lane: Lane,
    /// Business command identity in CLR Guid byte order.
    pub operation: [u8; 16],
    /// Expected actual source owner, never a process PID substitute.
    pub worker: [u8; 16],
    /// Expected actual source generation.
    pub generation: [u8; 16],
    /// Explicit API ValueDate.
    pub date: time::Date,
}
impl MarketData {
    fn desired_date(&self, lane: Lane) -> Option<time::Date> {
        if lane == Lane::OptionsGlbx {
            self.options.control.intent().map(|i| i.date)
        } else {
            self.futures
                .get(&lane)
                .and_then(|f| f.control.intent())
                .map(|i| i.date)
        }
    }
    /// Accepts recovery before returning from the FFI command. Cancelling the
    /// caller's subsequent observer cannot erase accepted native recovery.
    ///
    /// # Errors
    /// Invalid command identity, date mismatch or unavailable native control.
    pub fn begin_recovery(&self, request: &Request) -> Result<Option<String>, String> {
        if request.operation == [0; 16] {
            return Err("Native recovery requires an operation identity".into());
        }
        let date = self
            .desired_date(request.lane)
            .or_else(|| self.desired_date(Lane::FuturesGlbx))
            .or_else(|| self.desired_date(Lane::FuturesXcbf));
        if date != Some(request.date) {
            return Ok(Some(
                "Recovery ValueDate differs from native desired intent".into(),
            ));
        }
        let identity = self.publication_identity(request.lane);
        let (worker, generation) =
            identity.map_or(([0; 16], [0; 16]), |i| (i.worker, i.generation));
        if worker != request.worker || generation != request.generation {
            return Ok(None);
        }
        let mut commands = self
            .recovery_commands
            .lock()
            .map_err(|_| "Native recovery command authority interrupted")?;
        if commands.get(&request.lane) == Some(&request.operation) {
            return Ok(None);
        }
        commands.insert(request.lane, request.operation);
        // The native supervisor preserves an earlier incident deadline. No
        // external request deadline, C# retry loop or later observer renews it.
        if let Err(error) = self.report_fault(
            request.lane,
            "Requested native dataset recovery",
            Deadline::recovery(),
        ) {
            return Ok(Some(error));
        }
        Ok(None)
    }
    /// Observes the accepted native incident and returns the existing public
    /// result shape. Recovery means provider connectivity; full financial
    /// readiness remains explicit in LiveFeedEnabled. A stale owner joins the
    /// current native connection; it never
    /// replaces the newer owner. Failed cleanup is not falsely certified.
    ///
    /// # Errors
    /// Interrupted native encoding/state; ordinary recovery failure is a result.
    pub async fn recovery_result(
        &self,
        request: &Request,
        rejected: Option<String>,
    ) -> Result<Value, String> {
        let outcome = match rejected {
            Some(error) => Err(error),
            None => self.wait_connected(request.lane, false).await,
        };
        let health = self.health(request.lane).status();
        let recovered = outcome.is_ok()
            && (health.phase == Phase::Running || self.health(request.lane).connection_alive());
        let monitoring_ready = recovered && health.phase == Phase::Running;
        let error = outcome.err().or_else(|| {
            if recovered {
                None
            } else {
                health.latest_error.or(health.root_error)
            }
        });
        let identity = self.publication_identity(request.lane);
        let (worker, generation) =
            identity.map_or(([0; 16], [0; 16]), |i| (i.worker, i.generation));
        let value = Value::Array(vec![
            guid(request.operation).into(),
            request.lane.role().into(),
            request.lane.dataset().into(),
            recovered.into(),
            recovered.into(),
            (monitoring_ready && self.has_desired(request.lane)).into(),
            error.map_or(Value::Nil, Into::into),
            guid(worker).into(),
            guid(generation).into(),
        ]);
        if codec::encode(&value)?.len() > 8192 {
            return Err("Native recovery result capacity exceeded".into());
        }
        Ok(value)
    }
}
