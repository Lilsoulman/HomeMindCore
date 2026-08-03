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
```

See `docs/api-implementation.md` for current API coverage and guarded external
integrations.

- `HomeMind.Api/Properties/launchSettings.json` controls the IDE profile.
- `HomeMind.Api/Program.cs` provides the same default for direct Kestrel execution.

Both use port 5280 so the application does not conflict with the project
already using port 5000. To use another port temporarily, set
`ASPNETCORE_URLS` before starting the application, for example:

```powershell
$env:ASPNETCORE_URLS = 'http://localhost:5290'
dotnet run --project .\HomeMind.Api
```
