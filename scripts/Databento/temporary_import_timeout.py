"""Temporarily extend the local Scylla write wait, restoring it after the load.

No schema or data is changed. A durable receipt retains the original setting;
the watchdog also restores it at a bounded deadline if the importer fails.
"""
import argparse
import datetime as dt
import json
import time
import subprocess
from pathlib import Path

CONFIG='write_request_timeout_in_ms'


def cql(query):
    return subprocess.run(['cqlsh','--request-timeout=30','-e',query],
                          text=True,capture_output=True,check=True).stdout


def read_timeout():
    output=cql("SELECT JSON value FROM system.config WHERE name='"+CONFIG+"';")
    for line in output.splitlines():
        if line.strip().startswith('{'):
            return float(json.loads(line.strip())['value'])/1000
    raise RuntimeError('Current write timeout was not returned')


def set_timeout(seconds):
    milliseconds=int(seconds*1000)
    cql("UPDATE system.config SET value='"+str(milliseconds)+"' WHERE name='"+CONFIG+"';")
    assert read_timeout()==seconds


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,required=True)
    parser.add_argument('--seconds',type=float,default=10)
    parser.add_argument('--maximum-minutes',type=int,default=180)
    parser.add_argument('--restore',action='store_true')
    args=parser.parse_args()
    receipt=args.root/'temporary-write-timeout.json'
    def save(value):
        temp=receipt.with_suffix('.tmp')
        temp.write_text(json.dumps(value,indent=2))
        temp.replace(receipt)
    if args.restore:
        proof=json.loads(receipt.read_text())
    else:
        if receipt.exists():
            previous=json.loads(receipt.read_text())
            if not previous.get('restored'):
                raise RuntimeError('An existing timeout receipt must be restored before a new guard starts')
            receipt.rename(receipt.with_name('previous-timeout-'+str(time.time_ns())+'.json'))
        assert 2<=args.seconds<=30 and 1<=args.maximum_minutes<=180
        proof=dict(original_seconds=read_timeout(),temporary_seconds=args.seconds,
                   started_utc=dt.datetime.now(dt.timezone.utc).isoformat(),restored=False)
        save(proof)
    try:
        if not args.restore:
            set_timeout(args.seconds)
            print(json.dumps(proof),flush=True)
            manifest=json.loads((args.root/'import-manifest.json').read_text())
            required={table+'/'+p['value_date'] for table in manifest['tables'] for p in manifest['partitions']}
            deadline=time.monotonic()+args.maximum_minutes*60
            while time.monotonic()<deadline:
                checkpoint=args.root/'load-checkpoint.json'
                done=json.loads(checkpoint.read_text()) if checkpoint.exists() else {}
                if required.issubset(done):
                    proof['restore_reason']='All source dates loaded into both tables'
                    break
                time.sleep(15)
            else:
                proof['restore_reason']='Watchdog deadline reached'
    finally:
        current=read_timeout()
        if current not in (proof['original_seconds'],proof['temporary_seconds']):
            proof['restore_error']='Setting changed externally; not overwriting it'
            save(proof)
            raise RuntimeError(proof['restore_error'])
        set_timeout(proof['original_seconds'])
        proof.update(restored=True,restored_utc=dt.datetime.now(dt.timezone.utc).isoformat())
        save(proof)
        print(json.dumps(proof),flush=True)


if __name__=='__main__':
    main()
