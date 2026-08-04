# Frontend API Integration Guide

This document is the contract for the HomeMind backend (`HomeMind.Api`).
Update it together with any controller change.

## 1. Base information

| Item | Value |
| --- | --- |
| Base URL (local dev) | `http://localhost:5280` |
| API prefix | `/api/v1` |
| Content-Type | `application/json; charset=utf-8` |
| Auth header | `Authorization: Bearer <accessToken>` |
| Swagger UI | `http://localhost:5280/swagger` |

## 2. Naming convention

| Direction | Convention | Example |
| --- | --- | --- |
| Request body (JSON) | **小驼峰 camelCase** | `{"displayName":"Alex"}` |
| Request query / path params | **小驼峰 camelCase** | `?from=2026-08-01&to=2026-08-31` |
| Response envelope fields | PascalCase | `{"Code":0,"Msg":"ok","Data":{...}}` |
| Response `Data` fields | **大驼峰 PascalCase** | `"StartAt":"2026-08-01T09:00:00Z"` |

> The envelope (`Code / Msg / Data`) is fixed. The actual business payload
> inside `Data` is always PascalCase, matching the C# property names. Requests
> are accepted in camelCase.

## 3. Unified response envelope

```json
{
  "Code": 0,
  "Msg": "ok",
  "Data": { ... }
}
```

| Field | Type | Description |
| --- | --- | --- |
| `Code` | int | `0` = success; non-zero = business / framework error code |
| `Msg` | string | Human readable message, `ok` on success |
| `Data` | object \| null | Business payload; structure depends on the endpoint |

### Common error codes

| HTTP | `Code` | Meaning |
| --- | --- | --- |
| 200 | 0 | Success |
| 400 | 400 | Validation error (missing required field, etc.) |
| 401 | 401 | Missing or invalid bearer token |
| 401 | 401 | Refresh token invalid / expired / revoked |
| 404 | 404 | Resource not found, or not owned by the caller |
| 409 | 409 | Conflict (e.g. phone already bound) |
| 422 | 422 | Business validation failure |
| 503 | 503 | Database service temporarily unavailable |
| 500 | 500 | Unhandled server error |
| 501 | 501 | Endpoint is gated until external config is supplied |

## 4. Auth module (`/api/v1/auth`)

All non-auth endpoints require a valid bearer access token. The token carries
`user_id`, `tenant_id`, `device_id` and `role` claims. Refresh tokens are
opaque and rotated on every `POST /api/v1/auth/refresh`.

### 4.1 `POST /api/v1/auth/register`

Register a new personal account with phone + password and return session
tokens. The backend creates a personal tenant automatically.

Request:

```json
{
  "phone": "13800138000",
  "password": "my-strong-pwd",
  "displayName": "Alex",
  "installationId": "9c1f...uuid",
  "platform": "h5"
}
```

Response `Data`:

```json
{
  "AccessToken": "<jwt>",
  "RefreshToken": "<opaque>",
  "UserId": 12,
  "TenantId": 12
}
```

When the phone is already bound and the supplied password is correct, this
endpoint creates a session and returns `200`, so the combined
"register and sign in" action is idempotent. Errors: `422` (missing phone or
password < 8 chars), `409` (phone already bound but password does not match).

### 4.2 `POST /api/v1/auth/login`

Phone + password login. `installationId` is required to bind the current
device session; the server upserts a row in `auth_devices`.

Request:

```json
{
  "phone": "13800138000",
  "password": "my-strong-pwd",
  "installationId": "9c1f...uuid",
  "platform": "h5"
}
```

Response `Data`: same shape as register.

### 4.3 `POST /api/v1/auth/refresh`

Exchange a refresh token for a fresh access + refresh pair. The previous
refresh token is revoked; reusing a revoked token invalidates the entire
family.

Request:

```json
{ "refreshToken": "<opaque>" }
```

Response `Data`: same shape as register.

### 4.4 `GET /api/v1/auth/me`

Permission: `identity.read`. Returns the profile of the current user.

```json
{
  "id": 12,
  "DisplayName": "Alex",
  "AvatarUrl": null,
  "status": "active",
  "timezone": "Asia/Shanghai",
  "locale": "zh-CN",
  "CreatedAt": "2026-08-01T03:11:22.123Z"
}
```

### 4.5 `POST /api/v1/auth/logout`

Permission: `identity.read`. Revokes the current access token and the
device's refresh tokens.

```json
{ "loggedOut": true }
```

### 4.6 `POST /api/v1/auth/wechat/exchange` (gated)

Currently returns HTTP `501` with `Code=501` and the message
`"WeChat AppId, secret and callback configuration are required before code
exchange can be enabled."` The client should treat this as a configuration
blocker and stop retrying.

## 5. Todos module (`/api/v1/todos`)

### 5.1 `GET /api/v1/todos`

Permission: `todo.read`. Query parameters (all optional): `status`, `type`,
`from`, `to` (UTC `DateTime`).

```json
[
  {
    "id": 1,
    "title": "Buy milk",
    "description": "low-fat",
    "type": "task",
    "priority": "p1",
    "color": "#ff8800",
    "status": "pending",
    "DueAt": "2026-08-10T09:00:00Z",
    "RemindAt": "2026-08-10T08:30:00Z",
    "CompletedAt": null,
    "pinned": true,
    "SortOrder": 10,
    "RepeatRule": null,
    "CreatedAt": "2026-08-01T03:11:22.123Z",
    "UpdatedAt": "2026-08-02T03:11:22.123Z"
  }
]
```

### 5.2 `POST /api/v1/todos`

Permission: `todo.write`.

Request:

```json
{
  "title": "Buy milk",
  "description": "low-fat",
  "type": "task",
  "priority": "p1",
  "color": "#ff8800",
  "status": "pending",
  "dueAt": "2026-08-10T09:00:00Z",
  "remindAt": "2026-08-10T08:30:00Z",
  "pinned": true,
  "sortOrder": 10,
  "repeatRule": "FREQ=DAILY;COUNT=3",
  "listId": 1,
  "parentId": null
}
```

`status` defaults to `pending`; `pinned` defaults to `false`;
`sortOrder` defaults to `0`. `title` is required.

Response `Data`: same shape as a single row from `GET /api/v1/todos`.

### 5.3 `PUT /api/v1/todos/{id}`

Permission: `todo.write`. Any omitted field is left unchanged. Setting
`status` to `completed` stamps `CompletedAt`; resetting to `pending` clears
it.

Request body: same as `POST /api/v1/todos`.

### 5.4 `DELETE /api/v1/todos/{id}`

Permission: `todo.write`. Soft delete; returns `{ "id": 12 }`.

### 5.5 `POST /api/v1/todos/{id}/subtasks`

Permission: `todo.write`.

```json
{ "text": "Pick up at store", "seq": 1 }
```

Response `Data`:

```json
{ "id": 5, "text": "Pick up at store", "done": 0, "seq": 1 }
```

### 5.6 `PUT /api/v1/todos/{id}/subtasks/{subId}`

Permission: `todo.write`. Omitted fields keep their previous value.

```json
{ "text": "Pick up at store", "done": true, "seq": 1 }
```

### 5.7 `DELETE /api/v1/todos/{id}/subtasks/{subId}`

Permission: `todo.write`. Soft delete; returns `{ "id": 5 }`.

## 6. Calendar module (`/api/v1/calendar`)

### 6.1 `GET /api/v1/calendar/events`

Permission: `calendar.read`. Optional `from` / `to` query params (UTC
`DateTime`).

```json
[
  {
    "id": 7,
    "title": "Sprint review",
    "description": "Demo + retro",
    "location": "Zoom",
    "StartAt": "2026-08-12T09:00:00Z",
    "EndAt": "2026-08-12T10:00:00Z",
    "timezone": "Asia/Shanghai",
    "AllDay": false,
    "color": "#3366ff",
    "opacity": 1.0,
    "RepeatRule": "FREQ=WEEKLY;BYDAY=WE",
    "CreatedAt": "2026-08-01T03:11:22.123Z",
    "UpdatedAt": "2026-08-02T03:11:22.123Z"
  }
]
```

### 6.2 `POST /api/v1/calendar/events`

Permission: `calendar.write`. `title` and `startAt` are required.

```json
{
  "title": "Sprint review",
  "description": "Demo + retro",
  "location": "Zoom",
  "startAt": "2026-08-12T09:00:00Z",
  "endAt": "2026-08-12T10:00:00Z",
  "timezone": "Asia/Shanghai",
  "allDay": false,
  "color": "#3366ff",
  "opacity": 1.0,
  "repeatRule": "FREQ=WEEKLY;BYDAY=WE"
}
```

### 6.3 `PUT /api/v1/calendar/events/{id}` / `DELETE /api/v1/calendar/events/{id}`

Permission: `calendar.write`. Same payload as create, soft delete returns
`{ "id": 7 }`.

### 6.4 `GET /api/v1/calendar/subscriptions`

Permission: `calendar.read`. Returns the current user's external iCal
subscriptions.

```json
[
  {
    "id": 3,
    "name": "Holidays",
    "enabled": true,
    "RefreshIntervalMin": 60,
    "LastFetchAt": "2026-08-02T03:11:22.123Z",
    "LastError": null,
    "CreatedAt": "2026-08-01T03:11:22.123Z"
  }
]
```

### 6.5 `POST /api/v1/calendar/subscriptions`

Permission: `calendar.write`. `url` must be an absolute URL and is stored
encrypted server-side.

```json
{
  "url": "https://example.com/holidays.ics",
  "name": "Holidays",
  "enabled": true,
  "refreshIntervalMin": 60
}
```

### 6.6 `PUT /api/v1/calendar/subscriptions/{id}` / `DELETE /api/v1/calendar/subscriptions/{id}`

Permission: `calendar.write`. Update `name` / `enabled` / `refreshIntervalMin`
only. Delete is soft.

### 6.7 `POST /api/v1/calendar/ical/fetch` (gated)

Returns HTTP `501` with `Code=501` and message
`"iCal network fetch is disabled until SSRF allow-list rules are
configured."`

## 7. AI Skills module (`/api/v1/skills`)

### 7.1 `GET /api/v1/skills`

Permission: `ai.skills.read`.

```json
[
  {
    "id": 1,
    "name": "Polite rewrite",
    "prompt": "Rewrite the following text politely ...",
    "scopes": "[\"todos\"]",
    "IsBuiltin": true,
    "IsActive": true,
    "CreatedAt": "2026-08-01T03:11:22.123Z",
    "UpdatedAt": "2026-08-01T03:11:22.123Z"
  }
]
```

### 7.2 `POST /api/v1/skills`

Permission: `ai.skills.write`. `name` and `prompt` are required; `scopes`
is a JSON array string; `isActive` defaults to `true`.

```json
{
  "name": "Translate to English",
  "prompt": "Translate the following Chinese text to English ...",
  "scopes": "[\"todos\",\"skills\"]",
  "isActive": true
}
```

### 7.3 `PUT /api/v1/skills/{id}` / `DELETE /api/v1/skills/{id}`

Permission: `ai.skills.write`. Soft delete returns `{ "id": 2 }`.

## 8. AI Experts and AgentRun module (`/api/v1/...`)

`ExpertsController` is mounted on `api/v1` (not `api/v1/experts`) to keep
the legacy `/experts` and `/expert-runs` paths. `/expert-runs` is the stable
compatibility route name; its domain resource and Flutter DTO are `AgentRun`.
All new AI workflows must use AgentRun. The Expert endpoint supplies policy
(role, prompt, permitted Skills and permissions) and never executes a Skill or
Connector call itself.

AgentRun status is always one of:

```text
draft | queued | planning | running | completed | failed | cancelled
```

The client renders only display-safe Run events and controlled actions. It must
not show prompts, chain-of-thought, provider logs, credentials or vendor fields.

### 8.1 `GET /api/v1/experts`

Permission: `ai.read`. Optional query params: `query`, `category`, `type`
(`expert` | `group`).

```json
[
  {
    "CatalogType": "expert",
    "id": 1,
    "code": "writing-coach",
    "name": "Writing coach",
    "category": "writing",
    "description": "...",
    "EstimatedCredits": 1
  },
  {
    "CatalogType": "group",
    "id": 2,
    "code": "research-team",
    "name": "Research team",
    "category": "research",
    "description": "...",
    "EstimatedCredits": 3
  }
]
```

### 8.2 `GET /api/v1/experts/{id}?type=expert|group`

Permission: `ai.read`.

```json
{
  "id": 1,
  "code": "writing-coach",
  "name": "Writing coach",
  "category": "writing",
  "description": "...",
  "VersionId": 4,
  "version": 4,
  "persona": "...",
  "methodology": "...",
  "ToolPolicy": "{\"tools\":[\"web.search\"]}",
  "OutputSchema": "{\"type\":\"object\"}",
  "EstimatedCredits": 1
}
```

For `type=group` the persona/methodology/toolPolicy fields are replaced by
`OrchestrationPolicy`.

### 8.3 `POST /api/v1/expert-runs` (create AgentRun)

Permission: `ai.run`. Creates an AgentRun and queues it; response `Data` is the same as
`GET /api/v1/expert-runs/{id}`.

```json
{
  "sourceType": "expert",
  "sourceId": 1,
  "inputJson": "{\"topic\":\"AI safety\"}",
  "idempotencyKey": "9c1f...uuid"
}
```

### 8.4 `GET /api/v1/expert-runs/{id}` (get AgentRun)

Permission: `ai.run`.

```json
{
  "id": 9,
  "SourceType": "expert",
  "status": "queued",
  "Input": "{\"topic\":\"AI safety\"}",
  "Result": null,
  "ResultSummary": null,
  "EstimatedCredits": 1,
  "ActualCredits": null,
  "CreatedAt": "2026-08-02T03:11:22.123Z",
  "StartedAt": null,
  "FinishedAt": null
}
```

### 8.5 `GET /api/v1/expert-runs/{id}/events`

Permission: `ai.run`. Returns ordered run events.

```json
[
  {
    "id": 1,
    "sequence": 1,
    "EventType": "queued",
    "Payload": "{\"message\":\"Run queued\"}",
    "CreatedAt": "2026-08-02T03:11:22.123Z"
  }
]
```

### 8.6 `POST /api/v1/expert-runs/{id}/cancel`

Permission: `ai.run`. Best-effort cancel; immediately-cancellable runs are
flipped to `cancelled`, others record `cancelRequestedAt`.

```json
{ "id": 9, "cancelRequested": true }
```

### 8.7 `POST /api/v1/expert-runs/{id}/retry`

Permission: `ai.run`. Allowed only when the AgentRun is in `failed` or
`cancelled`.

```json
{ "id": 9, "status": "queued" }
```

### 8.8 `POST /api/v1/expert-runs/{id}/actions`

Permission: `ai.run`. Creates a controlled action for later Skill execution;
creating an action does not execute an external effect.

```json
{
  "actionType": "todos",
  "requestJson": "{\"assignToListId\":1}",
  "idempotencyKey": "9c1f...uuid"
}
```

`actionType` must be one of `plan`, `todos`, `calendar_events`,
`smart_home_device`. A future Skill executor validates the action and then uses
the Connector gateway; Flutter must never invoke vendor, Home Assistant, MQTT,
Zigbee or Matter protocols directly.

### 8.9 `POST /api/v1/housekeeper-runs` (legacy compatibility)

This endpoint is retained for the existing SmartHome Mock workflow. New screens
must start from `POST /api/v1/expert-runs` and consume AgentRun events/actions.
Home Assistant is a future SmartHome Connector adapter, not a dependency of the
AgentRun API contract.

Permission: `ai.run`. Creates a completed, displayable household analysis from
the already-synced SmartHome read model. It never sends device commands. The
request uses a fixed intent, an optional current-tenant space, and an optional
UUID idempotency key:

```json
{
  "intent": "sleep",
  "spaceId": 12,
  "idempotencyKey": "9c1f9a71-6d38-4e6a-b1c2-7ef6cf16d6d3"
}
```

`intent` is one of `sleep`, `away`, `arrive`, `environment_review`. The response
has `Id`, `Status`, `ResultSummary`, timestamps, `Events` and `Actions`. Events
contain only `Sequence`, `Type`, `Message` and `CreatedAt`. Each device action
has `Status: "pending"`; it exposes a canonical `DeviceId`, display name,
capability and target value, but no connector credential, vendor ID or protocol
field. `environment_review` returns no device actions.

```json
{
  "Code": 0,
  "Msg": "家庭管家分析完成。",
  "Data": {
    "Id": 42,
    "Status": "completed",
    "ResultSummary": "已完成家庭状态分析，并生成 1 个待确认行动。",
    "CreatedAt": "2026-08-04T10:00:00Z",
    "FinishedAt": "2026-08-04T10:00:00Z",
    "Events": [
      { "Sequence": 1, "Type": "running", "Message": "正在收集已同步的家庭状态。", "CreatedAt": "2026-08-04T10:00:00Z" }
    ],
    "Actions": [
      { "Id": 78, "ActionType": "smart_home_device", "Status": "pending", "Title": "关闭卧室照明", "Description": "睡眠准备建议关闭卧室照明。", "DeviceId": 34, "DeviceName": "卧室主灯", "Capability": "power", "TargetValue": false }
    ]
  }
}
```

An unsupported intent returns `422`; when migration `009` has not initialized
the household expert, the endpoint returns a readable `503`. Neither error
executes a device action.

### 8.10 `GET /api/v1/expert-runs/{id}/actions`

Permission: `ai.run`. Returns the same safe household Run/Event/Action view for
the current user and tenant. A `404` response means the run is not owned by the
current user in the current tenant.

### 8.11 `POST /api/v1/expert-runs/{runId}/actions/{actionId}/confirm`

Permission: `ai.run`. Confirms exactly one pending `smart_home_device` action.
The client must create and preserve a UUID idempotency key for this confirmation:

```json
{
  "idempotencyKey": "7c1e7702-e4af-4a9e-b9d4-5f913b50cc91"
}
```

Before sending a command, the API rechecks that the Run belongs to the current
user and tenant, the device remains online, its capability is writable with a
matching value type, the Connector is healthy, and the member's Connector scope
contains the capability permission. A repeated request with the same key returns
the recorded result and never sends a second command.

```json
{
  "Code": 0,
  "Msg": "设备行动已执行。",
  "Data": {
    "ActionId": 78,
    "Status": "executed",
    "Message": "设备行动已执行。",
    "UpdatedAt": "2026-08-04T10:02:00Z"
  }
}
```

Configuration/secret errors return `503`; remote device-service failures return
`502`. Neither response exposes credentials, vendor IDs, service names or raw
provider errors. A cross-user or cross-tenant action returns `404`.

### 8.12 SmartHome read model (`/api/v1/smart-home`)

Permission: `smart_home.read`. These endpoints support the Home+ space-first
view. All data is isolated by the access token tenant; clients must not send a
tenant ID. The responses intentionally omit connector credentials, vendor IDs
and protocol-specific fields.

`GET /api/v1/smart-home/spaces`

```json
[
  {
    "Id": 12,
    "Name": "客厅",
    "SpaceType": "living_room",
    "Summary": "环境舒适，主灯已开启。",
    "DeviceCount": 2,
    "UpdatedAt": "2026-08-04T09:00:00Z"
  }
]
```

`GET /api/v1/smart-home/devices?spaceId=12` accepts an optional `spaceId`.
The device response supplies normalized capabilities and state freshness:

```json
[
  {
    "Id": 34,
    "SpaceId": 12,
    "Name": "客厅主灯",
    "DeviceType": "light",
    "OnlineStatus": "online",
    "StateSummary": "已开启，亮度 60%。",
    "StateUpdatedAt": "2026-08-04T09:00:00Z",
    "Capabilities": [
      { "Capability": "power", "ValueSchema": "{\"type\":\"boolean\"}", "Permission": "smart_home.light.write", "IsWritable": true }
    ]
  }
]
```

`GET /api/v1/smart-home/scenes` returns active scene cards (`Id`, `Key`,
`Name`, `Summary`, `Status`, `UpdatedAt`). Scene execution is not yet exposed;
the UI must treat this as read-only until the confirmed Action API is delivered.

### 8.13 Dashboard and scene runs (`/api/v1`)

`GET /api/v1/dashboard` requires `smart_home.read`. It returns a single
user-and-tenant-scoped view with `GeneratedAt` and `PartialFailure`. `Home`,
`Scenes`, `Todos`, `Calendar`, and `Suggestion` are independent modules. Each
module has `Status` (`available` or `unavailable`), `Data`, `UpdatedAt`, and an
optional readable `Message`; the UI must retain available cards when another
module is unavailable.

`Home.Data` contains household counts and space summaries with device online /
offline counts and the latest normalized state timestamp. `Todos.Data` and
`Calendar.Data` contain at most six current-user items for today. `Scenes.Data`
always exposes the standard `arrive_home`, `leave_home`, and `sleep` shortcuts.
The response never exposes connector credentials, vendor entity IDs, protocol
fields, or raw device state.

`POST /api/v1/smart-home/scenes/{sceneKey}/run` requires `ai.run` and accepts:

```json
{ "idempotencyKey": "9c1f9a71-6d38-4e6a-b1c2-7ef6cf16d6d3" }
```

Supported keys are `arrive_home`, `leave_home`, and `sleep` (the aliases
`arrive` and `away` are also accepted). The endpoint creates the same safe
Housekeeper Run view as `POST /api/v1/housekeeper-runs`: actions remain
`pending` until individually confirmed through the existing Action API. It
never dispatches a device command directly.

### 8.14 Connector management (`/api/v1`)

Connector responses never contain `credentialRef`, URL, access token, refresh
token, vendor entity ID or protocol fields. The tenant is derived from the
access token; clients must not send a tenant ID.

`GET /api/v1/connector-providers`

Permission: `connector.read`. Returns active catalog entries (`Id`, `Code`,
`Name`, `ConnectorType`, `Description`).

`GET /api/v1/connectors`

Permission: `connector.read`. Owners and admins receive the tenant's connector
list; members receive only connectors that have an active personal grant.
Each item includes `Id`, `ProviderId`, `ProviderCode`, `ProviderName`, `Name`,
`Status`, `LastSyncAt`, `LastHealthAt`, `CreatedAt` and `UpdatedAt`.

`POST /api/v1/connectors`

Permission: `connector.write` (owner/admin). Only the following body fields are
accepted; unknown or vendor credential fields return `422`.

```json
{
  "providerId": 1,
  "name": "My home",
  "credentialRef": "vault://tenants/12/secrets/home-assistant"
}
```

`credentialRef` must belong to the caller's tenant. It is validated but never
returned. With `SecretVault:Enabled=false` (the default), creation returns
`503` and a readable configuration message. A successful creation always starts
as `disconnected`.

`POST /api/v1/connectors/{id}/test`

Permission: `connector.write` (owner/admin). Tests the configured Home
Assistant connection and updates its health. A successful response returns only
the normalized operation view; it never exposes the HA URL, token or entity
information.

```json
{
  "Code": 0,
  "Msg": "Home Assistant 连接测试成功。",
  "Data": {
    "ConnectorId": 8,
    "Status": "connected",
    "DeviceCount": 0,
    "LastHealthAt": "2026-08-04T10:00:00Z",
    "LastSyncAt": null
  }
}
```

`POST /api/v1/connectors/{id}/discovery` and `POST /api/v1/connectors/{id}/sync`

Permission: `connector.write` (owner/admin). Both requests query the HA state
API, map only light, switch, air-conditioner, cover and sensor entities to the
standard device model, then write the current normalized state snapshot. They
return `ConnectorId`, `Status`, `DeviceCount`, `LastHealthAt` and `LastSyncAt`.
Unknown HA domains are ignored. `502` means HA is unreachable, rejected the
request or returned invalid data; `503` means the Vault is unavailable, denied
the tenant path, or contains an invalid secret. Neither response includes raw
HA entity IDs or protocol fields.

`POST /api/v1/connectors/{id}/sync` returns `202` after persisting a background
sync job. Poll `GET /api/v1/connectors/sync-jobs/{jobId}` with `connector.read`.
The job view contains only `Id`, `ConnectorId`, `Status`, `Reason`, `AttemptNo`,
`AvailableAt`, `CompletedAt` and `UpdatedAt`; status is `queued`, `running`,
`completed`, or `failed`. The server applies a 30-second timeout and up to
three attempts with exponential backoff. Clients must not retry by creating
parallel requests while a job is queued or running.

Runtime Vault requirements: set `SecretVault:Enabled=true` and
`SecretVault:Endpoint` to a HashiCorp Vault base URL, and supply its token only
through the process environment variable named by
`SecretVault:TokenEnvironmentVariable` (default
`NEXUSMIND_SECRET_VAULT_TOKEN`). For
`vault://tenants/12/secrets/home-assistant`, the adapter reads
`GET {Endpoint}/v1/tenants/12/secrets/home-assistant`. The Vault KV response
may use either `data.baseUrl` / `data.accessToken` or KV v2
`data.data.baseUrl` / `data.data.accessToken`; both values remain inside the
adapter process memory.

`GET /api/v1/connectors/{id}/authorization`

Permission: `connector.read`. Returns only the current member's grant:
`ConnectorId`, `UserId`, `Scopes`, `UpdatedAt`. A member without a grant gets
`403`; a connector outside the current tenant gets `404`.

`PUT /api/v1/connectors/{id}/authorizations/{memberUserId}`

Permission: `connector.write` (owner/admin). Grants or replaces a current
tenant member's scopes. The request must contain one to 32 well-formed scopes.

```json
{ "scopes": ["smart_home.read", "smart_home.light.write"] }
```

### 8.15 Automation rules (`/api/v1/automation-rules`)

`GET /api/v1/automation-rules` requires `automation.read`; `POST` and
`PATCH /api/v1/automation-rules/{id}` require `automation.write` (owner/admin).
The tenant and owner come from the access token. Clients cannot submit an
owner, tenant, credential, vendor entity identifier, or arbitrary command.

```json
{
  "name": "日落后回家照明",
  "triggerType": "time_schedule",
  "trigger": {
    "kind": "sun",
    "event": "sunset",
    "timeZone": "China Standard Time",
    "latitude": 39.9042,
    "longitude": 116.4074,
    "offsetMinutes": 5
  },
  "conditions": [],
  "actions": [{ "sceneKey": "arrive_home" }],
  "approvalPolicy": "manual_confirmation",
  "enabled": true
}
```

`triggerType` is `time_schedule`, `device_state_change`, `scene_completed`, or
`sync_completed`. Time triggers support `fixed_time` (`time: "21:30"`),
`sun` (`sunrise`/`sunset`) and one-shot `countdown` (`fireAt` UTC).
Device-state triggers require a tenant-owned `deviceId`; scene triggers use an
existing scene key; sync-completion triggers can optionally narrow to a
connector ID. Conditions compare a normalized device state using `deviceId`,
`capability`, optional `operator: "not_equals"`, and `value`. Actions are
limited to built-in `sceneKey` values.

`approvalPolicy` is `manual_confirmation` or `auto_execute`. The first creates
normal pending Run Actions. The second uses the rule owner's current Connector
authorization and the existing idempotent confirmation/audit path; it does not
expose a direct device command API. Updates require `rowVersion` and return
`409` on a concurrent change. Responses expose only the normalized rule view.

### 8.16 Expert Files (`/api/v1`)

All responses and audits are tenant-isolated. Files, attachments, and
read-tokens never include credentials, internal object paths, storage provider
keys, vendor entity IDs or third-party file IDs.

`POST /api/v1/expert-files` — permission `expert_file.write`. Creates an upload
session. The request is metadata-only; binary is uploaded through the
returned short-lived URL.

```json
{
  "name": "周报模板.md",
  "mimeType": "text/markdown",
  "sizeBytes": 4096,
  "sha256": "9c1f9a71...",
  "quotaBytes": 4096,
  "idempotencyKey": "9c1f9a71-6d38-4e6a-b1c2-7ef6cf16d6d3"
}
```

```json
{
  "FileId": 901,
  "Status": "pending_upload",
  "UploadToken": "...",
  "UploadUrl": "api/v1/expert-files/901/objects/<objectKey>?uploadToken=...",
  "ExpiresAtUnixTime": 1722768000
}
```

`POST /api/v1/expert-files/{fileId}/objects` — permission `expert_file.write`.
Posts the committed object metadata; the server transitions the file to
`scanning` and then to `ready` or `rejected` based on the scanner result. Only
`ready` files can be attached or read.

`GET /api/v1/expert-files` — permission `expert_file.read`. Returns the latest
100 summary rows (`Id`, `Name`, `MimeType`, `SizeBytes`, `Status`, scan fields,
expiry, soft-delete flag, `RowVersion`).

`DELETE /api/v1/expert-files/{fileId}` — permission `expert_file.write`. Soft
delete plus storage cleanup; the response is the post-delete summary.

`POST /api/v1/expert-files/{fileId}/read-token?purpose=download` — permission
`expert_file.read`. Issues a 10-minute `readToken` and `readUrl`; the response
never contains the internal object key or storage path.

`POST /api/v1/experts/{expertId}/files` and
`POST /api/v1/expert-runs/{runId}/files` — permission `expert_file.write`. The
body is `{ "fileId": <id>, "idempotencyKey"?: "<uuid>" }`. Only `ready` files
in the caller's tenant are accepted.

### 8.17 Team Runs (`/api/v1/team-runs`)

Permission: `team_run.write` for `POST /team-runs`, `/cancel`, `/retry`;
`team_run.read` for everything else. The first published `teamVersion` is `1`;
clients must send `"teamVersion": "1"` exactly. Only three modes are accepted:
`sequential`, `parallel`, `synthesis`. External side effects (device writes,
notifications, etc.) remain governed by the existing Run Action confirmation,
Adapter and audit chain; team runs may only orchestrate Expert and ExpertFile
references that the caller already owns.

`POST /api/v1/team-runs` body:

```json
{
  "teamVersion": "1",
  "mode": "sequential",
  "parentAgentRunId": 12345,
  "members": [
    { "expertVersionId": 11, "displayName": "梳理员", "stageOrder": 0 },
    { "expertVersionId": 12, "displayName": "评审员", "stageOrder": 1 }
  ],
  "fileIds": [901, 902],
  "idempotencyKey": "9c1f9a71-6d38-4e6a-b1c2-7ef6cf16d6d3"
}
```

The server freezes the team, computes the per-member permission intersection
(`ai.read`, `ai.run`, plus the `toolPolicy` from the ExpertVersion), and writes
the audit entry `team_run_create`. `parentAgentRunId` must reference an
existing `AgentRun` in the caller's tenant. The response is `TeamRunSummary`
(`Id`, `Status`, `Mode`, `TeamVersion`, `ParentAgentRunId`, timestamps,
`RowVersion`).

`GET /api/v1/team-runs/{id}` returns `TeamRunSummary`. `Status` is one of
`pending | running | completed | failed | cancelled`.

`GET /api/v1/team-runs/{id}/events` returns the recent audit-derived
`TeamRunEvent` list. No prompt, model output or vendor log is exposed.

`GET /api/v1/team-runs/{id}/members` returns each member's display name,
`StageOrder`, `ExpertVersionId`, optional `ChildAgentRunId`, `Status`,
optional `LastErrorCode`, and `PermissionIntersectionSummary` (comma-separated
scope names).

`GET /api/v1/team-runs/{id}/synthesis` is only available when the run is
`completed`; otherwise the endpoint returns `409`. The view contains
`Summary`, `Highlights`, and `CompletedAt`. Intermediate member outputs and
prompts are not returned at any step.

`POST /api/v1/team-runs/{id}/cancel` is only valid while the run is `pending`
or `running`. `POST /api/v1/team-runs/{id}/retry` is only valid after a
terminal status. Both write audit entries and update the
`HomeMind.Automation` counters. Cross-tenant or unknown `teamRunId` returns
`404`.

## 9. Permission summary

| Endpoint group | Policy |
| --- | --- |
| `GET /api/v1/auth/me`, `POST /api/v1/auth/logout` | `identity.read` |
| `GET /api/v1/experts[...]` | `ai.read` |
| `POST /api/v1/expert-runs[...]` | `ai.run` |
| `GET /api/v1/skills`, `POST /api/v1/skills`, `PUT/DELETE /api/v1/skills/{id}` | `ai.skills.read` / `ai.skills.write` |
| `GET /api/v1/calendar/events`, `GET /api/v1/calendar/subscriptions` | `calendar.read` |
| `POST/PUT/DELETE /api/v1/calendar/...` | `calendar.write` |
| `GET /api/v1/todos`, `.../subtasks` (read) | `todo.read` |
| `POST/PUT/DELETE /api/v1/todos[...]` | `todo.write` |
| `GET /api/v1/smart-home/spaces`, `/devices`, `/scenes` | `smart_home.read` |
| `GET /api/v1/connector-providers`, `GET /api/v1/connectors`, `GET /api/v1/connectors/{id}/authorization` | `connector.read` |
| `POST /api/v1/connectors`, `/connectors/{id}/test`, `/connectors/{id}/discovery`, `/connectors/{id}/sync`, `PUT /api/v1/connectors/{id}/authorizations/{memberUserId}` | `connector.write` |
| `GET /api/v1/automation-rules` | `automation.read` |
| `POST/PATCH /api/v1/automation-rules[...]` | `automation.write` |
| `GET /api/v1/expert-files`, `POST /api/v1/expert-files/{fileId}/read-token` | `expert_file.read` |
| `POST /api/v1/expert-files`, `/expert-files/{fileId}/objects`, `DELETE /api/v1/expert-files/{fileId}`, `POST /api/v1/experts/{expertId}/files`, `POST /api/v1/expert-runs/{runId}/files` | `expert_file.write` |
| `GET /api/v1/team-runs/{id}`, `/events`, `/members`, `/synthesis` | `team_run.read` |
| `POST /api/v1/team-runs`, `/cancel`, `/retry` | `team_run.write` |

Roles (`owner` / `admin` / `member` / `viewer`) and the allowed policies are
defined in `HomeMind.Api/Services/Authorization.cs`. Adjust there when adding
new roles or scopes.

## 10. Type / enum reference

| Field | Allowed values |
| --- | --- |
| `Todo.status` | `pending`, `in_progress`, `completed` |
| `Todo.type` | `task`, `shopping`, `habit`, `note` (free-form string is accepted) |
| `Todo.priority` | `p0`–`p3` (free-form string is accepted) |
| `CalendarEvent.allDay` | boolean |
| `CalendarEvent.repeatRule` | iCalendar RRULE string |
| `Todo.repeatRule` | iCalendar RRULE string |
| `ExpertCatalog.catalogType` | `expert` \| `group` |
| `AgentRun.sourceType` | `expert` \| `group` |
| `AgentRun.status` | `draft` \| `queued` \| `planning` \| `running` \| `completed` \| `failed` \| `cancelled` |
| `AgentRunAction.actionType` | `plan` \| `todos` \| `calendar_events` \| `smart_home_device` |
| `HousekeeperRun.intent` | `sleep` \| `away` \| `arrive` \| `environment_review` |
| `HousekeeperRunAction.status` | Legacy compatibility action; `pending` until its explicit confirmation route is invoked. New screens use `AgentRunAction`. |
| `SmartHomeDevice.onlineStatus` | `online` \| `offline` \| `unknown` |
| `AutomationRule.triggerType` | `time_schedule` \| `device_state_change` \| `scene_completed` \| `sync_completed` |
| `AutomationRule.approvalPolicy` | `manual_confirmation` \| `auto_execute` |
| `ConnectorSyncJob.status` | `queued` \| `running` \| `completed` \| `failed` |
| `Subscription.platform` (auth) | `h5` \| `android` \| `ios` |
