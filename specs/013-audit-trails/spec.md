# Feature Specification: MCP Audit Trails

**Feature Branch**: `013-audit-trails`
**Created**: 2025-05-25
**Status**: Draft
**Input**: User description: "Audit trails for MCP tool calls over HTTP transport. MCP-only feature (CLI not affected, stdio transport not audited). All tool calls (reads/edits/search etc.) must be written to audit trails (persisted). Persistence via RDBMS supporting SQLite and PostgreSQL. Opt-in, disabled by default (Audit section in config). Core project defines interface IAuditTrails; implementation lives in Mcp project."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record Tool Invocations for Compliance (Priority: P1)

A workspace operator enables audit trails to maintain a persistent record of every MCP tool invocation over HTTP transport. When an LLM agent calls any tool (get_page_markdown, update_page, search, create_page, delete_page, restore_page, database_view), the system persists a complete record of when it was called, with what parameters, what the outcome was, and which MCP session it belonged to — without affecting tool behavior or response times beyond a negligible overhead.

**Why this priority**: This is the core value proposition. Without persisted records, there is no audit trail feature. All other stories depend on this foundation.

**Independent Test**: Can be fully tested by enabling audit trails, invoking any MCP tool, and verifying a record appears in the database with correct fields.

**Acceptance Scenarios**:

1. **Given** audit trails are enabled with SQLite, **When** an agent calls `get_page_markdown` with `page_id=123`, **Then** a record is persisted containing tool name `get_page_markdown`, the page_id parameter, a timestamp, and a success outcome.
2. **Given** audit trails are enabled with PostgreSQL, **When** an agent calls `update_page` which fails with a patch rejection, **Then** a record is persisted containing tool name `update_page`, the failure outcome, and the error details.
3. **Given** audit trails are disabled (default), **When** any MCP tool is invoked, **Then** no audit records are written and tool behavior is identical to a build without audit trail support.

---

### User Story 2 - Choose Persistence Backend (Priority: P2)

A workspace operator configures which RDBMS backend to use for audit trail storage. For single-user or local development, they choose SQLite (zero-setup, file-based). For team or production deployments, they choose PostgreSQL (shared access, robust concurrency, existing infrastructure).

**Why this priority**: Backend flexibility is essential for adoption. SQLite lowers the barrier to entry; PostgreSQL supports real-world deployments. Without both options, the feature excludes valid use cases.

**Independent Test**: Can be tested by configuring each backend, invoking tools, and confirming records are stored in the correct database with the correct schema.

**Acceptance Scenarios**:

1. **Given** configuration specifies `Audit:Provider` as `sqlite` and `Audit:SqlitePath` as `/data/audit.db`, **When** the MCP server starts, **Then** the SQLite database file is created at that path with the correct schema, and subsequent tool invocations are recorded there.
2. **Given** configuration specifies `Audit:Provider` as `postgresql` and `Audit:ConnectionString` as a valid PostgreSQL connection string, **When** the MCP server starts, **Then** it connects to PostgreSQL, ensures the schema exists, and subsequent tool invocations are recorded there.
3. **Given** configuration specifies `Audit:Provider` as `postgresql` but the connection string is invalid or the database is unreachable, **Then** the MCP server refuses to start with a clear error message naming the offending configuration key.

---

### User Story 3 - Zero Impact When Disabled (Priority: P3)

A workspace operator runs the MCP server without audit trails (the default). Every tool invocation completes with identical performance and behavior compared to a build that has no audit trail code at all. No database connections are opened, no files are created, and no overhead is incurred.

**Why this priority**: Disabled-by-default is a stated requirement. Operators must trust that opting out truly means zero cost — no latent connections, no background writes, no hidden latency.

**Independent Test**: Can be tested by running the full tool suite with `Audit:Enabled=false` (or with no Audit section), measuring latency, and verifying no database/file artifacts are created.

**Acceptance Scenarios**:

1. **Given** no `Audit` section exists in configuration, **When** the MCP server starts and any tool is invoked, **Then** no audit-related database connections or file handles are opened, and the tool response is indistinguishable from a build without audit trail support.
2. **Given** `Audit:Enabled` is explicitly `false`, **When** the MCP server starts, **Then** the behavior is identical to having no Audit section at all.

---

### Edge Cases

- What happens when the audit database is temporarily unavailable (e.g., PostgreSQL connection drops mid-session)? Tool invocations must succeed regardless; audit write failures must be logged but must never block or fail a tool call.
- What happens when the SQLite file becomes corrupted or read-only? Same principle: tool calls succeed, audit write failure is logged, no tool impact.
- What happens when disk is full and SQLite cannot write? Tool calls succeed, audit write failure is logged.
- What happens with very large tool parameters (e.g., a page update with a massive Markdown payload)? The audit record must store or truncate parameters to a reasonable limit without failing.
- What happens when two MCP server instances write to the same SQLite file concurrently? The database must handle write contention gracefully without data loss or corruption.
- What happens when the audit schema needs to evolve in a future version? The system must detect schema version and handle migration at startup.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST record an audit entry for every MCP tool invocation over HTTP transport when audit trails are enabled, capturing: tool name, `Mcp-Session-Id`, timestamp (UTC), invocation parameters, outcome (success or failure), duration, error details on failure, and the authenticated MCP token identity (when authorization is enabled; empty when authorization is `none` or `passthrough`).
- **FR-002**: Audit trail recording MUST NOT alter the tool's return value, error behavior, or observable latency characteristics when enabled.
- **FR-003**: The system MUST support two persistence backends: SQLite (file-based) and PostgreSQL (connection-string-based), selectable via configuration.
- **FR-004**: The system MUST create the required database schema automatically on startup when audit trails are enabled — no manual database setup required.
- **FR-005**: When audit trails are disabled (the default), the system MUST NOT open any database connections, create any files, or allocate any audit-related resources.
- **FR-006**: The system MUST continue serving tool invocations even if an audit write fails; failures MUST be logged but MUST NOT propagate to the tool caller.
- **FR-007**: Audit trail configuration MUST be exposed through both the JSON configuration file and `Buildout__`-prefixed environment variables, per the dual-channel configuration discipline.
- **FR-008**: An `IAuditTrail` interface MUST be defined in `Buildout.Core`; concrete persistence implementations MUST live in `Buildout.Mcp`.
- **FR-009**: The `Buildout.Core` DI registration MUST register a no-op `IAuditTrail` implementation when audit trails are disabled, so that MCP tool handlers can always depend on the interface without conditional logic.
- **FR-010**: Configuration MUST fail fast at startup if audit trails are enabled but required settings (provider, connection path/string) are missing or invalid.
- **FR-011**: Audit parameters MUST be truncated to a configurable maximum length to prevent unbounded storage growth from large payloads.
- **FR-012**: Each audit entry MUST include a unique identifier (UUID v7 for time-ordering) and a UTC timestamp to support chronological querying and deduplication.

- **FR-013**: Audit trails MUST only record invocations arriving over HTTP transport. Tool invocations via stdio transport MUST NOT trigger audit recording.
- **FR-014**: Audit writes MUST be asynchronous (fire-and-forget) relative to tool execution. The tool response MUST be returned to the caller without waiting for the audit record to be persisted.

### Key Entities

- **AuditEntry**: Represents a single tool invocation record. Attributes: unique identifier, tool name, `Mcp-Session-Id` (from the protocol-native HTTP header), authenticated token identity (MCP token name/label when authorization is enabled; empty otherwise), timestamp (UTC), invocation parameters (serialized, truncated), outcome (success/failure), duration, error details (on failure).
- **AuditOptions**: Configuration shape for the Audit section. Attributes: Enabled flag, Provider selection (sqlite/postgresql), SqlitePath, ConnectionString (for PostgreSQL), MaxParameterLength.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every MCP tool invocation produces exactly one audit record when enabled, regardless of tool outcome (success or failure).
- **SC-002**: Tool response latency increases by less than 5 milliseconds on average when audit trails are enabled, compared to disabled.
- **SC-003**: Operators can switch between SQLite and PostgreSQL backends by changing only configuration — no code changes or rebuilds required.
- **SC-004**: No file artifacts or database connections exist when audit trails are disabled, verified by process inspection.
- **SC-005**: A new workspace operator can enable SQLite-based audit trails, start the server, invoke a tool, and find the record in the database within 5 minutes of reading the configuration documentation.

## Clarifications

### Session 2025-05-25

- Q: The spec claims "tamper-evident records" but no functional requirement backs this. Remove the claim or add tamper-evidence requirements? → A: Remove "tamper-evident"; records are standard database rows. Tamper-proofing can be added in a future iteration if needed.
- Q: What constitutes an MCP "session" for session identification, and does this apply to stdio? → A: This is an HTTP-only feature; no audit trails for stdio/local deployments. The session identifier is the protocol-native `Mcp-Session-Id` HTTP header, assigned by the server during initialization and echoed by the client on all subsequent requests.
- Q: Should the async (fire-and-forget) write model be explicit in the spec, or left as an implementation detail implied by FR-002 and FR-006? → A: Make it explicit — add a requirement that audit writes MUST be asynchronous relative to tool execution, ensuring tool responses are never delayed by audit persistence.
- Q: Should audit entries reference the MCP token used for authentication? → A: Yes. Each audit entry must include the MCP token identity (token name/label) when authorization is enabled. This provides traceability to the authenticated caller, not the Buildin API key used behind the scenes.

## Assumptions

- This feature is MCP HTTP-transport-only; the CLI and stdio transport are not in scope and will not be affected.
- Audit trail storage is append-only; querying, searching, or exporting audit records is out of scope for this feature and may be addressed in a future feature.
- The audit trail schema will be versioned to support future evolution, but this feature defines only version 1.
- SQLite concurrent writes from multiple MCP server processes are acceptable with reasonable retry/wait behavior; true high-concurrency scenarios should use PostgreSQL.
- Audit entries older than a retention period may need cleanup, but retention policy and automatic pruning are out of scope for this feature.
- The interface defined in Core (`IAuditTrail`) is a thin contract sufficient for MCP tool handlers to record audit entries without depending on persistence details.
- The session identifier is the protocol-native `Mcp-Session-Id` HTTP header defined by the MCP Streamable HTTP transport specification. The server assigns it during initialization; the client echoes it on all subsequent requests. No custom session mechanism is needed.
- No authentication or authorization is required to read/write audit records — the database file (SQLite) or database (PostgreSQL) is assumed to be protected by filesystem or infrastructure access controls.
