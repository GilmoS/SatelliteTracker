# Shared helpers for deploy/measure/*.sh. Sourced, never run directly.
# Works in Git Bash (Windows) and bash on macOS/Linux. Needs: docker, curl, node, awk.

set -euo pipefail

# Git Bash rewrites arguments that look like POSIX paths (/run/secrets/... -> C:/Program Files/Git/run/...)
# before handing them to docker.exe. Turn that off for everything these scripts run.
export MSYS_NO_PATHCONV=1
export MSYS2_ARG_CONV_EXCL='*'

MEASURE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_DIR="$(dirname "$MEASURE_DIR")"
OUT_DIR="$MEASURE_DIR/out"

COMPOSE_PROJECT="${COMPOSE_PROJECT:-sattrakk}"
CADDY_URL="${CADDY_URL:-https://localhost}"
ADMIN_KEY_FILE="${ADMIN_KEY_FILE:-$DEPLOY_DIR/secrets/Admin__ApiKey}"
TESTER_EMAIL="${TESTER_EMAIL:-memory-test@example.com}"
TESTER_KEY_FILE="$OUT_DIR/tester-api-key"
PID_FILE="$OUT_DIR/measure.pid"

# In Git Bash, `docker` on the PATH is a sh wrapper that starts docker.exe as a child: killing a
# backgrounded `docker logs -f` would kill only the wrapper and orphan docker.exe. Long-running
# docker processes use $DOCKER_BIN.
DOCKER_BIN=docker
command -v docker.exe > /dev/null 2>&1 && DOCKER_BIN=docker.exe

mkdir -p "$OUT_DIR"

# Path for native Windows programs (curl.exe -o): with path conversion off, Git Bash would hand
# them /e/... or /tmp/... unchanged. No-op outside Git Bash.
native_path() { if command -v cygpath > /dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

die() { echo "ERROR: $*" >&2; exit 1; }
utc_now() { date -u +%Y-%m-%dT%H:%M:%SZ; }
epoch_now() { date -u +%s; }

# Container id of a compose service of the stack (empty if not running).
container_id() {
  docker ps -q \
    --filter "label=com.docker.compose.project=$COMPOSE_PROJECT" \
    --filter "label=com.docker.compose.service=$1" | head -n1
}

require_container() {
  local id
  id="$(container_id "$1")"
  [ -n "$id" ] || die "service '$1' of stack '$COMPOSE_PROJECT' is not running (deploy/local.sh up -d)"
  printf '%s' "$id"
}

# The API's loopback URL on the host (127.0.0.1:8080, or whatever a local override maps it to).
api_local_url() {
  local hp
  hp="$(docker port "$(require_container api)" 8080/tcp | grep -m1 '^127\.0\.0\.1:' || true)"
  [ -n "$hp" ] || die "the api container publishes no 127.0.0.1 port for 8080"
  printf 'http://%s' "$hp"
}

admin_key() {
  [ -s "$ADMIN_KEY_FILE" ] || die "admin key file not found or empty: $ADMIN_KEY_FILE"
  tr -d '\r\n' < "$ADMIN_KEY_FILE"
}

tester_key() {
  [ -s "$TESTER_KEY_FILE" ] || die "no tester key in $TESTER_KEY_FILE (run setup-tester.sh)"
  tr -d '\r\n' < "$TESTER_KEY_FILE"
}

# Interruptible sleep for background loops: with stop_on_term set, TERM kills the pending sleep
# too, instead of leaving it orphaned (an orphan keeps inherited output handles open).
NAP_PID=""
nap() { sleep "$1" & NAP_PID=$!; wait "$NAP_PID" 2>/dev/null || true; NAP_PID=""; }
stop_on_term() { trap '[ -n "$NAP_PID" ] && kill "$NAP_PID" 2>/dev/null; exit 0' TERM INT HUP; }

# 10h / 5m / 30s / 90 -> seconds
duration_to_seconds() {
  case "$1" in
    *h) echo $(( ${1%h} * 3600 )) ;;
    *m) echo $(( ${1%m} * 60 )) ;;
    *s) echo $(( ${1%s} )) ;;
    *[!0-9]*|'') die "bad duration '$1' (use e.g. 10h, 5m, 30s)" ;;
    *) echo "$1" ;;
  esac
}

# Reads JSON on stdin, runs a JS expression over it as `j`, prints the result (arrays one per line).
json_eval() {
  node -e '
    let s = ""; process.stdin.on("data", d => s += d).on("end", () => {
      const j = JSON.parse(s.replace(/^\uFEFF/, ""));
      const r = (new Function("j", "return (" + process.argv[1] + ")"))(j);
      if (Array.isArray(r)) r.forEach(x => console.log(x)); else if (r !== undefined && r !== null) console.log(r);
    });' "$1"
}
