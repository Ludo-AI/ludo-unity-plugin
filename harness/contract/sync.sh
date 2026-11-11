#!/bin/sh
# Refresh the API contract snapshot the harness validates against.
# Source of truth: the backend's generated public spec on master (prod == master),
# read from git, so a branch or uncommitted work checked out there never leaks in.
#   harness/contract/sync.sh [path-to-prometheus-server] [ref, default master]
set -e
here="$(cd "$(dirname "$0")" && pwd)"
backend="${1:-$here/../../../prometheus-server}"
ref="${2:-master}"
spec=src/api/swagger/openapi-public.json
git -C "$backend" show "$ref:$spec" > "$here/openapi-public.json"
echo "contract synced from $backend $ref ($(git -C "$backend" log -1 --format='%h %ad' --date=short "$ref" -- $spec))"
