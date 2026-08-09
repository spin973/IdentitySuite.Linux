# IdentitySuite.Linux — Docker + PostgreSQL Example

This repository is the companion project for the article  
**[Deploy IdentitySuite on Docker: Complete Guide with PostgreSQL](https://identitysuite.net/blog/identitysuite/docker-postgresql-guide)**  
published on [identitysuite.net](https://identitysuite.net).

It demonstrates the minimum setup required to run [IdentitySuite](https://www.nuget.org/packages/IdentitySuite) — a self-hosted OpenID Connect and OAuth 2.0 authentication server — inside a Linux Docker container, backed by a PostgreSQL database in a separate container, orchestrated with Docker Compose.

---

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) 10
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Windows/macOS) or Docker Engine + Docker Compose plugin (Linux)

No PostgreSQL installation on the host is required.

---

## Project structure

```
.
├── docker-compose.yml
└── IdentitySuite.Linux/
    ├── Dockerfile
    ├── IdentitySuite.Linux.csproj
    ├── Program.cs
    └── IdentitySuite/
        └── IdentitySuiteSettings.Development.json
```

> **Why `IdentitySuite.Linux` and not `IdentitySuite`?**  
> Naming the project `IdentitySuite` would conflict with the NuGet package of the same name, causing circular reference errors at build time.

---

## Quick start

Clone the repository and run from the solution root:

```bash
docker compose up --build
```

On first run Docker will:

1. Pull the `postgres:16` and `mcr.microsoft.com/dotnet/aspnet:10.0` base images.
2. Build the application image using the multi-stage `Dockerfile`.
3. Start the `db` container and initialize the PostgreSQL database.
4. Start the `identitysuite.linux` container, connect to PostgreSQL, create the schema and seed the initial data.

> ⚠️ **Before the first run**, make sure `Initialize: true` is set in `IdentitySuite/IdentitySuiteSettings.{environment}.json`. Without this flag, IdentitySuite will not create or migrate the database and the application will fail to start correctly.

Once you see `Now listening on: http://[::]:8080` in the logs, open your browser at:

```
http://localhost:5000
```

You will see the IdentitySuite login page. Use the default administrator credentials created during initialization to access the admin dashboard.

---

## Configuration

### docker-compose.yml

The PostgreSQL connection string is injected into the application container via an environment variable:

```yaml
environment:
  - IdentitySuiteOptions__Database__ConnectionStrings__PostgreSqlConnection=Host=db;Port=5432;Database=identitydb;Username=identity;Password=secret
```

ASP.NET Core maps the double-underscore (`__`) separator to nested JSON keys, so this overrides the value in `IdentitySuiteSettings.Development.json` at runtime.

### IdentitySuiteSettings.Development.json

```json
{
  "IdentitySuiteOptions": {
    "Initialize": true,
    "Database": {
      "ConnectionStrings": {
        "PostgreSqlConnection": "Host=db;Port=5432;Database=identitydb;Username=identity;Password=secret"
      }
    }
  }
}
```

- **`Initialize: true`** — **must be set explicitly before the first run.** When enabled, IdentitySuite creates the database schema and applies EF Core migrations on startup. It is safe to leave enabled after that; it is a no-op when the schema is already up to date.
- **`Host=db`** — resolves to the `db` service via the Docker Compose internal network. For local development outside Docker, change this to `Host=localhost`.

> **Production note:** move credentials out of `docker-compose.yml` into a `.env` file (added to `.gitignore`) or use Docker Secrets / your cloud provider's secrets manager.

---

## Going to production: persisting configuration, certificates, and the license binding

The `docker-compose.yml` above only declares a persistent volume for `pgdata` (PostgreSQL). The `identitysuite.linux` container itself starts from a **fresh filesystem** every time it's recreated — an image update, `docker compose up --force-recreate`, a host reboot, etc. That silently resets:

- **`/app/IdentitySuite`** — where IdentitySuite persists any setting written at runtime after first boot (themes, client logos, and other admin-side customization).
- **`/app/Certificates`** — where OpenIddict generates its signing and encryption certificates on first run. A new certificate on every restart invalidates every existing access/refresh token (forced logout for everyone) and breaks decryption of the ASP.NET Core DataProtection keyring if it's shared through the database.
- The license environment binding — inside a container, IdentitySuite backs it with a persisted seed file rather than a hardware ID (containers are, by design, hardware-agnostic). If that seed doesn't survive a recreation either, the license has to reactivate against the license server.

None of this is destructive on its own (certificates and the license both regenerate/reactivate automatically), but it means the app never reaches a stable configuration in a container that gets recreated regularly.

**Fix**: mount persistent volumes on these paths, and set `IDENTITYSUITE_DATA_DIR` so the license seed backup lands on one of them instead of falling back to `LocalApplicationData` — which inside a container resolves under `/app` itself, not a real home directory, defeating the point of having a *second*, independent anchor:

```yaml
services:
  identitysuite.linux:
    # ...
    volumes:
      - suite-config:/app/IdentitySuite
      - suite-certs:/app/Certificates
      - suite-seed:/data
    environment:
      - IdentitySuiteOptions__Database__ConnectionStrings__PostgreSqlConnection=Host=db;Port=5432;Database=identitydb;Username=identity;Password=secret
      - IDENTITYSUITE_DATA_DIR=/data

volumes:
  pgdata:
  suite-config:
  suite-certs:
  suite-seed:
```

A named volume that's never been used before is created empty and owned by `root` — which the non-root `$APP_UID` user in the base runtime image can't write to. Pre-create and `chown` the mount points in the `Dockerfile`, **before** switching to `$APP_UID` and before `WORKDIR`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN mkdir -p /data /app/IdentitySuite /app/Certificates \
    && chown -R $APP_UID:$APP_UID /data /app
USER $APP_UID
WORKDIR /app
```

> Chown the whole `/app`, not just the two subfolders: `mkdir -p /app/IdentitySuite` also creates the parent `/app` as a side effect while still running as `root`. A `chown` limited to the two subfolders alone would leave `/app` itself root-owned and break every other file the app needs to write there directly (like `license.token`).

Also make sure the final stage's `COPY` keeps ownership consistent with the non-root user — the sample `Dockerfile` in this repo is missing this and should be updated before use in production:

```dockerfile
COPY --from=publish --chown=$APP_UID:$APP_UID /app/publish .
```

Same tradeoff as the `pgdata` volume: once these volumes exist, a future image update that ships new default settings/themes/assets under `IdentitySuite/` won't automatically reach an already-deployed instance, since the volume's content wins over what's baked into the image. That's the correct behavior here — certificates, license state, and admin customization should never be silently overwritten by an image update.

---

## Useful commands

| Command | Description |
|---|---|
| `docker compose up --build` | Build and start both containers |
| `docker compose up` | Start without rebuilding (after first run) |
| `docker compose down` | Stop containers, preserve the `pgdata` volume |
| `docker compose down -v` | Stop containers and delete all data (full reset) |
| `docker compose logs -f` | Follow live logs from all services |

---

## Related resources

- 📖 [Full guide on identitysuite.net](https://identitysuite.net/blog/identitysuite/docker-postgresql-guide)
- 📖 [Getting Started](https://identitysuite.net/documentation/GetStarted)
- 📖 [Authentication Made Easy — Securing SPAs with IdentitySuite](https://identitysuite.net/blog/identitysuite/authentication-made-easy-guide)
- 📦 [IdentitySuite on NuGet](https://www.nuget.org/packages/IdentitySuite)
- 📚 [IdentitySuite Documentation](https://identitysuite.net/documentation)
