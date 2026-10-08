#!/usr/bin/env bash
set -euo pipefail
# Requires an already provisioned scheduler database; credentials come from a protected external settings file.
repo_root="$(cd -- "$(dirname -- "$0")/../.." && pwd)"
deploy_root="${1:?Usage: Install-IFMLinuxScheduler.sh deployment-root protected-settings.json}"
settings="${2:?A protected settings file is required}"
mkdir -p -- "$deploy_root"
for entry in Host:TomasAI.IFM.Application.ServerManager.SchedulerHost Administration:TomasAI.IFM.Application.ScheduledTask.Administration Tasks/FuturesMarketClose:TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose Tasks/FuturesMarketOpen:TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen Tasks/SetClosingPrice:TomasAI.IFM.Application.ScheduledTask.SetClosingPrice; do
  destination="${entry%%:*}"; project="${entry#*:}"
  dotnet publish "$repo_root/$project/$project.csproj" -m:1 -c Release --no-self-contained -o "$deploy_root/$destination"
done
python3 - "$repo_root" "$deploy_root" "$settings" <<'PY'
import json, pathlib, sys, os
repo, root, settings = map(pathlib.Path, sys.argv[1:])
base = json.loads((repo/'TomasAI.IFM.Application.ServerManager.SchedulerHost/appsettings.json').read_text())
supplied = json.loads(settings.read_text())
base['ConnectionStrings'] = supplied['ConnectionStrings']
host = base['SchedulerHost']; host.update(supplied.get('SchedulerHost', {}))
host.update(ActorManaged=True, SeedInitialSchedules=False, DeploymentRoot=str(root.resolve()), TaskRunRoot=str(root.resolve()/'TaskRuns'))
host['DependencyEndpoints']['API'] = supplied.get('SchedulerHost', {}).get('DependencyEndpoints', {}).get('API', 'http://localhost:22543/health/launch-ready')
for task in host['TaskCatalog']:
 task['ExecutablePath'] = task['ExecutablePath'].replace('\\', '/').removesuffix('.exe')
 task['WorkingDirectory'] = task['WorkingDirectory'].replace('\\', '/')
for seed in host['InitialSchedules']: seed['Enabled'] = False
for section in ('Nats','Telemetry'):
 if section in supplied: base[section] = supplied[section]
path = root/'scheduler.settings.json'
path.write_text(json.dumps(base, indent=2)); path.chmod(0o600)
for task in host['TaskCatalog']:
 path=root/task['WorkingDirectory']/'appsettings.json'; data=json.loads(path.read_text())
 for sink in data.get('Serilog', {}).get('WriteTo', []):
  if sink['Name']=='File': sink['Args']['path']=str(root/'TaskRuns'/(task['TaskKey']+'_.log'))
 if 'Telemetry' in supplied: data['Telemetry']=supplied['Telemetry']
 if 'Nats' in supplied: data['Nats']['Url']=supplied['Nats']['Producer']['Url']
 path.write_text(json.dumps(data,indent=2))
PY
printf 'Installed actor-managed artifacts at %s; configure one systemd owner before preparing definitions.\n' "$deploy_root"
