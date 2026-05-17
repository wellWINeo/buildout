using System.Text.Json.Serialization;

namespace Buildout.Core.Markdown.Editing.PatchOperations;

[JsonConverter(typeof(PatchOperationJsonConverter))]
public abstract record PatchOperation;
