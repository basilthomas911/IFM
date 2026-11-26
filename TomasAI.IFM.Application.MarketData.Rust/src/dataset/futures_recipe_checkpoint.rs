//! Exact resolved futures catalog/source recipe, excluding credentials and resource handles.
use super::futures_owner::Intent;
use crate::{checkpoint as c,dataset::{live::{Evidence,Registration},transport::{SessionPlan,SubscriptionSpec}},latest::Asset};
use rmpv::Value as V;
pub(crate) fn capture(intent:&Intent,contracts:Vec<V>,public_models:Vec<V>)->V{
    V::Array(vec![1.into(),c::day(intent.date),c::day(intent.definition),V::Array(intent.source.plan.subscriptions.iter().map(|source|V::Array(vec![V::Array(source.symbols.iter().cloned().map(Into::into).collect()),source.symbology.to_string().into(),source.schema.to_string().into(),source.replay_start_ns.map_or(V::Nil,Into::into)])).collect()),V::Array(intent.source.registrations.iter().map(|r|V::Array(vec![r.instrument.into(),r.publisher.into(),r.contract.clone().into(),r.source.clone().into(),(if r.asset==Asset::Futures{1}else{2}).into(),r.tick.map_or(V::Nil,Into::into),r.vwap.into(),r.required.into()])).collect()),(match intent.source.evidence{Evidence::ControlOnly=>0,Evidence::ControlOnlyWithOptionalQuoteReplay=>1,Evidence::Observations=>2}).into(),V::Array(contracts),V::Array(public_models)])
}
pub(crate) struct Recipe{pub date:time::Date,pub definition:time::Date,pub plan:SessionPlan,pub registrations:Vec<Registration>,pub evidence:Evidence,pub contracts:Vec<V>,pub public_models:Vec<V>}
pub(crate) fn decode(value:&V,lane:super::transport::Lane)->Result<Recipe,String>{
    let f=c::fields(value,8)?;if c::unsigned(&f[0])?!=1{return Err("Unknown original futures recipe".into());}
    let mut subscriptions=Vec::new();for row in f[3].as_array().filter(|rows|!rows.is_empty()&&rows.len()<=64).ok_or("Futures recipe subscription bound")?{let p=c::fields(row,4)?;let symbols=p[0].as_array().filter(|symbols|!symbols.is_empty()&&symbols.len()<=65536).ok_or("Futures recipe symbol bound")?.iter().map(|symbol|c::string(symbol,70)).collect::<Result<Vec<_>,_>>()?;subscriptions.push(SubscriptionSpec{symbols,symbology:c::string(&p[1],64)?.parse().map_err(|_|"Original futures symbology invalid")?,schema:c::string(&p[2],64)?.parse().map_err(|_|"Original futures schema invalid")?,replay_start_ns:if p[3].is_nil(){None}else{Some(c::number(&p[3])?)}});}
    let mut registrations=Vec::new();for row in f[4].as_array().filter(|rows|!rows.is_empty()&&rows.len()<=2048).ok_or("Futures recipe registration bound")?{let r=c::fields(row,8)?;registrations.push(Registration{instrument:c::unsigned(&r[0])?.try_into().map_err(|_|"Original futures instrument invalid")?,publisher:c::unsigned(&r[1])?.try_into().map_err(|_|"Original futures publisher invalid")?,contract:c::string(&r[2],256)?,source:c::string(&r[3],256)?,asset:match c::unsigned(&r[4])?{1=>Asset::Futures,_=>return Err("Original futures recipe asset invalid".into())},tick:if r[5].is_nil(){None}else{Some(c::number(&r[5])?)},vwap:c::boolean(&r[6])?,required:c::boolean(&r[7])?});}
    let plan=SessionPlan{lane,subscriptions};plan.validate().map_err(|error|format!("Original futures plan invalid: {error:?}"))?;
    let evidence=match c::unsigned(&f[5])?{0=>Evidence::ControlOnly,1=>Evidence::ControlOnlyWithOptionalQuoteReplay,2=>Evidence::Observations,_=>return Err("Original futures evidence policy invalid".into())};
    let contracts=f[6].as_array().filter(|values|values.len()<=2048).ok_or("Futures catalog count exceeds bound")?.clone();let public_models=f[7].as_array().filter(|values|values.len()<=2048).ok_or("Public futures catalog count exceeds bound")?.clone();
    Ok(Recipe{date:c::date(&f[1])?,definition:c::date(&f[2])?,plan,registrations,evidence,contracts,public_models})
}
pub(crate) fn restore(value:&V,template:&Intent)->Result<(Intent,Vec<V>,Vec<V>),String>{
    let recipe=decode(value,template.source.plan.lane)?;
    if recipe.date!=template.date{return Err("Original futures recipe ValueDate differs from explicit replay date".into());}
    let source=super::live::Start{plan:recipe.plan,registrations:recipe.registrations,key:template.source.key.clone(),gateway:template.source.gateway.clone(),evidence:recipe.evidence};source.validate(2048)?;
    Ok((Intent{bootstrap:None,date:recipe.date,definition:recipe.definition,source,broker:template.broker.clone(),scope:template.scope.clone(),revision:template.revision},recipe.contracts,recipe.public_models))
}
