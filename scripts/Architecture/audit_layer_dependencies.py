"""Declared project graph audit. No production processes or stores are touched.
Run with --baseline FILE to compare staged work, or --strict for the final zero-violation gate.
This is the project-graph gate only; compiled-symbol checks are a separate required gate.
"""
import argparse, json, xml.etree.ElementTree as ET
from pathlib import Path
parser=argparse.ArgumentParser()
parser.add_argument('--root',default=str(Path(__file__).resolve().parents[2]))
parser.add_argument('--output',required=True)
parser.add_argument('--baseline')
parser.add_argument('--strict',action='store_true')
args=parser.parse_args();root=Path(args.root).resolve()
# Explicit composition roots; reusable Application libraries remain subject to the rule.
roots={'TomasAI.IFM.Application.Api.Server.Core','TomasAI.IFM.Application.Api.Server',
'TomasAI.IFM.Application.MarketData.Host','TomasAI.IFM.Application.MarketData.Host.Worker',
'TomasAI.IFM.Application.ServerManager','TomasAI.IFM.Application.DatabaseBackup.Console'}
projects={p.parent.name:p for p in root.glob('TomasAI.IFM.*/*.csproj') if not any(x in p.parent.name for x in ('Tests','Benchmarks','TestProcess','Probe','QualificationHost'))}
edges=[];violations=[]
for name,p in projects.items():
 for ref in ET.parse(p).iter('ProjectReference'):
  target=(p.parent/ref.attrib['Include'].replace('\\','/')).resolve().parent.name
  edges.append((name,target))
  reason=None
  if name.startswith('TomasAI.IFM.Domain.') and target.startswith('TomasAI.IFM.Framework.'): reason='Domain -> Framework'
  elif name.startswith('TomasAI.IFM.Domain.') and target.startswith('TomasAI.IFM.Domain.') and name != target: reason='Domain -> another Domain implementation'
  elif name.startswith('TomasAI.IFM.Application.') and name not in roots and target.startswith('TomasAI.IFM.Domain.'): reason='Reusable Application -> Domain'
  elif name.startswith('TomasAI.IFM.Framework.') and target.startswith(('TomasAI.IFM.Application.','TomasAI.IFM.Domain.')): reason='Framework -> higher layer'
  elif name.startswith('TomasAI.IFM.Shared') and target.startswith(('TomasAI.IFM.Application.','TomasAI.IFM.Domain.','TomasAI.IFM.Framework.')): reason='Shared -> implementation layer'
  if reason: violations.append({'source':name,'target':target,'reason':reason})
graph={n:[] for n in projects}
for a,b in edges: graph.setdefault(a,[]).append(b)
visiting=set();done=set()
def visit(n):
 if n in visiting: raise RuntimeError('Project cycle at '+n)
 if n in done:return
 visiting.add(n)
 for child in graph.get(n,[]):visit(child)
 visiting.remove(n);done.add(n)
for n in graph:visit(n)
report={'projects':len(projects),'edges':len(edges),'compositionRoots':sorted(roots),'violations':sorted(violations,key=lambda x:(x['source'],x['target']))}
Path(args.output).write_text(json.dumps(report,indent=2)+'\n')
if args.baseline:
 baseline=json.loads(Path(args.baseline).read_text())
 old={(v['source'],v['target']) for v in baseline['violations']}
 new=[v for v in violations if (v['source'],v['target']) not in old]
 if new:raise SystemExit('New forbidden edges: '+json.dumps(new))
print(f"{len(projects)} projects; {len(edges)} references; {len(violations)} forbidden declared edges; no cycles")
if args.strict and violations:raise SystemExit(1)
