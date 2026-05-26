# Contract: Audit CallToolFilter

**Feature**: `013-audit-trails` | **Date**: 2025-05-25

## Location

`src/Buildout.Mcp/Audit/AuditTrailFilter.cs`

## Signature

The filter is registered via the MCP SDK's `AddCallToolFilter` mechanism:

```csharp
builder.Services.AddMcpServer()
    .WithRequestFilters(filters =>
    {
        filters.AddCallToolFilter(async (context, next, cancellationToken) =>
        {
            // audit pre-processing
            var result = await next(context, cancellationToken);
            // audit post-processing
            return result;
        });
    });
```

## Contract

### Filter Behavior

1. **Before tool execution**: Capture `CallToolRequestParams.Name` (tool name) and `CallToolRequestParams.Arguments` (parameters dict). Start a stopwatch.
2. **Execute tool**: Call `await next(context, cancellationToken)`.
3. **After tool execution (success)**: Stop stopwatch. Construct `AuditEntry` with `Outcome=Success`. Fire-and-forget `IAuditTrail.RecordEntryAsync`.
4. **After tool execution (failure)**: Catch exception. Stop stopwatch. Construct `AuditEntry` with `Outcome=Failure` and `ErrorDetails` from exception message. Fire-and-forget `IAuditTrail.RecordEntryAsync`. Re-throw the exception.

### Session ID Extraction

Extract `Mcp-Session-Id` from `context.Server.SessionId` (or equivalent MCP SDK session metadata). If unavailable (e.g., stdio), the field is null.

### Filter Registration Condition

The filter is ONLY registered when:
- `Audit:Enabled=true` in configuration, AND
- HTTP transport is configured

When either condition is false, no filter is registered — zero overhead.

### Error Isolation

If `IAuditTrail.RecordEntryAsync` throws, the filter MUST catch and log the exception. The tool result MUST be returned to the caller unmodified. Audit failures are never visible to tool callers.

## Registration

In `Program.cs`, after `AddMcpServer()`:

```csharp
var auditEnabled = mergedConfig.GetValue<bool>("Audit:Enabled");
// ... after AddMcpServer() and transport selection ...
if (auditEnabled && /* http transport */)
{
    mcpBuilder.WithRequestFilters(filters =>
    {
        filters.AddCallToolFilter(/* AuditTrailFilter delegate */);
    });
}
```
