using Buildout.Core.Buildin.Models;
using Gen = Buildout.Core.Buildin.Generated.Models;

namespace Buildout.Core.Buildin.Mapping;

internal static class SearchMapper
{
    public static SearchResults Map(Gen.SearchResult? gen)
    {
        if (gen is null) return new SearchResults();
        var results = new List<object>();
        if (gen.Data is not null) results.AddRange(gen.Data.Cast<object>());
        return new SearchResults { Results = results };
    }

    public static PageSearchResults Map(Gen.V1SearchResponse? gen)
    {
        if (gen is null) return new PageSearchResults();
        var pages = new List<Page>();
        if (gen.Results is not null)
        {
            foreach (var r in gen.Results)
            {
                pages.Add(new Page
                {
                    Id = r.Id?.ToString() ?? string.Empty,
                    CreatedAt = r.CreatedTime,
                    LastEditedAt = r.LastEditedTime,
                    Archived = r.Archived ?? false,
                    Title = r.Properties?.Title?.Title?.Select(RichTextMapper.Map).ToList(),
                    Parent = ParentIconMapper.MapSearchResultParent(r.Parent),
                    ObjectType = r.Object
                });
            }
        }
        return new PageSearchResults
        {
            Results = pages,
            HasMore = gen.HasMore ?? false,
            NextCursor = gen.NextCursor
        };
    }
}
