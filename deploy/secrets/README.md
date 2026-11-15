# deploy/secrets

One file per secret, read by `compose.local.yaml` and mounted into the containers at
`/run/secrets/<file name>`. Everything in this folder except this README is git-ignored. Never
commit a value, and never paste one into an issue, PR or log.

| File | Used by | Content |
|------|---------|---------|
| `N2YO__ApiKey` | api → `N2YO:ApiKey` | N2YO API key |
| `Firebase__ServiceAccountJson` | api → `Firebase:ServiceAccountJson` | the Firebase service-account key **JSON content** (the whole file, not a path) |
| `Admin__ApiKey` | api → `Admin:ApiKey` | `X-Admin-Key` value, at least 32 characters (`openssl rand -hex 32`) |
| `ConnectionStrings__DefaultConnection` | api → `ConnectionStrings:DefaultConnection` | the app's Postgres connection string, user `sattrakk_app` (below) |
| `migrator_connection_string` | migrator | the migration bundle's connection string, user `sattrakk_migrator` (below) |
| `postgres_password` | postgres (`POSTGRES_PASSWORD_FILE`) | the password of the local superuser `satellite_user` (used only by the init script) |
| `migrator_db_password` | postgres (init script), db-grants | the password of `sattrakk_migrator` |
| `app_db_password` | postgres (init script) | the password of `sattrakk_app` |

The API's file names are its configuration keys with `__` in place of `:`. The API reads every
file mounted into its `/run/secrets` as a setting, and a secret file overrides `appsettings*.json`.

## Connection strings

The local database runs in the `postgres` service. Each service connects as its own role (see
`../db/README.md`), and each password must equal the matching password file:

```text
migrator_connection_string:
Host=postgres;Port=5432;Database=satellite_tracker;Username=sattrakk_migrator;Password=<migrator_db_password>;SSL Mode=Disable;Timeout=15;Command Timeout=30;Keepalive=30

ConnectionStrings__DefaultConnection:
Host=postgres;Port=5432;Database=satellite_tracker;Username=sattrakk_app;Password=<app_db_password>;SSL Mode=Disable;Timeout=15;Command Timeout=30;Keepalive=30;Maximum Pool Size=20
```

Use a different random password for each role (`openssl rand -hex 24`), and don't reuse
`postgres_password`.

All three database passwords only take effect when the `postgres_data` volume is first created
(the init script sets the role passwords then). To change one later, either change it inside the
database or remove the volume (`./local.sh down -v`, which deletes the local data).

## Writing the files

Write single-line values without a trailing newline, so nothing extra ends up in a key:

```bash
printf '%s' '<your-n2yo-api-key>' > N2YO__ApiKey
printf '%s' "$(openssl rand -hex 32)" > Admin__ApiKey
printf '%s' "$(openssl rand -hex 24)" > migrator_db_password
printf '%s' "$(openssl rand -hex 24)" > app_db_password
cp <path-to-service-account.json> Firebase__ServiceAccountJson
```

One trailing LF is trimmed by the API anyway, and the migrator strips CR/LF, but a Windows CRLF
would leave a `\r` in an API setting.

Files must be readable by the container users (`app`, UID 1654; `postgres`). With Docker Desktop
that is automatic; on Linux, make them readable (e.g. `chmod 644`).
