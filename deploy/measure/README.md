# deploy/measure — overnight memory measurement (I2.9)

Measures real memory use of the local production-like stack (`deploy/`, `local.sh`) over ~10 hours
(≈10 hourly pass calculations, ≈5 TLE fetches, ~600 notification ticks) under a light synthetic
Android-like load, to choose the VM's swap size and confirm the API's 512 MB limit.

Decision rule, fixed before the run:

| API peak memory overnight | API limit | Swap |
|---|---|---|
| ≤ 300 MB | 512 MB | 1 GB |
| 300–420 MB | 512 MB | 2 GB |
| > 420 MB | investigate before changing anything | — |

`summarize.sh` prints the API peak in both MiB (docker's unit; the 512m limit is 512 MiB) and MB.

## Run it

```bash
# Git Bash (Windows) or bash (macOS/Linux), with the stack up (deploy/local.sh up -d)
deploy/measure/run-overnight.sh          # = preflight → setup-tester → measure.sh 10h, detached
deploy/measure/stop.sh                   # stop early (output is kept)
deploy/measure/summarize.sh              # summary of out/ (any time, also during the run)
```

`run-overnight.sh [duration]` prints the PID and the stop command. The measurement keeps running
after the terminal is closed:

- **Windows (Git Bash):** it runs in a hidden PowerShell process (`keep-awake.ps1`) that holds a
  "system + display required" request (`SetThreadExecutionState`, the Windows equivalent of
  `caffeinate -dimsu`) until `measure.sh` ends. No admin rights needed. Also keep the laptop on AC
  power: a manual lid-close or power-button sleep is not blocked.
- **macOS:** `nohup caffeinate -dimsu ./measure.sh <duration>`. Linux: `nohup` only.

## Scripts

| Script | Does |
|---|---|
| `preflight.sh` | api/caddy/postgres running; `/health/ready` through Caddy is `Healthy`, or `Degraded` only from `tle-freshness`/`pass-freshness` (fresh DB; reported); Workstation GC in the api container (`System.GC.Server: false` in the published `runtimeconfig.json`, no `DOTNET_gcServer`); image tags/ids, memory limits, Docker VM memory, CPUs, arch; `RestartCount`/`OOMKilled`. Report in `out/preflight.txt`, exit 1 on failure. |
| `setup-tester.sh` | Allowlists `memory-test@example.com` (`TESTER_EMAIL=` to change) through the admin API on the API's loopback port (key from `deploy/secrets/Admin__ApiKey`), registers through Caddy, stores the tester key in `out/tester-api-key`. Sets alert minutes `[5,10,30]` and a dummy FCM token so `PassNotificationJob` does its real per-tick work (it returns early when no tester has a token); the tester subscribes to no pass, so nothing is sent (`SET_FCM_TOKEN=0` skips this). Idempotent: reuses a working key. |
| `measure.sh <duration>` | `10h`, `5m`, `30s`. Starts the samplers, the log stream and `load.sh`; moves a previous run's files to `out/run-<time>/`. Refuses to start twice. |
| `load.sh` | Started by `measure.sh`. Synthetic load through Caddy with the tester key (below). |
| `summarize.sh [dir]` | Samples per container, memory min/avg/max, load by endpoint and status, restart/OOM, job marker counts and times. Writes `<dir>/summary.txt`. |
| `run-overnight.sh [duration]` | Preflight → setup-tester → detached `measure.sh` (default `10h`). |
| `stop.sh` | Stops a running measurement. |

The scripts find the containers by compose labels (project `sattrakk`) and the API's loopback port
with `docker port`, so a local port override works unchanged. Every output, the tester key
included, goes to `out/`, which is git-ignored (`deploy/measure/.gitignore`). No script prints a
secret. Needs `docker`, `curl`, `node` (JSON parsing; no `jq`) and `awk`.

## Output (`out/`)

| File | Content |
|---|---|
| `samples.csv` | every 15 s, per container (api, caddy, postgres): `timestamp_utc,container,mem_bytes,mem_limit_bytes,mem_pct,cpu_pct`. `docker stats` values (MiB/GiB/kB/…) normalized to bytes. |
| `events.csv` | every 5 min and at the end, api and caddy: `timestamp_utc,container,restart_count,oom_killed,status,started_at`. A changed `started_at` with the same `restart_count` means a manual restart/recreate. |
| `load.csv` | one row per request: `timestamp_utc,endpoint,target,http_status,duration_ms`. `000` = no answer (timeout/connection). |
| `api.log` | `docker logs --timestamps -f` of the api for the whole run. Starts with everything the container logged since it was created (earlier restarts included), so the summary's marker counts can include lines from before the run; compare timestamps with the run's start. Re-attached if the container restarts (marked `[measure] api log stream ended`). |
| `preflight.txt`, `summary.txt`, `runner.log` | reports; `runner.log` has the detached runs' own output. |

### Synthetic load (`load.sh`)

| Every | Requests |
|---|---|
| 30 s | `GET /api/satellites`, then `GET /api/passes/{id}` for each active satellite |
| 60 s | `GET /api/satellites/{id}/position` for each active satellite (calls N2YO through the 30 s cache; don't go faster) |
| 5 min | `GET /api/satellites/{id}/orbit` for each active satellite; `GET /api/passes/{id}/track` for the first upcoming pass |

About 8 requests a minute with two satellites, far under the tester limit (60/min). Position calls
are about 120 N2YO requests an hour.

## Job correlation — grep patterns for `api.log`

The jobs do **not** log their own start/end. Decided for I2.9 (option b): no application change;
use the markers below. Console log entries span two lines (`info: <Category>[0]` then the
message), each with a docker timestamp, so grep the **message** line.

| Job | Interval | Pattern (ERE) | Meaning |
|---|---|---|---|
| TleUpdateJob | 2 h, first at startup | `Sending HTTP request GET [^ ]*/satellite/tle/` | start of one satellite's TLE fetch; the run's **first** one ≈ job start |
| | | `Received HTTP response [0-9]+ for GET [^ ]*/satellite/tle/` | end of one fetch; the run's **last** one ≈ job end (then an unlogged 6-month TLE cleanup) |
| | | `Failed to fetch TLE for NORAD\|TLE update job encountered` | failure |
| PassCalculationJob | 1 h, first at startup | `Upserted passes for satellite` | one satellite's prediction + upsert done (inserted/updated/deleted counts) |
| | | `Calculated passes for ` | the same satellite logged by the job; the **last** of a run ≈ job end. No start line: the run starts shortly before the first one. |
| | | `Pass calculation failed for\|Pass calculation job encountered` | failure (`No TLE record found` on a fresh DB's startup run is the known case) |
| PassNotificationJob | 60 s | — | **nothing** on a normal tick. Correlate by time only (one tick a minute from api start). |
| | | `No active testers with an FCM token` | tick skipped (setup-tester.sh's dummy token prevents this) |
| | | `Pass notification job encountered\|Failed to (get user settings\|get pending notification passes\|get pass subscriptions\|get pass notification log\|claim notification\|record notification outcome\|clear dead FCM token)` | errors |
| PassRetentionJob | startup + 03:00 Asia/Jerusalem | `Pass retention deleted` | one run (start ≈ end) |
| (load) N2YO positions | 60 s | `Sending HTTP request GET [^ ]*/satellite/positions/` | live-position calls from the load, not a job |

```bash
grep -E 'Received HTTP response [0-9]+ for GET [^ ]*/satellite/tle/' deploy/measure/out/api.log
grep -E 'Calculated passes for |Pass calculation failed for' deploy/measure/out/api.log
```

Timestamps in `api.log`, `samples.csv`, `events.csv` and `load.csv` are all UTC, so a memory step in
`samples.csv` can be matched to the job lines around the same time.

## Notes for this machine (Windows + Docker Desktop)

- Apache holds `0.0.0.0:8080`, so the stack runs with a local, git-ignored port override that maps
  the API to `127.0.0.1:18080`: `deploy/local.sh -f measure/out/port-override.yaml up -d`
  (file content: `services: api: ports: !override ["127.0.0.1:18080:8080"]`).
- `docker` in Git Bash is a sh wrapper around `docker.exe`; the log stream calls `docker.exe`
  directly so stopping it doesn't orphan the process. Git Bash path conversion is turned off in
  the scripts (`MSYS_NO_PATHCONV`), and paths for `curl -o` go through `cygpath`.
- Docker Desktop's VM has more memory (≈2.8 GiB) than the target VM (1 GiB). The container
  numbers (api/caddy/postgres usage against their limits) are what this measures; the host VM's
  total is not representative.
