# Deploying Mobile Bill Management with Docker

Two containers, started together with Docker Compose:

| Container | What it runs | Reached at |
|---|---|---|
| `web` | nginx serving the React app; forwards `/api/*` and `/health` to the API | `http://<server>:WEB_PORT` (default 8080) |
| `api` | ASP.NET Core 10 API | inside Docker only (port 8080), never published to the host |

The database is your **existing SQL Server**. The containers do not create, migrate or change the database on start-up.
The only exception is the optional first-administrator setting described below, which is empty by default.

## Files

| File | Purpose |
|---|---|
| `docker-compose.yml` | Defines both containers and the volume for uploaded bill PDFs |
| `.env.example` | Template for settings and secrets. Copy to `.env` (git-ignored) |
| `src/MobileBill.Api/Dockerfile` | Builds and runs the API; installs fonts the PDF report needs |
| `src/mobilebill-web/Dockerfile` | Builds the React app and serves it with nginx |
| `src/mobilebill-web/nginx.conf` | Page routing, `/api` forwarding, 25 MB upload limit, caching |
| `.dockerignore`, `src/mobilebill-web/.dockerignore` | Keep secrets, local settings and build output out of the images |

## Requirements

- A server with **Docker Engine** and the **Docker Compose plugin** (Linux), or **Docker Desktop** (Windows).
- Network access from that server to SQL Server (host and port in the connection string).
- A **SQL login** (user name and password). Windows authentication does not work from Linux containers.
  Prefer a dedicated login with access to `MobileBillDB` only, rather than `sa`.

## First deployment

1. Copy the repository to the server (for example `git clone`).
2. Create the settings file:
   ```bash
   cp .env.example .env
   ```
   Edit `.env`:
   - `DB_CONNECTION_STRING`: the server, port, `MobileBillDB`, user and password. Keep `Pooling=True`.
   - `JWT_SIGNING_KEY`: a **new** random secret of 64+ characters. For example:
     `openssl rand -base64 64 | tr -d '\n'` (Linux) or
     `[Convert]::ToBase64String((1..64 | % { Get-Random -Max 256 }))` (PowerShell).
   - Leave `BOOTSTRAP_ADMIN_EMAIL` and `BOOTSTRAP_ADMIN_PASSWORD` **empty** when the database already has users.
3. Build and start:
   ```bash
   docker compose up -d --build
   ```
4. Check:
   - `http://<server>:8080/health` shows `Healthy`.
   - `http://<server>:8080` opens the sign-in page, and an existing user can sign in.

### Existing uploaded bill PDFs (optional)

Uploaded PDFs are kept in the Docker volume `bill-uploads`, not in the database.
Batches already in the database work for review and reports without them; the PDF is only needed to parse a batch again.
To bring existing files across, copy the contents of the old `App_Data/BillUploads` folder into the volume:

```bash
docker compose cp ./BillUploads/. api:/data/bill-uploads/
```

## Updating to a new version

```bash
git pull
docker compose up -d --build
```

If the new version includes database changes (new EF Core migrations), apply them **before** starting it.
Generate the script on a development PC and review it with the database owner first:

```bash
.tools/dotnet-ef migrations script --idempotent -p src/MobileBill.Infrastructure -s src/MobileBill.Api -o deploy.sql
```

## Everyday commands

| Task | Command |
|---|---|
| Status | `docker compose ps` |
| API logs | `docker compose logs -f api` |
| Web logs | `docker compose logs -f web` |
| Restart | `docker compose restart` |
| Stop | `docker compose down` (keeps the `bill-uploads` volume) |

## Security checklist

- Serve the site over **HTTPS**: put it behind the company reverse proxy or load balancer, or add a certificate to nginx.
  Sign-in passwords and tokens travel over this connection.
- Never commit `.env`. It holds the database password and the token key.
- Use a SQL login with rights on `MobileBillDB` only, and enable `Encrypt=True` if the SQL Server is reached over the internet.
- Back up the database **and** the `bill-uploads` volume.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| API stops at start-up: "Connection string 'DefaultConnection' is required" | `DB_CONNECTION_STRING` missing from `.env` |
| API stops at start-up: "Configuration 'Jwt:SigningKey' is required" | `JWT_SIGNING_KEY` missing from `.env` |
| Sign-in fails for everyone after an update | `JWT_SIGNING_KEY` changed; users simply sign in again |
| Pages load but every screen shows errors | API cannot reach SQL Server: check host, port, firewall and the SQL login |
| "413 Request Entity Too Large" on upload | PDF over 25 MB (nginx) or 20 MB (API) |
| Refreshing a page gives 404 | `nginx.conf` not used: rebuild the `web` image |
| PDF report text looks wrong | Fonts missing: rebuild the `api` image (it installs `fonts-liberation`) |
