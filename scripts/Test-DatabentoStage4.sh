#!/usr/bin/env bash
set -euo pipefail

# Managed offline subset only. Use an already-built net10.0 application test assembly;
# this does not qualify a Linux native backend or connect to PostgreSQL/live providers.
test_assembly="${1:?Usage: bash Test-DatabentoStage4.sh /path/to/Application.MarketData.UnitTests.dll /path/to/results}"
results_directory="${2:?An explicit writable test-results directory is required}"
test -f "$test_assembly"
mkdir -p -- "$results_directory"

dotnet vstest "$test_assembly" \
  '--TestCaseFilter:FullyQualifiedName~Stage4&FullyQualifiedName!~Stage4SubscriptionBoundsTests' \
  '--logger:trx;LogFileName=stage4-linux-contracts.trx' "--ResultsDirectory:$results_directory"
dotnet vstest "$test_assembly" \
  '--TestCaseFilter:FullyQualifiedName~Stage4SubscriptionBoundsTests' \
  '--logger:trx;LogFileName=stage4-linux-bounds.trx' "--ResultsDirectory:$results_directory"

# VSTest can exit zero when a filter matches no tests; do not call an empty run qualified.
for result in stage4-linux-contracts.trx stage4-linux-bounds.trx; do
  if command -v rg >/dev/null 2>&1; then
    rg -q '<Counters total="[1-9][0-9]*"' "$results_directory/$result"
  else
    grep -Eq '<Counters total="[1-9][0-9]*"' "$results_directory/$result"
  fi
done
printf '%s\n' 'Implemented managed offline subset passed; native/provider/composer and live acceptance are not implied.'
