#!/bin/sh
# Checks the least-privilege database roles on the LOCAL compose stack (./local.sh up first).
# Prints PASS/FAIL per check and exits 1 if any check fails.
#
#   deploy/db/verify-roles.sh
#
# Everything runs through `docker compose exec` in the postgres container (no psql needed on the
# host). sattrakk_app and sattrakk_migrator connect over TCP with their passwords from the secret
# files, so a check also proves the init script set them. The superuser (the local stand-in for the
# Azure admin) connects over the Unix socket. Writes happen inside rolled-back transactions; the one
# table created (check 5) is dropped again. Never prints a password.
set -u
# Git Bash on Windows would rewrite the /run/... paths inside the sh -c strings below.
export MSYS_NO_PATHCONV=1

cd "$(dirname "$0")/.."

failures=0
out_file="$(mktemp)"
trap 'rm -f "$out_file"' EXIT

# SQL on stdin, run as one role. Output (stdout + stderr) goes to $out_file.
as_app() {
  ./local.sh exec -T postgres sh -c 'PGPASSWORD="$(tr -d "\r\n" < /run/secrets/app_db_password)" exec psql -h 127.0.0.1 -U sattrakk_app -d "$POSTGRES_DB" -X -q -At -v ON_ERROR_STOP=1' >"$out_file" 2>&1
}
as_migrator() {
  ./local.sh exec -T postgres sh -c 'PGPASSWORD="$(tr -d "\r\n" < /run/secrets/migrator_db_password)" exec psql -h 127.0.0.1 -U sattrakk_migrator -d "$POSTGRES_DB" -X -q -At -v ON_ERROR_STOP=1' >"$out_file" 2>&1
}
as_admin() {
  ./local.sh exec -T postgres sh -c 'exec psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -X -q -At -v ON_ERROR_STOP=1' >"$out_file" 2>&1
}

pass() { echo "PASS  $1"; }
fail() {
  echo "FAIL  $1"
  sed 's/^/        /' "$out_file"
  failures=$((failures + 1))
}

# expect_denied <label> <runner>: SQL on stdin must fail with a privilege error.
expect_denied() {
  label="$1"; runner="$2"
  if "$runner"; then
    fail "$label (succeeded, expected permission denied)"
  elif grep -Eqi 'permission denied|must be owner' "$out_file"; then
    pass "$label -> $(grep -Eio 'permission denied[^"]*|must be owner[^"]*' "$out_file" | head -n 1)"
  else
    fail "$label (failed, but not with a privilege error)"
  fi
}

# expect_allowed <label> <runner>: SQL on stdin must succeed.
expect_allowed() {
  label="$1"; runner="$2"
  if "$runner"; then pass "$label"; else fail "$label"; fi
}

echo "Least-privilege role checks (local stack)"

# 1. No DDL for the app.
expect_denied "1. sattrakk_app: CREATE TABLE is denied" as_app <<'SQL'
CREATE TABLE verify_roles_app_create (id int);
SQL

# 2. The app owns nothing, so it cannot drop. In a transaction anyway, so a wrong result can't
#    destroy the table.
expect_denied "2. sattrakk_app: DROP TABLE \"AllowlistedEmails\" is denied" as_app <<'SQL'
BEGIN;
DROP TABLE "AllowlistedEmails";
ROLLBACK;
SQL

# 3. Normal data access, rolled back.
expect_allowed "3. sattrakk_app: SELECT/INSERT/UPDATE/DELETE on \"AllowlistedEmails\" are allowed (rolled back)" as_app <<'SQL'
BEGIN;
INSERT INTO "AllowlistedEmails" ("Id", "Email", "AddedAt")
  VALUES (gen_random_uuid(), 'verify-roles@example.invalid', now());
SELECT count(*) FROM "AllowlistedEmails" WHERE "Email" = 'verify-roles@example.invalid';
UPDATE "AllowlistedEmails" SET "Email" = 'verify-roles-2@example.invalid'
  WHERE "Email" = 'verify-roles@example.invalid';
DELETE FROM "AllowlistedEmails" WHERE "Email" = 'verify-roles-2@example.invalid';
ROLLBACK;
SQL

# 4. Migration history is read-only for the app.
expect_denied "4. sattrakk_app: INSERT into \"__EFMigrationsHistory\" is denied" as_app <<'SQL'
BEGIN;
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('verify-roles', '0');
ROLLBACK;
SQL
expect_allowed "4b. sattrakk_app: SELECT from \"__EFMigrationsHistory\" is allowed" as_app <<'SQL'
SELECT count(*) FROM "__EFMigrationsHistory";
SQL

# 5. Default privileges: a table the migrator creates now is readable by the app straight away.
if as_migrator <<'SQL'
DROP TABLE IF EXISTS verify_roles_default_privs;
CREATE TABLE verify_roles_default_privs (id int);
SQL
then
  if as_app <<'SQL'
SELECT count(*) FROM verify_roles_default_privs;
SQL
  then
    pass "5. sattrakk_migrator creates a table -> sattrakk_app can SELECT it (default privileges)"
  else
    fail "5. sattrakk_app cannot SELECT a table sattrakk_migrator just created"
  fi
  if ! as_migrator <<'SQL'
DROP TABLE verify_roles_default_privs;
SQL
  then
    fail "5. cleanup: sattrakk_migrator could not drop verify_roles_default_privs"
  fi
else
  fail "5. sattrakk_migrator could not create a test table"
fi

# 6. Connection limits.
if as_admin <<'SQL' && [ "$(tr -d '\r' < "$out_file")" = "$(printf 'sattrakk_app|25\nsattrakk_migrator|3')" ]
SELECT rolname || '|' || rolconnlimit FROM pg_roles
WHERE rolname IN ('sattrakk_app', 'sattrakk_migrator') ORDER BY rolname;
SQL
then
  pass "6. pg_roles: sattrakk_migrator CONNECTION LIMIT 3, sattrakk_app CONNECTION LIMIT 25"
else
  fail "6. pg_roles connection limits are not 3 / 25"
fi

# 7. Idempotency: both scripts again, as their intended roles, without errors.
if ./local.sh exec -T postgres sh -c 'exec psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -X -v ON_ERROR_STOP=1 -f /sattrakk-db/01-roles-admin.sql' >"$out_file" 2>&1 \
  && ! grep -q 'ERROR' "$out_file"; then
  pass "7a. 01-roles-admin.sql re-run as superuser: no errors"
else
  fail "7a. 01-roles-admin.sql re-run failed"
fi
if ./local.sh exec -T postgres sh -c 'PGPASSWORD="$(tr -d "\r\n" < /run/secrets/migrator_db_password)" exec psql -h 127.0.0.1 -U sattrakk_migrator -d "$POSTGRES_DB" -X -v ON_ERROR_STOP=1 -f /sattrakk-db/02-default-privileges-migrator.sql' >"$out_file" 2>&1 \
  && ! grep -q 'ERROR' "$out_file"; then
  pass "7b. 02-default-privileges-migrator.sql re-run as sattrakk_migrator: no errors"
else
  fail "7b. 02-default-privileges-migrator.sql re-run failed"
fi

echo
if [ "$failures" -eq 0 ]; then
  echo "All checks passed."
else
  echo "$failures check(s) failed."
  exit 1
fi
