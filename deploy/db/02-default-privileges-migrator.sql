-- 02-default-privileges-migrator.sql: least-privilege database roles, part 2 of 2.
--
-- RUN AS: sattrakk_migrator (the owner of every table), connected to the application database,
-- after 01-roles-admin.sql:
--   psql -v ON_ERROR_STOP=1 -U sattrakk_migrator -d satellite_tracker -f 02-default-privileges-migrator.sql
--
-- What it does (idempotent, safe to re-run at any time, including after migrations):
--   * default privileges: every table / sequence sattrakk_migrator creates in schema public from now
--     on is readable and writable by sattrakk_app
--   * the same grants on objects that already exist
--   * __EFMigrationsHistory: sattrakk_app may only SELECT it. Skipped (with a notice) while the table
--     doesn't exist yet: run this script again after the first migration.
--
-- sattrakk_app gets exactly: tables SELECT, INSERT, UPDATE, DELETE; sequences USAGE, SELECT.
-- Never TRUNCATE, REFERENCES or TRIGGER, never DDL (it owns nothing).

\set ON_ERROR_STOP on
\echo Applying 02-default-privileges-migrator.sql to database :DBNAME as :USER

-- ALTER DEFAULT PRIVILEGES without FOR ROLE applies to objects created by the CURRENT role. Run as
-- anyone else, it would silently set defaults for the wrong role, so refuse.
DO $$
BEGIN
  IF current_user <> 'sattrakk_migrator' THEN
    RAISE EXCEPTION '02-default-privileges-migrator.sql must run as sattrakk_migrator, not %', current_user;
  END IF;
END
$$;

-- Future objects.
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO sattrakk_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO sattrakk_app;

-- Existing objects. The REVOKEs repair drift if a broader grant was ever made by hand.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO sattrakk_app;
REVOKE TRUNCATE, REFERENCES, TRIGGER ON ALL TABLES IN SCHEMA public FROM sattrakk_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO sattrakk_app;
REVOKE UPDATE ON ALL SEQUENCES IN SCHEMA public FROM sattrakk_app;

-- EF Core's migration history: the app may read it, never change it. Must come after the GRANT ON
-- ALL TABLES above, which includes this table. The migrator creates it on its first run, and the
-- default privileges above then grant it read/write, which is why this script runs again after the
-- first migration.
DO $$
BEGIN
  IF to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
    RAISE NOTICE '__EFMigrationsHistory does not exist yet: run this script again after the first migration';
  ELSE
    REVOKE ALL ON TABLE public."__EFMigrationsHistory" FROM sattrakk_app;
    GRANT SELECT ON TABLE public."__EFMigrationsHistory" TO sattrakk_app;
  END IF;
END
$$;
