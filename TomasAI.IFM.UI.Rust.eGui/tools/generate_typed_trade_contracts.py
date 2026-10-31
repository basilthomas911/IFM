"""Generate concrete Rust trade contract records from the committed C# wire schema.
Run from any directory; --check verifies reproducibility without rewriting sources.
The generated records never contain serde_json::Value or untyped field maps.
"""
from pathlib import Path
import argparse, json, re
ROOT=Path(__file__).resolve().parents[1]
SCHEMA=ROOT/'tests/fixtures/backend/contracts.json'
OUT=ROOT/'src/models/trade_contracts.rs'
D={row['type']:row for row in json.loads(SCHEMA.read_text(encoding='utf-8-sig'))}
ROOTS=['TomasAI.IFM.Domain.Trade.Shared.'+n for n in [
 'EstablishedTradeDefinition','StrategyPositionSnapshot','Trade.Position.Plan.StrategyTradePlanSnapshot',
 'Trade.Realtime.TradeRealtimeSnapshot','Trade.Position.Plan.StrategyTradePlanHistoryPage','ViewModels.TradeLimitReadModel']]+[
 'TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionTickDataV2ReadModel',
 'TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.MarketOutlookReadModel',
 'TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.FuturesBarDataReadModel',
 'TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialBalanceSnapshot',
 'TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshotChangedEvent',
 'TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondorPositionChangedEvent',
 'TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanUpdatedEvent']
ROOTS += [
 'TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesEodDataV2ReadModel',
 'TomasAI.IFM.Domain.Trade.Shared.Order.TradeEndOfDayDisplay',
 'TomasAI.IFM.Domain.Trade.Shared.Order.TradeEndOfDayDisplayRequest']
ROOTS += ['TomasAI.IFM.Domain.MarketData.Shared.ViewModels.EvaluatedOptionChainReadModel', 'TomasAI.IFM.Domain.MarketData.Shared.ViewModels.OptionContractExpiryReadModel', 'TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel', 'TomasAI.IFM.Domain.Trade.Shared.Order.TradeOrderPreview', 'TomasAI.IFM.Domain.BrokerAccount.Contracts.BrokerAccountDefinition', 'TomasAI.IFM.Domain.Trade.Shared.Order.Broker.BrokerOrderDefinition', 'TomasAI.IFM.Domain.Trade.Shared.OrderExecutionDefinition', 'TomasAI.IFM.Domain.Trade.Shared.ViewModels.OptionTradeReadModel', 'TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioReadModel', 'TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundMandateReadModel', 'TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderProjectionReadModel', 'TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderTradeProjectionReadModel', 'TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundAllocationReadModel', 'TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundRiskEnvelopeReadModel', 'TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundTradeTemplateAssignmentReadModel', 'TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioFinancialPolicyReadModel', 'TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialCapacityUsage', 'TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialReservationView']
ROOTS += ['TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts.'+n for n in ['ScheduledTaskRun','ScheduledTaskDefinition','ScheduledTaskSchedulePreview','ScheduledTasksDashboard','ScheduledTaskOutputPage']]
ROOTS += ['TomasAI.IFM.Domain.Reference.Shared.ParameterSets.'+n for n in ['ParameterComponentSummary','ParameterAssignmentSnapshot','ParameterLegacyVersion','ParameterValidationReport','ParameterSignalStartupPlan','ParameterSignalStartupReport','ParameterSignalMonitoringSnapshot']]
PRIMITIVES={'System.Boolean':'bool','System.Byte':'u8','System.UInt16':'u16','System.UInt32':'u32','System.Int32':'i32','System.Int64':'i64','System.Double':'f64','System.String':'String','System.Guid':'String','System.Decimal':'DecimalText','System.DateOnly':'DateText','System.DateTime':'UtcText','System.DateTimeOffset':'OffsetText','System.TimeOnly':'TimeText'}
seen=set()
def visit(kind):
 if kind in seen:return
 seen.add(kind)
 if kind.endswith('[]'):return visit(kind[:-2])
 if '[' in kind and kind not in D:return visit(kind.split('[',1)[1][:-1])
 if kind not in D:
  if kind not in PRIMITIVES:raise ValueError('Missing concrete adapter: '+kind)
  return
 if D[kind]['mapByName'] or D[kind]['unions']:raise ValueError('Requires explicit map/union adapter: '+kind)
 for member in D[kind]['members']:visit(member['type'])
for kind in ROOTS:visit(kind)
TYPE_NAMES={'TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment':'AccountBrokerEnvironment'}
names={kind:TYPE_NAMES.get(kind,kind.rsplit('.',1)[1]) for kind in seen if kind in D}
assert len(set(names.values()))==len(names),'Contract short names must be unique'
def snake(name):
 name=re.sub(r'(.)([A-Z][a-z]+)',r'\1_\2',name)
 name=re.sub(r'([a-z0-9])([A-Z])',r'\1_\2',name).lower()
 return 'r#'+name if name in {'type','ref','self','match','loop','mod','move','in','where'} else name
BOXED_RECORDS={"StrategyPositionSnapshot","IronCondorTradePlanSnapshot","StrategyTradePlanSnapshot","TradeRealtimeSnapshot","FuturesOptionTickDataV2ReadModel"}
def rust_type(kind,indirect=False):
 if kind.startswith('System.Nullable`1['):return rust_type(kind.split('[',1)[1][:-1],indirect)
 if kind=='System.Byte[]':return 'ByteBuffer'
 if kind.endswith('[]'):return 'Vec<'+rust_type(kind[:-2])+'>'
 if kind.startswith('System.Collections.Immutable.ImmutableArray`1['):return 'Vec<'+rust_type(kind.split('[',1)[1][:-1])+'>'
 if indirect and names.get(kind) in BOXED_RECORDS:return "Box<"+names[kind]+">"
 return PRIMITIVES.get(kind,names.get(kind)) or (_ for _ in ()).throw(ValueError(kind))
lines=['//! Generated by tools/generate_typed_trade_contracts.py from compiled C# contract metadata.',
       '//! Concrete fields preserve retired numeric slots and exact backend decimal text.',
       'use super::wire_types::*;', 'use super::cells::{Cell,Cells,ScalarCell};', 'use super::trade_presentation::{PresentationRecord, PresentationExtras, PublishedIdentity, PresentationMode, PresentationValue, FieldValue};', 'use rmpv::Value;', 'use serde::{Deserialize, Serialize};', 'use super::backend_document::{BackendRecord,BackendDocument};', 'use super::labels::{Label,PublishedEnum};', 'use serde::ser::SerializeMap;', '']
for kind in sorted(names):
 row=D[kind];name=names[kind]
 if row['isEnum']:
  lines += ['#[derive(Clone, Debug, PartialEq, Eq)]',f'pub enum {name} {{']
  for v in row['enumValues']:lines.append('    '+('None' if v['name']=='None' else v['name'])+',')
  lines += ['    Unrecognized(i64),','}',f'impl {name} {{','    pub fn label(&self) -> std::borrow::Cow<\'static, str> {','        match self {']
  for v in row['enumValues']:lines.append(f'            Self::{v["name"]} => "{v["name"]}".into(),')
  lines += ['            Self::Unrecognized(value) => value.to_string().into(),','        }','    }','}',f'impl FromBackend for {name} {{',f'    const KIND: &\'static str = "{kind}";','    fn from_backend(value: &Value) -> Result<Self, String> {','        Ok(match value.as_i64().ok_or("Expected contract enum integer")? {']
  for v in row['enumValues']:lines.append(f'            {v["value"]} => Self::{v["name"]},')
  lines += ['            value => Self::Unrecognized(value),','        })','    }','}',f'impl Serialize for {name} {{','    fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok,S::Error> { match self { Self::Unrecognized(value)=>serializer.serialize_i64(*value), _=>serializer.serialize_str(&self.label()) } }','}']
  lines += [f"impl<'de> Deserialize<'de> for {name} {{", "    fn deserialize<D:serde::Deserializer<'de>>(deserializer:D)->Result<Self,D::Error>{", "        struct Visitor;", "        impl<'de> serde::de::Visitor<'de> for Visitor {", f"            type Value={name};", "            fn expecting(&self,formatter:&mut std::fmt::Formatter<'_>)->std::fmt::Result{formatter.write_str(\"a declared enum name or integer\")}", f"            fn visit_i64<E:serde::de::Error>(self,value:i64)->Result<{name},E>{{{name}::from_backend(&Value::from(value)).map_err(E::custom)}}", f"            fn visit_u64<E:serde::de::Error>(self,value:u64)->Result<{name},E>{{self.visit_i64(i64::try_from(value).map_err(E::custom)?)}}", f"            fn visit_str<E:serde::de::Error>(self,value:&str)->Result<{name},E>{{Ok(match value{{"]
  for value in row['enumValues']:lines.append(f'                "{value["name"]}"=>{name}::{value["name"]},')
  lines += [f'                value=>{name}::Unrecognized(value.parse().map_err(E::custom)?),', '            })}', "        }", "        deserializer.deserialize_any(Visitor)", "    }", "}",f'impl ScalarCell for {name} {{', "    fn scalar_cell(&self)->Cell<'_>{match self { Self::Unrecognized(value)=>Cell::Signed(*value), _=>Cell::Text(self.label()) }}", '}']
  lines += [f'impl PresentationValue for {name} {{', '    fn serialize_value<S:serde::Serializer>(&self,serializer:S,mode:PresentationMode)->Result<S::Ok,S::Error>{ if mode==PresentationMode::Native { self.serialize(serializer) } else { serializer.serialize_str(&self.label()) } }','}']
  lines += [f'impl BackendRecord for {name} {{', '    fn serialize_backend<S:serde::Serializer>(&self,serializer:S)->Result<S::Ok,S::Error>{ match self { Self::Unrecognized(value)=>serializer.serialize_i64(*value), _=>serializer.serialize_str(&self.label()) }}','}']
  lines += [f'impl PublishedEnum for {name} {{ fn published_label(&self)->Label{{match self{{Self::Unrecognized(value)=>Label::Number(*value), _=>Label::Text(self.label().into_owned())}}}} }}']
 else:
  lines += ['#[derive(Clone, Debug, Default, PartialEq, Deserialize)]',f'pub struct {name} {{']
  for m in sorted(row['members'],key=lambda m:m['index']):
   camel=m['name'][0].lower()+m['name'][1:]
   lines += [f'    /// C# {m["name"]}, wire slot {m["index"]}.',f'    #[serde(default, rename = "{camel}", alias = "{m["name"]}")]',f'    pub {snake(m["name"])}: Option<{rust_type(m["type"],True)}>,']
  lines += ['}',f'impl FromBackend for {name} {{',f'    const KIND: &\'static str = "{kind}";','    fn from_backend(value: &Value) -> Result<Self,String> {','        let fields = value.as_array().ok_or("Expected positional contract record")?;','        Ok(Self {']
  for m in sorted(row['members'],key=lambda m:m['index']):lines.append(f'            {snake(m["name"])}: FromBackend::from_backend(fields.get({m["index"]}).unwrap_or(&Value::Nil)).map_err(|error| format!("{name}.{m["name"]}: {{error}}"))?,')
  lines += ['        })','    }','}']
  lines += [f'impl PresentationRecord for {name} {{', '    fn serialize_presentation<S: serde::Serializer>(&self, serializer: S, mode: PresentationMode) -> Result<S::Ok,S::Error> {', '        let child_mode=if mode==PresentationMode::History { PresentationMode::Monitor } else { mode };', '        let mut map = serializer.serialize_map(None)?;']
  if name in {'StrategyTradePlanSnapshot','IronCondorTradePlanSnapshot'}:lines.append('        let history=mode==PresentationMode::History;')
  for m in row['members']:
   camel=m['name'][0].lower()+m['name'][1:]
   skip=(name=='StrategyTradePlanSnapshot' and m['name'] in {'Position','Parameters'}) or (name=='IronCondorTradePlanSnapshot' and m['name'] in {'Position','LegacyLegs','StrategyRiskParameterSet'})
   child_mode='mode' if name=='StrategyTradePlanSnapshot' and m['name']=='IronCondorTradePlanSnapshot' else 'child_mode'
   expression='&FieldValue { value: &self.'+snake(m['name'])+', mode: '+child_mode+' }'
   statement=f'map.serialize_entry("{camel}", {expression})?;'
   lines.append('        '+(f'if !history {{ {statement} }}' if skip else statement))
  lines += ['        self.serialize_extras(&mut map)?;','        map.end()','    }','}',f'impl Serialize for {name} {{','    fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok,S::Error> { self.serialize_presentation(serializer, PresentationMode::Native) }','}']
  lines += [f'impl PresentationValue for {name} {{', '    fn serialize_value<S:serde::Serializer>(&self,serializer:S,mode:PresentationMode)->Result<S::Ok,S::Error>{ self.serialize_presentation(serializer,mode) }','}']
  member_names={member['name'] for member in row['members']}
  identity=None
  guid=lambda member:f'self.{snake(member)}.as_deref().and_then(|value|uuid::Uuid::parse_str(value).ok()).is_some_and(|value|!value.is_nil())'
  if {'PortfolioId','FundId','OrderId'}<=member_names:
   identity=' && '.join(f'self.{snake(member)}.is_some_and(|value|value>0)' for member in ['PortfolioId','FundId','OrderId']+(['TradeId'] if 'TradeId' in member_names else []))
  elif {'TradeOrder','ExecutionAttemptId'}<=member_names:identity='self.trade_order.as_ref().is_some_and(PublishedIdentity::published_is_valid) && '+guid('ExecutionAttemptId')
  elif {'Execution','ComponentId'}<=member_names:identity='self.execution.as_ref().is_some_and(PublishedIdentity::published_is_valid) && '+guid('ComponentId')
  elif {'Trade','PositionId'}<=member_names:identity='self.trade.as_ref().is_some_and(PublishedIdentity::published_is_valid) && '+guid('PositionId')
  if identity:lines += [f'impl PublishedIdentity for {name} {{ fn published_is_valid(&self)->bool{{ {identity} }} }}']
  if name not in {'TradeEntityId','StrategyPositionId','EstablishedTradeDefinition','IronCondorTradePlanSnapshot','FuturesOptionTickDataV2ReadModel'}:
   lines += [f'impl PresentationExtras for {name} {{','    fn serialize_extras<M:SerializeMap>(&self,map:&mut M)->Result<(),M::Error>{','        let _=map;']
   if identity:lines.append('        map.serialize_entry("isValid",&self.published_is_valid())?;')
   if {'TradeOrderId','ExecutionAttemptId'}<=member_names:lines.append('        map.serialize_entry("id",&OrderExecutionId{trade_order:self.trade_order_id.clone(),execution_attempt_id:self.execution_attempt_id.clone()})?;')
   lines += ['        Ok(())','    }','}']
  lines += [f'impl BackendRecord for {name} {{','    fn serialize_backend<S:serde::Serializer>(&self,serializer:S)->Result<S::Ok,S::Error>{','        let mut map=serializer.serialize_map(None)?;']
  for m in row['members']:lines.append(f'        map.serialize_entry("{m["name"]}", &BackendDocument(&self.{snake(m["name"])}))?;')
  lines += ['        map.end()','    }','}']
  lines += [f'impl Cells for {name} {{', '    fn cell(&self,column:&str)->Cell<\'_>{ match column {']
  for m in row['members']:
   inner=m['type'].removeprefix('System.Nullable`1[').removesuffix(']') if m['type'].startswith('System.Nullable`1[') else m['type']
   if inner in PRIMITIVES or (inner in D and D[inner]['isEnum']):
    camel=m['name'][0].lower()+m['name'][1:]
    lines.append(f'        "{camel}" | "{m['name']}" => self.{snake(m["name"])}.scalar_cell(),')
  if name=='IronCondorTradePlanSnapshot':
   lines += ['        "isComplete" => Cell::Boolean(self.is_complete()),', '        "forwardSpreadForChart" => self.forward_spread_for_chart().map(|value|Cell::Text(value.0.into())).unwrap_or(Cell::Missing),']
  lines += ['        _=>Cell::Missing,','    }}','}']
 lines.append('')
# Pin every consumed slot and CLR type, allowing parity verification without reflection in production.
lines += ['pub const CONTRACT_FIELDS: &[(&str,&str,usize,&str)] = &[']
for kind in sorted(names):
 for m in D[kind]['members']:lines.append(f'    ("{kind}", "{m["name"]}", {m["index"]}, "{m["type"]}"),')
lines+= ['];','']
output='\n'.join(lines)
import subprocess
output=subprocess.run(['rustfmt','--edition','2024','--emit','stdout'],input=output,text=True,capture_output=True,check=True).stdout
args=argparse.ArgumentParser();args.add_argument('--check',action='store_true');a=args.parse_args()
if a.check:
 if OUT.read_text(encoding='utf-8')!=output:raise SystemExit('Typed trade contracts are stale; regenerate them.')
else:OUT.write_text(output,encoding='utf-8')
print(f'{len(names)} concrete contract types, {sum(len(D[k]["members"]) for k in names)} pinned fields')
