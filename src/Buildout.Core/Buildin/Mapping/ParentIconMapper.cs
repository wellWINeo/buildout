using Buildout.Core.Buildin.Models;
using Gen = Buildout.Core.Buildin.Generated.Models;

namespace Buildout.Core.Buildin.Mapping;

internal static class ParentIconMapper
{
    public static Parent? MapParent(Gen.Parent? gen)
    {
        if (gen is null) return null;
        if (gen.ParentDatabaseId is not null)
            return new ParentDatabase(gen.ParentDatabaseId.DatabaseId?.ToString() ?? string.Empty);
        if (gen.ParentPageId is not null)
            return new ParentPage(gen.ParentPageId.PageId?.ToString() ?? string.Empty);
        if (gen.ParentBlockId is not null)
            return new ParentBlock(gen.ParentBlockId.BlockId?.ToString() ?? string.Empty);
        if (gen.ParentSpaceId is not null)
            return new ParentWorkspace(gen.ParentSpaceId.Type);
        return null;
    }

    public static Icon? MapIcon(Gen.Icon? gen)
    {
        if (gen is null) return null;
        if (gen.IconEmoji is not null)
            return new IconEmoji(gen.IconEmoji.Emoji ?? string.Empty);
        if (gen.IconExternal is not null)
            return new IconExternal(gen.IconExternal.External?.Url ?? string.Empty);
        if (gen.IconFile is not null)
            return new IconFile(gen.IconFile.File?.Url ?? string.Empty);
        return null;
    }

    public static Parent? MapSearchResultParent(Gen.V1SearchPageResult.V1SearchPageResult_parent? gen)
    {
        if (gen is null) return null;
        if (gen.ParentDatabaseId is not null)
            return new ParentDatabase(gen.ParentDatabaseId.DatabaseId?.ToString() ?? string.Empty);
        if (gen.ParentPageId is not null)
            return new ParentPage(gen.ParentPageId.PageId?.ToString() ?? string.Empty);
        if (gen.ParentBlockId is not null)
            return new ParentBlock(gen.ParentBlockId.BlockId?.ToString() ?? string.Empty);
        if (gen.ParentSpaceId is not null)
            return new ParentWorkspace(gen.ParentSpaceId.Type);
        return null;
    }
}
