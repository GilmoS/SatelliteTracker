#!/bin/sh
# LOCAL STACK ONLY. Mounted by compose.local.yaml into the postgres container as
# /docker-entrypoint-initdb.d/10-sattrakk-roles.sh, so the official image runs it ONCE, when the
# postgres_data volume is first created (never on later starts). It runs while the init server is
# up on the Unix socket only, before the migrator can connect.
#
#   1. 01-roles-admin.sql as the container superuser ($POSTGRES_USER), the local stand-in for the
#      Azure server admin
#   2. sets both roles' passwords from /run/secrets/migrator_db_password and app_db_password
#   3. 02-default-privileges-migrator.sql connected as sattrakk_migrator
#
# __EFMigrationsHistory doesn't exist yet at this point; the db-grants service re-runs 02 after the
# migrator on every `up` (see compose.local.yaml).
#
# The image either executes this file or sources it (depending on the execute bit, which a Windows
# bind mount may or may not show), so everything runs in a subshell: options and variables stay
# local, and a failure still returns non-zero, which aborts the init.
(
  set -eu

  sql_dir=/sattrakk-db

  read_secret() {
    secret_file="/run/secrets/$1"
    if [ ! -r "$secret_file" ]; then
      echo "initdb-roles: secret file '$secret_file' is missing or not readable" >&2
      return 1
    fi
    # One line: drop CR/LF so a file saved with a trailing (CR)LF still matches the connection string.
    secret_value="$(tr -d '\r\n' < "$secret_file")"
    if [ -z "$secret_value" ]; then
      echo "initdb-roles: secret file '$secret_file' is empty" >&2
      return 1
    fi
    printf '%s' "$secret_value"
  }

  migrator_password="$(read_secret migrator_db_password)"
  app_password="$(read_secret app_db_password)"

  run_psql() {
    psql -v ON_ERROR_STOP=1 --no-psqlrc --dbname "$POSTGRES_DB" "$@"
  }

  echo "initdb-roles: 01-roles-admin.sql as $POSTGRES_USER"
  run_psql --username "$POSTGRES_USER" -f "$sql_dir/01-roles-admin.sql"

  # The passwords reach psql through the environment and \getenv, never through the command line
  # (visible in the process list) and never as text in a file. :'var' quotes them as SQL literals.
  # Postgres stores them as SCRAM-SHA-256 hashes (the default password_encryption).
  echo "initdb-roles: setting the passwords of sattrakk_migrator and sattrakk_app"
  SATTRAKK_MIGRATOR_PW="$migrator_password" SATTRAKK_APP_PW="$app_password" \
    run_psql --username "$POSTGRES_USER" --quiet <<'SQL'
\getenv migrator_pw SATTRAKK_MIGRATOR_PW
\getenv app_pw SATTRAKK_APP_PW
ALTER ROLE sattrakk_migrator PASSWORD :'migrator_pw';
ALTER ROLE sattrakk_app PASSWORD :'app_pw';
SQL

  # Over the init server's Unix socket. PGPASSWORD is only used if local auth isn't "trust".
  echo "initdb-roles: 02-default-privileges-migrator.sql as sattrakk_migrator"
  PGPASSWORD="$migrator_password" \
    run_psql --username sattrakk_migrator -f "$sql_dir/02-default-privileges-migrator.sql"
)
