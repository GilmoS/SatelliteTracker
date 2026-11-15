#!/usr/bin/env bash
# Stops a running measurement (the PID in out/measure.pid). Output files are kept.
source "$(dirname "$0")/lib.sh"

[ -s "$PID_FILE" ] || die "no measurement running (no $PID_FILE)"
pid="$(cat "$PID_FILE")"
if kill -0 "$pid" 2>/dev/null; then
  kill -TERM "$pid"
  echo "Sent TERM to measurement PID $pid; it stops within a few seconds."
else
  echo "PID $pid is not running; removing the stale $PID_FILE"
  rm -f "$PID_FILE"
fi
