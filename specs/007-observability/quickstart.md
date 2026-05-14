# Quickstart: Observability — Logs & Metrics

**Feature**: 007-observability
**Date**: 2026-05-14

## Prerequisites

- .NET 10 SDK
- Docker + Docker Compose

## Start the Observability Stack

```bash
cd observability
docker compose up -d
```

Services will be available at:
- **Grafana**: http://localhost:3000 (admin/admin)
- **Prometheus**: http://localhost:9090
- **OTel Collector**: http://localhost:4318 (OTLP HTTP)

## Run the MCP Server with Telemetry

```bash
cd src/Buildout.Mcp
export BUILDIN__BOT_TOKEN="your-token"
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4318"

dotnet run
```

The MCP server starts with stdio transport. Invoke any MCP tool — logs and metrics flow to the OTel Collector, then to Grafana.

## View in Grafana

1. Open http://localhost:3000
2. Go to **Dashboards** — three pre-provisioned dashboards are available:
   - **Buildout Operations Overview**: Operation rates, latencies, error rates
   - **Buildin API Client Health**: Per-method API call metrics
   - **MCP Tool Usage**: Per-tool invocation counts and durations
3. Go to **Explore** — query logs from Loki using `{service_name="buildout-mcp"} | json | operation="search"`

## Configuration

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://localhost:4318` | OTLP HTTP endpoint |
| `OTEL_DOTNET_AUTO_METRICS_ENABLED` | (not set) | Set to `false` to disable auto-instrumented metrics |
| `Buildin__BotToken` | (required) | Buildin API token |
| `BUILDOUT_TELEMETRY_ENABLED` | `true` | Set to `false` to disable all telemetry |

## Tear Down

```bash
cd observability
docker compose down
```

Volumes are preserved. To remove all data:

```bash
docker compose down -v
```

## Verify Telemetry is Working

```bash
# Check OTel Collector is receiving data
curl http://localhost:8889/metrics | grep buildout

# Query Prometheus directly
curl 'http://localhost:9090/api/v1/query?query=buildout_operations_total'
```
