"""Summarize five alternating performance-plan comparisons without replacing evidence."""
import argparse,hashlib,json,statistics,subprocess
from pathlib import Path
from datetime import datetime,timezone

def equivalent(a,b):
    if isinstance(a,bool) or isinstance(b,bool):return type(a) is type(b) and a==b
    if isinstance(a,(int,float)) and isinstance(b,(int,float)):return a==b
    if type(a) is not type(b):return False
    if isinstance(a,dict):return a.keys()==b.keys() and all(equivalent(a[k],b[k]) for k in a)
    if isinstance(a,list):return len(a)==len(b) and all(equivalent(x,y) for x,y in zip(a,b))
    return a==b

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--artifacts',type=Path,required=True);args=parser.parse_args()
    p=args.artifacts.resolve();app=Path(__file__).resolve().parents[1];repo=app.parent
    def read(n):return json.loads((p/n).read_text(encoding='utf-8-sig'))
    def digest(f):return hashlib.sha256(f.read_bytes()).hexdigest()
    runs=[read(f'models-pair-{i}.json') for i in range(1,6)]
    identity=lambda r:(r['case'],r.get('count',0))
    expected=[identity(r) for r in runs[0]]
    assert all([identity(r) for r in run]==expected for run in runs),'Model cases differ'
    models=[]
    for i,key in enumerate(expected):
        row={'case':key[0],'count':key[1]};metrics={}
        for field in runs[0][i]:
            if not field.endswith('_ms'):continue
            values=[run[i][field] for run in runs];assert all(v>=0 for v in values)
            metrics[field]={'median':statistics.median(values),'min':min(values),'max':max(values),'runs':values}
        row['milliseconds']=metrics
        after='after_ms' if 'after_ms' in metrics else 'after_prepare_ms'
        row['speedup']=metrics['before_ms']['median']/metrics[after]['median'] if metrics[after]['median']>0 else None
        if 'after_repaint_ms' in metrics:row['unchanged_visible_cells_speedup']=metrics['before_ms']['median']/metrics['after_repaint_ms']['median']
        models.append(row)
    frames=[];portfolios=[]
    data={s:[read(f'frames-{s}-{i}.json') for i in range(1,6)] for s in ('before','after')}
    for i,initial in enumerate(data['before'][0]):
        row={'plans':initial['plans']}
        for side,runs in data.items():
            assert all(r[i]['plans']==row['plans'] and r[i]['frames']==initial['frames'] for r in runs)
            row[side]={k:statistics.median(r[i][k] for r in runs) for k in ('p50Ms','p95Ms','maxMs','preparationP50Ms','preparedFirstFrameP50Ms')}
        frames.append(row)
    data={s:[read(f'portfolio-{s}-{i}.json') for i in range(1,6)] for s in ('before','after')}
    for i,initial in enumerate(data['before'][0]):
        row={'fundRows':initial['fundRows'],'display_parity':True}
        for side,runs in data.items():
            assert all(r[i]['fundRows']==row['fundRows'] and equivalent(r[i]['document'],initial['document']) for r in runs),'Portfolio display fields changed'
            row[side]={k:statistics.median(r[i][k] for r in runs) for k in ('p50Ms','p95Ms')}
        row['speedup']=row['before']['p50Ms']/row['after']['p50Ms'];portfolios.append(row)
    sources=[f for folder in ('src','tests','benches','tools') for f in (app/folder).rglob('*') if f.suffix in ('.rs','.py','.ps1') and '__pycache__' not in f.parts and 'obj' not in f.parts and 'bin' not in f.parts]
    report={'generated_utc':datetime.now(timezone.utc).isoformat(),'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip(),'profile':'development','method':'Median of five pairs; AB/BA alternating order. Stage means use at least 50 ms per algorithm; frames use the original 60-pointer-frame harness. CPU-only; excludes GPU and live broker latency.','baseline':read('baseline-source.json'),'rustc':subprocess.check_output(['rustc','-Vv'],text=True),'configuration_sha256':{n:digest(app/n) for n in ('Cargo.toml','Cargo.lock')},'source_sha256':{str(f.relative_to(app)).replace(chr(92),'/'):digest(f) for f in sorted(sources)},'fixture_sha256':{str(f.relative_to(app)).replace(chr(92),'/'):digest(f) for f in sorted((app/'tests/fixtures/backend').glob('*')) if f.is_file()},'executables_sha256':{f.name:digest(f) for f in sorted(p.glob('*.exe'))},'models':models,'frames':frames,'portfolio':portfolios}
    (p/'performance-plan-final.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    for r in models:
        m=r['milliseconds'];a='after_ms' if 'after_ms' in m else 'after_prepare_ms'
        speed=f"{r['speedup']:.2f}x" if r['speedup'] is not None else 'below timer resolution'
        print(f"{r['case']}/{r['count']}: {m['before_ms']['median']:.6f} -> {m[a]['median']:.6f} ms ({speed})")
    print('Portfolio fields match across all five original/refactored pairs.')

if __name__=='__main__':main()
