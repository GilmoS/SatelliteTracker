-- 01-roles-admin.sql: least-privilege database roles, part 1 of 2.
--
-- RUN AS: the server admin, connected to the application database (locally the container
-- superuser; on Azure the Flexible Server admin, which is NOT a superuser).
--   psql -v ON_ERROR_STOP=1 -d satellite_tracker -f 01-roles-admin.sql
--
-- What it does (idempotent, safe to re-run):
--   * creates sattrakk_migrator and sattrakk_app if missing, WITHOUT passwords
--     (set them separately: locally the init script, on Azure \password; see README.md)
--   * connection limits: migrator 3, app 25 (above the app's pool maximum of 20)
--   * database: CONNECT only for the two roles, nothing for PUBLIC
--   * schema public: migrator USAGE + CREATE (owns the schema objects), app USAGE only
--
-- Table and sequence privileges are NOT here: they belong to the objects' owner, sattrakk_migrator,
-- and are set by 02-default-privileges-migrator.sql. README.md explains the split.
--
-- No password and no database name appear in this file: the database is current_database().

\set ON_ERROR_STOP on
\echo Applying 01-roles-admin.sql to database :DBNAME as :USER

-- Roles. CREATE ROLE has no IF NOT EXISTS, hence the DO block. Attributes not named here keep their
-- defaults (NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS). They are deliberately
-- not spelled out: on Azure a non-superuser may not even name SUPERUSER/REPLICATION/BYPASSRLS in
-- CREATE/ALTER ROLE.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'sattrakk_migrator') THEN
    CREATE ROLE sattrakk_migrator LOGIN;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'sattrakk_app') THEN
    CREATE ROLE sattrakk_app LOGIN;
  END IF;
END
$$;

-- Re-asserted on every run, so a re-run also repairs drift.
ALTER ROLE sattrakk_migrator LOGIN CONNECTION LIMIT 3;
ALTER ROLE sattrakk_app      LOGIN CONNECTION LIMIT 25;

-- Database: nobody but these two roles (and the owner/admin) may connect. REVOKE ALL also removes
-- PUBLIC's default TEMPORARY privilege. GRANT/REVOKE ON DATABASE takes no expression, hence format().
DO $$
BEGIN
  EXECUTE format('REVOKE ALL ON DATABASE %I FROM PUBLIC', current_database());
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO sattrakk_migrator, sattrakk_app', current_database());
END
$$;

-- Schema public. Since Postgres 15 PUBLIC has no CREATE on it; the REVOKE makes that explicit, so
-- sattrakk_app cannot create objects even on a database that inherited an older default.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO sattrakk_migrator;
GRANT USAGE ON SCHEMA public TO sattrakk_app;
REVOKE CREATE ON SCHEMA public FROM sattrakk_app;
