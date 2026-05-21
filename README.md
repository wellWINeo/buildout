# Buildout

Buildout is a CLI tool and MCP (Model Context Protocol) server for building and managing content.

## Installation

### Prerequisites

- For self-contained binaries: No prerequisites required
- For framework-dependent dlls (FDD): [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

### Download

Build artifacts are available from the CI workflow on GitHub Actions. Navigate to the latest [Actions run](../../actions) to download the artifacts for your platform:

**Self-contained binaries (recommended):**
- `buildout-cli-linux-x64` — CLI for Linux AMD64
- `buildout-mcp-linux-x64` — MCP server for Linux AMD64
- `buildout-cli-linux-arm64` — CLI for Linux ARM64
- `buildout-mcp-linux-arm64` — MCP server for Linux ARM64
- `buildout-cli-macos-arm64` — CLI for macOS ARM64 (Apple Silicon)
- `buildout-mcp-macos-arm64` — MCP server for macOS ARM64 (Apple Silicon)

**Framework-dependent dlls:**
- `buildout-cli-fdd` — CLI dll (requires .NET 10 runtime)
- `buildout-mcp-fdd` — MCP server dll (requires .NET 10 runtime)

### Manual Install

For self-contained binaries:

```bash
# Download and extract the artifact for your platform
chmod +x buildout-cli
chmod +x buildout-mcp

# Move to a directory in your PATH
mv buildout-cli /usr/local/bin/
mv buildout-mcp /usr/local/bin/
```

For framework-dependent dlls:

```bash
# Download and extract the artifact
dotnet Buildout.Cli.dll [args]
dotnet Buildout.Mcp.dll [args]
```

## Usage

### CLI

```bash
buildout-cli [command] [options]
```

### MCP Server

The MCP server can be run as a standalone process:

```bash
buildout-mcp
```

Or configured in your MCP client settings.

## Development

### Build

```bash
dotnet build
```

### Test

```bash
# Run unit tests
dotnet test tests/Buildout.UnitTests

# Run integration tests (requires OPENROUTER_API_KEY)
dotnet test tests/Buildout.IntegrationTests
```

### Publish Locally

```bash
# Self-contained single-file binary
dotnet publish src/Buildout.Cli -c Release -r <rid> --self-contained -p:PublishSingleFile=true
dotnet publish src/Buildout.Mcp -c Release -r <rid> --self-contained -p:PublishSingleFile=true

# Framework-dependent dll
dotnet publish src/Buildout.Cli -c Release
dotnet publish src/Buildout.Mcp -c Release
```

## License

TBD