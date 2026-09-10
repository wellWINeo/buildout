# Contract: V2 Contract Refresh, Overlay, and Client Regeneration

## Canonical source

- Published OpenAPI URI: `https://api.buildin.ai/v2/openapi.json`
- Checked-in snapshot: `openapi.json`
- Required title: `Buildin Developer API V2`
- Required version baseline: `2.0.0`
- Reviewed snapshot SHA-256:
  `413c8a8aa356a942b9f46e829b92e1cd047d470fbdf9d91edfbcbd6094cca49d`
- Reviewed `/v2/openapi/meta` response (2026-09-06): `pathCount: 17`,
  `generatedAt: 2026-09-05T02:59:07.000Z`, and ETag
  `da96784fcf4d3b29e4baaf5ded926ec9e2d3ef10a18a4536fecd456906493c90`.
- Required production route prefix: `/v2/`

The checked-in snapshot is preserved byte-for-byte as published. Official V2
documentation describes block DELETE missing from that snapshot, so a separate
OpenAPI Overlay 1.0 document supplies only that retained operation for generation.

## Documentation overlay

`scripts/openapi.buildout-overlay.json` contains exactly one targeted update:

1. Add `DELETE /v2/blocks/{block_id}` as `deleteBlock`, returning `Block`,
   requiring `blocks.write`, with the documented V2 error/header conventions.

The overlay records its upstream hash and evidence URLs. It must not change any
other operation, schema, example, scope, or server.

Before application, block DELETE must still be absent from canonical OpenAPI. If
upstream adds it, generation stops and names the colliding pointer. The maintainer
compares upstream against the documented definition, then removes the overlay/
applier and generates directly from canonical.

The overlay intentionally does not add page DELETE or page-update `in_trash`:
feature 016 removes every Buildout page lifecycle caller, and generating unused
documented operations would expand the maintained surface without user value.

## Refresh behavior

`scripts/fetch_openapi.sh` must:

1. Resolve the repository root independently of caller working directory.
2. Fetch public `/v2/openapi/meta` first and record its ETag, generated timestamp,
   and path count without treating documentation examples as live metadata.
3. Download to a temporary file only when required, without printing
   authorization data.
4. Parse and validate OpenAPI version, title, version, actual path count, and V2
   path identity.
5. Reject a former V1 contract, a metadata/document mismatch, or an unreviewed
   hash change.
6. Atomically replace repository-root `openapi.json` only after validation.
7. Report old/new metadata, hash, and identity without rewriting an unchanged file.

Refresh is an explicit maintainer/network operation. Ordinary tests never fetch
the live contract.

## Effective-contract normalization

The deterministic normalizer must:

- Read canonical `openapi.json` plus the overlay and write a derived effective
  contract at a stable ignored path such as `obj/Buildin/OpenApi/openapi.effective.json`.
- Verify the overlay's upstream hash and absence preconditions, recursively apply
  only its one update, and emit canonical UTF-8/LF JSON.
- Give stable names/discriminator mappings to inline `oneOf` members needed by
  current operations, including block, block-input/update, parent, property,
  icon/cover/file, and page/database search unions.
- Preserve the effective operation paths, methods, fields, examples, scopes, and
  service meaning.
- Produce byte-identical output for byte-identical canonical input and overlay.
- Fail rather than overwrite or guess when source pointers change.

Neither canonical OpenAPI nor the overlay is overwritten by normalization.

## Generation behavior

Both `regenerate-buildin-client.sh` and `regenerate-buildin-client.ps1` invoke the
same normalizer and pinned Kiota settings:

- language: C#
- client: V2-specific generated class/namespace
- input: stable-path effective contract
- included production paths: V2 only
- output: `src/Buildout.Core/Buildin/Generated/V2/`
- clean output enabled
- backward-compatible generated overloads excluded
- tool version from `.config/dotnet-tools.json`

The scripts recreate one shared `_README.md` marker with identical LF-normalized
content. No hand-maintained production code lives under `Generated/`.

## Acceptance gates

- Removing the overlaid block DELETE node from the effective contract produces a deep
  structural match with canonical OpenAPI.
- Effective inventory for the pinned snapshot is 17 paths and 22 operations with
  unique operation IDs; only `deleteBlock` is added.
- The generated block DELETE builder returns `Block`; no page DELETE or overlaid
  page-update trash field is added for current use.
- Canonical generated request configuration exposes `Idempotency-Key` on page
  creation, and page-read plumbing can preserve the response `ETag`. Canonical
  `If-Match` support may remain generated with page PATCH but has no hand-maintained
  production caller in feature 016.
- Block update maps its canonical `in_trash` field and no generated block write
  builder claims undocumented `If-Match` support.
- Generation emits no unresolved serialization-risk warning for a used model, and
  `Buildout.Core` compiles against the output.
- Representative search unions, supported blocks, parent variants, null values,
  structured errors, and typed lists pass JSON fixture round trips.
- Generated request builders contain no `/v1` route.
- Two runs yield identical effective-contract and generated file hashes.
- Determinism tests use an isolated temporary copy and never dirty the developer's
  working tree.
