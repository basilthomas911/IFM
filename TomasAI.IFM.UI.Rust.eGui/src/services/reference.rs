use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    lookup_editor::LookupRow,
};
use rmpv::Value;
use std::collections::BTreeMap;

pub async fn load_lookups(
    backend: &Backend,
    registry: &Registry,
) -> Result<Vec<LookupRow>, String> {
    let subject = ActorSubject::query("LookupTypeQuery", "GetLookupTypes", "all");
    let request = registry.encode(
        "TomasAI.IFM.Domain.Reference.Shared.Queries.GetLookupTypesQuery",
        &BTreeMap::from([
            ("Subject".into(), subject.wire()),
            (
                "EntityId".into(),
                Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]),
            ),
        ]),
    )?;
    let result = backend.request(&subject, &request, None, None).await?;
    project_lookups(registry, service_result(&result)?)
}
pub fn project_lookups(registry: &Registry, result: &Value) -> Result<Vec<LookupRow>, String> {
    let named = registry.project(
        "TomasAI.IFM.Domain.Reference.Shared.LookupTypeCollection",
        result,
    )?;
    let items = named["Items"]
        .as_array()
        .ok_or("Lookup collection has no Items")?;
    items
        .iter()
        .map(|item| {
            Ok(LookupRow {
                lookup_type_name: item["LookupTypeName"]
                    .as_str()
                    .ok_or("Missing lookup name")?
                    .into(),
                short_code: item["ShortCode"].as_str().unwrap_or_default().into(),
                order_id: item["OrderId"]
                    .as_i64()
                    .and_then(|n| i32::try_from(n).ok())
                    .ok_or("Invalid lookup order ID")?,
                description: item["Description"].as_str().unwrap_or_default().into(),
                created_on: item["CreatedOn"].as_str().unwrap_or_default().into(),
                created_by: item["CreatedBy"].as_str().unwrap_or_default().into(),
            })
        })
        .collect()
}
