# Memoria — Production Deploy Guide

End-to-end recipe for deploying Memoria to a single Linux VPS using GitHub
Actions + GHCR + Docker Compose. Tested on Ubuntu 24.04 LTS.

**Topology**

```
┌────── GitHub ──────┐         ┌──────────────────── VPS ────────────────────┐
│                    │         │                                              │
│  push to main      │         │  nginx (443)  →  127.0.0.1:8080  (memoria)   │
│      │             │         │       │                  │                   │
│      ▼             │         │       │                  ├─→  127.0.0.1:5432 │
│  build Docker      │ ──ssh→  │   certbot                │     (postgres)    │
│      │             │         │       │                  │                   │
│      ▼             │         │       └─ memoria.example.com               │
│  push to GHCR      │         │                                              │
│      │             │         │  ufw: 22, 80, 443 ONLY                       │
│      ▼             │         │                                              │
│  ssh: pull+restart │ ────────►  docker compose pull && up -d                │
└────────────────────┘         └──────────────────────────────────────────────┘
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

### 1.8 Configure nginx vhost

Copy `deploy/nginx-memoria.conf` from the repo to the VPS (manually for the
first time — after this, you'll edit it in place when needed):

```bash
# Easiest: clone the repo somewhere temporary just to grab the file.
git clone --depth=1 https://github.com/futuricon/memoria /tmp/memoria-repo
cp /tmp/memoria-repo/deploy/nginx-memoria.conf /etc/nginx/sites-available/memoria
rm -rf /tmp/memoria-repo

ln -s /etc/nginx/sites-available/memoria /etc/nginx/sites-enabled/memoria
nginx -t
systemctl reload nginx
```

Set up DNS first — add an **A record** for `memoria.example.com` pointing
to your VPS's public IP. Wait ~1 min for propagation, then verify:

```bash
PUBLIC_IP=$(curl -4 -s ifconfig.me)
echo "VPS public IP: $PUBLIC_IP"
dig +short memoria.example.com   # → should print PUBLIC_IP
```

### 1.9 Obtain TLS cert via certbot

```bash
apt-get install -y certbot python3-certbot-nginx
certbot --nginx \
  -d memoria.example.com \
  -m you@example.com \
  --agree-tos \
  --redirect \
  --non-interactive

# Verify auto-renewal will work
certbot renew --dry-run
```

After this `memoria.example.com` serves HTTPS and 80 redirects to 443.
But there's nothing behind it yet — `curl https://memoria.example.com/healthz`
will return 502 until the first GH Actions deploy puts the container up.

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
git add docker-compose.prod.yml .github/workflows/deploy.yml deploy/nginx-memoria.conf docs/DEPLOY.md
git commit -m "ci: set up GitHub Actions deploy pipeline"
git push origin main
```

Watch the run at **github.com/futuricon/memoria/actions**. First run takes
~5–7 min (Docker layers are cold); subsequent runs ~1–2 min.

---

## Part 3 — Verify

```bash
# On VPS
docker ps                                    # memoria-app should be Up + healthy
docker logs memoria-app --tail 50            # → "Memoria started, listening for requests"
curl -fsS http://127.0.0.1:8080/healthz      # → {"status":"alive"}
curl -fsS http://127.0.0.1:8080/readyz       # → {"status":"Healthy", ...}

# From anywhere
curl -fsS https://memoria.example.com/healthz    # → {"status":"alive"}
```

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
