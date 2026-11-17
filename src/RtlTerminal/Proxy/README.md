# Proxy

Windows, environment, and developer-tool proxy management, status inspection, and encrypted backup snapshots.

## Files

- `DeveloperProxySummary.cs` — Reads and summarizes configured proxy addresses from supported developer tool user files.
- `DeveloperTools.cs` — Detects installed developer tools and maps them to proxy providers and capabilities.
- `MachineProxyElevation.cs` — Elevated helper communication and verification for machine-level proxy environment changes.
- `ProxyDiagnostics.cs` — Sanitizes sensitive proxy strings and formats diagnostic exception details.
- `ProxyHistory.cs` — Manages the locked, DPAPI-encrypted proxy snapshot history store.
- `ProxyInspectionProviders.cs` — Inspection-only proxy providers for recording non-mutated scopes in snapshots.
- `ProxyModels.cs` — Core proxy endpoint, state, validation, and `ProxyService` orchestration models.
- `ProxyOperationState.cs` — Observable progress and outcome state for sequential proxy operations in the UI.
- `ProxyStatus.cs` — Read-only status snapshot reader across Environment, Windows, and WinHTTP scopes.
- `ProxyToolVersion.cs` — Detects installed developer tool versions and supported proxy capabilities.
- `WindowsProxyProviders.cs` — Providers for Windows user environment variables, WinINET/system settings, and WinHTTP.
- `XmlProxyProvider.cs` — XML configuration proxy provider (such as Maven settings).
