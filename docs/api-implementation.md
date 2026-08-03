# Local API implementation

The API connects to local MySQL through `HomeMind.Api/appsettings.json`:

```text
Server=localhost;Database=nexus_mind;User=root;Password=123456;
```

This is a local development setting only. Move the connection string and
`Auth:SigningKey` into environment variables or a secret store before any
shared deployment.

## Implemented routes

| Area | Routes |
| --- | --- |
| Auth | `POST /api/v1/auth/register`, `/login`, `/refresh`; `GET /me` |
| Todo | `GET/POST /api/v1/todos`, `PUT/DELETE /api/v1/todos/{id}`, subtask create/update/delete |
| Calendar | event CRUD and subscription list/create/update/delete under `/api/v1/calendar` |
| Skills | CRUD under `/api/v1/skills` |
| Experts | catalog/detail, create/get run, events, cancel, retry and result action routes |

Authenticated routes require `Authorization: Bearer <accessToken>`. The server
derives both `user_id` and `tenant_id` from the access token and never trusts
those values from JSON input.

## Deliberately gated routes

- `POST /api/v1/auth/wechat/exchange` returns `501` until WeChat channel
  configuration (AppId, secret, redirect callback and server-side exchange)
  is supplied. It does not fabricate a WeChat identity.
- `POST /api/v1/calendar/ical/fetch` returns `501` until an SSRF allow-list
  policy is configured.
- Expert runs are inserted into `expert_jobs` and return `queued`. A background
  worker and model-provider credentials are required to execute them; model
  calls never run in the API request thread.

## Run locally

The current process on port `5280` must be restarted after code changes. Run:

```powershell
dotnet run --project .\HomeMind.Api
```

Swagger is available at `http://localhost:5280/swagger`.
