#!/usr/bin/env dotnet
// Deterministically applies the reviewed Buildout overlay without modifying the
// canonical snapshot. This file is intentionally dependency-free for offline CI.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

var repo = Directory.GetCurrentDirectory();
var input = args.Length > 0 ? args[0] : Path.Combine(repo, "openapi.json");
var overlayPath = args.Length > 1 ? args[1] : Path.Combine(repo, "scripts", "openapi.buildout-overlay.json");
var output = args.Length > 2 ? args[2] : Path.Combine(repo, "obj", "Buildin", "OpenApi", "openapi.effective.json");

using var source = JsonDocument.Parse(File.ReadAllText(input));
using var overlay = JsonDocument.Parse(File.ReadAllText(overlayPath));
var root = JsonNode.Parse(source.RootElement.GetRawText())?.AsObject() ?? throw new InvalidDataException("OpenAPI root is not an object.");
var info = root["info"]?.AsObject() ?? throw new InvalidDataException("OpenAPI info is missing.");
if (!string.Equals(info["title"]?.GetValue<string>(), "Buildin Developer API V2", StringComparison.Ordinal) ||
    !string.Equals(info["version"]?.GetValue<string>(), "2.0.0", StringComparison.Ordinal))
    throw new InvalidDataException("Expected Buildin Developer API V2 2.0.0 snapshot.");

var paths = root["paths"]?.AsObject() ?? throw new InvalidDataException("OpenAPI paths are missing.");
foreach (var path in paths)
    if (path.Key.Contains("/v1/", StringComparison.Ordinal) || path.Key.StartsWith("/v1", StringComparison.Ordinal))
        throw new InvalidDataException($"V1 route found: {path.Key}");

var expectedHash = overlay.RootElement.GetProperty("info").GetProperty("upstreamSha256").GetString();
var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))).ToLowerInvariant();
if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
    throw new InvalidDataException($"Canonical snapshot hash mismatch. Expected {expectedHash}, got {actualHash}.");

var target = "/v2/blocks/{block_id}";
if (!paths.TryGetPropertyValue(target, out var blockPath) || blockPath is not JsonObject blockObject)
    throw new InvalidDataException($"Overlay target does not exist: {target}");
if (blockObject.ContainsKey("delete"))
    throw new InvalidDataException("Overlay collision: block DELETE already exists upstream.");
blockObject["delete"] = JsonNode.Parse(overlay.RootElement.GetProperty("actions")[0].GetProperty("update").GetProperty("delete").GetRawText());

Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
File.WriteAllText(output, json, new UTF8Encoding(false));
