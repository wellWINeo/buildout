# Feature Specification: Management UI Skeleton

**Feature Branch**: `016-admin-ui-skeleton`  
**Created**: 2026-06-01  
**Status**: Draft  
**Input**: User description: "Management UI skeleton with C#, ASP.NET Core & Blazor in a separate Buildout.AdminUI project. Two tabs: keys management and audit logs view. Mocked data only."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse the Admin UI (Priority: P1)

An administrator opens the management UI and sees a two-tab interface: one tab for managing API keys and one for reviewing audit logs. They can switch between tabs and confirm that each view loads with visible, representative data.

**Why this priority**: Establishes the foundational navigation structure. All other stories depend on the UI being accessible and navigable.

**Independent Test**: Open the admin interface in a browser; verify two tabs are present and switching between them renders distinct content. Delivers a working navigation scaffold.

**Acceptance Scenarios**:

1. **Given** the admin opens the management UI, **When** the page loads, **Then** two clearly labelled tabs ("Keys Management" and "Audit Logs") are visible.
2. **Given** the admin is on any tab, **When** they click the other tab, **Then** the corresponding view is displayed without a full-page reload.

---

### User Story 2 - View Keys Management Tab (Priority: P2)

An administrator navigates to the Keys Management tab and sees a list of API keys with relevant metadata (name, status, creation date). The list is populated with representative mock data.

**Why this priority**: Keys management is a primary operational concern — administrators need to see at a glance which keys exist and their current state.

**Independent Test**: Navigate to the Keys Management tab; verify a table or list of mock API keys is rendered with at least name, status, and creation date columns.

**Acceptance Scenarios**:

1. **Given** the admin opens the Keys Management tab, **When** the view loads, **Then** a list of mock API keys is displayed with columns for name, status, and creation date.
2. **Given** the keys list is visible, **When** the admin inspects a key entry, **Then** the key's status (e.g., Active / Revoked) is clearly indicated.
3. **Given** no real data source is connected, **When** the view loads, **Then** mock data is used and no errors are shown.

---

### User Story 3 - View Audit Logs Tab (Priority: P3)

An administrator navigates to the Audit Logs tab and sees a chronological list of recorded events (actor, action, timestamp, target resource). The list is populated with representative mock data.

**Why this priority**: Audit visibility is a compliance and security requirement. Providing the view skeleton allows stakeholders to validate the information layout before real data integration.

**Independent Test**: Navigate to the Audit Logs tab; verify a list of mock log entries is rendered with at least actor, action, timestamp, and resource columns.

**Acceptance Scenarios**:

1. **Given** the admin opens the Audit Logs tab, **When** the view loads, **Then** a chronological list of mock audit log entries is displayed.
2. **Given** the audit logs list is visible, **When** the admin inspects an entry, **Then** actor, action performed, timestamp, and affected resource are clearly shown.
3. **Given** no real data source is connected, **When** the view loads, **Then** mock data is used and no errors are shown.

---

### Edge Cases

- What happens when the admin opens a direct URL to a specific tab? The correct tab must activate.
- How does the UI behave when the mock data list is empty? An appropriate empty-state message is shown rather than a blank area.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display a two-tab navigation interface accessible from the root admin URL.
- **FR-002**: System MUST provide a "Keys Management" tab showing a list of API keys with name, status, and creation date.
- **FR-003**: System MUST provide an "Audit Logs" tab showing a list of events with actor, action, timestamp, and affected resource.
- **FR-004**: Both tabs MUST render representative mock data — no real backend connection is required.
- **FR-005**: System MUST allow switching between tabs without a full-page reload.
- **FR-006**: System MUST show an empty-state message when a tab's list has no data entries.
- **FR-007**: The admin interface MUST be hosted as a standalone web application, separate from any existing API or service projects.
- **FR-008**: Direct URL navigation to a tab MUST activate that tab on load.

### Key Entities

- **API Key**: Represents an issued credential. Key attributes: name, status (Active / Revoked), creation date.
- **Audit Log Entry**: Represents a recorded administrative or API event. Key attributes: actor, action, affected resource, timestamp.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An administrator can navigate to either tab within 2 seconds of the page first becoming interactive (first visible render in the browser).
- **SC-002**: Both tabs display mock data on first load without any user-visible errors.
- **SC-003**: Switching between tabs takes less than 500 ms and requires no full-page reload.
- **SC-004**: All mandatory columns (name, status, creation date for keys; actor, action, resource, timestamp for logs) are visible without horizontal scrolling on a standard 1280 px-wide viewport.
- **SC-005**: The admin UI runs independently — starting it does not require starting any other service.

## Assumptions

- The target audience is internal administrators; no authentication or authorisation is required for this skeleton.
- Mobile responsiveness is out of scope for this skeleton; standard desktop viewport (1280 px+) is the target.
- The standalone project will follow the same solution structure as existing projects in the repository.
- The initial skeleton does not need create, edit, or delete actions on keys — read-only listing is sufficient for P1 scope.
