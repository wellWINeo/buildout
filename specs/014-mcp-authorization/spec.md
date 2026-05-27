# Feature Specification: MCP Authorization Modes

**Feature Branch**: `014-mcp-authorization`
**Created**: 2025-05-27
**Status**: Draft
**Input**: User description: "Authorization modes for MCP server: none, passthrough, token-proxy, token-mapped — controlling how incoming MCP requests are authenticated and which Buildin Bot API keys are used for outbound requests. MCP-only feature, no CLI impact."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Single Global Key, No MCP Auth (Priority: P1)

As a single-user deploying the MCP server locally or behind a private network, I want all MCP requests to use one shared Buildin Bot API key without requiring any MCP-level authentication, so that I can get started quickly with zero configuration overhead.

**Why this priority**: This is the current behavior. It must remain the default and must not break. It is the baseline all other modes extend from.

**Independent Test**: Deploy the MCP server with HTTP transport and a single BotToken configured. Invoke any tool without providing credentials. Verify the request succeeds and uses the configured BotToken.

**Acceptance Scenarios**:

1. **Given** the server is configured with `Auth:Mode` set to `none` (or not set), **When** an MCP client calls any tool without credentials, **Then** the request succeeds using the global BotToken from config.
2. **Given** the server is configured with `Auth:Mode` set to `none`, **When** an MCP client provides an `Authorization` header anyway, **Then** the header is ignored and the request proceeds with the global BotToken.

---

### User Story 2 - Passthrough Mode (Priority: P2)

As a team lead running the MCP server for multiple trusted users, I want each user to provide their own Buildin Bot API key with every MCP request, so that each user's actions are attributed to their own Buildin account and respects their own workspace permissions.

**Why this priority**: Enables multi-user accountability without requiring any server-side token management. Users bring their own key — the simplest multi-user model.

**Independent Test**: Deploy the server with `Auth:Mode` set to `passthrough`. Have two clients call tools, each providing a different Buildin Bot API key. Verify each request uses the respective key.

**Acceptance Scenarios**:

1. **Given** the server is configured with `Auth:Mode` set to `passthrough`, **When** an MCP client calls a tool with `Authorization: Bearer <buildin-bot-key>`, **Then** the server uses that provided key for the outbound Buildin API call.
2. **Given** the server is configured with `Auth:Mode` set to `passthrough`, **When** an MCP client calls a tool without an `Authorization` header, **Then** the server rejects the request with an authentication error.
3. **Given** the server is configured with `Auth:Mode` set to `passthrough`, **When** an MCP client provides an invalid (non-functional) Buildin Bot API key, **Then** the request proceeds and the Buildin API returns a 401/403, which the server propagates as a tool error.

---

### User Story 3 - Token Proxy Mode (Priority: P3)

As a platform operator, I want to issue my own MCP-specific tokens to users while routing all requests through a single shared Buildin Bot API key, so that I can control who accesses the MCP server without managing multiple Buildin accounts.

**Why this priority**: Adds access control layer on top of the single-key model. Useful when the operator wants to gate MCP access but all users share one Buildin workspace.

**Independent Test**: Configure the server with `Auth:Mode` set to `proxy`, create one MCP token in the database, and set one global BotToken. Call a tool with the valid MCP token. Verify it works. Call without the token. Verify it is rejected.

**Acceptance Scenarios**:

1. **Given** the server is configured with `Auth:Mode` set to `proxy`, **When** an MCP client calls a tool with a valid MCP token via `Authorization: Bearer <mcp-token>`, **Then** the server accepts the request and uses the configured global BotToken for the Buildin API call.
2. **Given** the server is configured with `Auth:Mode` set to `proxy`, **When** an MCP client calls a tool without an `Authorization` header or with an invalid token, **Then** the server rejects the request with an authentication error.
3. **Given** the server is configured with `Auth:Mode` set to `proxy`, **When** a previously valid MCP token is revoked, **Then** subsequent requests with that token are rejected.

---

### User Story 4 - Token Mapped Mode (Priority: P4)

As a platform operator managing multiple Buildin workspaces, I want to issue MCP-specific tokens that are each mapped to a different Buildin Bot API key, so that I can provide MCP access to users across different workspaces while maintaining centralized token management.

**Why this priority**: The most flexible mode — full N:N mapping. Requires the most setup but provides complete per-user Buildin workspace isolation.

**Independent Test**: Configure the server with `Auth:Mode` set to `mapped`, create two MCP tokens in the database each mapped to a different Buildin Bot API key. Call a tool with each token. Verify each request uses its mapped Buildin key.

**Acceptance Scenarios**:

1. **Given** the server is configured with `Auth:Mode` set to `mapped`, **When** an MCP client calls a tool with a valid MCP token mapped to a specific Buildin Bot API key, **Then** the server uses that mapped Buildin key for the outbound request.
2. **Given** the server is configured with `Auth:Mode` set to `mapped`, **When** an MCP client provides a valid MCP token that has no mapped Buildin Bot API key, **Then** the server rejects the request with a configuration error.
3. **Given** the server is configured with `Auth:Mode` set to `mapped`, **When** an MCP client provides an MCP token mapped to an invalid (non-functional) Buildin Bot API key, **Then** the request proceeds and the Buildin API returns a 401/403, which the server propagates as a tool error.

---

### User Story 5 - Token Lifecycle Management (Priority: P4)

As an operator using proxy or mapped mode, I want to create, list, and revoke MCP tokens and their Buildin Bot API key mappings through the CLI, so that I can manage access without directly editing the database.

**Why this priority**: Management tooling is required for proxy and mapped modes to be practically usable. Same priority as the modes that need it.

**Independent Test**: Use CLI commands to create an MCP token, list it, and then revoke it. Verify each step produces the expected output.

**Acceptance Scenarios**:

1. **Given** the server is configured for proxy or mapped mode, **When** the operator runs a CLI command to create an MCP token, **Then** a new token is generated and stored, and the token value is displayed.
2. **Given** the operator has created MCP tokens, **When** the operator runs a CLI command to list tokens, **Then** all active tokens are displayed with their metadata (name, created date, status).
3. **Given** an MCP token exists, **When** the operator runs a CLI command to revoke it, **Then** the token is marked as revoked and subsequent MCP requests using it are rejected.
4. **Given** the server is configured for mapped mode, **When** the operator runs a CLI command to map an MCP token to a Buildin Bot API key, **Then** the mapping is stored and requests using that token will use the mapped key.

---

### Edge Cases

- What happens when `Auth:Mode` is set to `passthrough` but no `Authorization` header is provided? — Request is rejected with an authentication error.
- What happens when `Auth:Mode` is set to `proxy` or `mapped` but the token database is empty? — All requests are rejected; operator must provision tokens first.
- What happens when a revoked token is used? — Request is rejected as if the token never existed.
- What happens when `Auth:Mode` is changed while the server is running? — Mode is read at startup; a restart is required to change modes.
- What happens to CLI commands when `Auth:Mode` is `none` or `passthrough`? — Token management CLI commands are not applicable and should inform the user.
- What happens when `Auth:Mode` is `none` but no global BotToken is configured? — This is the existing behavior — startup fails with a configuration error.
- What happens when the token database is corrupted or unreachable? — Server startup fails with a clear error message indicating the auth database is unavailable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The MCP server MUST support four authorization modes: `none`, `passthrough`, `proxy`, and `mapped`.
- **FR-002**: The server MUST default to `none` mode when `Auth:Mode` is not specified, preserving current behavior.
- **FR-003**: In `none` mode, the server MUST use a single global Buildin Bot API key from configuration for all requests, ignoring any client-provided credentials.
- **FR-004**: In `passthrough` mode, the server MUST require each MCP request to include a Buildin Bot API key via the `Authorization: Bearer` header, and MUST use that key for the outbound Buildin API call.
- **FR-005**: In `passthrough` mode, the server MUST reject requests without a valid `Authorization` header.
- **FR-006**: In `proxy` mode, the server MUST validate the incoming MCP token against a stored token registry and use the global Buildin Bot API key for all accepted requests.
- **FR-007**: In `mapped` mode, the server MUST validate the incoming MCP token against a stored token registry and resolve the corresponding Buildin Bot API key for each request.
- **FR-008**: The server MUST support creating, listing, and revoking MCP tokens via CLI commands for `proxy` and `mapped` modes.
- **FR-009**: The server MUST support managing Buildin Bot API keys and their mappings to MCP tokens via CLI commands for `mapped` mode.
- **FR-010**: Token validation MUST distinguish between invalid tokens, revoked tokens, and missing credentials, returning appropriate error responses.
- **FR-011**: Authorization MUST apply only to the MCP HTTP transport. The CLI and stdio transport MUST NOT be affected.
- **FR-012**: The authorization mode MUST be validated at server startup and an invalid or incomplete configuration MUST prevent the server from starting.
- **FR-013**: MCP tokens MUST be stored securely with hashed values, never in plaintext.
- **FR-014**: The server MUST log authentication failures without exposing token values.
- **FR-015**: When audit trails are enabled alongside authorization, each audit entry MUST reference the MCP token identity used to authenticate the request (not the Buildin Bot API key). In `none` and `passthrough` modes, the identity field is empty.

### Key Entities

- **MCP Token**: An operator-issued credential used by MCP clients to authenticate. Has a name/label, a hashed secret, a creation timestamp, a revocation flag, and optional metadata. Unique by its hashed value.
- **Buildin Bot API Key**: A Buildin platform credential used for outbound API calls. Has a name/label, the key value (stored in plaintext — must be retrievable to send as Bearer token), and a creation timestamp.
- **Token-Key Mapping**: A link between an MCP Token and a Buildin Bot API Key. Used in `mapped` mode to determine which Buildin key to use for each authenticated request. One MCP token maps to exactly one Buildin Bot API Key. Multiple MCP tokens may map to the same Buildin Bot API Key (true N:N).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can switch between all four authorization modes by changing a single configuration value and restarting the server.
- **SC-002**: All four modes pass their acceptance scenarios with zero authentication bypasses.
- **SC-003**: Token management operations (create, list, revoke, map) complete in under 1 second for up to 100 tokens.
- **SC-004**: Authentication validation adds less than 5ms latency to each MCP request in `proxy` and `mapped` modes.
- **SC-005**: The `none` mode behavior is identical to pre-feature behavior — existing deployments require zero changes.
- **SC-006**: All existing MCP tool tests continue to pass without modification in `none` mode.

## Clarifications

### Session 2025-05-27

- Q: How should Buildin Bot API keys be stored in the database (they must be retrievable to send as Bearer tokens)? → A: Plaintext storage, relying on database/file-level access controls. The global BotToken continues to be provided via configuration as today; only per-token mapped keys in `mapped` mode are stored in the database.
- Q: Should auth tables share the same database as audit trails or have a separate database? → A: Same database as audit trails (shared connection, same SQLite file / PostgreSQL database). Audit trail entries must reference the MCP token used to authenticate the request (not the Buildin key).
- Q: Can multiple MCP tokens map to the same Buildin Bot API key in mapped mode (true N:N)? → A: Yes, multiple MCP tokens can map to the same Buildin key (true N:N). A team of users can each have their own MCP token while sharing a Buildin workspace.

## Assumptions

- MCP tokens are simple opaque strings (e.g., UUIDs or random hex) generated by the server, not JWTs or externally-issued tokens.
- Token storage uses the same database as audit trails (shared connection, same SQLite file / PostgreSQL database).
- The `Authorization: Bearer <token>` header is used for all modes that require client credentials, regardless of whether the token is an MCP token or a Buildin Bot API key.
- Buildin Bot API keys stored in the database are kept in plaintext since they must be retrievable to send as Bearer tokens to the Buildin API. The global BotToken used in `none` and `proxy` modes continues to come from configuration (not the database).
- CLI commands for token management are separate from the MCP server process (not MCP tools).
- Token hashing uses a standard one-way hash; the original token value is shown only once at creation time.
- The feature does not include token expiration (tokens remain valid until explicitly revoked).
- Rate limiting and brute-force protection are out of scope for this feature.
- The stdio transport bypasses all authorization (local-only, trusted context).
