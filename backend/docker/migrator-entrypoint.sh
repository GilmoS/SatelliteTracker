#!/bin/sh
# Entrypoint of the migrator image: applies pending EF Core migrations, then exits.
#
# With no arguments (how compose runs it) the connection string is read from a secret file, so it
# never appears in compose files, environment variables or `docker inspect`:
#   MIGRATOR_CONNECTION_FILE (default /run/secrets/migrator_connection_string)
# With arguments they go to the bundle unchanged, e.g.
#   docker run --rm sattrakk-migrator:local --connection "<connection-string>"
set -eu

if [ "$#" -gt 0 ]; then
  exec ./efbundle "$@"
fi

connection_file="${MIGRATOR_CONNECTION_FILE:-/run/secrets/migrator_connection_string}"

if [ ! -r "$connection_file" ]; then
  echo "migrator: connection string file '$connection_file' is missing or not readable" >&2
  exit 1
fi

# A connection string is one line: drop CR/LF so a file saved with a trailing (CR)LF still works.
connection="$(tr -d '\r\n' < "$connection_file")"

if [ -z "$connection" ]; then
  echo "migrator: connection string file '$connection_file' is empty" >&2
  exit 1
fi

exec ./efbundle --connection "$connection"
