//! Native browser command/result boundary. Managed callers supply copied query
//! fields only. Native dataset control owns view lifetimes, sources and recovery.
use super::*;
use crate::{
    messaging::codec,
    option_chain::{browser::Command, composition::capture::Request},
};
use rmpv::Value;
use tokio_util::sync::CancellationToken;
fn reply(model: Value, code: i32, message: Option<&str>) -> Value {
    Value::Array(vec![
        model,
        code.into(),
        message.map_or(Value::Nil, Value::from),
    ])
}
fn rejected(message: &str) -> Value {
    reply(Value::Nil, 503, Some(message))
}
impl MarketData {
    /// Accepts native view demand synchronously before an observer can await I/O.
    ///
    /// # Errors
    /// No configured native options source or conflicting/bounded view demand.
    pub fn begin_browser(&self, command: Command) -> Result<(), String> {
        let live = self
            .contract_price(&command.family.contract, false, 9_000_000_000)?
            .1;
        self.option_claims()
            .ok_or("Native browser source is unavailable")?
            .browser(command, live)
    }
    /// Copies an evaluated browser result after current-generation native setup.
    /// Cancellation only abandons observation; native view demand expires itself.
    ///
    /// # Errors
    /// Dataset failure, source replacement, invalid projection or original expiry.
    pub async fn browser_result(
        &self,
        command: &Command,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Value, String> {
        self.wait_ready(Lane::OptionsGlbx, false).await?;
        if command.release {
            return Ok(reply(
                empty(command, self.clock_ticks()?, "Released")?,
                0,
                None,
            ));
        }
        let background = self
            .options
            .control
            .intent()
            .and_then(|intent| intent.background.clone())
            .ok_or("Native browser source ended")?;
        let Some(resolved) = background.browser_result(&command.family)? else {
            return Ok(rejected("Native browser scope is unavailable"));
        };
        let live = self
            .contract_price(&command.family.contract, false, 9_000_000_000)?
            .1;
        let selected =
            background.select_browser(&resolved, live, self.clock_ticks()?, stop, deadline)?;
        if selected.window.rows.is_empty() {
            return Ok(rejected(
                if selected.window.method == "WindowInputsUnavailable" {
                    "Neither a qualified expiry IV nor current Bollinger window inputs are available."
                } else {
                    "No contracts are available in the selected strike window."
                },
            ));
        }
        let physical = resolved
            .selection
            .buffered
            .iter()
            .copied()
            .collect::<std::collections::BTreeSet<_>>();
        if selected
            .window
            .rows
            .iter()
            .any(|index| !physical.contains(index))
        {
            return Ok(rejected("Native browser window changed during capture"));
        }
        let generation = self
            .publication_identity(Lane::OptionsGlbx)
            .ok_or("Native browser generation unavailable")?
            .generation;
        let request = Request {
            id: rand::random(),
            scope: resolved.demand.scope.clone(),
            horizon: "Daily".into(),
            generation,
            at: -621355968000000000,
            until: self
                .clock_ticks()?
                .checked_add(1_200_000_000)
                .ok_or("Native browser time overflow")?,
            maximum: 2048,
            age: 5000,
            skew: 1000,
            options: true,
            selection: true,
            allow_missing: true,
            risk: resolved.command.required.clone(),
        };
        let snapshot = self
            .capture_options_ready(request, stop, deadline)
            .await
            .map_err(|failure| {
                format!("Native browser capture {}: {}", failure.code, failure.input)
            })?;
        background.observe_browser_iv(&command.family, &snapshot, selected.price)?;
        // The current public API returns the complete buffered snapshot, not
        // only the narrower window rows. Preserve that observable UI contract.
        let models = snapshot
            .instruments
            .iter()
            .map(project)
            .collect::<Result<Vec<_>, _>>()?;
        if models.len() != resolved.models.len() {
            return Ok(rejected("Native browser snapshot membership is incomplete"));
        }
        let mut model = empty(command, snapshot.at, &selected.window.method)?
            .as_array()
            .ok_or("Invalid browser model")?
            .clone();
        model[2] = selected.price.map_or(Ok(Value::Nil), codec::decimal_bits)?;
        model[3] = selected
            .window
            .lower
            .map_or(Ok(Value::Nil), codec::decimal_bits)?;
        model[4] = selected
            .window
            .upper
            .map_or(Ok(Value::Nil), codec::decimal_bits)?;
        model[7] = Value::Array(models);
        Ok(reply(Value::Array(model), 0, None))
    }
}
fn empty(command: &Command, at: i64, method: &str) -> Result<Value, String> {
    Ok(Value::Array(vec![
        command.family.contract.clone().into(),
        command.family.expiry.into(),
        Value::Nil,
        Value::Nil,
        Value::Nil,
        method.into(),
        codec::utc_offset(at)?,
        Value::Array(vec![]),
        Value::Nil,
        Value::Nil,
        Value::Nil,
        "Waiting for IV observation".into(),
        Value::Nil,
        Value::Nil,
        Value::Nil,
        Value::Nil,
        Value::Nil,
        Value::Nil,
        Value::Nil,
    ]))
}
fn instant(value: Option<i64>) -> Result<Value, String> {
    Ok(value.map_or(Ok(Value::Nil), codec::utc_offset)?)
}
fn project(item: &crate::option_chain::composition::model::Valued) -> Result<Value, String> {
    let instrument = &item.instrument;
    let quote = instrument.quote.as_ref().map(|quote| quote.quote);
    let risk = item.valuation.as_ref().map(|risk| risk.values);
    let selection = instrument
        .selection
        .as_ref()
        .map(|selection| selection.value())
        .transpose()?;
    let selected = |index: usize| {
        selection
            .as_ref()
            .and_then(Value::as_array)
            .and_then(|fields| fields.get(index))
            .cloned()
            .unwrap_or(Value::Nil)
    };
    let number = |risk: Option<f64>, index| risk.map_or_else(|| selected(index), Value::F64);
    let size =
        |amount: Option<crate::option_chain::qualification::Decimal>| -> Result<Value, String> {
            let Some(amount) = amount else {
                return Ok(Value::Nil);
            };
            let units =
                crate::reference::decimal::scaled_28(amount).ok_or("Invalid browser quote size")?;
            Ok(u32::try_from(units / 10i128.pow(28))
                .map_err(|_| "Invalid browser quote size")?
                .into())
        };
    Ok(Value::Array(vec![
        instrument.contract.clone().into(),
        codec::decimal_bits(instrument.strike.ok_or("Missing browser strike")?)?,
        instrument.call.ok_or("Missing browser option side")?.into(),
        quote
            .map(|quote| quote.bid)
            .map_or(Ok(Value::Nil), codec::decimal_bits)?,
        quote
            .map(|quote| quote.ask)
            .map_or(Ok(Value::Nil), codec::decimal_bits)?,
        size(quote.map(|quote| quote.bid_size))?,
        size(quote.map(|quote| quote.ask_size))?,
        Value::Nil,
        Value::Nil,
        number(risk.map(|risk| risk.volatility), 2),
        number(risk.map(|risk| risk.price), 0),
        number(risk.map(|risk| risk.delta), 1),
        risk.map(|risk| Value::F64(risk.gamma))
            .unwrap_or(Value::Nil),
        risk.map(|risk| Value::F64(risk.vega)).unwrap_or(Value::Nil),
        risk.map(|risk| Value::F64(risk.theta))
            .unwrap_or(Value::Nil),
        risk.map(|risk| Value::F64(risk.rho)).unwrap_or(Value::Nil),
        instrument.volume.map_or(Value::Nil, Value::from),
        instrument.interest.map_or(Value::Nil, Value::from),
        risk.is_some().into(),
        quote.is_none().into(),
        instant(quote.map(|quote| quote.event))?,
        Value::Nil,
        if selection.is_some() {
            selected(6)
        } else {
            instant(risk.and(quote).map(|quote| quote.received))?
        },
        instrument.volume_date.map_or(Value::Nil, Value::from),
        instrument.interest_date.map_or(Value::Nil, Value::from),
        instrument.selection.is_some().into(),
        instrument
            .unavailable
            .clone()
            .map_or(Value::Nil, Value::from),
        instant(instrument.last_event)?,
        instant(instrument.last_received)?,
    ]))
}
