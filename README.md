# Memoria

> Telegram bot for spaced-repetition learning based on Ebbinghaus curves,
> with an Angular 20 SPA for dashboard analytics and library browsing.

## Tech stack

**Backend**

- **.NET 8** + ASP.NET Core Minimal API
- **PostgreSQL 16** + EF Core 8 (Npgsql, snake_case naming)
- **Hangfire** (PostgreSQL storage) for the reminder scheduler
- **Telegram.Bot** for the bot (long polling)
- **MediatR** + **FluentValidation** for the CQRS pipeline
- **Serilog** (Compact JSON to console + rolling file)
- **JWT** for API auth; **OAuth** (Google + GitHub) for the Hangfire dashboard
- **Polly** for Telegram rate-limit retries
- **xUnit** + **FluentAssertions** + **NSubstitute** + **NetArchTest** for tests

**Frontend** ([frontend/](frontend/))

- **Angular 20** — standalone components, signals (`signal` / `computed` /
  `resource`), `@if` / `@for` control flow, `inject()` everywhere
- **Tailwind CSS v4** (via `@tailwindcss/postcss`) + **Angular CDK**
- **TypeScript 5.8** in strict mode

## Architecture

Modular monolith. Four bounded contexts: **Users**, **Cards**, **Reminders**, **Reviews**.
Each module has:

- A public `*.Contracts` project: commands, queries, events, DTOs, ports.
- An internal module project: domain entities, persistence, MediatR handlers, services, jobs.
- Its own PostgreSQL schema.

Cross-module communication goes through MediatR only (`IRequest` / `INotification`).
Ports (e.g., `IReminderNotificationSender`) live in `*.Contracts`; implementations live in
presentation projects (`Memoria.Bot`, `Memoria.Api`). `Memoria.Host` is the composition
root — only it references every internal module.

Architecture rules are enforced by [NetArchTest](https://github.com/BenMorris/NetArchTest)
in `tests/Memoria.ArchitectureTests` (20 checks: module isolation, contracts purity,
host composition, handler conformance, external library scope).

## Quick start

### Prerequisites

- .NET 8 SDK
- Docker (for PostgreSQL — optional; a local install also works)
- A Telegram bot token from [@BotFather](https://t.me/BotFather)

### Option A — local dotnet, Postgres in Docker

```powershell
# 1. Start Postgres
docker compose up -d postgres

# 2. Configure secrets (one-time)
dotnet user-secrets set "Jwt:SigningKey" "<32+ byte random string>" --project src/Memoria.Host
dotnet user-secrets set "Telegram:BotToken" "<bot token>" --project src/Memoria.Host
dotnet user-secrets set "Telegram:BotUsername" "memoria_bot" --project src/Memoria.Host
# Optional — only needed for /jobs dashboard
dotnet user-secrets set "Hangfire:AllowedEmails:0" "you@example.com" --project src/Memoria.Host
dotnet user-secrets set "OAuth:Google:ClientId" "..." --project src/Memoria.Host
dotnet user-secrets set "OAuth:Google:ClientSecret" "..." --project src/Memoria.Host

# 3. Run
dotnet run --project src/Memoria.Host
```

EF migrations are applied automatically on startup for all four modules.

### Option B — full Docker stack

```bash
cp .env.example .env
# Fill in TELEGRAM_BOT_TOKEN, JWT_SIGNING_KEY, HANGFIRE_ADMIN_EMAIL, OAuth keys.
docker compose --profile full up -d --build
```

This builds the app image and starts both `postgres` and `app` containers. The
default profile (no `--profile`) still only brings up Postgres.

### Option C — run the SPA locally against the API

```powershell
# 1. Start the backend (Option A or B)
# 2. In a second terminal:
cd frontend
npm ci
npm start                # ng serve on http://localhost:4200
```

The dev server expects the API at `http://localhost:5133` (configured in
[frontend/src/environments/environment.ts](frontend/src/environments/environment.ts)).
`Cors:AllowedOrigins` in [appsettings.json](src/Memoria.Host/appsettings.json)
already whitelists `http://localhost:4200`.

### What's available

| Endpoint | Purpose |
|---|---|
| `/` | health string `Memoria 0.1.0` |
| `/healthz` | liveness probe |
| `/readyz` | readiness probe (Postgres + Hangfire) |
| `/swagger` | OpenAPI explorer (Development only) |
| `/jobs` | Hangfire dashboard (OAuth required) |
| `/api/v1/auth/*` | telegram-widget / bot-code / email / refresh / telegram-linking/start |
| `/api/v1/users/me` | profile (GET / PATCH / identities) |
| `/api/v1/cards*` | CRUD + trash + due-today + upcoming + worst + review |
| `/api/v1/tags` | tag list |

Card list/detail responses include per-card `reviewCount`, `avgRating`
(0–100, normalized from `Rating` enum), and `avgAiScore` (0–100, from AI-graded
Question reviews). Stats are merged at the API layer — the Cards module itself
has no dependency on Reviews.

## Tests

```bash
dotnet test                       # backend — ~290 tests across seven projects
cd frontend && npm run build      # frontend — typecheck + production bundle
```

Backend test projects:

- `Memoria.Users.UnitTests` — verification codes, JWT, command/query handlers
- `Memoria.Cards.UnitTests` — tag normalization, handler tests
- `Memoria.Reminders.UnitTests` — Ebbinghaus scheduler, hangfire jobs, port events
- `Memoria.Reviews.UnitTests` — review entity + record handler (CardTitleSnapshot)
- `Memoria.Bot.UnitTests` — FSM dialog, parser, notification sender
- `Memoria.Api.UnitTests` — Telegram widget HMAC validator
- `Memoria.AI.UnitTests` — Claude/DeepSeek adapters
- `Memoria.ArchitectureTests` — NetArchTest module-boundary rules

Per-module tests use EF Core InMemory provider (no Docker needed).
Integration tests with Testcontainers are scaffolded but currently empty.

## Project structure

```
src/
├─ Memoria.Host/                ASP.NET Core entry point + DI composition root
├─ Memoria.Bot/                 Telegram bot: long polling, router, FSM, callbacks
├─ Memoria.Api/                 HTTP endpoints, JWT, CORS, OpenAPI, Hangfire dashboard
├─ Modules/
│  ├─ Users/                    auth, identities, JWT issuance, account linking
│  ├─ Cards/                    cards CRUD + tags + soft-delete/restore + purge job
│  ├─ Reminders/                Ebbinghaus scheduler + Hangfire jobs + reveal/skip
│  ├─ Reviews/                  append-only review history + grade aggregations
│  └─ AI/                       Claude / DeepSeek grading + question validation
└─ Shared/
   ├─ Memoria.Shared.Kernel/             Result, Error, ValueObject base
   └─ Memoria.Shared.Infrastructure/     EF conventions, MediatR ValidationBehavior,
                                         shared Options (Jwt, Telegram)
frontend/                       Angular 20 SPA
├─ src/app/core/
│  ├─ auth/                     AuthService (signals), interceptor, guard, token storage
│  ├─ api/                      typed HttpClient wrapper + DTOs
│  ├─ layout/                   ShellComponent (sidebar + outlet)
│  └─ ui/                       grade pill, relative-time helpers
└─ src/app/features/
   ├─ auth-pages/login.component.ts        email + Telegram-widget tabs
   ├─ dashboard/dashboard.component.ts     4 read-only widgets
   └─ cards/cards-list.component.ts        search + tag filter + pagination
tests/
├─ Memoria.ArchitectureTests/   NetArchTest module-boundary rules
├─ Memoria.IntegrationTests/    (scaffolded, empty)
└─ Memoria.<Module>.UnitTests/  per-module unit tests
```

## Development conventions

**Backend**

- Read [CLAUDE.md](./CLAUDE.md) before adding files — it dictates folder
  organization (vertical slice for features, configurations under `Persistence/`,
  one type per file, file name = primary type name).
- Run `dotnet test` before pushing; arch tests fail the build if a boundary is broken.
- Use the `Result<T>` pattern from `Memoria.Shared.Kernel.Results` for domain
  errors (don't throw). Unexpected infrastructure failures (DbUpdateException
  etc.) bubble up to `GlobalExceptionHandler`.
- Cross-module aggregation (e.g., grade stats over cards) is composed at the
  API endpoint layer via MediatR — modules never reference each other directly.

**Frontend** ([frontend/](frontend/))

- Standalone components only — no NgModules.
- Signals only for component state (`signal`, `computed`, `effect`, `resource`).
  No NgRx, no BehaviorSubject-as-state.
- New control flow (`@if`, `@for`, `@let`). No `*ngIf` / `*ngFor`.
- `inject()` for DI; never constructor injection.

## License

MIT — see [LICENSE](./LICENSE).
