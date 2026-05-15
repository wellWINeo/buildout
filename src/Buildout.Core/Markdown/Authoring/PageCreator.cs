using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Diagnostics;
using Buildout.Core.Markdown.Authoring.Properties;
using Microsoft.Extensions.Logging;

namespace Buildout.Core.Markdown.Authoring;

public sealed class PageCreator : IPageCreator
{
    private readonly ParentKindProbe _probe;
    private readonly IMarkdownToBlocksParser _parser;
    private readonly IBuildinClient _client;
    private readonly IDatabasePropertyValueParser _propertyParser;
    private readonly ILogger<PageCreator> _logger;

    public PageCreator(ParentKindProbe probe, IMarkdownToBlocksParser parser, IBuildinClient client,
        IDatabasePropertyValueParser propertyParser, ILogger<PageCreator> logger)
    {
        _probe = probe;
        _parser = parser;
        _client = client;
        _propertyParser = propertyParser;
        _logger = logger;
    }

    [SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Dynamic operation names prevent compile-time LoggerMessage definitions")]
    public async Task<CreatePageOutcome> CreateAsync(CreatePageInput input, CancellationToken cancellationToken = default)
    {
        using var recorder = OperationRecorder.Start(_logger, "page_create");

        ParentKind parentKind;
        try
        {
            parentKind = await _probe.ProbeAsync(input.ParentId, cancellationToken);
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
        {
            recorder.Fail("auth");
            return new CreatePageOutcome { NewPageId = string.Empty, FailureClass = FailureClass.Auth, UnderlyingException = ex };
        }
        catch (BuildinApiException ex) when (ex.Error is TransportError)
        {
            recorder.Fail("transport");
            return new CreatePageOutcome { NewPageId = string.Empty, FailureClass = FailureClass.Transport, UnderlyingException = ex };
        }
        catch (BuildinApiException ex)
        {
            recorder.Fail("unknown");
            return new CreatePageOutcome { NewPageId = string.Empty, FailureClass = FailureClass.Unexpected, UnderlyingException = ex };
        }

        var parentKindStr = parentKind switch
        {
            ParentKind.Page => "page",
            ParentKind.DatabaseParent => "database",
            ParentKind.NotFound => "not_found",
            _ => "unknown"
        };
        recorder.SetTag("parent_kind", parentKindStr);

        var document = _parser.Parse(input.Markdown);
        var blockCount = document.Body.Count;
        recorder.SetTag("block_count", blockCount);

        var title = input.Title ?? document.Title;

        var validationOutcome = ValidateRequest(input, parentKind, title);
        if (validationOutcome is not null)
        {
            recorder.Succeed();
            return validationOutcome;
        }

        Dictionary<string, PropertyValue> properties;
        try
        {
            properties = BuildProperties(parentKind, title, input);
        }
        catch (ArgumentException ex)
        {
            recorder.Succeed();
            return new CreatePageOutcome { NewPageId = string.Empty, FailureClass = FailureClass.Validation, UnderlyingException = ex };
        }

        var allBatches = AppendBatcher.BatchTopLevel(document.Body);
        var firstBatch = allBatches.Count > 0 ? allBatches[0] : null;

        var parent = parentKind switch
        {
            ParentKind.Page p => (Parent)new ParentPage(p.PageId),
            ParentKind.DatabaseParent db => new ParentDatabase(db.Schema.Id),
            _ => throw new InvalidOperationException("Unexpected parent kind.")
        };

        var request = new CreatePageRequest
        {
            Parent = parent,
            Properties = properties,
            Children = (IReadOnlyList<Block>?)firstBatch ?? []
        };

        Page page;
        try
        {
            page = await _client.CreatePageAsync(request, cancellationToken);
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 400 })
        {
            recorder.Fail("validation");
            return new CreatePageOutcome { NewPageId = string.Empty, FailureClass = FailureClass.Validation, UnderlyingException = ex };
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
        {
            recorder.Fail("auth");
            return new CreatePageOutcome { NewPageId = string.Empty, FailureClass = FailureClass.Auth, UnderlyingException = ex };
        }
        catch (BuildinApiException ex) when (ex.Error is TransportError)
        {
            recorder.Fail("transport");
            return new CreatePageOutcome { NewPageId = string.Empty, FailureClass = FailureClass.Transport, UnderlyingException = ex };
        }
        catch (BuildinApiException ex)
        {
            recorder.Fail("unknown");
            return new CreatePageOutcome { NewPageId = string.Empty, FailureClass = FailureClass.Unexpected, UnderlyingException = ex };
        }

        var partialOutcome = await AppendBlocksAsync(page, document, allBatches, cancellationToken);
        if (partialOutcome is not null)
        {
            recorder.Fail("partial");
            return partialOutcome;
        }

        BuildoutMeter.PagesCreatedTotal.Add(1, new TagList { { "parent_kind", parentKindStr } });
        BuildoutMeter.BlocksProcessedTotal.Add(blockCount, new TagList { { "operation", "page_create" } });
        recorder.Succeed();
        return new CreatePageOutcome { NewPageId = page.Id, ResolvedTitle = title };
    }

    private static CreatePageOutcome? ValidateRequest(CreatePageInput input, ParentKind parentKind, string? title)
    {
        if (parentKind is ParentKind.NotFound)
            return new CreatePageOutcome
            {
                NewPageId = string.Empty,
                FailureClass = FailureClass.NotFound,
                UnderlyingException = new InvalidOperationException($"Parent '{input.ParentId}' is neither a page nor a database.")
            };

        if (input.Icon != null || input.CoverUrl != null)
            return new CreatePageOutcome
            {
                NewPageId = string.Empty,
                FailureClass = FailureClass.Validation,
                UnderlyingException = new ArgumentException("--icon and --cover are not supported in v1; omit them or use the Buildin web UI.")
            };

        // FR-005: title is required
        if (string.IsNullOrWhiteSpace(title))
            return new CreatePageOutcome
            {
                NewPageId = string.Empty,
                FailureClass = FailureClass.Validation,
                UnderlyingException = new ArgumentException("Cannot determine the new page's title: no leading '# Title' heading found and --title was not provided.")
            };

        if (parentKind is ParentKind.Page && input.Properties?.Count > 0)
            return new CreatePageOutcome
            {
                NewPageId = string.Empty,
                FailureClass = FailureClass.Validation,
                UnderlyingException = new ArgumentException($"--property is only valid when the parent is a database; '{input.ParentId}' resolved to a page.")
            };

        return null;
    }

    private async Task<CreatePageOutcome?> AppendBlocksAsync(Page page, AuthoredDocument document, IReadOnlyList<IReadOnlyList<Block>> allBatches, CancellationToken cancellationToken)
    {
        var firstBatch = allBatches.Count > 0 ? allBatches[0] : null;
        var remainingBatches = allBatches.Skip(1).ToArray();
        var idMap = new Dictionary<BlockSubtreeWrite, string>();

        var firstChunkSubtrees = document.Body.Take(firstBatch?.Count ?? 0).ToList();
        if (firstBatch is { Count: > 0 } && firstChunkSubtrees.Any(s => s.Children.Count > 0))
        {
            var resp = await _client.GetBlockChildrenAsync(page.Id, null, cancellationToken);
            for (int i = 0; i < firstChunkSubtrees.Count && i < resp.Results.Count; i++)
                idMap[firstChunkSubtrees[i]] = resp.Results[i].Id;
        }

        try
        {
            int offset = firstBatch?.Count ?? 0;
            foreach (var batch in remainingBatches)
            {
                var batchSubtrees = document.Body.Skip(offset).Take(batch.Count).ToList();
                var appendResult = await _client.AppendBlockChildrenAsync(page.Id,
                    new AppendBlockChildrenRequest { Children = batch }, cancellationToken);
                for (int i = 0; i < batchSubtrees.Count && i < appendResult.Results.Count; i++)
                    idMap[batchSubtrees[i]] = appendResult.Results[i].Id;
                offset += batch.Count;
            }

            foreach (var subtree in document.Body.Where(s => s.Children.Count > 0))
            {
                if (!idMap.TryGetValue(subtree, out var blockId))
                    continue;
                await AppendNestedRecursive(blockId, subtree.Children, idMap, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is BuildinApiException or OperationCanceledException)
        {
            return new CreatePageOutcome
            {
                NewPageId = page.Id,
                PartialPageId = page.Id,
                FailureClass = FailureClass.Partial,
                UnderlyingException = ex
            };
        }

        return null;
    }

    private async Task AppendNestedRecursive(string parentBlockId, IReadOnlyList<BlockSubtreeWrite> children, Dictionary<BlockSubtreeWrite, string> idMap, CancellationToken cancellationToken)
    {
        for (int i = 0; i < children.Count; i += 100)
        {
            var chunk = children.Skip(i).Take(100).ToList();
            var result = await _client.AppendBlockChildrenAsync(parentBlockId,
                new AppendBlockChildrenRequest { Children = chunk.Select(s => s.Block).ToArray() },
                cancellationToken);
            for (int j = 0; j < chunk.Count && j < result.Results.Count; j++)
                idMap[chunk[j]] = result.Results[j].Id;
        }

        foreach (var child in children.Where(c => c.Children.Count > 0))
        {
            if (!idMap.TryGetValue(child, out var childId))
                continue;
            await AppendNestedRecursive(childId, child.Children, idMap, cancellationToken);
        }
    }

    private Dictionary<string, PropertyValue> BuildProperties(ParentKind parentKind, string? title, CreatePageInput input)
    {
        var properties = new Dictionary<string, PropertyValue>();

        if (parentKind is ParentKind.DatabaseParent db)
        {
            if (db.Schema.Properties is not null)
            {
                foreach (var (name, schema) in db.Schema.Properties)
                {
                    if (schema is TitlePropertySchema)
                    {
                        properties[name] = new TitlePropertyValue
                        {
                            Title = string.IsNullOrEmpty(title) ? [] : [new RichText { Type = "text", Content = title }]
                        };
                    }
                }
            }

            if (input.Properties is not null)
            {
                foreach (var (key, value) in input.Properties)
                {
                    if (db.Schema.Properties is not null && db.Schema.Properties.TryGetValue(key, out var schema))
                    {
                        properties[key] = _propertyParser.Parse(key, value, schema);
                    }
                    else
                    {
                        throw new ArgumentException($"Property '{key}' does not exist in database '{db.Schema.Id}'.");
                    }
                }
            }
        }
        else
        {
            properties["title"] = new TitlePropertyValue
            {
                Title = string.IsNullOrEmpty(title) ? [] : [new RichText { Type = "text", Content = title }]
            };
        }

        return properties;
    }
}
