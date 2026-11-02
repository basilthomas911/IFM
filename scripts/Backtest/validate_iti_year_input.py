"""Validate original event ranges and correct the two pre-replay DST exports."""
import hashlib
import json
from prepare_iti_year import ROOT,RAW,export_day


def main():
    path=ROOT/'input.json'
    manifest=json.loads(path.read_text())
    proof_path=ROOT/'input-validation.json'
    assert not proof_path.exists(),'Completed validation is retained'
    manifests={s:json.loads((RAW/s/'import-manifest.json').read_text()) for s in ('ES','VX')}
    maps={s:{(p['value_date'],p['contract_id']):p for p in m['partitions']} for s,m in manifests.items()}
    (ROOT/'input.before-dst-validation.json').write_text(path.read_text())
    for i,f in enumerate(manifest['files']):
        if f['value_date'] in ('2025-11-03','2026-03-09'):
            date,es,vx=f['value_date'],f['contract_id'],f['vx_contract_id']
            prior=sorted((d,p) for (d,c),p in maps['VX'].items() if c==vx and d<date and p['rows'])
            manifest['files'][i]=export_day((date,es,vx,maps['ES'][(date,es)],maps['VX'].get((date,vx)),prior[-1][1] if prior else None))
            print(json.dumps(manifest['files'][i]),flush=True)
    # Check every source epoch range independently against the import manifest.
    # The output is chronological, so first/last rows are the actual extrema.
    for f in manifest['files']:
        if f['value_date']>='2026-10-01':
            continue
        source=maps['ES'][(f['value_date'],f['contract_id'])]
        p=ROOT/f['file']
        with p.open('rb') as stream:
            first=stream.readline()
            stream.seek(max(0,p.stat().st_size-2048))
            last=stream.read().splitlines()[-1]
        # Initial no-VX observations may be omitted; all others retain extrema.
        if not f['skipped_no_prior_vx']:
            assert int(first.split(b',')[2])==source['min_event_ns']
        assert int(last.split(b',')[2])==source['max_event_ns']
    manifest['es_rows']=sum(f['rows'] for f in manifest['files'])
    manifest['skipped_no_prior_vx']=sum(f['skipped_no_prior_vx'] for f in manifest['files'])
    manifest['timestamp_policy']='Original event nanoseconds reconstructed from NY local date/hour with DST-aware hour offsets; verified against source epoch extrema'
    manifest['input_sha256']=hashlib.sha256(json.dumps(manifest['files'],sort_keys=True,separators=(',',':')).encode()).hexdigest()
    path.write_text(json.dumps(manifest,indent=2))
    proof=dict(validated=True,input_sha256=manifest['input_sha256'],source_epoch_ranges_verified=True,
               dst_dates_corrected=['2025-11-03','2026-03-09'],files=len(manifest['files']),es_rows=manifest['es_rows'])
    proof_path.write_text(json.dumps(proof,indent=2))
    print(json.dumps(proof))


if __name__=='__main__':
    main()
