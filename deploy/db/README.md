# deploy/db: least-privilege database roles (I2.8)

The migrator and the API connect as two different Postgres roles:

| Role | Can | Cannot | Connection limit |
|------|-----|--------|------------------|
| `sattrakk_migrator` | `CONNECT`; `USAGE` + `CREATE` on schema `public`; **owns** every table, index and sequence it creates (all of them, through EF Core migrations) | anything outside schema `public` / this database | 3 |
| `sattrakk_app` | `CONNECT`; `USAGE` on schema `public`; tables `SELECT, INSERT, UPDATE, DELETE`; sequences `USAGE, SELECT`; `__EFMigrationsHistory` **`SELECT` only** | `CREATE` anything, `DROP`/`ALTER` (owns nothing), `TRUNCATE`, `REFERENCES`, `TRIGGER`, temp tables | 25 (the app's pool maximum is 20) |

`PUBLIC` has no privilege on the database (`REVOKE ALL ON DATABASE … FROM PUBLIC`), so no other role
can connect unless granted. The admin (database owner) keeps its rights.

The scripts apply to **fresh databases**: the local compose stack (`deploy/`) and the future Azure
server. The dev database used by `dotnet run` (root `docker-compose.yml`) is not touched.

## Files

| File | Run as | Does |
|------|--------|------|
| `01-roles-admin.sql` | the admin (local superuser / Azure server admin) | creates both roles if missing, **without passwords**; connection limits; database `CONNECT`; schema `public` privileges |
| `02-default-privileges-migrator.sql` | `sattrakk_migrator` | default privileges for every table/sequence it creates from now on; the same grants on existing objects; `__EFMigrationsHistory` restricted to `SELECT` (skipped with a notice while the table doesn't exist) |
| `local/initdb-roles.sh` | the postgres container, once | local stack only: 01, then the role passwords from secret files, then 02 |
| `local/apply-grants.sh` | the `db-grants` service | local stack only: 02 again after the migrator, on every `up` |
| `verify-roles.sh` | you, on the host | checks the result on the local stack, prints PASS/FAIL per check |

Both SQL files are idempotent (safe to re-run), take the database from `current_database()`, and
contain no password. 02 refuses to run as any role other than `sattrakk_migrator`.

## Why the script is split by who runs it

On a local Postgres the superuser could run everything. The split exists for Azure Database for
PostgreSQL Flexible Server, and the same two files run everywhere so local and Azure don't drift.

- **The Azure server admin is not a superuser.** It is a member of `azure_pg_admin` with
  `CREATEROLE` and `CREATEDB`. Superuser belongs to Microsoft's own `azuresu`.
- **Postgres 16 changed what `CREATEROLE` grants.** Up to 15, a `CREATEROLE` role could act much
  like a superuser over other roles. Since 16, creating a role gives the creator only membership
  `WITH ADMIN OPTION` on it (with `createrole_self_grant` empty, the default, no `INHERIT` and no
  `SET`). The admin can rename, alter, set the password of, and drop `sattrakk_migrator`, but
  **cannot act as it**: no `SET ROLE sattrakk_migrator`, and no
  `ALTER DEFAULT PRIVILEGES FOR ROLE sattrakk_migrator`, which requires being able to act as that
  role.
- **Default privileges belong to the role that creates the objects.** `ALTER DEFAULT PRIVILEGES`
  only affects objects created later *by the role it is set for*. The migrator creates every table,
  so the defaults must be set as (or for) `sattrakk_migrator`, which the admin can't do.
- **Grants on existing tables need the owner.** `GRANT … ON ALL TABLES` only works on tables the
  grantor owns (or holds with grant option). They belong to `sattrakk_migrator`; a non-superuser
  admin can't grant on them.
- **Database and schema privileges need the owner of those.** The database is owned by the admin,
  and since Postgres 15 schema `public` is owned by `pg_database_owner`, i.e. also the admin.
  `sattrakk_migrator` owns neither, so it can't grant `CONNECT`, `USAGE` or `CREATE`, or revoke
  them from `PUBLIC`.

So 01 is everything only the database owner can do, and 02 is everything only the table owner can
do. Giving the admin `SET` on `sattrakk_migrator` would avoid the split, but would let anyone with
the admin credentials act as the schema owner without a trace of it, so we don't.

## Local stack

Automated, nothing to run by hand. On a **fresh** `postgres_data` volume the official image runs
`local/initdb-roles.sh` (mounted as `/docker-entrypoint-initdb.d/10-sattrakk-roles.sh`) once:

1. `01-roles-admin.sql` as the container superuser `satellite_user`
2. `ALTER ROLE … PASSWORD` for both roles, from `/run/secrets/migrator_db_password` and
   `/run/secrets/app_db_password` (passed to psql through the environment and `\getenv`, never on
   the command line; stored as SCRAM-SHA-256 hashes)
3. `02-default-privileges-migrator.sql` connected as `sattrakk_migrator`

Then on every `up`: `migrator` (as `sattrakk_migrator`) → `db-grants` (02 again, which applies the
`__EFMigrationsHistory` restriction once the migrator has created the table) → `api` (as
`sattrakk_app`). The secret files are described in `../secrets/README.md`.

Only the init script is mounted into `initdb.d`, not the SQL files: the image runs every `*.sql`
there as the superuser, and 02 must run as `sattrakk_migrator`.

**Upgrading an existing I2.7 local stack:** its volume already holds tables owned by
`satellite_user`, and the init script never runs on an existing volume. Recreate it (this deletes
the local data and Caddy's local certificates):

```bash
deploy/local.sh down -v
deploy/local.sh up -d --build
deploy/db/verify-roles.sh
```

## Azure runbook (fresh Flexible Server)

Nothing in the repo runs this automatically. Run it once per new server, in this order. The server
is reachable only from the VM, so connect through an SSH tunnel from a workstation with **psql 16
or newer**:

```bash
ssh -N -L 6432:<server-name>.postgres.database.azure.com:5432 <vm-user>@<vm-address>
```

In another terminal, from the repo root. `host` is the real server name (TLS verifies the
certificate against it), `hostaddr` is the tunnel. `sslrootcert` is the Azure root CA bundle
(Microsoft's Flexible Server TLS documentation lists it), or `system` to use the OS trust store
(libpq 16+).

```bash
ADMIN="host=<server-name>.postgres.database.azure.com hostaddr=127.0.0.1 port=6432 dbname=satellite_tracker user=<server-admin> sslmode=verify-full sslrootcert=<ca-bundle-or-system>"
MIGRATOR="host=<server-name>.postgres.database.azure.com hostaddr=127.0.0.1 port=6432 dbname=satellite_tracker user=sattrakk_migrator sslmode=verify-full sslrootcert=<ca-bundle-or-system>"
```

**Prerequisite:** the database `satellite_tracker` exists and is **owned by the server admin**
(e.g. `psql "${ADMIN/dbname=satellite_tracker/dbname=postgres}" -c 'CREATE DATABASE satellite_tracker'`).
Check with `\l`. 01 grants on the database and schema `public` as their owner.

1. **As server admin, run 01:**
   ```bash
   psql "$ADMIN" -v ON_ERROR_STOP=1 -f deploy/db/01-roles-admin.sql
   ```
2. **As server admin, set both passwords:**
   ```bash
   psql "$ADMIN"
   \password sattrakk_migrator
   \password sattrakk_app
   \q
   ```
   `\password` prompts twice and sends only a SCRAM hash, so the password never appears in the
   server log, `pg_stat_activity` or shell history (unlike `ALTER ROLE … PASSWORD '…'`). Use
   distinct random values and store them where the deploy reads them (connection strings for the
   migrator and the API, `SSL Mode=VerifyFull`).
3. **As `sattrakk_migrator`, run 02:**
   ```bash
   psql "$MIGRATOR" -v ON_ERROR_STOP=1 -f deploy/db/02-default-privileges-migrator.sql
   ```
   Expect the notice `__EFMigrationsHistory does not exist yet`: correct on an empty database.
4. **Run the migrator (first deploy)** with the `sattrakk_migrator` connection string, e.g. on the VM
   `docker run --rm sattrakk-migrator:<tag> --connection "<sattrakk_migrator connection string>"`, or
   however the Azure deploy runs the `migrator` service. It creates every table, owned by
   `sattrakk_migrator`, and the default privileges from step 3 give `sattrakk_app` its access.
5. **As `sattrakk_migrator`, run 02 again:**
   ```bash
   psql "$MIGRATOR" -v ON_ERROR_STOP=1 -f deploy/db/02-default-privileges-migrator.sql
   ```
   This time without the notice: `__EFMigrationsHistory` now exists and becomes `SELECT`-only for
   `sattrakk_app`. Between steps 4 and 5 the app role can write to it, so do step 5 before starting
   the API.

After that, nothing else is needed per deploy: new tables get their grants from the default
privileges, and `__EFMigrationsHistory` is created only once. Re-running 02 after a migration is
harmless.

To check the result: `\dp` in psql as the admin lists table privileges (`sattrakk_app=arwd` on data
tables, `sattrakk_app=r` on `__EFMigrationsHistory`); `\ddp` lists the default privileges;
`SELECT rolname, rolconnlimit FROM pg_roles WHERE rolname LIKE 'sattrakk%';` shows the limits.
`verify-roles.sh` runs against the local stack only.
