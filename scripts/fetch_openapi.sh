#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TARGET="$REPO_ROOT/openapi.json"
TMP="$(mktemp "$REPO_ROOT/openapi.json.XXXXXX")"
trap 'rm -f "$TMP"' EXIT

curl --fail --silent --show-error --location https://api.buildin.ai/v2/openapi.json --output "$TMP"
python3 - "$TMP" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding='utf-8'))
info = doc.get('info', {})
if doc.get('openapi') != '3.1.0' or info.get('title') != 'Buildin Developer API V2' or info.get('version') != '2.0.0':
    raise SystemExit('error: downloaded document is not Buildin Developer API V2 2.0.0')
if any(path.startswith('/v1') for path in doc.get('paths', {})):
    raise SystemExit('error: downloaded document contains V1 routes')
PY
cmp -s "$TMP" "$TARGET" || mv "$TMP" "$TARGET"
echo "Validated and refreshed $TARGET"
