#!/usr/bin/env bash
# Pre-flight checks before a memory measurement run. Prints a report and saves it to
# out/preflight.txt. Exit 1 if the stack is not fit to measure.
#   ./preflight.sh
source "$(dirname "$0")/lib.sh"

REPORT="$OUT_DIR/preflight.txt"
: > "$REPORT"
say() { echo "$*" | tee -a "$REPORT"; }
fail=0

say "# Pre-flight $(utc_now)"
say

say "## Containers"
for svc in api caddy postgres; do
  id="$(container_id "$svc")"
  if [ -z "$id" ]; then say "  $svc: NOT RUNNING"; fail=1; continue; fi
  say "  $(docker inspect --format "$svc: {{.State.Status}}, started {{.State.StartedAt}}, image {{.Config.Image}} ({{printf \"%.19s\" .Image}}), mem limit {{.HostConfig.Memory}} bytes, RestartCount {{.RestartCount}}, OOMKilled {{.State.OOMKilled}}" "$id")"
done
[ "$fail" -eq 0 ] || { say; say "RESULT: FAIL (stack not up: deploy/local.sh up -d)"; exit 1; }
say

say "## Readiness (through Caddy: $CADDY_URL/health/ready)"
ready="$(curl -sk --max-time 10 "$CADDY_URL/health/ready" || true)"
say "  status: ${ready:-<no answer>}"
if [ "$ready" != "Healthy" ]; then
  # Which checks are not Healthy: /health/details is admin-only and reachable only on loopback.
  details="$(curl -s --max-time 10 -H "X-Admin-Key: $(admin_key)" "$(api_local_url)/health/details" || true)"
  bad="$(printf '%s' "$details" | json_eval 'j.checks.filter(c => c.status !== "Healthy").map(c => c.name + "|" + c.status + "|" + (c.reason || ""))' || true)"
  [ -n "$bad" ] || { say "  could not read /health/details"; fail=1; }
  while IFS='|' read -r name status reason; do
    [ -n "$name" ] || continue
    case "$name" in
      tle-freshness|pass-freshness) say "  $name: $status ($reason) -> accepted on a fresh database" ;;
      *) say "  $name: $status ($reason) -> NOT accepted"; fail=1 ;;
    esac
  done <<< "$bad"
fi
say

say "## GC mode inside the api container"
api="$(container_id api)"
gc="$(docker exec "$api" sh -c 'cat /app/SatelliteTracker.API.runtimeconfig.json' | json_eval 'j.runtimeOptions.configProperties["System.GC.Server"]' || true)"
gc_env="$(docker exec "$api" sh -c 'env | grep -i -E "^(DOTNET|COMPlus)_gcServer=" || true')"
say "  runtimeconfig System.GC.Server: ${gc:-<missing>}"
say "  DOTNET_gcServer / COMPlus_gcServer env: ${gc_env:-<not set>}"
if [ "$gc" = "false" ] && { [ -z "$gc_env" ] || echo "$gc_env" | grep -q '=0$'; }; then
  say "  -> Workstation GC in effect"
else
  say "  -> NOT Workstation GC"; fail=1
fi
say

say "## Environment"
say "  $(docker info --format 'Docker {{.ServerVersion}} ({{.OperatingSystem}}), VM memory {{.MemTotal}} bytes, CPUs {{.NCPU}}, arch {{.Architecture}}')"
say "  host: $(uname -s) $(uname -m)"
say

if [ "$fail" -eq 0 ]; then say "RESULT: OK"; else say "RESULT: FAIL"; exit 1; fi
