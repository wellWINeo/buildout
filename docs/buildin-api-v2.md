# Buildin API V2

Buildout production traffic uses the published Buildin Developer API V2 contract
and `/v2/` routes only. Generated sources under `src/Buildout.Core/Buildin/Generated/V2`
are disposable and must be recreated with the pinned Kiota tool.

The checked-in OpenAPI snapshot is the reviewed 2.0.0 contract. The generation
overlay adds only documented `DELETE /v2/blocks/{block_id}` while that operation
is absent upstream. Page delete/restore is intentionally not exposed.

Page creation sends a native `Idempotency-Key`. Editing reads expose the service
`ETag` as the opaque revision value. Block writes do not receive `If-Match`; a
multi-block update remains a visible, non-atomic sequence.

Use `AccessToken` / `Buildout__AccessToken` for either integration or OAuth bearer
credentials. `BotToken` / `Buildout__BotToken` is a deprecated fallback and emits
one warning containing key names only.

Refresh and regenerate explicitly:

```bash
./scripts/fetch_openapi.sh
./scripts/regenerate-buildin-client.sh
```

Tests use WireMock only; no live service or real credential is required.
