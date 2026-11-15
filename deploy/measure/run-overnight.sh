#!/usr/bin/env bash
# Overnight run: preflight -> setup-tester -> measure.sh <duration> (default 10h), the last one
# detached so it survives closing the terminal, with sleep prevented for the whole run.
#   macOS/Linux: nohup caffeinate -dimsu ./measure.sh 10h   (caffeinate when available)
#   Windows (Git Bash): a hidden PowerShell process (keep-awake.ps1) holds a "system + display
#                       required" request and runs measure.sh
# Prints the PID and the command to stop it. Runner output: out/runner.log.
#   ./run-overnight.sh [duration]
source "$(dirname "$0")/lib.sh"

DURATION="${1:-10h}"
duration_to_seconds "$DURATION" > /dev/null
RUNNER_LOG="$OUT_DIR/runner.log"

if [ -s "$PID_FILE" ] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
  die "a measurement is already running (PID $(cat "$PID_FILE")); $MEASURE_DIR/stop.sh to stop it"
fi
rm -f "$PID_FILE"

"$MEASURE_DIR/preflight.sh"
echo
"$MEASURE_DIR/setup-tester.sh"
echo

echo "=== run-overnight $(utc_now), duration $DURATION ===" >> "$RUNNER_LOG"
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*)
    git_bash="$(cygpath -w /)bin\\bash.exe"
    [ -f "$(cygpath -u "$git_bash")" ] || git_bash="$(cygpath -w "$(command -v bash)")"
    q() { printf '"%s"' "$1"; }   # one quoted argument inside Start-Process -ArgumentList
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -WindowStyle Hidden -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File $(q "$(cygpath -w "$MEASURE_DIR/keep-awake.ps1")") -Bash $(q "$git_bash") -Script $(q "$(cygpath -w "$MEASURE_DIR/measure.sh")") -Duration $DURATION -Log $(q "$(cygpath -w "$RUNNER_LOG")")'"
    awake="keep-awake.ps1 (hidden PowerShell; system + display kept awake)"
    ;;
  *)
    if command -v caffeinate > /dev/null; then
      nohup caffeinate -dimsu "$MEASURE_DIR/measure.sh" "$DURATION" >> "$RUNNER_LOG" 2>&1 < /dev/null &
      awake="caffeinate -dimsu"
    else
      nohup "$MEASURE_DIR/measure.sh" "$DURATION" >> "$RUNNER_LOG" 2>&1 < /dev/null &
      awake="none (no caffeinate here: make sure the machine does not sleep)"
    fi
    ;;
esac

# measure.sh writes its PID once it has started.
for _ in $(seq 1 30); do [ -s "$PID_FILE" ] && break; sleep 1; done
[ -s "$PID_FILE" ] || die "measure.sh did not start; see $RUNNER_LOG"

echo "Measurement running for $DURATION, detached from this terminal."
echo "  PID:        $(cat "$PID_FILE")   (in $PID_FILE)"
echo "  sleep:      $awake"
echo "  output:     $OUT_DIR  (samples.csv, events.csv, load.csv, api.log; runner.log)"
echo "  stop it:    $MEASURE_DIR/stop.sh"
echo "  summary:    $MEASURE_DIR/summarize.sh   (after it ends, or any time during the run)"
