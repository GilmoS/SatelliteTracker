#!/usr/bin/env bash
# Synthetic Android-like load through Caddy, authenticated as the measurement tester, until the
# given Unix time. Started by measure.sh; not meant to be run alone.
#   every 30 s : GET /api/satellites, then GET /api/passes/{id} for each active satellite
#   every 60 s : GET /api/satellites/{id}/position for each active satellite (calls N2YO: keep it 60 s)
#   every 5 min: GET /api/satellites/{id}/orbit for each active satellite, GET /api/passes/{id}/track
#                for one upcoming pass
# About 8 requests a minute, far under the tester limit (60/min). One CSV row per request.
#   ./load.sh <end-unix-time>
source "$(dirname "$0")/lib.sh"

END="${1:?end unix time}"
CSV="$OUT_DIR/load.csv"
KEY="$(tester_key)"
BODY="$(mktemp)"
trap 'rm -f "$BODY"' EXIT
[ -s "$CSV" ] || echo "timestamp_utc,endpoint,target,http_status,duration_ms" > "$CSV"

SAT_IDS=()      # active satellite ids, refreshed every 30 s
SAT_NAMES=()
PASS_ID=""      # one upcoming pass id, refreshed with the passes

# request <endpoint-template> <target> <path> : logs one row, leaves the body in $BODY
request() {
  local endpoint="$1" target="$2" path="$3" out code secs
  out="$(curl -sk --max-time 30 -o "$(native_path "$BODY")" -w '%{http_code} %{time_total}' \
    -H "X-Api-Key: $KEY" "$CADDY_URL$path" || true)"
  code="${out%% *}"; secs="${out##* }"
  [ -n "$code" ] || { code=000; secs=0; }
  echo "$(utc_now),$endpoint,$target,$code,$(awk -v s="$secs" 'BEGIN{printf "%.0f", s*1000}')" >> "$CSV"
  [ "$code" = "200" ]
}

every_30s() {
  if request "GET /api/satellites" "-" "/api/satellites"; then
    SAT_IDS=(); SAT_NAMES=()
    while IFS='|' read -r id name; do
      [ -n "$id" ] && SAT_IDS+=("$id") && SAT_NAMES+=("${name// /_}")
    done < <(json_eval 'j.filter(s => s.isActive).map(s => s.id + "|" + s.name)' < "$BODY" | tr -d '\r')
  fi
  local i first=""
  for i in "${!SAT_IDS[@]}"; do
    if request "GET /api/passes/{satelliteId}" "${SAT_NAMES[$i]}" "/api/passes/${SAT_IDS[$i]}"; then
      [ -n "$first" ] || first="$(json_eval 'j.length ? j[0].id : null' < "$BODY" | tr -d '\r')"
    fi
  done
  PASS_ID="$first"
}

every_60s() {
  local i
  for i in "${!SAT_IDS[@]}"; do
    request "GET /api/satellites/{id}/position" "${SAT_NAMES[$i]}" "/api/satellites/${SAT_IDS[$i]}/position" || true
  done
}

every_5m() {
  local i
  for i in "${!SAT_IDS[@]}"; do
    request "GET /api/satellites/{id}/orbit" "${SAT_NAMES[$i]}" "/api/satellites/${SAT_IDS[$i]}/orbit" || true
  done
  if [ -n "$PASS_ID" ]; then
    request "GET /api/passes/{id}/track" "$PASS_ID" "/api/passes/$PASS_ID/track" || true
  else
    echo "$(utc_now),GET /api/passes/{id}/track,no-upcoming-pass,skipped,0" >> "$CSV"
  fi
}

stop_on_term
start="$(epoch_now)"; tick=0
while [ "$(epoch_now)" -lt "$END" ]; do
  every_30s
  (( tick % 2 == 0 )) && every_60s
  (( tick % 10 == 0 )) && every_5m
  tick=$((tick + 1))
  next=$((start + tick * 30))
  now="$(epoch_now)"
  [ "$next" -ge "$END" ] && next="$END"
  if [ "$next" -gt "$now" ]; then nap $((next - now)); fi
done
