#!/bin/sh
# Refresh the API contract snapshot the harness validates against.
# Source of truth: the backend's generated public spec (prod == master).
#   harness/contract/sync.sh [path-to-prometheus-server]
set -e
here="$(cd "$(dirname "$0")" && pwd)"
backend="${1:-$here/../../../prometheus-server}"
cp "$backend/src/api/swagger/openapi-public.json" "$here/openapi-public.json"
echo "contract synced from $backend ($(cd "$backend" && git log -1 --format='%h %ad' --date=short -- src/api/swagger/openapi-public.json))"
