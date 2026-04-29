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
