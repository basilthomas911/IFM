//! Paged option-reference editor. Cursors are opaque and symbol scoped. Provider
//! identities/evidence and exact strikes survive edits without reconstruction.
use crate::{
    backend::{
        contracts::*,
        identity::{
            decimal_to_legacy_double, exact_option_strike as exact_strike, option_contract_id,
            option_strike,
        },
        schema::Registry,
        transport::Backend,
    },
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    lookup_editor::EditorOutcome,
    services::{actor_requests, reference},
    state::futures_contract::{review_fields, wire_offset},
};
use chrono::Datelike;
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
const ROOT: &str = "TomasAI.IFM.Domain.MarketData.Shared.";
const MODEL: &str =
    "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel";
const REVIEW: [(&str, &str); 16] = [
    ("reviewState", "ReviewState"),
    ("mappingVersion", "MappingVersion"),
    ("evidenceId", "EvidenceId"),
    ("effectiveFromUtc", "EffectiveFromUtc"),
    ("effectiveUntilUtc", "EffectiveUntilUtc"),
    ("exchangeTimeZoneId", "ExchangeTimeZoneId"),
    ("calendarVersion", "CalendarVersion"),
    ("lastTradingUtc", "LastTradingUtc"),
    ("settlementStyle", "SettlementStyle"),
    ("exerciseStyle", "ExerciseStyle"),
    ("premiumStyle", "PremiumStyle"),
    ("premiumTickRule", "PremiumTickRule"),
    ("tickRuleVersion", "TickRuleVersion"),
    ("dayCount", "DayCount"),
    ("exerciseCutoffUtc", "ExerciseCutoffUtc"),
    ("exerciseResultContractId", "ExerciseResultContractId"),
];
fn text(value: &Json) -> String {
    value.as_str().unwrap_or_default().into()
}
fn expiry(value: &Json) -> Result<chrono::NaiveDate, String> {
    chrono::NaiveDate::parse_from_str(
        value["ContractMonth"]
            .as_str()
            .ok_or("Missing option expiry")?,
        "%Y-%m-%d",
    )
    .map_err(|_| "Invalid option expiry".into())
}
pub fn display_contract(registry: &Registry, wire: &Value) -> Result<ContractRow, String> {
    let value = registry.project(MODEL, wire)?;
    let mut fields = review_fields(&value);
    for (key, member) in [
        ("contractId", "ContractId"),
        ("description", "Description"),
        ("symbol", "Symbol"),
        ("localSymbol", "LocalSymbol"),
        ("securityType", "SecurityType"),
        ("currency", "Currency"),
        ("exchange", "Exchange"),
        ("multiplier", "Multiplier"),
        ("contractMonth", "ContractMonth"),
        ("optionType", "OptionType"),
        ("exerciseStyle", "ExerciseStyle"),
        ("premiumStyle", "PremiumStyle"),
        ("premiumTickRule", "PremiumTickRule"),
        ("tickRuleVersion", "TickRuleVersion"),
        ("dayCount", "DayCount"),
        ("underlyingContractId", "UnderlyingContractId"),
        ("exerciseResultContractId", "ExerciseResultContractId"),
    ] {
        fields.insert(key.into(), text(&value[member]));
    }
    fields.insert(
        "provider".into(),
        if value["SchemaVersion"].as_i64().unwrap_or(0) > 0 {
            "true"
        } else {
            "false"
        }
        .into(),
    );
    fields.insert("strikePrice".into(), exact_strike(&value)?);
    for (key, index) in [
        ("lastTradingUtc", 22),
        ("effectiveFromUtc", 31),
        ("effectiveUntilUtc", 32),
        ("exerciseCutoffUtc", 38),
    ] {
        if let Some(value) = wire.as_array().and_then(|v| v.get(index)) {
            fields.insert(key.into(), wire_offset(value)?);
        }
    }
    let id = value["ContractId"]
        .as_str()
        .filter(|v| !v.is_empty())
        .ok_or("Missing option identity")?
        .into();
    Ok(ContractRow {
        id,
        fields,
        data: None,
    })
}
/// Build a draft using the same user-editable fields as the current hybrid.
/// Provider contracts allow description changes only. Legacy contract identities
/// cannot change after creation; a new identity requires an explicit Add.
pub fn draft(
    registry: &Registry,
    fields: &Json,
    original: Option<&Value>,
    review: bool,
) -> Result<Value, String> {
    let field = |name: &str| fields[name].as_str().unwrap_or_default().trim();
    let old = original.map(|v| registry.project(MODEL, v)).transpose()?;
    let mut changes = BTreeMap::new();
    if review {
        if old.as_ref().and_then(|v| v["SchemaVersion"].as_i64()) != Some(1) {
            return Err(
                "Import a provider definition before reviewing this legacy contract".into(),
            );
        }
        if !field("exchangeTimeZoneId").is_empty()
            && !crate::backend::time_zone::is_known(field("exchangeTimeZoneId"))
        {
            return Err("Select a known exchange time zone".into());
        }
        for (key, member) in REVIEW {
            let empty = field(key).is_empty();
            if member.ends_with("Utc") && !empty {
                let time = chrono::DateTime::parse_from_rfc3339(field(key))
                    .map_err(|_| format!("Enter {key} as a UTC timestamp"))?;
                if time.offset().local_minus_utc() != 0 {
                    return Err(format!("Enter {key} as a UTC timestamp"));
                }
            }
            changes.insert(
                member.into(),
                if empty && (member.ends_with("Utc") || member == "ExerciseResultContractId") {
                    Json::Null
                } else {
                    json!(field(key))
                },
            );
        }
    } else {
        changes.insert("Description".into(), json!(field("description")));
        if old
            .as_ref()
            .and_then(|v| v["SchemaVersion"].as_i64())
            .unwrap_or(0)
            == 0
        {
            let date = chrono::NaiveDate::parse_from_str(field("contractMonth"), "%Y-%m-%d")
                .map_err(|_| "Select an option expiry")?;
            let (canonical, strike) = option_strike(field("strikePrice"))?;
            if let Some(old) = &old {
                if old["Symbol"] != field("symbol")
                    || old["ContractMonth"] != date.to_string()
                    || old["OptionType"] != field("optionType")
                    || option_strike(&exact_strike(old)?)?.0 != canonical
                {
                    return Err(
                        "Contract identity cannot be changed. Add a new contract instead.".into(),
                    );
                }
            }
            let id = match &old {
                Some(old) => text(&old["ContractId"]),
                None => option_contract_id(
                    field("symbol"),
                    date,
                    field("optionType"),
                    field("strikePrice"),
                )?,
            };
            changes.insert("ContractId".into(), json!(id));
            for (key, member) in [
                ("symbol", "Symbol"),
                ("localSymbol", "LocalSymbol"),
                ("securityType", "SecurityType"),
                ("currency", "Currency"),
                ("exchange", "Exchange"),
                ("multiplier", "Multiplier"),
                ("contractMonth", "ContractMonth"),
                ("optionType", "OptionType"),
            ] {
                changes.insert(member.into(), json!(field(key)));
            }
            changes.insert("StrikePriceDecimal".into(), json!(strike.as_str()));
            changes.insert(
                "StrikePrice".into(),
                json!(decimal_to_legacy_double(strike.as_str())?),
            );
        }
    }
    let base = original
        .cloned()
        .unwrap_or(registry.encode(MODEL, &BTreeMap::new())?);
    let next = registry.patch(MODEL, &base, &changes)?;
    let value = registry.project(MODEL, &next)?;
    if [
        "ContractId",
        "Symbol",
        "LocalSymbol",
        "SecurityType",
        "Currency",
        "Exchange",
        "Multiplier",
        "OptionType",
    ]
    .iter()
    .any(|key| value[*key].as_str().is_none_or(|v| v.is_empty()))
        || value["ContractMonth"]
            .as_str()
            .is_none_or(|v| v == "0001-01-01")
    {
        return Err("Invalid option contract".into());
    }
    exact_strike(&value)?;
    Ok(next)
}
#[derive(Default)]
pub struct OptionContractController {
    generation: u64,
    loaded: bool,
    symbol: String,
    token: Option<String>,
    restart_attempted: bool,
    models: BTreeMap<String, Value>,
    result: ContractResult,
    history: BTreeMap<String, (Json, EditorOutcome)>,
}
impl OptionContractController {
    async fn page(
        backend: &Backend,
        registry: &Registry,
        symbol: &str,
        token: Option<&str>,
    ) -> Result<(Vec<(ContractRow, Value)>, Option<String>), String> {
        if symbol.is_empty() || token.is_some_and(|v| v.encode_utf16().count() > 16384) {
            return Err("Invalid option page request".into());
        }
        let request = registry.encode(
            &format!("{ROOT}QueryParameters.GetFuturesOptionContractsPageParameter"),
            &BTreeMap::from([
                ("Symbol".into(), symbol.into()),
                ("PageSize".into(), 200.into()),
                (
                    "ContinuationToken".into(),
                    token.map(|v| v.into()).unwrap_or(Value::Nil),
                ),
            ]),
        )?;
        let response = actor_requests::query(
            backend,
            registry,
            &format!("{ROOT}Queries.GetFuturesOptionContractsPageQuery"),
            symbol,
            BTreeMap::from([("Request".into(), request)]),
        )
        .await?;
        let parts = response
            .as_array()
            .filter(|v| v.len() == 2)
            .ok_or("Invalid option page reply")?;
        let items = parts[0]
            .as_array()
            .filter(|v| v.len() <= 1000)
            .ok_or("Invalid option page items")?;
        let next = if parts[1].is_nil() {
            None
        } else {
            Some(
                parts[1]
                    .as_str()
                    .filter(|v| v.encode_utf16().count() <= 16384)
                    .ok_or("Invalid option page cursor")?
                    .to_string(),
            )
        };
        if next.is_some() && items.is_empty() {
            return Err("Option page made no progress; refresh the list".into());
        }
        let mut rows = Vec::new();
        let mut ids = std::collections::BTreeSet::new();
        for wire in items {
            let row = display_contract(registry, wire)?;
            if row.fields["symbol"] != symbol || !ids.insert(row.id.clone()) {
                return Err(
                    "Option page contains an incorrect symbol or duplicate identity".into(),
                );
            }
            rows.push((row, wire.clone()));
        }
        Ok((rows, next))
    }
    fn publish_page(
        &mut self,
        page: (Vec<(ContractRow, Value)>, Option<String>),
        append: bool,
    ) -> Result<(), String> {
        if append
            && (self.models.len() + page.0.len() > 100_000
                || page
                    .0
                    .iter()
                    .any(|(row, _)| self.models.contains_key(&row.id)))
        {
            return Err(
                "Option page overlaps earlier rows or exceeds the browsing bound; refresh the list"
                    .into(),
            );
        }
        if !append {
            self.models.clear();
            self.result.rows.clear();
        }
        for (row, wire) in page.0 {
            self.models.insert(row.id.clone(), wire);
            self.result.rows.push(row);
        }
        self.token = page.1;
        self.result.has_more = self.token.is_some();
        self.loaded = true;
        Ok(())
    }
    async fn first(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        symbol: &str,
    ) -> Result<(), String> {
        let page = if symbol.is_empty() {
            (Vec::new(), None)
        } else {
            Self::page(backend, registry, symbol, None).await?
        };
        self.publish_page(page, false)?;
        self.symbol = symbol.into();
        self.restart_attempted = false;
        self.result.view = "option-contract".into();
        Ok(())
    }
    async fn load(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        requested: &str,
    ) -> Result<(), String> {
        let lookups = reference::load_lookups(backend, registry).await?;
        let mut choices = BTreeMap::new();
        for (key, kind) in [
            ("symbol", "Symbol"),
            ("securityType", "SecurityType"),
            ("currency", "Currency"),
            ("exchange", "Exchange"),
            ("multiplier", "Multiplier"),
            ("optionType", "OptionType"),
        ] {
            choices.insert(
                key.into(),
                lookups
                    .iter()
                    .filter(|v| v.lookup_type_name == kind)
                    .map(|v| v.short_code.clone())
                    .collect::<Vec<_>>(),
            );
        }
        for (key, kind) in [
            ("reviewState", "ReferenceReviewState"),
            ("exerciseStyle", "ReferenceExerciseStyle"),
            ("settlementStyle", "ReferenceSettlementStyle"),
            ("premiumStyle", "ReferencePremiumStyle"),
            ("premiumTickRule", "ReferencePremiumTickRule"),
            ("dayCount", "ReferenceDayCount"),
        ] {
            choices.insert(
                key.into(),
                registry.enum_names(&format!("{ROOT}ViewModels.{kind}"))?,
            );
        }
        let symbols = &choices["symbol"];
        let selected = if requested.is_empty() {
            symbols.first().cloned().unwrap_or_default()
        } else if symbols.iter().any(|s| s == requested) {
            requested.into()
        } else {
            return Err("Select a published symbol".into());
        };
        self.first(backend, registry, &selected).await?;
        self.result.choices = choices;
        Ok(())
    }
    async fn next(&mut self, backend: &Backend, registry: &Registry) -> Result<(), String> {
        let Some(token) = self.token.clone() else {
            return Ok(());
        };
        match Self::page(backend, registry, &self.symbol, Some(&token)).await {
            Ok(page) => {
                if page.1.as_deref() == Some(&token) {
                    return Err("The contract page did not advance. Refresh the list.".into());
                }
                self.publish_page(page, true)?;
                self.restart_attempted = false;
                Ok(())
            }
            Err(_) if !self.restart_attempted => {
                // One cursor restart only, under the original caller deadline.
                // Failed reads retain the last displayed rows for explicit retry.
                self.restart_attempted = true;
                let page = Self::page(backend, registry, &self.symbol, None).await?;
                self.publish_page(page, false)
            }
            Err(error) => Err(error),
        }
    }
    async fn mutate(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        id: &str,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        if !self.loaded || intent["expectedGeneration"].as_u64() != Some(self.generation) {
            return Err("Options changed; reload before editing".into());
        }
        let action = intent["intent"].as_str().unwrap_or_default();
        let original_id = intent["originalId"].as_str().unwrap_or_default();
        let original = if action == "add" {
            None
        } else {
            Some(
                self.models
                    .get(original_id)
                    .ok_or("Option contract is no longer loaded")?,
            )
        };
        let next = if action == "remove" {
            original.unwrap().clone()
        } else {
            draft(registry, &intent["fields"], original, action == "review")?
        };
        let value = registry.project(MODEL, &next)?;
        let contract_id = text(&value["ContractId"]);
        let year = expiry(&value)?.year();
        let name = match action {
            "add" => "AddFuturesOptionContractCommand",
            "remove" => "RemoveFuturesOptionContractCommand",
            _ => "ChangeFuturesOptionContractCommand",
        };
        let stem = match action {
            "add" => "Added",
            "remove" => "Removed",
            _ => "Changed",
        };
        let kind = format!("{ROOT}Commands.{name}");
        let (actor, verb) = registry.route(&kind)?;
        let subject = ActorSubject {
            kind: ActorKind::Command,
            name: actor.into(),
            verb: verb.into(),
            entity: format!("{contract_id}.{year}"),
        };
        let mut fields = BTreeMap::from([
            ("CommandId".into(), id.into()),
            ("Subject".into(), subject.wire()),
            ("PostEvents".into(), false.into()),
            (
                "EntityId".into(),
                Value::Array(vec![contract_id.into(), year.into()]),
            ),
            ("ErrorCode".into(), registry.error_id(&kind)?.into()),
            (
                "RouteTo".into(),
                registry
                    .enum_value(
                        "TomasAI.IFM.Shared.EventSourcing.BoundedContextName",
                        "FuturesOptionContractBoundedContext",
                    )?
                    .into(),
            ),
            ("Overwrite".into(), true.into()),
        ]);
        if action != "add" {
            fields.insert("ContractId".into(), original_id.into());
        }
        if action != "remove" {
            fields.insert("Contract".into(), next);
        }
        let body = registry.encode(&kind, &fields)?;
        actor_requests::command(
            backend,
            registry,
            &subject,
            &body,
            &format!("{ROOT}Events.FuturesOptionContract{stem}CompleteEvent"),
            &format!("{ROOT}Events.FuturesOptionContract{stem}FailEvent"),
            id,
            deadline,
        )
        .await?;
        let symbol = text(&value["Symbol"]);
        match tokio::time::timeout_at(deadline, self.first(backend, registry, &symbol)).await {
            Ok(Ok(())) => Ok("Completed".into()),
            result => {
                self.loaded = false;
                Ok(format!(
                    "Saved; reload option contracts: {}",
                    match result {
                        Ok(Err(error)) => error,
                        _ => "Refresh deadline expired".into(),
                    }
                ))
            }
        }
    }
    pub async fn execute(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
    ) -> Result<ContractPublication, String> {
        if intent["schema"] != 1 || intent["view"] != "option-contract" {
            return Err("Invalid option editor intent".into());
        }
        let id = intent["requestId"]
            .as_str()
            .and_then(|v| uuid::Uuid::parse_str(v).ok())
            .filter(|v| !v.is_nil())
            .ok_or("RequestId is required")?
            .to_string();
        let outcome = if let Some((request, outcome)) = self.history.get(&id) {
            if request != intent {
                return Err("RequestId belongs to another option operation".into());
            }
            outcome.clone()
        } else {
            if self.history.len() >= 256 && self.history.values().all(|(_, v)| v.state == "Unknown")
            {
                return Err("Reconcile uncertain option operations before continuing".into());
            }
            let deadline = tokio::time::Instant::now() + backend.default_timeout();
            let action = intent["intent"].as_str().unwrap_or_default();
            let result = match action {
                "load" => tokio::time::timeout_at(
                    deadline,
                    self.load(
                        backend,
                        registry,
                        intent["fields"]["symbol"].as_str().unwrap_or_default(),
                    ),
                )
                .await
                .map_err(|_| "Option load deadline expired".to_string())
                .and_then(|v| v)
                .map(|_| "Completed".into()),
                "next" => {
                    if !self.loaded
                        || intent["expectedGeneration"].as_u64() != Some(self.generation)
                    {
                        Err("Option page selection changed; reload before continuing".into())
                    } else {
                        tokio::time::timeout_at(deadline, self.next(backend, registry))
                            .await
                            .map_err(|_| "Option page deadline expired".to_string())
                            .and_then(|v| v)
                            .map(|_| "Completed".into())
                    }
                }
                "add" | "change" | "remove" | "review" => {
                    self.mutate(backend, registry, intent, &id, deadline).await
                }
                _ => Err("Unsupported option action".into()),
            };
            let outcome = match result {
                Ok(message) => EditorOutcome {
                    request_id: id.clone(),
                    state: "Completed".into(),
                    message,
                },
                Err(error) => EditorOutcome {
                    request_id: id.clone(),
                    state: if error.starts_with("OutcomeUnknown:") {
                        self.loaded = false;
                        "Unknown"
                    } else {
                        "Failed"
                    }
                    .into(),
                    message: error,
                },
            };
            if self.history.len() >= 256 {
                if let Some(key) = self
                    .history
                    .iter()
                    .find(|(_, (_, v))| v.state != "Unknown")
                    .map(|(key, _)| key.clone())
                {
                    self.history.remove(&key);
                }
            }
            self.history.insert(id, (intent.clone(), outcome.clone()));
            outcome
        };
        self.generation += 1;
        self.result.view = "option-contract".into();
        Ok(ContractPublication {
            schema: 1,
            generation: self.generation,
            status: outcome.message.clone(),
            result: self.result.clone(),
            outcomes: vec![outcome],
            ..Default::default()
        })
    }
}
