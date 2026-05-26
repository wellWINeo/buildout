# Configuration

Buildout loads configuration from multiple sources in a specific precedence order, allowing flexible setup for different environments.

## Loading and Precedence Order

Configuration values are loaded from multiple sources, with later sources overriding earlier ones:

1. **In-code defaults** (property defaults in options classes)
2. **Default JSON file** at `~/.config/buildout/config.json` (if it exists and no `--config` flag is provided)
3. **Override JSON file** specified via `--config` or `-c` flag (hard error if missing)
4. **Legacy OTel endpoint** from `OTEL_EXPORTER_OTLP_ENDPOINT` env var (fallback only)
5. **Environment variables** with `Buildout__` prefix (e.g., `Buildout__BotToken`)
6. **Http section remapping** (projects `Http:Timeout` to `HttpTimeout` property)

Higher-numbered sources override lower-numbered sources for the same key. Environment variables use double underscore (`__`) as the section separator.

## File Location

**Default location:** `~/.config/buildout/config.json`

**Override via flag:** Use `--config <path>` or `-c <path>` to specify a different file.

**Error semantics:**
- Default file missing → silently ignored, uses defaults
- Override file specified but missing → hard error with "Configuration file not found: <path>"
- Override file is a directory → hard error with "Configuration file not found: <path>"

## Configuration Keys

| Key | Type | Default | Required | Validation | Env Var Form |
|-----|------|---------|----------|------------|--------------|
| `BotToken` | `string` | — | **yes** | non-empty / non-whitespace | `Buildout__BotToken` |
| `BaseUrl` | URI string | `https://api.buildin.ai/` | no | absolute URI; HTTPS unless `Http:UnsafeAllowInsecure=true` | `Buildout__BaseUrl` |
| `Http:Timeout` | `TimeSpan` (`HH:MM:SS`) | `00:00:30` | no | > `00:00:00` | `Buildout__Http__Timeout` |
| `Http:UnsafeAllowInsecure` | `bool` | `false` | no | — | `Buildout__Http__UnsafeAllowInsecure` |
| `Limitations:LargeDeleteThreshold` | `int` | `10` | no | `>= 0` | `Buildout__Limitations__LargeDeleteThreshold` |
| `Telemetry:Enabled` | `bool` | `false` | no | — | `Buildout__Telemetry__Enabled` |
| `Telemetry:OtlpEndpoint` | URI string | `http://localhost:4318` | no | absolute URI; `http`/`https` scheme only | `Buildout__Telemetry__OtlpEndpoint` |
| `Transport:Type` | `string` | `stdio` | no | `stdio` or `http` | `Buildout__Transport__Type` |
| `Audit:Enabled` | `bool` | `false` | no | — | `Buildout__Audit__Enabled` |
| `Audit:Provider` | `string` | — | yes if `Audit:Enabled=true` | `sqlite` or `postgresql` | `Buildout__Audit__Provider` |
| `Audit:SqlitePath` | `string` | — | yes if `Provider=sqlite` | non-empty, valid file path | `Buildout__Audit__SqlitePath` |
| `Audit:ConnectionString` | `string` | — | yes if `Provider=postgresql` | non-empty | `Buildout__Audit__ConnectionString` |
| `Audit:MaxParameterLength` | `int` | `10000` | no | `> 0` | `Buildout__Audit__MaxParameterLength` |

### Examples by Channel

**JSON file (`~/.config/buildout/config.json`):**
```json
{
  "BotToken": "ntn_xxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "BaseUrl": "https://api.buildin.ai/",
  "Http": {
    "Timeout": "00:00:30",
    "UnsafeAllowInsecure": false
  },
  "Limitations": {
    "LargeDeleteThreshold": 10
  },
  "Telemetry": {
    "Enabled": false,
    "OtlpEndpoint": "http://localhost:4318"
  },
  "Transport": {
    "Type": "stdio"
  },
  "Audit": {
    "Enabled": false,
    "Provider": null,
    "SqlitePath": null,
    "ConnectionString": null,
    "MaxParameterLength": 10000
  }
}
```

**Environment variables:**
```bash
export Buildout__BotToken="ntn_xxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
export Buildout__BaseUrl="https://api.buildin.ai/"
export Buildout__Http__Timeout="00:01:00"
export Buildout__Http__UnsafeAllowInsecure="false"
export Buildout__Limitations__LargeDeleteThreshold="20"
export Buildout__Telemetry__Enabled="true"
export Buildout__Telemetry__OtlpEndpoint="http://localhost:4318"
export Buildout__Transport__Type="http"
export Buildout__Audit__Enabled="true"
export Buildout__Audit__Provider="sqlite"
export Buildout__Audit__SqlitePath="/path/to/audit.db"
export Buildout__Audit__ConnectionString=""
export Buildout__Audit__MaxParameterLength="10000"
```

**Command-line override:**
```bash
buildout-cli --config ./prod.json search "release notes"
buildout-cli -c ~/dev-config.json get page-123
```

## Precedence Diagram

```
┌─────────────────────────────────────────────────────────┐
│ 1. In-code defaults (lowest priority)                    │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ 2. Default JSON file (~/.config/buildout/config.json)    │
│    (only if --config flag not provided)                  │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ 3. Override JSON file (--config / -c flag)               │
│    (hard error if missing)                               │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ 4. Legacy OTel endpoint (OTEL_EXPORTER_OTLP_ENDPOINT)   │
│    (fallback for Telemetry:OtlpEndpoint only)           │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ 5. Environment variables (Buildout__ prefix)             │
│    (highest priority for most settings)                  │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ 6. Http section remapping (projects Http: keys to        │
│    HttpTimeout/UnsafeAllowInsecure properties)           │
└─────────────────────────────────────────────────────────┘
```

## Migration from Earlier Versions

If you have configuration from earlier versions of Buildout, you may need to update your keys:

| Pre-010 Key (env / config) | Pre-010 Channel | New Key (config-key syntax) | New Env Var |
|----------------------------|-----------------|------------------------------|-------------|
| `Buildin:BotToken` | JSON only | `BotToken` | `Buildout__BotToken` |
| `Buildin:BaseUrl` | JSON only | `BaseUrl` | `Buildout__BaseUrl` |
| `Buildin:HttpTimeout` | JSON only | `Http:Timeout` | `Buildout__Http__Timeout` |
| `Buildin:UnsafeAllowInsecure` | JSON only | `Http:UnsafeAllowInsecure` | `Buildout__Http__UnsafeAllowInsecure` |
| `PageEditor:LargeDeleteThreshold` | JSON only | `Limitations:LargeDeleteThreshold` | `Buildout__Limitations__LargeDeleteThreshold` |
| `BUILDOUT_TELEMETRY_ENABLED` | env var only | `Telemetry:Enabled` | `Buildout__Telemetry__Enabled` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | env var only | `Telemetry:OtlpEndpoint` | `Buildout__Telemetry__OtlpEndpoint` (fallback still supported) |

**Note:** `OTEL_EXPORTER_OTLP_ENDPOINT` continues to be honoured as a low-precedence fallback for `Telemetry:OtlpEndpoint`.

## Common Pitfalls

### `appsettings.json` Not Discovered
Buildout does not automatically discover `appsettings.json`. The default configuration file location is `~/.config/buildout/config.json`. Use the `--config` flag to specify a custom file path.

### Missing `$HOME` Fallback
If `$HOME` is not set, the default file location (`~/.config/buildout/config.json`) cannot be resolved. Ensure your environment has a valid `$HOME` variable set.

### TimeSpan Format (`HH:MM:SS`)
All `TimeSpan` values must use the `HH:MM:SS` format (e.g., `00:00:30` for 30 seconds, `00:01:00` for 1 minute). Other formats will fail validation.

### Section Separator (`__`)
Environment variables use double underscore (`__`) as the section separator, not colon. For example:
- ✅ `Buildout__Http__Timeout`
- ❌ `Buildout__Http:Timeout`

### Env Var Case Sensitivity
Environment variables are case-sensitive on most systems. Ensure your variable names exactly match the documented format (e.g., `Buildout__BotToken`, not `buildout__BotToken`).

## Audit Trail Configuration

Audit trails provide detailed logging of MCP tool invocations for compliance and monitoring purposes. This feature is opt-in and disabled by default.

### Audit Configuration Keys

| Key | Type | Default | Required | Validation | Env Var Form |
|-----|------|---------|----------|------------|--------------|
| `Audit:Enabled` | `bool` | `false` | no | — | `Buildout__Audit__Enabled` |
| `Audit:Provider` | `string` | — | yes if `Audit:Enabled=true` | `sqlite` or `postgresql` | `Buildout__Audit__Provider` |
| `Audit:SqlitePath` | `string` | — | yes if `Provider=sqlite` | non-empty, valid file path | `Buildout__Audit__SqlitePath` |
| `Audit:ConnectionString` | `string` | — | yes if `Provider=postgresql` | non-empty | `Buildout__Audit__ConnectionString` |
| `Audit:MaxParameterLength` | `int` | `10000` | no | `> 0` | `Buildout__Audit__MaxParameterLength` |

### Audit Configuration Examples

**SQLite audit trail:**
```json
{
  "Audit": {
    "Enabled": true,
    "Provider": "sqlite",
    "SqlitePath": "/path/to/audit.db",
    "MaxParameterLength": 10000
  }
}
```

```bash
export Buildout__Audit__Enabled="true"
export Buildout__Audit__Provider="sqlite"
export Buildout__Audit__SqlitePath="/path/to/audit.db"
export Buildout__Audit__MaxParameterLength="10000"
```

**PostgreSQL audit trail:**
```json
{
  "Audit": {
    "Enabled": true,
    "Provider": "postgresql",
    "ConnectionString": "Host=localhost;Port=5432;Database=audit;Username=user;Password=pass",
    "MaxParameterLength": 10000
  }
}
```

```bash
export Buildout__Audit__Enabled="true"
export Buildout__Audit__Provider="postgresql"
export Buildout__Audit__ConnectionString="Host=localhost;Port=5432;Database=audit;Username=user;Password=pass"
export Buildout__Audit__MaxParameterLength="10000"
```

**Important notes:**
- Audit trails are only active when `Transport:Type=http` and `Audit:Enabled=true`
- When disabled, audit recording has zero performance impact
- Audit failures are logged but never block tool execution
- Parameters and error details are truncated to `MaxParameterLength` characters