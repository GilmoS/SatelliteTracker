#!/usr/bin/env bash
# Creates (or reuses) the measurement tester and stores its API key in out/tester-api-key.
# Never prints the key.
#   1. allowlist $TESTER_EMAIL through the admin API on the API's loopback port (X-Admin-Key)
#   2. register through Caddy (/api/auth/register) -> real tester API key
#   3. give the tester alert minutes and a dummy FCM token (SET_FCM_TOKEN=0 skips this), so
#      PassNotificationJob does its real per-tick work instead of returning at "No active testers
#      with an FCM token". The tester subscribes to no pass, so nothing is ever sent to FCM.
# Idempotent: with a working key already in out/, only step 3 is re-applied.
source "$(dirname "$0")/lib.sh"

SET_FCM_TOKEN="${SET_FCM_TOKEN:-1}"
DUMMY_FCM_TOKEN="memory-measurement-dummy-token-not-a-device"

status_of() { # method url [curl args...] -> prints HTTP status, body in $BODY_FILE
  local method="$1" url="$2"; shift 2
  curl -sk --max-time 30 -o "$(native_path "$BODY_FILE")" -w '%{http_code}' -X "$method" "$@" "$url"
}
BODY_FILE="$(mktemp)"
trap 'rm -f "$BODY_FILE"' EXIT

if [ -s "$TESTER_KEY_FILE" ]; then
  code="$(status_of GET "$CADDY_URL/api/settings/me" -H "X-Api-Key: $(tester_key)")"
  [ "$code" = "200" ] || die "the stored key in $TESTER_KEY_FILE is rejected (HTTP $code). It belongs to another database? Delete the file and re-run (a new email may be needed: TESTER_EMAIL=...)."
  echo "Reusing the tester key in $TESTER_KEY_FILE"
else
  code="$(status_of POST "$(api_local_url)/api/admin/allowlist" \
    -H "X-Admin-Key: $(admin_key)" -H 'Content-Type: application/json' \
    --data "{\"email\":\"$TESTER_EMAIL\"}")"
  [ "$code" = "200" ] || die "allowlisting $TESTER_EMAIL failed (HTTP $code)"
  echo "Allowlisted $TESTER_EMAIL"

  code="$(status_of POST "$CADDY_URL/api/auth/register" -H 'Content-Type: application/json' \
    --data "{\"email\":\"$TESTER_EMAIL\",\"displayName\":\"Memory measurement\"}")"
  case "$code" in
    200) ;;
    409) die "$TESTER_EMAIL already has an active key but out/ has none. Re-run with another TESTER_EMAIL=..." ;;
    *) die "registration failed (HTTP $code)" ;;
  esac
  # The key is shown only once: keep the raw response in out/ if it cannot be parsed.
  if ! (umask 077; json_eval 'j.apiKey' < "$BODY_FILE" | tr -d '\r\n' > "$TESTER_KEY_FILE") \
     || [ ! -s "$TESTER_KEY_FILE" ]; then
    rm -f "$TESTER_KEY_FILE"
    (umask 077; cp "$BODY_FILE" "$OUT_DIR/register-response.json")
    die "registration answered 200 but no apiKey could be read; raw response kept in $OUT_DIR/register-response.json"
  fi
  echo "Registered $TESTER_EMAIL; key saved to $TESTER_KEY_FILE"
fi

if [ "$SET_FCM_TOKEN" = "1" ]; then
  code="$(status_of PUT "$CADDY_URL/api/settings/me" -H "X-Api-Key: $(tester_key)" \
    -H 'Content-Type: application/json' --data '{"alertMinutes":[5,10,30]}')"
  [ "$code" = "200" ] || die "setting alert minutes failed (HTTP $code)"
  code="$(status_of PUT "$CADDY_URL/api/settings/me/fcm-token" -H "X-Api-Key: $(tester_key)" \
    -H 'Content-Type: application/json' --data "{\"fcmToken\":\"$DUMMY_FCM_TOKEN\"}")"
  [ "$code" = "200" ] || die "setting the dummy FCM token failed (HTTP $code)"
  echo "Alert minutes [5,10,30] and a dummy FCM token set (no pass subscriptions, so no sends)"
fi
