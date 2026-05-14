using System.Globalization;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Authoring;

public sealed class PageCreator : IPageCreator
{
    private readonly ParentKindProbe _probe;
    private readonly IMarkdownToBlocksParser _parser;
    private readonly IBuildinClient _client;

    public PageCreator(ParentKindProbe probe, IMarkdownToBlocksParser parser, IBuildinClient client)
    {
        _probe = probe;
        _parser = parser;
        _client = client;
    }

    public async Task<CreatePageOutcome> CreateAsync(CreatePageInput input, CancellationToken cancellationToken = default)
    {
        var parentKind = await _probe.ProbeAsync(input.ParentId, cancellationToken);

        if (parentKind is ParentKind.NotFound)
            return new CreatePageOutcome
            {
                NewPageId = string.Empty,
                FailureClass = FailureClass.NotFound,
                UnderlyingException = new InvalidOperationException($"Parent '{input.ParentId}' is neither a page nor a database.")
            };

        var document = _parser.Parse(input.Markdown);

        var title = input.Title ?? document.Title;

        var properties = BuildProperties(parentKind, title, input);

        var allBatches = AppendBatcher.BatchTopLevel(document.Body);
        var firstBatch = allBatches.Count > 0 ? allBatches[0] : null;
        var remainingBatches = allBatches.Skip(1).ToArray();

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
            return new CreatePageOutcome
            {
                NewPageId = string.Empty,
                FailureClass = FailureClass.Validation,
                UnderlyingException = ex
            };
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
        {
            return new CreatePageOutcome
            {
                NewPageId = string.Empty,
                FailureClass = FailureClass.Auth,
                UnderlyingException = ex
            };
        }
        catch (BuildinApiException ex) when (ex.Error is TransportError)
        {
            return new CreatePageOutcome
            {
                NewPageId = string.Empty,
                FailureClass = FailureClass.Transport,
                UnderlyingException = ex
            };
        }
        catch (BuildinApiException ex)
        {
            return new CreatePageOutcome
            {
                NewPageId = string.Empty,
                FailureClass = FailureClass.Unexpected,
                UnderlyingException = ex
            };
        }

        var itemsWithChildren = AppendBatcher.GetItemsWithChildren(document.Body);

        var totalAppendBatches = remainingBatches.Length + itemsWithChildren.Count;
        var batchesAppended = 0;

        try
        {
            foreach (var batch in remainingBatches)
            {
                await _client.AppendBlockChildrenAsync(page.Id, new AppendBlockChildrenRequest { Children = batch }, cancellationToken);
                batchesAppended++;
            }

            foreach (var subtree in itemsWithChildren)
            {
                await AppendNestedRecursive(page.Id, subtree, cancellationToken);
                batchesAppended++;
            }
        }
        catch (Exception ex) when (ex is BuildinApiException or OperationCanceledException)
        {
            throw new PartialCreationException(page.Id, batchesAppended, totalAppendBatches, ex);
        }

        return new CreatePageOutcome { NewPageId = page.Id, ResolvedTitle = title };
    }

    private async Task AppendNestedRecursive(string parentId, BlockSubtreeWrite subtree, CancellationToken cancellationToken)
    {
        if (subtree.Children.Count == 0) return;

        var blockId = subtree.Block.Id;

        if (string.IsNullOrEmpty(blockId))
        {
            var result = await _client.AppendBlockChildrenAsync(parentId,
                new AppendBlockChildrenRequest { Children = [subtree.Block] }, cancellationToken);
            blockId = result.Results.Count > 0 ? result.Results[0].Id : subtree.Block.Id;
        }

        var childBatches = AppendBatcher.BatchTopLevel(subtree.Children);
        foreach (var batch in childBatches)
        {
            await _client.AppendBlockChildrenAsync(blockId, new AppendBlockChildrenRequest { Children = batch }, cancellationToken);
        }

        var nested = AppendBatcher.GetItemsWithChildren(subtree.Children);
        foreach (var child in nested)
        {
            await AppendNestedRecursive(blockId, child, cancellationToken);
        }
    }

    private static Dictionary<string, PropertyValue> BuildProperties(ParentKind parentKind, string? title, CreatePageInput input)
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
                        properties[key] = MapPropertyValue(schema, value);
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

    private static PropertyValue MapPropertyValue(PropertySchema schema, string value)
    {
        return schema switch
        {
            RichTextPropertySchema => new RichTextPropertyValue
            {
                RichText = [new RichText { Type = "text", Content = value }]
            },
            NumberPropertySchema => new NumberPropertyValue { Number = double.Parse(value, CultureInfo.InvariantCulture) },
            UrlPropertySchema => new UrlPropertyValue { Url = value },
            EmailPropertySchema => new UrlPropertyValue { Url = value },
            CheckboxPropertySchema => new CheckboxPropertyValue { Checkbox = bool.Parse(value) },
            SelectPropertySchema => new SelectPropertyValue
            {
                Select = new SelectOption { Id = value, Name = value }
            },
            MultiSelectPropertySchema => new MultiSelectPropertyValue
            {
                MultiSelect = value.Split(',').Select(s => s.Trim()).Select(s => new SelectOption { Id = s, Name = s }).ToArray()
            },
            DatePropertySchema => new DatePropertyValue
            {
                Date = new DateRange { Start = value }
            },
            TitlePropertySchema => new TitlePropertyValue
            {
                Title = [new RichText { Type = "text", Content = value }]
            },
            _ => new RichTextPropertyValue
            {
                RichText = [new RichText { Type = "text", Content = value }]
            }
        };
    }
}
