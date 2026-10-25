use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    lookup_editor::EditorOutcome,
    services::business_context::BusinessContext,
};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;

pub const KINDS: [&str; 7] = [
    "Family",
    "Strategy",
    "Structure",
    "Variant",
    "ParameterSchema",
    "ParameterSet",
    "Deployment",
];
#[derive(Default)]
pub struct CatalogController {
    references: Vec<Json>,
    products: Vec<Json>,
    selected: Option<Json>,
    draft: Option<Json>,
    kind: usize,
    loaded: bool,
    show_all: bool,
    retry: Option<Json>,
    pub generation: u64,
}
fn identity(key: &Json) -> Result<String, String> {
    let kind = key["Kind"]
        .as_u64()
        .filter(|n| (1..=7).contains(n))
        .ok_or("Invalid catalog kind")? as usize;
    let id = key["Id"]
        .as_str()
        .and_then(|s| uuid::Uuid::parse_str(s).ok())
        .ok_or("Invalid catalog ID")?;
    let version = key["Version"]
        .as_u64()
        .filter(|n| *n > 0)
        .ok_or("Invalid catalog version")?;
    Ok(format!("{}:{id}:{version}", KINDS[kind - 1]))
}
fn defaults() -> Vec<Json> {
    serde_json::from_str(include_str!(
        "../../tests/fixtures/backend/catalog-defaults.json"
    ))
    .expect("Compiled catalog defaults")
}

pub async fn query(backend: &Backend, registry: &Registry, request: &Json) -> Result<Json, String> {
    let subject = ActorSubject::query("ReferenceQuery", "StrategyCatalog", "none");
    let message = registry.encode(
        "TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogQuery",
        &BTreeMap::from([
            ("Subject".into(), subject.wire()),
            (
                "EntityId".into(),
                Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]),
            ),
            (
                "RequestJson".into(),
                serde_json::to_string(request)
                    .map_err(|e| e.to_string())?
                    .into(),
            ),
        ]),
    )?;
    let response = backend.request(&subject, &message, None, None).await?;
    let text = service_result(&response)?
        .as_str()
        .ok_or("Catalog query returned no JSON")?;
    if text.len() > 524288 {
        return Err("Catalog message exceeds 512 KiB".into());
    }
    serde_json::from_str(text).map_err(|e| e.to_string())
}
fn list_request(kind: usize, after: Option<&str>) -> Json {
    json!({"Operation":1,"Kind":kind+1,"Key":null,"Limit":100,"AfterCode":after})
}
fn exact_request(key: &Json) -> Json {
    json!({"Operation":2,"Kind":1,"Key":key,"Limit":100,"AfterCode":null})
}

/// Retry the exact uncertain wire request, including its original effective
/// instant. A newly read API clock must not create a second operation.
fn retain_retry(prior: &Json, next: &Json) -> Result<Json, String> {
    let mut previous = prior.clone();
    let mut proposed = next.clone();
    for field in ["OperationId", "EffectiveUtc"] {
        previous[field] = Json::Null;
        proposed[field] = Json::Null;
    }
    if previous != proposed {
        return Err("OutcomeUnknown: reconcile the pending catalog operation before submitting a different change".into());
    }
    Ok(prior.clone())
}

impl CatalogController {
    pub async fn execute(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
    ) -> Result<ContractPublication, String> {
        let request_id = intent["requestId"]
            .as_str()
            .and_then(|s| uuid::Uuid::parse_str(s).ok())
            .filter(|id| !id.is_nil())
            .ok_or("RequestId is required")?;
        let action = intent["intent"].as_str().ok_or("Missing catalog action")?;
        if intent["schema"] != 1 || intent["view"] != "strategy-catalog" {
            return Err("Invalid catalog intent".into());
        }
        if self.retry.is_some()
            && !matches!(
                action,
                "catalog-save" | "catalog-publish" | "catalog-retire" | "catalog-resume"
            )
        {
            return Err("OutcomeUnknown: retry or reconcile the pending catalog operation before leaving its working copy".into());
        }
        if !self.loaded && action != "load" {
            return Err("Load the catalog first".into());
        }
        if !matches!(action, "load" | "catalog-resume")
            && intent["expectedGeneration"].as_u64() != Some(self.generation)
        {
            return Err("Catalog changed; refresh before editing".into());
        }
        if self.draft.is_some()
            && matches!(
                action,
                "load" | "catalog-section" | "catalog-select" | "catalog-new" | "catalog-edit"
            )
        {
            return Err("Save or discard the working copy first".into());
        }
        let field = |key: &str| intent["fields"][key].as_str().unwrap_or_default();
        match action {
            "load" => {
                self.load(backend, registry).await?;
                self.selected = None;
            }
            "catalog-resume" => {}
            "catalog-section" => {
                self.kind = KINDS
                    .iter()
                    .position(|name| *name == field("kind"))
                    .ok_or("Unknown catalog section")?;
                self.show_all = field("showAll") == "true";
                self.selected = None;
            }
            "catalog-select" => {
                let original = intent["originalId"]
                    .as_str()
                    .ok_or("Select a catalog row")?;
                let key = self
                    .references
                    .iter()
                    .find(|r| identity(&r["Key"]).as_deref() == Ok(original))
                    .ok_or("Selected version is not in the catalog")?["Key"]
                    .clone();
                self.selected = Some(self.exact(backend, registry, &key).await?);
            }
            "catalog-new" => {
                let id = uuid::Uuid::new_v4();
                let template = defaults()
                    .into_iter()
                    .find(|t| identity(&t["Key"]).as_deref() == Ok(field("template")));
                let mut draft = if let Some(mut template) = template {
                    if template["Key"]["Kind"].as_u64() != Some((self.kind + 1) as u64) {
                        return Err("Template belongs to another section".into());
                    }
                    template["Code"] = format!(
                        "{}-{}",
                        template["Code"].as_str().unwrap_or_default(),
                        &id.simple().to_string()[..6]
                    )
                    .into();
                    template
                } else {
                    json!({"Key":null,"Code":"","Name":"","Description":"","SchemaVersion":1,"Parent":null,"Horizon":if self.kind==6{1}else{0},"Side":if self.kind==3{"Long"}else{""},"Bias":if self.kind==3{"Balanced"}else{""},"PremiumMode":if self.kind==3{"None"}else{""},"Settings":{},"Families":[],"Structures":[],"Variants":[],"Capabilities":[],"ExpiryGroups":[],"Legs":[],"VariantLegs":[],"Products":[],"PipelineParameters":[],"Parameters":[],"LegacyFamilies":[]})
                };
                draft["Key"] = json!({"Kind":self.kind+1,"Id":id.to_string(),"Version":1});
                self.draft = Some(draft);
            }
            "catalog-edit" => {
                self.require_selection(intent)?;
                let mut draft =
                    self.selected.as_ref().ok_or("Select an exact version")?["Definition"].clone();
                let version = draft["Key"]["Version"]
                    .as_i64()
                    .and_then(|n| n.checked_add(1))
                    .filter(|n| *n <= i32::MAX as i64)
                    .ok_or("Catalog version overflow")?;
                draft["Key"]["Version"] = version.into();
                self.draft = Some(draft);
            }
            "catalog-discard" => {
                self.draft = None;
                self.retry = None;
            }
            "catalog-save" => {
                let draft = self.draft.as_ref().ok_or("Open a working copy first")?;
                if field("definition").len() > 524288 {
                    return Err("Catalog definition exceeds 512 KiB".into());
                }
                let supplied: Json =
                    serde_json::from_str(field("definition")).map_err(|e| e.to_string())?;
                if supplied["Key"] != draft["Key"]
                    || (supplied["Key"]["Version"].as_i64().unwrap_or(0) > 1
                        && supplied["Code"] != draft["Code"])
                {
                    return Err("Catalog identity and existing code cannot change".into());
                }
                if ["Code", "Name"].iter().any(|key| {
                    supplied[*key]
                        .as_str()
                        .unwrap_or_default()
                        .trim()
                        .is_empty()
                }) {
                    return Err("Code and Name are required".into());
                }
                let key = supplied["Key"].clone();
                self.draft = Some(supplied.clone());
                self.send(backend, registry, json!({"OperationId":request_id.to_string(),"Operation":1,"Definition":supplied,"Key":null,"ExpectedPreviousVersion":key["Version"].as_i64().unwrap()-1,"ExpectedHash":null,"EffectiveUtc":null})).await?;
                self.selected = Some(self.confirm(backend, registry, &key, 0).await?);
                self.retry = None;
                self.draft = None;
                self.show_all = true;
                self.load(backend, registry).await?;
            }
            "catalog-publish" | "catalog-retire" => {
                self.require_selection(intent)?;
                if self.draft.is_some() {
                    return Err("Select a saved version first".into());
                }
                let retire = action == "catalog-retire";
                let selected = self
                    .selected
                    .as_ref()
                    .ok_or("Select a saved version first")?;
                if selected["Status"].as_i64() != Some(if retire { 1 } else { 0 }) {
                    return Err("Selected lifecycle state does not allow this action".into());
                }
                let key = selected["Definition"]["Key"].clone();
                let hash = selected["ContentHash"].clone();
                let time = BusinessContext::load(backend)
                    .await?
                    .as_of_utc
                    .to_rfc3339_opts(chrono::SecondsFormat::Micros, true);
                self.send(backend, registry, json!({"OperationId":request_id.to_string(),"Operation":if retire{3}else{2},"Definition":null,"Key":key,"ExpectedPreviousVersion":0,"ExpectedHash":hash,"EffectiveUtc":time})).await?;
                self.selected = Some(
                    self.confirm(backend, registry, &key, if retire { 2 } else { 1 })
                        .await?,
                );
                self.retry = None;
                self.load(backend, registry).await?;
            }
            _ => return Err("Unsupported catalog action".into()),
        }
        self.generation += 1;
        self.snapshot(request_id.to_string(), "Completed", "Completed")
    }
    fn require_selection(&self, intent: &Json) -> Result<(), String> {
        let selected = self.selected.as_ref().ok_or("Select an exact version")?;
        if intent["originalId"].as_str() != Some(identity(&selected["Definition"]["Key"])?.as_str())
        {
            return Err("Selected version changed".into());
        }
        Ok(())
    }
    async fn load(&mut self, backend: &Backend, registry: &Registry) -> Result<(), String> {
        let mut all = Vec::new();
        for kind in 0..7 {
            let mut after = None;
            loop {
                let page = query(backend, registry, &list_request(kind, after.as_deref())).await?;
                let page = page
                    .as_array()
                    .ok_or("Catalog list did not return an array")?;
                if page.len() > 100 {
                    return Err("Catalog page exceeds requested limit".into());
                }
                all.extend(page.iter().cloned());
                if all.len() > 4096 {
                    return Err("Catalog exceeds editor limit".into());
                }
                if page.len() < 100 {
                    break;
                }
                let next = page.last().unwrap()["Code"]
                    .as_str()
                    .ok_or("Catalog cursor missing")?
                    .to_string();
                if after.as_ref() == Some(&next) {
                    return Err("Catalog pagination did not advance".into());
                }
                after = Some(next);
            }
        }
        let mut products = Vec::new();
        for family in [1, 2] {
            let subject = ActorSubject::query("ReferenceQuery", "GetTradeStrategySymbols", "none");
            let body = Value::Array(vec![
                subject.wire(),
                Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]),
                family.into(),
            ]);
            let reply = backend.request(&subject, &body, None, None).await?;
            let named = registry.project(
                "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.TradeStrategySymbolReadModel[]",
                service_result(&reply)?,
            )?;
            for product in named.as_array().ok_or("Product list is not an array")? {
                let product = json!({"ProductId":product["Id"],"Symbol":product["Symbol"],"Exchange":product["Exchange"],"Currency":product["Currency"]});
                if !products.contains(&product) {
                    products.push(product);
                }
            }
        }
        self.references = all;
        self.products = products;
        self.loaded = true;
        Ok(())
    }
    async fn exact(
        &self,
        backend: &Backend,
        registry: &Registry,
        key: &Json,
    ) -> Result<Json, String> {
        let value = query(backend, registry, &exact_request(key)).await?;
        if value["Definition"]["Key"] != *key {
            return Err("Query returned another catalog version".into());
        }
        Ok(value)
    }
    async fn confirm(
        &self,
        backend: &Backend,
        registry: &Registry,
        key: &Json,
        expected: i64,
    ) -> Result<Json, String> {
        let value = self.exact(backend, registry, key).await.map_err(|e| {
            format!("OutcomeUnknown: command completed but authoritative verification failed: {e}")
        })?;
        if value["Status"].as_i64() != Some(expected) {
            return Err("OutcomeUnknown: catalog lifecycle state was not verified".into());
        }
        Ok(value)
    }
    async fn send(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        mut request: Json,
    ) -> Result<(), String> {
        if let Some(retry) = &self.retry {
            request = retain_retry(retry, &request)?;
        }
        self.retry = Some(request.clone());
        let subject = ActorSubject {
            kind: ActorKind::Command,
            name: "TradeStrategyFamilyCommand".into(),
            verb: "Catalog".into(),
            entity: "none".into(),
        };
        let body = registry.encode(
            "TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogCommand",
            &BTreeMap::from([
                (
                    "CommandId".into(),
                    request["OperationId"]
                        .as_str()
                        .ok_or("Missing OperationId")?
                        .into(),
                ),
                ("Subject".into(), subject.wire()),
                ("PostEvents".into(), false.into()),
                ("EntityId".into(), Value::Array(vec!["none".into()])),
                ("ErrorCode".into(), 8064.into()),
                ("RouteTo".into(), 51.into()),
                ("RequestJson".into(), request.to_string().into()),
            ]),
        )?;
        let result = backend
            .request(&subject, &body, None, None)
            .await
            .map_err(|e| format!("OutcomeUnknown: {e}"))?;
        if let Err(error) = service_result(&result) {
            if result
                .as_array()
                .and_then(|p| p.first())
                .and_then(Value::as_bool)
                == Some(false)
            {
                self.retry = None; // Authoritative rejection, rather than an uncertain reply.
                return Err(error);
            }
            return Err(format!(
                "OutcomeUnknown: invalid catalog acknowledgement: {error}"
            ));
        }
        // Retain identity until the authoritative version/state is verified.
        Ok(())
    }
    pub fn failed(&mut self, id: String, error: &str) -> Result<ContractPublication, String> {
        self.generation += 1;
        self.snapshot(
            id,
            if error.starts_with("OutcomeUnknown:") {
                "Unknown"
            } else {
                "Failed"
            },
            error,
        )
    }
    fn snapshot(
        &self,
        id: String,
        state: &str,
        message: &str,
    ) -> Result<ContractPublication, String> {
        let defaults = defaults();
        let mut references: Vec<_> = self
            .references
            .iter()
            .filter(|r| {
                r["Key"]["Kind"].as_u64() == Some((self.kind + 1) as u64)
                    && (self.show_all
                        || defaults.iter().any(|d| {
                            d["Key"]["Kind"] == r["Key"]["Kind"] && d["Key"]["Id"] == r["Key"]["Id"]
                        })
                        || self.kind == 6
                            && r["Code"]
                                .as_str()
                                .unwrap_or_default()
                                .starts_with("Legacy-"))
            })
            .collect();
        references.sort_by(|a, b| {
            let rank = |r: &Json| {
                if self.kind <= 1 {
                    defaults
                        .iter()
                        .filter(|d| d["Key"]["Kind"].as_u64() == Some((self.kind + 1) as u64))
                        .position(|d| d["Key"]["Id"] == r["Key"]["Id"])
                        .unwrap_or(usize::MAX)
                } else {
                    usize::MAX
                }
            };
            rank(a)
                .cmp(&rank(b))
                .then_with(|| a["Name"].as_str().cmp(&b["Name"].as_str()))
        });
        let rows = references
            .into_iter()
            .map(|r| {
                Ok(ContractRow {
                    id: identity(&r["Key"])?,
                    fields: BTreeMap::from([
                        ("name".into(), r["Name"].as_str().unwrap_or_default().into()),
                        ("code".into(), r["Code"].as_str().unwrap_or_default().into()),
                        ("version".into(), r["Key"]["Version"].to_string()),
                        (
                            "status".into(),
                            match r["Status"].as_i64() {
                                Some(0) => "Draft",
                                Some(1) => "Published",
                                Some(2) => "Retired",
                                _ => "Unknown",
                            }
                            .into(),
                        ),
                    ]),
                    ..Default::default()
                })
            })
            .collect::<Result<Vec<_>, String>>()?;
        let selected = self
            .selected
            .as_ref()
            .map(|s| identity(&s["Definition"]["Key"]))
            .transpose()?
            .unwrap_or_default();
        let definition = self
            .draft
            .as_ref()
            .or_else(|| self.selected.as_ref().map(|s| &s["Definition"]))
            .cloned()
            .unwrap_or(Json::Null);
        let templates: Vec<_> = defaults
            .into_iter()
            .filter(|d| d["Key"]["Kind"].as_u64() == Some((self.kind + 1) as u64))
            .collect();
        let context = BTreeMap::from([
            ("kind".into(),KINDS[self.kind].into()),("showAll".into(),if self.show_all{"True"}else{"False"}.into()),("selectedId".into(),selected),
            ("editing".into(),if self.draft.is_some(){"True"}else{"False"}.into()),("definition".into(),definition.to_string()),("references".into(),json!(self.references).to_string()),("products".into(),json!(self.products).to_string()),
            ("horizons".into(),json!([{"Name":"Daily","Value":1},{"Name":"Weekly","Value":2},{"Name":"Monthly","Value":3}]).to_string()),("templates".into(),json!(templates).to_string()),
        ]);
        Ok(ContractPublication {
            schema: 1,
            generation: self.generation,
            status: message.into(),
            outcomes: vec![EditorOutcome {
                request_id: id,
                state: state.into(),
                message: message.into(),
            }],
            result: ContractResult {
                view: "strategy-catalog".into(),
                rows,
                choices: BTreeMap::from([("kind".into(), KINDS.map(str::to_string).to_vec())]),
                context,
                ..Default::default()
            },
            ..Default::default()
        })
    }
}

#[cfg(test)]
mod retry_tests {
    use super::*;
    #[test]
    fn lifecycle_retry_preserves_original_id_time_and_payload_after_api_clock_advances() {
        let original = json!({"OperationId":uuid::Uuid::new_v4().to_string(),"Operation":2,"Key":{"Id":"fixed","Version":1},"ExpectedHash":"ABC","EffectiveUtc":"2026-10-26T12:30:00Z"});
        let mut next = original.clone();
        next["OperationId"] = uuid::Uuid::new_v4().to_string().into();
        next["EffectiveUtc"] = "2026-10-26T12:31:00Z".into();
        assert_eq!(retain_retry(&original, &next).unwrap(), original);
        next["ExpectedHash"] = "DIFFERENT".into();
        assert!(
            retain_retry(&original, &next)
                .unwrap_err()
                .contains("OutcomeUnknown")
        );
        next["ExpectedHash"] = "ABC".into();
        next["Operation"] = 3.into();
        assert!(retain_retry(&original, &next).is_err());
    }
}
