#!/usr/bin/env bash
# Memory measurement run. For the given duration:
#   - every 15 s : docker stats of api, caddy, postgres  -> out/samples.csv
#   - every 5 min: RestartCount / OOMKilled of api, caddy -> out/events.csv
#   - throughout : docker logs --timestamps -f of api     -> out/api.log
#   - throughout : synthetic load (load.sh)               -> out/load.csv
# Output files of a previous run are moved to out/run-<time>/ first.
# Stop early: ./stop.sh (or kill the PID in out/measure.pid). Needs setup-tester.sh first.
#   ./measure.sh 10h | 5m | 30s
source "$(dirname "$0")/lib.sh"

# Detached runs (run-overnight.sh) send this script's own output to a log file.
if [ -n "${MEASURE_LOG:-}" ]; then
  command -v cygpath > /dev/null 2>&1 && MEASURE_LOG="$(cygpath -u "$MEASURE_LOG")"
  exec >> "$MEASURE_LOG" 2>&1 < /dev/null
fi

DURATION_S="$(duration_to_seconds "${1:?usage: measure.sh <duration, e.g. 10h or 5m>}")"
SAMPLE_EVERY=15
EVENTS_EVERY=300

if [ -s "$PID_FILE" ] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
  die "a measurement is already running (PID $(cat "$PID_FILE")); ./stop.sh to stop it"
fi
tester_key > /dev/null
for svc in api caddy postgres; do require_container "$svc" > /dev/null; done

# Keep the previous run's output.
arch="$OUT_DIR/run-$(date -u +%Y%m%dT%H%M%SZ)"
for f in samples.csv events.csv load.csv api.log summary.txt; do
  if [ -e "$OUT_DIR/$f" ]; then mkdir -p "$arch"; mv "$OUT_DIR/$f" "$arch/"; fi
done
[ -d "$arch" ] && echo "Previous output moved to $arch"

START="$(epoch_now)"
END=$((START + DURATION_S))
echo $$ > "$PID_FILE"
echo "Measuring for ${DURATION_S}s: $(utc_now) -> $(date -u -d "@$END" +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -r "$END" +%Y-%m-%dT%H:%M:%SZ). PID $$. Output in $OUT_DIR"

CHILDREN=()
LOGS_PID_FILE="$OUT_DIR/.logs.pid"
cleanup() {
  trap - EXIT INT TERM HUP
  # Loops first (the log stream's own trap stops its docker logs), then any docker logs left over.
  for p in "${CHILDREN[@]}"; do kill "$p" 2>/dev/null || true; done
  wait 2>/dev/null || true
  [ -s "$LOGS_PID_FILE" ] && kill "$(cat "$LOGS_PID_FILE")" 2>/dev/null || true
  rm -f "$PID_FILE" "$LOGS_PID_FILE"
  echo "Measurement finished $(utc_now)"
}
trap cleanup EXIT
trap 'exit 130' INT TERM HUP

sleep_until() { local now; now="$(epoch_now)"; if [ "$1" -gt "$now" ]; then nap $(( $1 - now )); fi; }

# --- api log stream (re-attached if the container restarts or is recreated) -------------------
stream_logs() {
  local first=1 since dl=""
  # docker logs -f never ends by itself while the container runs: stop it with this loop.
  trap '[ -n "$dl" ] && kill "$dl" 2>/dev/null; [ -n "$NAP_PID" ] && kill "$NAP_PID" 2>/dev/null; exit 0' TERM INT HUP
  while [ "$(epoch_now)" -lt "$END" ]; do
    id="$(container_id api)"
    if [ -n "$id" ]; then
      if [ "$first" = 1 ]; then
        "$DOCKER_BIN" logs --timestamps -f "$id" >> "$OUT_DIR/api.log" 2>&1 &
      else
        "$DOCKER_BIN" logs --timestamps -f --since "$since" "$id" >> "$OUT_DIR/api.log" 2>&1 &
      fi
      dl=$!; echo "$dl" > "$LOGS_PID_FILE"
      first=0
      wait "$dl" 2>/dev/null || true
      dl=""
    fi
    since="$(utc_now)"
    echo "$(utc_now) [measure] api log stream ended; re-attaching" >> "$OUT_DIR/api.log"
    nap 5
  done
}

# --- docker stats every 15 s -------------------------------------------------------------------
sample_stats() {
  stop_on_term
  echo "timestamp_utc,container,mem_bytes,mem_limit_bytes,mem_pct,cpu_pct" > "$OUT_DIR/samples.csv"
  local n=0 ids ts
  while [ "$(epoch_now)" -lt "$END" ]; do
    ts="$(utc_now)"
    ids=()
    for svc in api caddy postgres; do id="$(container_id "$svc")"; [ -n "$id" ] && ids+=("$id"); done
    if [ "${#ids[@]}" -gt 0 ]; then
      docker stats --no-stream --format '{{.Name}}|{{.MemUsage}}|{{.MemPerc}}|{{.CPUPerc}}' "${ids[@]}" 2>/dev/null \
        | awk -F'|' -v ts="$ts" -v p="$COMPOSE_PROJECT-" '
          function bytes(s,   n, u) {
            gsub(/ /, "", s); n = s; sub(/[A-Za-z]+$/, "", n); u = s; sub(/^[0-9.]+/, "", u)
            if (u == "B") return n
            if (u == "KiB") return n * 1024;          if (u == "kB") return n * 1000
            if (u == "MiB") return n * 1048576;       if (u == "MB") return n * 1000000
            if (u == "GiB") return n * 1073741824;    if (u == "GB") return n * 1000000000
            if (u == "TiB") return n * 1099511627776
            return -1
          }
          {
            name = $1; sub("^" p, "", name); sub(/-[0-9]+$/, "", name)
            split($2, m, "/"); mem = $3; cpu = $4; gsub(/[% ]/, "", mem); gsub(/[% \r]/, "", cpu)
            printf "%s,%s,%.0f,%.0f,%s,%s\n", ts, name, bytes(m[1]), bytes(m[2]), mem, cpu
          }' >> "$OUT_DIR/samples.csv"
    fi
    n=$((n + 1))
    sleep_until $((START + n * SAMPLE_EVERY))
  done
}

# --- restart / OOM every 5 min (and once at the very end) --------------------------------------
sample_events() {
  stop_on_term
  echo "timestamp_utc,container,restart_count,oom_killed,status,started_at" > "$OUT_DIR/events.csv"
  local n=0
  while :; do
    for svc in api caddy; do
      id="$(container_id "$svc")"
      if [ -n "$id" ]; then
        echo "$(utc_now),$svc,$(docker inspect --format '{{.RestartCount}},{{.State.OOMKilled}},{{.State.Status}},{{.State.StartedAt}}' "$id")" >> "$OUT_DIR/events.csv"
      else
        echo "$(utc_now),$svc,,,not-running," >> "$OUT_DIR/events.csv"
      fi
    done
    [ "$(epoch_now)" -ge "$END" ] && break
    n=$((n + 1))
    next=$((START + n * EVENTS_EVERY)); [ "$next" -gt "$END" ] && next="$END"
    sleep_until "$next"
  done
}

stream_logs &   CHILDREN+=($!)
sample_stats &  CHILDREN+=($!)
sample_events & CHILDREN+=($!)
"$MEASURE_DIR/load.sh" "$END" & CHILDREN+=($!)

# Samplers and load stop by themselves at END; the log stream is cut by cleanup.
wait "${CHILDREN[1]}" "${CHILDREN[2]}" "${CHILDREN[3]}" 2>/dev/null || true
