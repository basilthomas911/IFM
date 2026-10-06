#!/bin/sh
set -eu
endpoint=http://greptimedb:4000/v1/sql
attempt=0
until curl --fail --silent --get --data-urlencode 'sql=SELECT 1' "$endpoint" >/dev/null; do
  attempt=$((attempt + 1))
  if [ "$attempt" -ge 60 ]; then echo 'GreptimeDB did not become ready' >&2; exit 1; fi
  sleep 2
done
for sql in "CREATE DATABASE IF NOT EXISTS ifm_logs WITH (ttl='5d')" "ALTER DATABASE ifm_logs SET 'ttl'='5d'" "CREATE DATABASE IF NOT EXISTS ifm_metrics WITH (ttl='30d')" "ALTER DATABASE ifm_metrics SET 'ttl'='30d'"; do
  response=$(curl --fail-with-body --silent --show-error --get --data-urlencode "sql=$sql" "$endpoint")
  printf '%s\n' "$response"
  echo "$response" | grep -Eq '"output"[[:space:]]*:[[:space:]]*\[' || exit 1
done
