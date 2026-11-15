# deploy/secrets

One file per secret, read by `compose.local.yaml` and mounted into the containers at
`/run/secrets/<file name>`. Everything in this folder except this README is git-ignored. Never
commit a value, and never paste one into an issue, PR or log.

| File | Used by | Content |
|------|---------|---------|
| `N2YO__ApiKey` | api → `N2YO:ApiKey` | N2YO API key |
| `Firebase__ServiceAccountJson` | api → `Firebase:ServiceAccountJson` | the Firebase service-account key **JSON content** (the whole file, not a path) |
| `Admin__ApiKey` | api → `Admin:ApiKey` | `X-Admin-Key` value, at least 32 characters (`openssl rand -hex 32`) |
| `ConnectionStrings__DefaultConnection` | api → `ConnectionStrings:DefaultConnection` | the app's Postgres connection string (below) |
| `migrator_connection_string` | migrator | the connection string the migration bundle uses (below) |
| `postgres_password` | postgres (`POSTGRES_PASSWORD_FILE`) | the password of the local `satellite_user` |

The API's file names are its configuration keys with `__` in place of `:`. The API reads every
file mounted into its `/run/secrets` as a setting, and a secret file overrides `appsettings*.json`.

## Connection strings

The local database runs in the `postgres` service. Both connection strings use the same user for
now (separate migrator and app roles come later), and the password must equal `postgres_password`:

```text
Host=postgres;Port=5432;Database=satellite_tracker;Username=satellite_user;Password=<postgres_password>;SSL Mode=Disable;Timeout=15;Command Timeout=30;Keepalive=30;Maximum Pool Size=20
```

The Postgres password only takes effect when the `postgres_data` volume is first created. To change
it later, either change it inside the database or remove the volume (`./local.sh down -v`, which
deletes the local data).

## Writing the files

Write single-line values without a trailing newline, so nothing extra ends up in a key:

```bash
printf '%s' '<your-n2yo-api-key>' > N2YO__ApiKey
printf '%s' "$(openssl rand -hex 32)" > Admin__ApiKey
cp <path-to-service-account.json> Firebase__ServiceAccountJson
```

One trailing LF is trimmed by the API anyway, and the migrator strips CR/LF, but a Windows CRLF
would leave a `\r` in an API setting.

Files must be readable by the container users (`app`, UID 1654; `postgres`). With Docker Desktop
that is automatic; on Linux, make them readable (e.g. `chmod 644`).
