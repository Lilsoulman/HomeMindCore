# Development Environment

## Local .NET baseline

| Component | Version |
| --- | --- |
| .NET SDK | 8.0.202 |
| Target framework | net8.0 |
| ASP.NET Core runtime | 8.0 |
| .NET runtime | 8.0 |
| Operating system | Windows 11 23H2 (10.0.22631), x64 |

The repository is pinned to SDK 8.0.202 by `global.json`.

## Start the API

```powershell
dotnet run --project .\HomeMind.Api
```

The API listens on `http://localhost:5280`.

The local MySQL database is `nexus_mind`. Apply the migrations in order before
starting a fresh environment:

```powershell
Get-Content -Raw .\database\001_mobile_initial_schema.mysql.sql | mysql -uroot -p
Get-Content -Raw .\database\002_expert_workbench_and_tenancy.mysql.sql | mysql -uroot -p
Get-Content -Raw .\database\003_builtin_expert_catalog.mysql.sql | mysql -uroot -p
Get-Content -Raw .\database\004_access_token_revocations.mysql.sql | mysql -uroot -p
Get-Content -Raw .\database\005_localized_default_display_names.mysql.sql | mysql -uroot -p
Get-Content -Raw .\database\007_smart_home_read_model.mysql.sql | mysql -uroot -p
Get-Content -Raw .\database\008_connector_provider_catalog.mysql.sql | mysql -uroot -p
Get-Content -Raw .\database\009_housekeeper_run_orchestration.mysql.sql | mysql -uroot -p
Get-Content -Raw .\database\010_confirmed_smart_home_actions.mysql.sql | mysql -uroot -p
Get-Content -Raw .\database\011_agent_runtime_architecture.mysql.sql | mysql -uroot -p
```

See `docs/api-implementation.md` for current API coverage and
`docs/frontend-api-integration.md` for the frontend-facing contract (request /
response samples for every endpoint).

- `HomeMind.Api/Properties/launchSettings.json` controls the IDE profile.
- `HomeMind.Api/Program.cs` provides the same default for direct Kestrel execution.

Both use port 5280 so the application does not conflict with the project
already using port 5000. To use another port temporarily, set
`ASPNETCORE_URLS` before starting the application, for example:

```powershell
$env:ASPNETCORE_URLS = 'http://localhost:5290'
dotnet run --project .\HomeMind.Api
```

## Development rules

### 1. API naming convention (mandatory)

| Direction | Convention | Example |
| --- | --- | --- |
| Request body / JSON | **小驼峰 (camelCase)** | `{"displayName":"Alex","installationId":"..."}` |
| Request query / path | **小驼峰 (camelCase)** | `?from=2026-08-01&to=2026-08-31` |
| Response envelope | PascalCase for envelope only | `{"Code":0,"Msg":"操作成功","Data":{...}}` |
| Response data fields | **大驼峰 (PascalCase)** | `"StartAt":"2026-08-01T09:00:00Z"` |

The convention is enforced centrally in `HomeMind.Api/Startup.cs`:

- `AddJsonOptions` sets `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` for
  request deserialization.
- `PascalCaseApiResponseOutputFormatter` serializes the `Data` of `ApiResponse<T>`
  with `PropertyNamingPolicy = null`, so the envelope stays
  `Code / Msg / Data` (PascalCase) and the inner data is emitted with the C#
  property names.

新增或改造的控制器不得包含 SQL、`DbContext` 或实体查询。数据查询与跨表事务放在
`HomeMind.Business.Services`，由 `HomeMind.Business.IServices` 定义接口；
实体表映射和入参/视图模型分别放在 `HomeMind.Common.Model/Entities` 与
`HomeMind.Common.Model/ViewModel`。通用 EF 仓储和工作单元在
`HomeMind.Common.Repository`。

### 2. Project layout

- All HTTP surface lives in `HomeMind.Api/Controllers/**`; controller actions
  only validate HTTP input, obtain the authenticated user and translate the
  business result into an HTTP response.
- Each persisted table must have an entity mapping. Aggregate business services
  may coordinate several tables (for example, account registration writes users,
  identities, credentials, tenants and members) within a transaction.
- The unified response wrapper is `ApiResponse<T>` in
  `HomeMind.Api/Services/Infrastructure.cs`. All client-facing `Msg` values
  must be Chinese.

### 3. Authentication & authorisation

- All authenticated routes live under `/api/v1` and require
  `Authorization: Bearer <accessToken>`.
- `user_id` and `tenant_id` come from the JWT — never trust them from the
  request body.
- Roles and their allowed actions are defined in
  `HomeMind.Api/Services/Authorization.cs` (`PermissionAuthorizationHandler`).
  Add new policies via `PermissionNames.All` and the corresponding handler
  rules; mirror them in `Startup.AddAuthorization`.
- Token TTLs: access = 15 min, refresh = 30 days. Both are configurable under
  `Auth:AccessTokenMinutes` and `Auth:RefreshTokenDays`. Production requires a
  non-development `Auth:SigningKey` of at least 32 bytes at startup. See
  `docs/linux-production-deployment.md` for the Linux systemd setup.

### 4. Error contract

- Every response uses `ApiResponse<T>`. `Code` is a stable application error
  code and is never an HTTP status code; `0` means success. The canonical code
  table is in `docs/api-implementation.md`.
- 401/403/404 are produced through `ApiControllerBase.UnauthorizedResult<T>` /
  `NotFoundResult<T>` so the envelope stays consistent. Login credential
  failures are HTTP 400 with `Code=20000`; reserve HTTP 401 for access or
  refresh token failures.
- Currently gated routes (`POST /api/v1/auth/wechat/exchange`,
  `POST /api/v1/calendar/ical/fetch`) return HTTP 501 with `Code=50000` and a
  descriptive `Msg`. Do not remove the 501 status until the underlying
  configuration is supplied.

### 5. Documentation hygiene

- Any new endpoint MUST be reflected in
  `docs/frontend-api-integration.md` in the same change set.
- `docs/api-implementation.md` is the internal route index; keep its table in
  sync.
- The root `README.md` is intentionally absent — do not reintroduce a
  placeholder. Top-level project entry points are `DEVELOPMENT.md` and
  `docs/frontend-api-integration.md`.
- PDMANER model work follows `database/pdmaner/README.md`; SQL migrations
  remain the source of truth.

### 6. Database migrations

- Migrations are numbered `NNN_*.mysql.sql` under `database/` and applied in
  order. Never edit a migration that has already been applied to a shared
  environment; add a new numbered file instead.
- Soft-delete columns (`deleted_at`), `updated_at` and `sync_version` are
  required on every mutable business table. Optimistic concurrency on catalog /
  run rows uses `row_version`, not `sync_version`.
