#!/bin/sh
# Runs docker compose on the base stack plus the local overlay. Any arguments go to compose:
#   ./local.sh config | up -d | ps | logs -f api | stop api | down
set -eu
cd "$(dirname "$0")"
# Images built locally are tagged sattrakk-api:local / sattrakk-migrator:local.
export IMAGE_TAG="${IMAGE_TAG:-local}"
exec docker compose -f compose.yaml -f compose.local.yaml "$@"
