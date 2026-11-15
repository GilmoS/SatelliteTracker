#!/usr/bin/env bash
# Summary of a measurement run: samples per container, memory min/max, load by HTTP status,
# restart/OOM values and the job log markers found. Saved to <dir>/summary.txt.
#   ./summarize.sh            # the current run in out/
#   ./summarize.sh out/run-…  # an archived run
source "$(dirname "$0")/lib.sh"

DIR="${1:-$OUT_DIR}"
S="$DIR/samples.csv"; E="$DIR/events.csv"; L="$DIR/load.csv"; LOG="$DIR/api.log"
for f in "$S" "$E" "$L" "$LOG"; do [ -e "$f" ] || die "missing $f"; done

{
echo "# Summary of $DIR ($(utc_now))"
echo
echo "## Samples (docker stats, every 15 s)"
awk -F, 'NR > 1 {
    c = $2; n[c]++; m = $3 + 0
    if (!(c in min) || m < min[c]) min[c] = m
    if (m > max[c]) { max[c] = m; maxat[c] = $1 }
    sum[c] += m; lim[c] = $4
    if (!(c in first)) first[c] = $1; last[c] = $1
    if ($6 + 0 > cpumax[c]) cpumax[c] = $6 + 0
  }
  END {
    printf "  %-9s %7s %10s %10s %10s %10s  %-20s %8s\n", "container", "samples", "min MiB", "avg MiB", "max MiB", "limit MiB", "max at (UTC)", "cpu max%"
    for (c in n) printf "  %-9s %7d %10.1f %10.1f %10.1f %10.0f  %-20s %8.2f\n", c, n[c], min[c]/1048576, sum[c]/n[c]/1048576, max[c]/1048576, lim[c]/1048576, maxat[c], cpumax[c]
    if ("api" in n) printf "\n  api peak: %.1f MiB = %.1f MB (10^6 bytes), first sample %s, last %s\n", max["api"]/1048576, max["api"]/1000000, first["api"], last["api"]
  }' "$S"
echo
echo "## Load requests by endpoint and HTTP status"
awk -F, 'NR > 1 { n[$2 " -> " $4]++ } END { for (k in n) printf "  %5d  %s\n", n[k], k }' "$L" | sort -k2
printf '\n  by status:'
awk -F, 'NR > 1 { st[$4]++ } END { for (s in st) printf "  %s=%d", s, st[s] }' "$L"
printf '   (total %d)\n\n' "$(($(wc -l < "$L") - 1))"
awk -F, 'NR > 1 && $4 != "skipped" { t[$2] += $5; c[$2]++; if ($5 + 0 > mx[$2]) mx[$2] = $5 + 0 }
  END { for (e in c) printf "  %-36s avg %6.0f ms, max %6d ms\n", e, t[e]/c[e], mx[e] }' "$L" | sort
echo
echo "## Restart / OOM (events.csv, every 5 min)"
awk -F, 'NR > 1 { n[$2]++; if (!($2 in f)) f[$2] = $3 "," $4 "," $6; l[$2] = $3 "," $4 "," $5 "," $6 }
  END { for (c in n) printf "  %-6s %d checks; first RestartCount,OOMKilled,StartedAt = %s; last RestartCount,OOMKilled,Status,StartedAt = %s\n", c, n[c], f[c], l[c] }' "$E"
echo
echo "## Job markers in api.log (patterns: README.md)"
cnt() { grep -c -E "$1" "$LOG" || true; }
echo "  TleUpdateJob   TLE requests sent            $(cnt 'Sending HTTP request GET [^ ]*/satellite/tle/')"
echo "                 TLE responses received       $(cnt 'Received HTTP response [0-9]+ for GET [^ ]*/satellite/tle/')"
echo "                 TLE fetch failures           $(cnt 'Failed to fetch TLE for NORAD|TLE update job encountered')"
echo "  PassCalcJob    'Upserted passes' lines      $(cnt 'Upserted passes for satellite')"
echo "                 'Calculated passes' lines    $(cnt 'Calculated passes for ')"
echo "                 failures                     $(cnt 'Pass calculation failed for|Pass calculation job encountered')"
echo "  PassNotifJob   skipped (no FCM token)       $(cnt 'No active testers with an FCM token')"
echo "                 errors                       $(cnt 'Pass notification job encountered|Failed to (get user settings|get pending notification passes|get pass subscriptions|get pass notification log|claim notification|record notification outcome|clear dead FCM token)')"
echo "  N2YO positions requests (load)              $(cnt 'Sending HTTP request GET [^ ]*/satellite/positions/')"
echo "  PassRetentionJob runs                       $(cnt 'Pass retention deleted')"
echo
echo "  TLE fetch times:";        grep -E 'Received HTTP response [0-9]+ for GET [^ ]*/satellite/tle/' "$LOG" | awk '{print "    " $1}' || true
echo "  Pass calculation times:"; grep -E 'Calculated passes for |Pass calculation failed for' "$LOG" | awk '{ts = $1; $1 = ""; sub(/^ +/, ""); print "    " ts "  " $0}' || true
} | tee "$DIR/summary.txt"
