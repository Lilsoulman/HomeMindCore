# Local API implementation

The API connects to local MySQL through `HomeMind.Api/appsettings.json`:

```text
Server=localhost;Database=nexus_mind;User=root;Password=123456;SslMode=None;
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
| Agent Runtime / Experts | Expert and Expert Group catalog/policy lookup; `POST /api/v1/expert-runs`, Run query, events, cancellation, retry and controlled actions. The route name is retained for compatibility, but the domain resource is `AgentRun`. |
| Expert Files (V1) | `POST/GET /api/v1/expert-files`, `POST /api/v1/expert-files/{fileId}/objects`, `DELETE /api/v1/expert-files/{fileId}`, `POST /api/v1/expert-files/{fileId}/read-token`, `POST /api/v1/experts/{expertId}/files`, `POST /api/v1/expert-runs/{runId}/files` |
| Team Runs (V1) | `POST /api/v1/team-runs`; `GET /api/v1/team-runs/{id}`, `/events`, `/members`, `/synthesis`; `POST /api/v1/team-runs/{id}/cancel`, `/retry` |
| SmartHome | `GET /api/v1/smart-home/spaces`, `/devices?spaceId=`, `/scenes`; `POST /api/v1/smart-home/scenes/{sceneKey}/run` creates confirmation-required scene Run actions; normalized device discovery/state sync runs through Connector routes |
| Dashboard | `GET /api/v1/dashboard` aggregates independently degradable home, scene, today-plan and latest-suggestion modules |
| Connectors | `GET /api/v1/connector-providers`, `GET/POST /api/v1/connectors`, `POST /api/v1/connectors/{id}/test`, `/discovery`, `/sync`, `GET /connectors/sync-jobs/{jobId}`, and member authorization routes |
| Automation | `GET/POST/PATCH /api/v1/automation-rules` manages tenant-isolated, authorized long-running rules |

Authenticated routes require `Authorization: Bearer <accessToken>`. The server
derives both `user_id` and `tenant_id` from the access token and never trusts
those values from JSON input.

SmartHome read routes return normalized space, device and scene data only. They
do not return connector credentials, vendor entity IDs, protocol fields or raw
device state. Local demonstration data remains disabled unless
`SmartHome:MockEnabled=true` is explicitly configured.

## Dashboard and scene shortcuts

`GET /api/v1/dashboard` requires `smart_home.read` and derives user and tenant
from the access token. It returns independent `Home`, `Scenes`, `Todos`,
`Calendar`, and `Suggestion` modules. Each module declares `available` or
`unavailable` status with its own timestamp and readable message, so a failed
module does not block the rest of the Dashboard.

`POST /api/v1/smart-home/scenes/{sceneKey}/run` requires `ai.run`. The supported
shortcuts are `arrive_home`, `leave_home`, and `sleep`; each is mapped to the
existing Housekeeper planning workflow. The result is a credential-free Run
with `pending` device actions, and existing confirmation, authorization,
idempotency, adapter dispatch, and audit rules remain mandatory.

Connector creation accepts only a tenant-owned `credentialRef` in the form
`vault://tenants/{tenantId}/...`. The reference is never returned by the API.
`SecretVault:Enabled` is `false` by default, so create requests return a readable
`503` configuration error until Vault configuration is supplied. Runtime HA
access uses HashiCorp Vault at `SecretVault:Endpoint`; its access token comes
only from the process environment variable named by
`SecretVault:TokenEnvironmentVariable` and HA credentials are read from the
tenant-owned `credentialRef`. Neither token is stored or returned by this API.

## Automation and reliable sync

`GET/POST/PATCH /api/v1/automation-rules` uses the `automation.read` and
`automation.write` policies. Only owners and admins can create or modify a
rule; the tenant is always derived from the access token. A rule has one of
four trigger types: `time_schedule`, `device_state_change`, `scene_completed`,
or `sync_completed`. The time scheduler accepts `fixed_time`, `sun`
(sunrise/sunset with optional coordinates and offset), and one-shot
`countdown` (`fireAt` UTC). Device-state rules are fed by normalized connector
state changes; the same internal event service is the adapter integration point
for MQTT subscriptions, while REST polling continues to provide the fallback.

Rule actions are deliberately restricted to built-in scene keys. They create
the normal credential-free Housekeeper Run and audited device actions.
`manual_confirmation` leaves actions pending. `auto_execute` invokes the same
authorization, idempotency, Adapter, audit and state-write workflow under the
rule owner's active authorization; it never bypasses the command boundary.
Rule updates require the returned `rowVersion`.

`POST /api/v1/connectors/{id}/sync` returns `202` with a sync-job view. The
work is durable in `connector_sync_jobs`, then signalled through an in-process
Channel consumed by `AutomationWorker`. Work is capped at three attempts, uses
a 30-second operation timeout and exponential retry delay. A periodic scan
recovers work after process restart. Structured worker logs and the
`HomeMind.Automation` meter expose rule-triggered, sync-queued, sync-retried
and sync-failed counters without credentials or vendor identifiers.

## Agent Runtime and compatibility workflows

Every new AI workflow creates and is managed as an `AgentRun`. The persisted
table remains `expert_runs` only to preserve existing foreign keys and client
route compatibility. `AgentRun` has exactly these statuses:

```text
draft | queued | planning | running | completed | failed | cancelled
```

`POST /api/v1/expert-runs` resolves the requested published Expert or Expert
Group policy, stores an AgentRun, creates an `expert_jobs` queue item and
returns `queued`. Model calls do not run in the API request thread. An Expert
defines role, prompt, allowed Skills and permissions; it never dispatches an
external command. A Skill is the execution boundary, and a Connector is the
only gateway to an external system.

`POST /api/v1/housekeeper-runs` and its confirmation routes remain available
only for the pre-AgentRuntime SmartHome compatibility workflow. New Flutter
features must create an AgentRun instead. SmartHome remains a Connector domain:
the current front-end contract is normalized read data and controlled action
drafts, while Home Assistant, MQTT, Zigbee and Matter are connector/adaptor
implementations rather than core business logic.

`POST /api/v1/housekeeper-runs` requires `ai.run`. The access token determines
the user and tenant; a caller cannot provide either value. It accepts a limited,
auditable intent and optionally narrows analysis to one tenant-owned space:

```json
{
  "intent": "sleep",
  "spaceId": 12,
  "idempotencyKey": "9c1f9a71-6d38-4e6a-b1c2-7ef6cf16d6d3"
}
```

Allowed intents are `sleep`, `away`, `arrive` and `environment_review`. The
service reads only normalized spaces, online devices and writable capabilities;
it never reads a Connector credential, vendor entity ID or protocol field. The
response contains display-safe events and action drafts. A device draft contains
only `Id`, `ActionType` (`smart_home_device`), `Status` (`pending`), title,
description, canonical device ID/name, capability and target value. It does not
execute a command.

`GET /api/v1/expert-runs/{id}/actions` returns the same safe Run/Event/Action
view for the current user in the current tenant. A cross-tenant or other-user
run returns `404`.

`POST /api/v1/expert-runs/{runId}/actions/{actionId}/confirm` requires `ai.run`
and accepts a required UUID `idempotencyKey`. It rechecks the caller's connector
scope, connection health, device ownership/online state and writable capability
before dispatching a canonical `DeviceCommand` through the provider Adapter.
The response contains only the action ID, execution status, readable message and
timestamp. Credentials, vendor entity IDs and protocol fields never leave the
Adapter. Every attempted dispatch has a credential-free audit record; successful
dispatches write a normalized device-state snapshot.

## Deliberately gated routes

- `POST /api/v1/auth/wechat/exchange` returns `501` until WeChat channel
  configuration (AppId, secret, redirect callback and server-side exchange)
  is supplied. It does not fabricate a WeChat identity.
- `POST /api/v1/calendar/ical/fetch` returns `501` until an SSRF allow-list
  policy is configured.

## Expert Files (V1)

`POST /api/v1/expert-files` requires `expert_file.write`. The request declares
metadata only (`name`, `mimeType`, `sizeBytes`, `sha256`, optional `quotaBytes`
and `idempotencyKey`); file binary must be uploaded separately through the
returned short-lived `uploadUrl`. The response includes `fileId`, `status`
(always `pending_upload` on success), `uploadToken`, `uploadUrl` and
`expiresAtUnixTime`. No storage credential, internal object path, or third-party
file ID is returned. By default `ExpertFiles:Storage:Enabled=false` returns a
readable `503`; the same applies to `ExpertFiles:Scanner:Enabled=false`.

`POST /api/v1/expert-files/{fileId}/objects` requires `expert_file.write`. The
client posts one or more committed object metadata blocks (`objectKey`,
`offsetBytes`, `sizeBytes`, `sha256`). The server stores the metadata, sets
`status` to `scanning`, then to `ready` or `rejected`. Only `ready` files are
visible to subsequent attachment or read-token calls. Rejection reasons are
limited to extension/MIME/size/SHA-256 mismatches and the local scanner toggle.

`GET /api/v1/expert-files` requires `expert_file.read` and returns the
tenant-scoped summary list (id, name, mimeType, sizeBytes, status, scan fields,
expiry, soft-delete flag, `rowVersion`). Cross-tenant file ids return `404`.

`DELETE /api/v1/expert-files/{fileId}` requires `expert_file.write` and is a
soft delete: the row is marked `deleted`, attachments are removed, and storage
cleanup is best-effort. A `file_delete` audit entry is written.

`POST /api/v1/expert-files/{fileId}/read-token` requires `expert_file.read`,
takes a required `purpose` query parameter, and returns a short-lived
(`expiresAtUnixTime` within 10 minutes) `readToken` plus a `readUrl` that does
not contain the internal object key or storage path. Every issuance writes a
`file_read` audit entry.

`POST /api/v1/experts/{expertId}/files` and `POST /api/v1/expert-runs/{runId}/files`
require `expert_file.write`. The body is `{ "fileId": <id>, "idempotencyKey"?: "<uuid>" }`.
Only `ready` files in the same tenant are accepted; cross-tenant or non-ready
files return `404`. Attachments are append-only and write a `file_attach`
audit entry.

Server responses never include credentials, vendor entity IDs, scan-provider
secrets, storage provider keys, internal object paths or third-party file IDs.

## Team Runs (V1)

`POST /api/v1/team-runs` requires `team_run.write`. The first published
`teamVersion` is `1`; clients must send `"teamVersion": "1"` exactly. Only
three modes are accepted: `sequential`, `parallel`, `synthesis`. The request
body is:

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

The server validates each `expertVersionId` belongs to the caller's tenant and
is `published`; every `fileId` must be a `ready` file in the same tenant. The
server then freezes the team into a `team_run_template_version` row, computes
the per-member permission intersection (`ai.read`, `ai.run` plus the
`toolPolicy` declared on the ExpertVersion), and creates a `team_runs` row plus
one `team_run_members` row per member. The returned `teamRunId` is the only
identifier a client should retain.

`team_run` request and response bodies never include member-level prompts,
model thinking chains, vendor logs, raw intermediate outputs, cross-member
context, file binary, or storage paths. Clients must not submit `prompt`,
`messages`, `tools`, or arbitrary DAG nodes in this request.

`GET /api/v1/team-runs/{id}` returns the `TeamRunSummary` (`id`, `status`,
`mode`, `teamVersion`, `parentAgentRunId`, timestamps, `rowVersion`). Status is
one of `pending | running | completed | failed | cancelled`.

`GET /api/v1/team-runs/{id}/events` returns the recent audit-derived
`TeamRunEvent` list (`id`, `eventType`, `displayPayload`, `createdAt`). No
prompt or model output is included.

`GET /api/v1/team-runs/{id}/members` returns each member's display name,
`stageOrder`, `expertVersionId`, optional `childAgentRunId`, `status`, optional
`lastErrorCode`, and a `permissionIntersectionSummary` (a comma-separated list
of effective scope names).

`GET /api/v1/team-runs/{id}/synthesis` is only available once the team run is
`completed`. It returns the `TeamRunSynthesis` view (`summary`, `highlights`,
`completedAt`). Until then the endpoint returns `409` with a readable message.

`POST /api/v1/team-runs/{id}/cancel` and `POST /api/v1/team-runs/{id}/retry`
require `team_run.write`. `cancel` is only valid while the run is `pending` or
`running`; `retry` is only valid once the run has reached a terminal state.
Both endpoints write audit entries and increment
`HomeMind.Automation` counters (`team_runs_triggered_total`,
`team_run_members_failed_total`, `team_run_synthesis_failed_total`).

External side effects for team runs are still produced by the existing Run
Action confirmation, Adapter and audit chain; team orchestration never bypasses
that boundary. Cross-tenant or unknown `teamRunId` returns `404`.

## Run locally

The current process on port `5280` must be restarted after code changes. Run:

```powershell
dotnet run --project .\HomeMind.Api
```

Swagger is available at `http://localhost:5280/swagger`.
