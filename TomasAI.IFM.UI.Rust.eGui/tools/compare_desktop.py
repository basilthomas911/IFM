"""Compare actual FlaUI publications; never substitute headless data for a UI run."""
import collections,json,sys,re,datetime
from pathlib import Path
root=Path(sys.argv[1]);spec=json.loads(Path(sys.argv[2]).read_text(encoding="utf-8-sig"))
allowed=set(spec.get("comparison",{}).get("allowedIdentityLabels",[]))
report={"status":"Passed","cases":[],"basis":"Actual FlaUI trees from both isolated applications; screenshots retained unchanged."}
expected=spec.get("comparison",{}).get("captures",[])
missing_captures=[f"{variant}/{capture}.json" for variant in ("Hybrid","Standalone") for capture in expected if not (root/variant/(capture+".json")).exists()]
if missing_captures:
 report["status"]="Failed";report["missingCaptures"]=missing_captures
clock=spec.get("comparison",{}).get("apiClockDefaults")
exceptions=[]
neighbors=[]
def clock_rows(items,variant,capture):
 apply_clock=bool(clock) and (not clock.get("captures") or Path(capture).stem in clock["captures"])
 if not apply_clock and not spec.get("operationalClock"):return items
 start=datetime.datetime.fromisoformat(json.loads((root/"Hybrid"/"executable.json").read_text())["startedEastern"])
 result=[]
 for original in items:
  r=dict(original);matched=None
  if spec.get("operationalClock") and r["type"]=="Text" and r["name"].startswith("Central observation: "):
   observed=re.match(r"Central observation: (\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) UTC",r["name"])
   if observed:
    instant=datetime.datetime.strptime(observed[1],"%Y-%m-%d %H:%M:%S").replace(tzinfo=datetime.timezone.utc)
    own_start=datetime.datetime.fromisoformat(json.loads((root/variant/"executable.json").read_text())["startedEastern"])
    if abs((instant-own_start).total_seconds())<=600:
     exceptions.append({"capture":capture,"variant":variant,"field":"operational observation time","actual":original,"basis":"The isolated health scenario supplies a moving API clock and current HTTP observations; both timestamps must be within ten minutes of their test process start."})
     r["name"]=r["name"].replace(observed[1],"<current API observation>",1)
  if apply_clock and (not r["enabled"] or clock.get("includeEnabledDates",False)) and r["type"]=="Button" and re.fullmatch(r"\d{4}-\d{2}-\d{2} 📆",r["name"]):
   expected=clock["standaloneDate"] if variant=="Standalone" else start.date().isoformat()
   if r["name"]==expected+" 📆":matched="date";r["name"]="<API-owned date default>"
  elif apply_clock and not r["enabled"] and r["type"]=="Edit" and r["name"]=="" and re.fullmatch(r"\d{2}:\d{2}:\d{2}",r.get("value") or ""):
   value=r["value"];valid=value==clock["standaloneTime"] if variant=="Standalone" else abs((datetime.datetime.combine(start.date(),datetime.time.fromisoformat(value),start.tzinfo)-start).total_seconds())<=600
   if valid:matched="time";r["value"]="<API-owned time default>"
  if matched:exceptions.append({"capture":capture,"variant":variant,"field":matched,"actual":original,"basis":clock["basis"]})
  result.append(r)
 return result
files=sorted((root/"Hybrid").glob("*.json"))
for left in files:
 if spec.get("comparison",{}).get("captures") and left.stem not in spec["comparison"]["captures"]:continue
 right=root/"Standalone"/left.name
 if not right.exists():continue
 a=json.loads(left.read_text(encoding="utf-8-sig"));b=json.loads(right.read_text(encoding="utf-8-sig"))
 if not isinstance(a,list) or not a or not isinstance(a[0],dict) or "type" not in a[0]:continue
 region=spec.get("comparison",{}).get("region")
 if region:
  def within(items,folder):
   size=json.loads((root/folder/(left.stem+"-window.json")).read_text());w=size["width"];h=size["height"]
   return [r for r in items if r["x"]>=w*region["left"] and r["x"]+r["width"]<=w*region["right"] and r["y"]>=h*region["top"] and r["y"]+r["height"]<=h*region["bottom"]]
  a=within(a,"Hybrid");b=within(b,"Standalone")
 def rows(items):
  # Provider-generated IDs and absolute screen placement are not business
  # output. Keep every named control and every editable/readonly value.
  return [r for r in items if (r.get("name") or r.get("value") is not None) and not r.get("offscreen",False) and r.get("name") not in allowed]
 clipped=spec.get("comparison",{}).get("clippedNeighborControls",[])
 if clipped:
  def owned(items,variant):
   result=[]
   for r in items:
    rule=next((v for v in clipped if r.get("type")==v["type"] and r.get("name")==v["name"]),None)
    if rule:neighbors.append({"capture":left.name,"variant":variant,"actual":r,"basis":rule["basis"]})
    else:result.append(r)
   return result
  a=owned(a,"Hybrid");b=owned(b,"Standalone")
 selected=spec.get("comparison",{}).get("onlyNamedControls")
 if selected:a=[r for r in a if r.get("name") in selected];b=[r for r in b if r.get("name") in selected]
 a=clock_rows(rows(a),"Hybrid",left.name);b=clock_rows(rows(b),"Standalone",left.name)
 def key(r):return (r["type"],r["name"],r["enabled"],r.get("value"))
 ac=collections.Counter(map(key,a));bc=collections.Counter(map(key,b))
 missing=list((ac-bc).elements());extra=list((bc-ac).elements())
 geometry=[]
 if not missing and not extra:
  grouped=collections.defaultdict(list)
  for r in b:grouped[key(r)].append(r)
  for r in a:
   candidate=grouped[key(r)].pop(0)
   for field in ("x","y","width","height"):
    if abs(r.get(field,0)-candidate.get(field,0))>2:geometry.append({"control":key(r),"field":field,"hybrid":r.get(field),"standalone":candidate.get(field)})
 case={"capture":left.name,"hybridControls":len(a),"standaloneControls":len(b),"missing":missing,"extra":extra,"geometryDifferences":geometry}
 required=spec.get("comparison",{}).get("requiredValues",{}).get(left.stem,[])
 absent={variant:[value for value in required if not any(value in (row.get("name"),row.get("value")) for row in values)] for variant,values in (("Hybrid",a),("Standalone",b))}
 if any(absent.values()):case["missingRequiredValues"]=absent;report["status"]="Failed"
 if not a or not b:case["error"]="The selected comparison area has no named controls or values"
 if missing or extra or geometry or not a or not b:report["status"]="Failed"
 report["cases"].append(case)
if not report["cases"]:report["status"]="Failed";report["error"]="No paired actual FlaUI captures"
report["approvedExceptions"]=exceptions
report["excludedNeighborControls"]=neighbors
if report["status"]=="Passed" and exceptions:report["status"]="PassedWithApprovedExceptions"
if report["status"]=="Passed" and neighbors:report["status"]="PassedWithScopedExclusions"
(root/"comparison.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
print(json.dumps({"status":report["status"],"captures":len(report["cases"]),"report":str(root/"comparison.json")}))
sys.exit(0 if report["status"] in ("Passed","PassedWithApprovedExceptions","PassedWithScopedExclusions") else 1)
