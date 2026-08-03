# Development Environment

## Local .NET baseline

| Component | Version |
| --- | --- |
| .NET SDK | 3.1.425 |
| Target framework | netcoreapp3.1 |
| ASP.NET Core runtime | 3.1.32 |
| .NET runtime | 3.1.32 |
| Operating system | Windows 11 23H2 (10.0.22631), x64 |

The repository is pinned to SDK 3.1.425 by `global.json`. .NET 8 SDKs are also
installed locally, but they are not used by this solution.

## Start the API

```powershell
dotnet run --project .\HomeMind.Api
```

The API listens on `http://localhost:5280`.

- `HomeMind.Api/Properties/launchSettings.json` controls the IDE profile.
- `HomeMind.Api/Program.cs` provides the same default for direct Kestrel execution.

Both use port 5280 so the application does not conflict with the project
already using port 5000. To use another port temporarily, set
`ASPNETCORE_URLS` before starting the application, for example:

```powershell
$env:ASPNETCORE_URLS = 'http://localhost:5290'
dotnet run --project .\HomeMind.Api
```
