# Memoria — Production Deploy Guide

End-to-end recipe for deploying Memoria to a single Linux VPS using GitHub
Actions + GHCR + Docker Compose. Tested on Ubuntu 24.04 LTS.

**Topology**

```
┌────── GitHub ──────┐         ┌──────────────────── VPS ─────────────────────────┐
│                    │         │                                                   │
│  push to main      │         │  nginx (443) ──┬─→ memoria.example.com            │
│      │             │         │                │     /var/www/memoria-spa/browser │
│      ├─► docker ──►│ ──ssh──►│                │     (static SPA, no proxy)       │
│      │   build &   │         │                │                                  │
│      │   GHCR push │         │                └─→ api.memoria.example.com        │
│      │             │         │                      127.0.0.1:8080 (Memoria.Host)│
│      └─► ng build ►│ ──scp──►│                      ├─→ 127.0.0.1:5432 (postgres)│
│          dist/     │         │   certbot (per host)  │                            │
│                    │         │                       └─ /jobs Hangfire dashboard │
│  ssh: pull+restart │ ────────►  docker compose pull && up -d                     │
└────────────────────┘         │  ufw: 22, 80, 443 ONLY                            │
                               └───────────────────────────────────────────────────┘
```

---

## Part 1 — VPS one-time setup

Run as `root` on the VPS (you're already root per your console output).
Replace `<PUBLIC_IP>` placeholders only if explicitly mentioned — most commands
work as-is.

### 1.1 Install Docker + Docker Compose plugin

```bash
# Prereqs
apt-get update
apt-get install -y ca-certificates curl gnupg

# Add Docker's official GPG key
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
  | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
chmod a+r /etc/apt/keyrings/docker.gpg

# Add Docker repo
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" \
  > /etc/apt/sources.list.d/docker.list

apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Verify
docker --version           # Docker version 28.x or later
docker compose version     # Docker Compose version v2.x or later
systemctl is-active docker # active
```

### 1.2 Lock down PostgreSQL

Make Postgres listen only on `localhost` (not on `0.0.0.0`). Memoria container
will use `network_mode: host` and connect to `127.0.0.1:5432`.

```bash
# Backup originals
cp /etc/postgresql/16/main/postgresql.conf /etc/postgresql/16/main/postgresql.conf.bak

# Change listen_addresses from '*' to 'localhost'
sed -i "s/^listen_addresses\s*=.*/listen_addresses = 'localhost'/" /etc/postgresql/16/main/postgresql.conf

# Verify the change
grep "^listen_addresses" /etc/postgresql/16/main/postgresql.conf
# → listen_addresses = 'localhost'

systemctl restart postgresql
ss -lntp | grep 5432
# → should now show 127.0.0.1:5432 and ::1:5432, NOT 0.0.0.0:5432
```

### 1.3 Enable ufw firewall

```bash
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp      # SSH
ufw allow 80/tcp      # HTTP (certbot challenge)
ufw allow 443/tcp     # HTTPS
ufw --force enable
ufw status verbose
```

> **Heads-up:** the `--force enable` is needed because you're SSH-ed in; ufw
> won't break your session because port 22 is allowed first. If you're
> paranoid, open a second SSH session before running and keep it as escape hatch.

### 1.4 Create Memoria's PostgreSQL DB + user

Generate a strong random password and remember it — you'll put it into `.env`
on the VPS, but **never** into git.

```bash
# Generate password (save it for step 1.7)
DB_PASS=$(openssl rand -base64 32 | tr -d '/+=' | head -c 32)
echo "DB_PASS=$DB_PASS"   # write this down!

sudo -u postgres psql <<EOF
CREATE USER memoria WITH PASSWORD '${DB_PASS}';
CREATE DATABASE memoria OWNER memoria;
GRANT ALL PRIVILEGES ON DATABASE memoria TO memoria;
\\c memoria
GRANT ALL ON SCHEMA public TO memoria;
EOF

# Test
PGPASSWORD="$DB_PASS" psql -h 127.0.0.1 -U memoria -d memoria -c "SELECT current_user, current_database();"
# → memoria | memoria
```

### 1.5 Create deploy OS user `memoria`

```bash
useradd --create-home --shell /bin/bash memoria
usermod -aG docker memoria        # so memoria can run docker without sudo
mkdir -p /opt/memoria
chown memoria:memoria /opt/memoria
```

### 1.6 Generate SSH key for GitHub Actions

Run **as the memoria user** (so the key lives in the right `authorized_keys`):

```bash
sudo -u memoria -H bash -c '
mkdir -p ~/.ssh && chmod 700 ~/.ssh
ssh-keygen -t ed25519 -N "" -C "gh-actions-memoria" -f ~/.ssh/gh-actions
cat ~/.ssh/gh-actions.pub >> ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys
echo "--- PRIVATE KEY (copy to GitHub secret DEPLOY_SSH_KEY) ---"
cat ~/.ssh/gh-actions
echo "----------------------------------------------------------"
'
```

**Copy the entire private key** (between `-----BEGIN OPENSSH PRIVATE KEY-----`
and `-----END OPENSSH PRIVATE KEY-----` inclusive) — you'll paste it into
GitHub Secrets in Part 2.

Verify the key works:

```bash
sudo -u memoria -H ssh -i ~memoria/.ssh/gh-actions -o StrictHostKeyChecking=no memoria@127.0.0.1 'echo OK'
# → OK
```

### 1.7 Place `.env` on the VPS

The `.env` lives **only on the VPS**, never in git.

```bash
sudo -u memoria -H bash <<'EOF'
cat > /opt/memoria/.env <<ENV
POSTGRES_DB=memoria
POSTGRES_USER=memoria
POSTGRES_PASSWORD=<paste-DB_PASS-from-step-1.4>

JWT_ISSUER=memoria
JWT_AUDIENCE=memoria-api
JWT_SIGNING_KEY=<openssl rand -base64 48 result>

TELEGRAM_BOT_TOKEN=<token from @BotFather>
TELEGRAM_BOT_USERNAME=<bot username, e.g. memoria_bot>

# OAuth — optional, only needed for /jobs dashboard.
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
GITHUB_CLIENT_ID=
GITHUB_CLIENT_SECRET=
HANGFIRE_ADMIN_EMAIL=you@example.com

# AI grading. AI_PROVIDER = Claude | DeepSeek. Optional: empty key → Question-card
# validation/grading degrade fail-open (cards still created, answers not auto-graded).
# Leave BASE_URL/models empty to use the provider defaults:
#   Claude   → claude-sonnet-4-6 @ api.anthropic.com   (key: console.anthropic.com)
#   DeepSeek → deepseek-chat      @ api.deepseek.com    (key: platform.deepseek.com)
# DeepSeek is ~10x cheaper: set AI_PROVIDER=DeepSeek and AI_API_KEY=<deepseek key>.
AI_PROVIDER=Claude
AI_API_KEY=
AI_BASE_URL=
AI_GRADING_MODEL=
AI_VALIDATION_MODEL=

SPA_ORIGIN=https://memoria.example.com
ENV
chmod 600 /opt/memoria/.env
EOF
```

Generate a JWT key:

```bash
openssl rand -base64 48
```

Edit `/opt/memoria/.env` and replace the placeholders. **All required fields
must be filled** (POSTGRES_PASSWORD, JWT_SIGNING_KEY, TELEGRAM_BOT_TOKEN) or
the container crashes on startup.

> ℹ️ `docker-compose.prod.yml` will land in `/opt/memoria/` automatically on
> the first successful GitHub Actions run — you don't need to create it manually.

### 1.8 Configure nginx vhosts

Memoria runs on **two** vhosts: the SPA at `memoria.example.com` (static
files) and the API at `api.memoria.example.com` (reverse-proxy to the .NET
container). The Hangfire `/jobs` dashboard rides along on the api vhost.

Add **both A records** to DNS, pointing at the VPS public IP, before you
touch nginx — certbot needs DNS resolution to issue the certs.

```bash
PUBLIC_IP=$(curl -4 -s ifconfig.me)
echo "VPS public IP: $PUBLIC_IP"
dig +short memoria.example.com      # → should print PUBLIC_IP
dig +short api.memoria.example.com  # → should print PUBLIC_IP
```

Copy both vhost templates from the repo:

```bash
git clone --depth=1 https://github.com/futuricon/memoria /tmp/memoria-repo

cp /tmp/memoria-repo/deploy/nginx-memoria.conf      /etc/nginx/sites-available/memoria
cp /tmp/memoria-repo/deploy/nginx-memoria-api.conf  /etc/nginx/sites-available/memoria-api

# Edit each file and replace `memoria.example.com` / `api.memoria.example.com`
# with your real hostnames.
sed -i "s/memoria\.example\.com/memoria.futuricon.net/g" /etc/nginx/sites-available/memoria
sed -i "s/api\.memoria\.example\.com/api.memoria.futuricon.net/g; s/memoria\.example\.com/memoria.futuricon.net/g" /etc/nginx/sites-available/memoria-api

rm -rf /tmp/memoria-repo

ln -sf /etc/nginx/sites-available/memoria      /etc/nginx/sites-enabled/memoria
ln -sf /etc/nginx/sites-available/memoria-api  /etc/nginx/sites-enabled/memoria-api

nginx -t
systemctl reload nginx
```

### 1.9 Obtain TLS certs via certbot

Run certbot once per hostname. Each invocation will edit the corresponding
`sites-available/` file in place to add the 443 server block and the 80→443
redirect — leave those edits in place on future deploys.

```bash
apt-get install -y certbot python3-certbot-nginx

certbot --nginx \
  -d memoria.example.com \
  -m you@example.com \
  --agree-tos --redirect --non-interactive

certbot --nginx \
  -d api.memoria.example.com \
  -m you@example.com \
  --agree-tos --redirect --non-interactive

# Verify auto-renewal will work for both
certbot renew --dry-run
```

### 1.10 Prepare the SPA directory

The GitHub Actions deploy uploads the built Angular bundle to
`/var/www/memoria-spa/browser/`. The `memoria` user (the SSH identity CI uses)
must own this directory so `scp` can overwrite files without `sudo`.

```bash
mkdir -p /var/www/memoria-spa/browser
chown -R memoria:memoria /var/www/memoria-spa
chmod 755 /var/www/memoria-spa /var/www/memoria-spa/browser

# nginx (running as www-data) must be able to read the files. Default umask
# from scp gives 644 files / 755 dirs — that's already world-readable.
```

After the first successful GH Actions run, you should see:

```bash
ls /var/www/memoria-spa/browser/
# → index.html  main-*.js  polyfills-*.js  styles-*.css  ...

curl -fsS https://memoria.example.com/             # → SPA index.html
curl -fsS https://api.memoria.example.com/healthz  # → {"status":"alive"}
```

### 1.11 Update OAuth redirect URIs

The `/jobs` Hangfire dashboard moved from `memoria.example.com/jobs` to
`api.memoria.example.com/jobs`, so the OAuth redirect URIs registered with
Google and GitHub must be updated. Without this, sign-in for the dashboard
fails with `redirect_uri_mismatch`.

The SPA also uses Google/GitHub OAuth (Phase 2), which adds two more
callback paths under the same OAuth apps. **Add both URIs to each provider**
— the OAuth app object stays the same, only the redirect-URI list grows.

- **Google Cloud Console** → APIs & Services → Credentials → your OAuth 2.0
  client → Authorized redirect URIs. Add:
  - `https://api.memoria.example.com/jobs/signin-google` (Hangfire dashboard)
  - `https://api.memoria.example.com/api/v1/auth/google/callback` (SPA login)
- **GitHub** → Settings → Developer settings → OAuth Apps → your app →
  Authorization callback URL. GitHub allows **only one** callback URL per
  OAuth app, so you must choose which flow uses it:
  - Either keep `https://api.memoria.example.com/jobs/signin-github` (only
    Hangfire dashboard has GitHub sign-in; SPA login still has Google + email),
  - Or use `https://api.memoria.example.com/api/v1/auth/github/callback`
    (SPA login has GitHub; Hangfire dashboard falls back to Google + email).
  - Or register a **second** OAuth app with the other callback URL, set its
    `Client ID` / `Secret` as separate env vars (e.g. `GITHUB_SPA_*`) and
    extend `OAuthAuthenticationConfiguration.cs` to bind them. Recommended
    only if you actually need both surfaces — most operators don't.

### 1.12 Email delivery (Resend)

SPA email login (`/api/v1/auth/email/start` → `/confirm`) sends a 6-digit
code to the user's inbox via [Resend](https://resend.com). If
`RESEND_API_KEY` in `/opt/memoria/.env` is empty, the app falls back to
`LoggingEmailSender` — codes are written to the app log only, which is fine
for first-boot debugging but useless for real users.

One-time setup (~5 min):

1. **Create a Resend account.** Free tier: 3000 emails/month, 100/day —
   plenty for early users.
2. **Add and verify your sending domain** (e.g. `memoria.futuricon.net`):
   Dashboard → Domains → "Add Domain" → enter the apex. Resend shows three
   DNS records to add: an MX, an SPF TXT, and a DKIM TXT. Paste them at your
   DNS provider. Click "Verify DNS records" — usually ready in 1–5 minutes.
   The "From" address can use any local part on this domain
   (`noreply@`, `hello@`, etc.).
3. **Create an API key.** Dashboard → API Keys → "Create API Key" →
   "Sending access" → name it `memoria-prod`. Copy the `re_…` token —
   shown only once.
4. **Drop it into `/opt/memoria/.env`** on the VPS:
   ```
   RESEND_API_KEY=re_paste_here
   RESEND_FROM_ADDRESS=Memoria <noreply@memoria.futuricon.net>
   ```
5. **Restart the container** so the new env is picked up:
   ```bash
   sudo -u memoria bash -c 'cd /opt/memoria && docker compose -f docker-compose.prod.yml up -d'
   ```

Smoke-test from the login screen: enter your email, click "Send code", and
the code should arrive within ~10 s. If it doesn't:

```bash
# Watch the live log; you'll see one of:
#   "Resend accepted verification code for fu***@gmail.com (status 200)"
#   "Resend returned 422 for fu***@gmail.com: {...}"   ← misconfigured domain
#   "ResendEmailSender: Email:FromAddress is not configured — ..."
docker logs -f memoria-app | grep -i resend
```

Common gotchas:

- **422 from Resend** — `FromAddress` domain isn't verified. Check the
  Domains page in the Resend dashboard.
- **Code never arrives but log says "accepted"** — check the recipient's
  spam folder. After ~10 emails the deliverability reputation warms up.
- **Want to revert to log-only mode** — empty `RESEND_API_KEY` in `.env`
  and restart. The stub takes over automatically.

---

## Part 2 — GitHub setup

### 2.1 Set repository secrets

Go to **github.com/futuricon/memoria/settings/secrets/actions** and add
**Repository secrets**:

| Name | Value |
|---|---|
| `DEPLOY_SSH_HOST` | Public IP of your VPS (`curl -4 ifconfig.me` on VPS). E.g. `1.2.3.4`. |
| `DEPLOY_SSH_USER` | `memoria` |
| `DEPLOY_SSH_KEY` | The full private key text from step 1.6 (BEGIN … END). |

The `GITHUB_TOKEN` for GHCR is created automatically by Actions — no manual
PAT needed because the workflow has `packages: write` permission.

### 2.2 (Optional) Make the GHCR package public

By default the first `docker push` to GHCR creates a **private** package.
Pulling it from the VPS works because the workflow logs in with `GITHUB_TOKEN`
each deploy. If you'd rather have a public image (one less moving part):

- After first successful deploy, open **github.com/futuricon?tab=packages**
- Click `memoria` → **Package settings** → "Change visibility" → "Public"

This is a one-time setting. Recommended unless you don't want the binary
publicly distributable. Either way the bot token / DB password / JWT key stay
on the VPS — the image itself contains zero secrets.

### 2.3 Trigger the first deploy

```bash
# On your dev machine
git add docker-compose.prod.yml .github/workflows/deploy.yml \
        deploy/nginx-memoria.conf deploy/nginx-memoria-api.conf \
        frontend/ docs/DEPLOY.md
git commit -m "ci: set up GitHub Actions deploy pipeline"
git push origin main
```

Watch the run at **github.com/futuricon/memoria/actions**. The pipeline now
runs `build-and-push` (.NET image) and `build-frontend` (Angular bundle) in
parallel, then `deploy` ssh+scp's both onto the VPS. First run takes ~6–8 min
(Docker layers cold + npm cache cold); subsequent runs ~2–3 min.

---

## Part 3 — Verify

```bash
# On VPS
docker ps                                    # memoria-app should be Up + healthy
docker logs memoria-app --tail 50            # → "Memoria started, listening for requests"
curl -fsS http://127.0.0.1:8080/healthz      # → {"status":"alive"}
curl -fsS http://127.0.0.1:8080/readyz       # → {"status":"Healthy", ...}
ls /var/www/memoria-spa/browser/index.html   # → exists (uploaded by GH Actions)

# From anywhere
curl -fsS https://api.memoria.example.com/healthz   # → {"status":"alive"}
curl -fsS https://memoria.example.com/ | head -1    # → <!doctype html>
```

Open `https://memoria.example.com/` in a browser — the SPA should load the
login screen. Sign in via email (a code is sent to your inbox) or the
Telegram widget. Then:

- `/` shows the dashboard widgets (hardest card, due today, upcoming, totals).
- `/cards` lists your library; search and tag filters work.
- `https://api.memoria.example.com/jobs` shows the Hangfire dashboard after
  Google/GitHub OAuth.

In Telegram, send `/start` to your bot — you should get the greeting.
`/help` should list all commands.

---

## Day-to-day operations

### View logs

CLI (quick tail):

```bash
sudo -u memoria docker compose -f /opt/memoria/docker-compose.prod.yml logs -f app
# or the live container directly:
docker logs -f memoria-app
```

Web UI — **Dozzle** at `https://logs.memoria.example.com` (live tail of all Docker
containers in the browser). Dozzle reads container stdout via the read-only docker
socket, so it shows `memoria-app` and `memoria-dozzle` — **not** host nginx/Postgres
(those aren't containers; use `journalctl -u nginx` / `/var/log/postgresql` on the host).

One-time setup (the Dozzle container itself ships automatically via compose, bound to
`127.0.0.1:8081`; this just publishes it behind TLS + basic-auth):

```bash
# 1. DNS: add an A record  logs.memoria.example.com → <VPS public IP>.
# 2. basic-auth user (prompts for a password):
sudo apt-get install -y apache2-utils
sudo htpasswd -c /etc/nginx/.htpasswd-dozzle <your-username>
# 3. vhost (copy the template from the repo):
git clone --depth=1 https://github.com/futuricon/memoria /tmp/memoria-repo
sudo cp /tmp/memoria-repo/deploy/nginx-dozzle.conf /etc/nginx/sites-available/memoria-logs
rm -rf /tmp/memoria-repo
sudo ln -s /etc/nginx/sites-available/memoria-logs /etc/nginx/sites-enabled/memoria-logs
sudo nginx -t && sudo systemctl reload nginx
# 4. TLS (also adds the 80 → 443 redirect):
sudo certbot --nginx -d logs.memoria.example.com -m you@example.com --agree-tos --redirect --non-interactive
```

> Migrating from the old Grafana Loki / Promtail setup? After the next deploy the
> orphaned `memoria-promtail` container is removed automatically (`--remove-orphans`).
> Drop its leftover volume once: `docker volume rm memoria_promtail-data` (ignore if
> absent), and you can delete `/opt/memoria/deploy/promtail-config.yml`. The old
> `LOKI_*` lines in `/opt/memoria/.env` are now unused and can be removed.

### Restart without redeploy

```bash
sudo -u memoria bash -c 'cd /opt/memoria && docker compose -f docker-compose.prod.yml restart app'
```

### Roll back to a previous image

Each push tags two images: `latest` and `sha-<short>`. To roll back:

```bash
# Find recent SHA tags
docker image ls ghcr.io/futuricon/memoria

# Force a specific sha by editing the image tag in compose:
sudo -u memoria bash -c '
cd /opt/memoria
sed -i "s|memoria:latest|memoria:sha-abc1234|" docker-compose.prod.yml
docker compose -f docker-compose.prod.yml up -d
'
# Don't forget to revert the tag after the next clean deploy.
```

### Renew TLS cert

certbot installs a systemd timer; check it's enabled:

```bash
systemctl list-timers | grep certbot
```

Manual renewal: `certbot renew && systemctl reload nginx`.

### Update OAuth / Telegram secrets

Edit `/opt/memoria/.env` and restart: `docker compose -f docker-compose.prod.yml up -d`.

---

## Troubleshooting

### `502 Bad Gateway` from nginx

App isn't listening on 127.0.0.1:8080.

```bash
docker logs memoria-app --tail 100
# Look for "Memoria terminated unexpectedly" or DB connection errors.
ss -lntp | grep 8080
# Should show: 127.0.0.1:8080 LISTEN
```

### App can't connect to Postgres

```bash
# Confirm PG is on 127.0.0.1:5432
ss -lntp | grep 5432

# Test from the host
PGPASSWORD=<pass> psql -h 127.0.0.1 -U memoria -d memoria -c '\dt'

# Test from inside the container (network_mode: host means same loopback)
docker exec memoria-app sh -c 'apt list --installed 2>/dev/null | grep postgresql-client || apt update && apt install -y postgresql-client'
docker exec -e PGPASSWORD=<pass> memoria-app psql -h 127.0.0.1 -U memoria -d memoria -c '\dt'
```

If `psql` from container fails: check `/etc/postgresql/16/main/pg_hba.conf` —
default Ubuntu config allows `host all all 127.0.0.1/32 scram-sha-256`,
which is what we need. If someone tightened it, restore.

### GH Actions step "Pull image & restart container" hangs

Usually an SSH issue.

```bash
# Verify key on VPS
sudo -u memoria cat ~memoria/.ssh/authorized_keys

# Verify port 22 is open in ufw + provider firewall
ufw status | grep 22

# Confirm host key fingerprint hasn't changed (if you rebuilt the VPS,
# GH Actions cache stale; we don't pin known_hosts so this shouldn't
# block, but provider DNS changes could).
```

### "manifest unknown" when pulling image

The build job didn't push successfully. Check the `Build & push image` step in
GH Actions — most often, package permissions on the org/user.
Visit **github.com/futuricon?tab=packages** to confirm `memoria` exists.

---

## Migrating an existing single-vhost deployment

If you set this up before Phase 0F, your `memoria.example.com` vhost reverse-
proxied everything to `127.0.0.1:8080`. The new split moves the API behind
`api.memoria.example.com` and reuses `memoria.example.com` for SPA statics.

Order matters — set up the api subdomain **first**, leave the old vhost in
place until the api is verified working, then swap.

```bash
# 1. DNS: add A record for api.memoria.example.com → VPS IP. Verify.
dig +short api.memoria.example.com

# 2. Install the new api vhost alongside the existing one (keeps old setup live).
git clone --depth=1 https://github.com/futuricon/memoria /tmp/memoria-repo
cp /tmp/memoria-repo/deploy/nginx-memoria-api.conf  /etc/nginx/sites-available/memoria-api
sed -i "s/api\.memoria\.example\.com/api.memoria.YOUR-DOMAIN/g; s/memoria\.example\.com/memoria.YOUR-DOMAIN/g" \
  /etc/nginx/sites-available/memoria-api
ln -sf /etc/nginx/sites-available/memoria-api /etc/nginx/sites-enabled/memoria-api
nginx -t && systemctl reload nginx

# 3. Issue cert for api subdomain.
certbot --nginx -d api.memoria.YOUR-DOMAIN -m you@example.com \
  --agree-tos --redirect --non-interactive

# 4. Update OAuth redirect URIs in Google + GitHub consoles
#    (see step 1.11 above). Test /jobs login on the new subdomain BEFORE
#    flipping the SPA vhost — otherwise admins can't get into Hangfire.

# 5. Prepare the SPA directory.
mkdir -p /var/www/memoria-spa/browser
chown -R memoria:memoria /var/www/memoria-spa

# 6. Trigger a GH Actions run to upload the first SPA bundle.
#    Verify /var/www/memoria-spa/browser/index.html exists after the run.

# 7. Swap the old memoria.example.com vhost for the SPA template.
cp /tmp/memoria-repo/deploy/nginx-memoria.conf /etc/nginx/sites-available/memoria
sed -i "s/memoria\.example\.com/memoria.YOUR-DOMAIN/g" /etc/nginx/sites-available/memoria

# certbot already added a 443 ssl block to the old file — that block must be
# preserved when replacing the file. Either:
#   a) edit by hand, keeping certbot's `listen 443 ssl;` server block
#      and the 80 → 443 redirect, OR
#   b) delete the cert, replace, re-run certbot:
#        certbot delete --cert-name memoria.YOUR-DOMAIN
#        certbot --nginx -d memoria.YOUR-DOMAIN ... (as in 1.9)

nginx -t && systemctl reload nginx
rm -rf /tmp/memoria-repo
```

After step 7, the old `memoria.YOUR-DOMAIN/healthz` and `/api/v1/*` paths
**stop working** — clients must use `api.memoria.YOUR-DOMAIN`. The Telegram
bot itself is unaffected (it uses long polling, not the public HTTPS surface).

## Security checklist

- [ ] `listen_addresses = 'localhost'` in `postgresql.conf`.
- [ ] `ufw status` shows only 22, 80, 443 open.
- [ ] `.env` permissions: `chmod 600`, owner `memoria:memoria`.
- [ ] Deploy SSH key: ed25519, no passphrase (CI needs unattended), only on
      `memoria` user's `authorized_keys`.
- [ ] JWT_SIGNING_KEY ≥ 32 bytes after base64 decode.
- [ ] Hangfire `/jobs` requires OAuth — without OAuth keys it's unreachable.
- [ ] Rate limit on `/api/v1/auth/*` is 5 req/min/IP — already wired.
- [ ] HTTPS-only — certbot's `--redirect` puts 80 → 443 redirect in nginx.
- [ ] `/var/www/memoria-spa` owned by `memoria:memoria` so CI scp succeeds
      without sudo; nginx (`www-data`) reads via the world-readable bit.
- [ ] `Cors:AllowedOrigins` in `appsettings.json` lists the production SPA
      origin (`https://memoria.YOUR-DOMAIN`) — without it, browser fetches
      from the SPA to the api subdomain are blocked.
