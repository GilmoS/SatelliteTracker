#!/bin/sh
# LOCAL STACK ONLY. Command of the one-shot db-grants service in compose.local.yaml, which runs after
# the migrator and before the api on every `up`: re-applies 02-default-privileges-migrator.sql as
# sattrakk_migrator. That is the local equivalent of Azure runbook step 5 (README.md): on a fresh
# volume __EFMigrationsHistory only exists once the migrator has run, so its SELECT-only
# restriction can only be applied then. Idempotent, so running it on every `up` is harmless.
#
# Connection from the environment: PGHOST, PGDATABASE (set by compose); the password from the
# migrator_db_password secret file.
set -eu

password_file=/run/secrets/migrator_db_password
if [ ! -r "$password_file" ]; then
  echo "db-grants: secret file '$password_file' is missing or not readable" >&2
  exit 1
fi

PGPASSWORD="$(tr -d '\r\n' < "$password_file")"
export PGPASSWORD

exec psql -v ON_ERROR_STOP=1 --no-psqlrc --username sattrakk_migrator \
  -f /sattrakk-db/02-default-privileges-migrator.sql
