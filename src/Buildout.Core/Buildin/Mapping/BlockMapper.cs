using Buildout.Core.Buildin.Models;
using Gen = Buildout.Core.Buildin.Generated.Models;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;

namespace Buildout.Core.Buildin.Mapping;

internal static class BlockMapper
{
    public static Block Map(Gen.Block gen)
    {
        var data = gen.Data;
        var richText = data?.RichText?.Select(RichTextMapper.Map).ToList();

        var typeStr = MappingHelpers.GetEnumValue(gen.Type);

        Block block = typeStr switch
        {
            "paragraph" => new ParagraphBlock { RichTextContent = richText },
            "heading_1" => new Heading1Block { RichTextContent = richText },
            "heading_2" => new Heading2Block { RichTextContent = richText },
            "heading_3" => new Heading3Block { RichTextContent = richText },
            "bulleted_list_item" => new BulletedListItemBlock { RichTextContent = richText },
            "numbered_list_item" => new NumberedListItemBlock { RichTextContent = richText },
            "to_do" => new ToDoBlock { RichTextContent = richText, Checked = data?.Checked },
            "toggle" => new ToggleBlock { RichTextContent = richText },
            "code" => new CodeBlock { RichTextContent = richText, Language = data?.Language },
            "quote" => new QuoteBlock { RichTextContent = richText },
            "divider" => new DividerBlock(),
            "image" => new ImageBlock { Url = data?.Url, Caption = data?.Caption?.Select(RichTextMapper.Map).ToList() },
            "embed" => new EmbedBlock { Url = data?.Url },
            "table" => new TableBlock { TableWidth = data?.TableWidth, HasColumnHeader = data?.HasColumnHeader, HasRowHeader = data?.HasRowHeader },
            "table_row" => new TableRowBlock(),
            "column_list" => new ColumnListBlock(),
            "column" => new ColumnBlock(),
            "child_page" => new ChildPageBlock { Title = data?.Title },
            "child_database" => new ChildDatabaseBlock { Title = data?.Title },
            "synced_block" => new SyncedBlock { SyncedFromId = data?.SyncedFrom?.BlockId?.ToString() },
            "link_preview" => new LinkPreviewBlock { Url = data?.Url },
            _ => new UnsupportedBlock()
        };

        return block with
        {
            Id = gen.Id?.ToString() ?? string.Empty,
            CreatedAt = gen.CreatedTime,
            LastEditedAt = gen.LastEditedTime,
            HasChildren = gen.HasChildren ?? false,
            Parent = ParentIconMapper.MapParent(gen.Parent)
        };
    }

    public static Gen.AppendBlockChildrenRequest_children MapToAppendChild(Block block)
    {
        var (type, data) = block switch
        {
            ParagraphBlock b        => (Gen.AppendBlockChildrenRequest_children_type.Paragraph,         MakeData(b.RichTextContent)),
            Heading1Block b         => (Gen.AppendBlockChildrenRequest_children_type.Heading_1,         MakeData(b.RichTextContent)),
            Heading2Block b         => (Gen.AppendBlockChildrenRequest_children_type.Heading_2,         MakeData(b.RichTextContent)),
            Heading3Block b         => (Gen.AppendBlockChildrenRequest_children_type.Heading_3,         MakeData(b.RichTextContent)),
            BulletedListItemBlock b => (Gen.AppendBlockChildrenRequest_children_type.Bulleted_list_item, MakeData(b.RichTextContent)),
            NumberedListItemBlock b => (Gen.AppendBlockChildrenRequest_children_type.Numbered_list_item, MakeData(b.RichTextContent)),
            ToDoBlock b             => (Gen.AppendBlockChildrenRequest_children_type.To_do,             MakeData(b.RichTextContent, @checked: b.Checked)),
            QuoteBlock b            => (Gen.AppendBlockChildrenRequest_children_type.Quote,             MakeData(b.RichTextContent)),
            ToggleBlock b           => (Gen.AppendBlockChildrenRequest_children_type.Toggle,            MakeData(b.RichTextContent)),
            CodeBlock b             => (Gen.AppendBlockChildrenRequest_children_type.Code,              MakeData(b.RichTextContent, language: b.Language)),
            DividerBlock            => (Gen.AppendBlockChildrenRequest_children_type.Divider,           new Gen.BlockData()),
            ImageBlock b            => (Gen.AppendBlockChildrenRequest_children_type.Image,             new Gen.BlockData { Url = b.Url }),
            EmbedBlock b            => (Gen.AppendBlockChildrenRequest_children_type.Embed,             new Gen.BlockData { Url = b.Url }),
            _ => throw new ArgumentException($"Block type {block.GetType().Name} cannot be appended")
        };
        return new Gen.AppendBlockChildrenRequest_children { Type = type, Data = data };
    }

    public static Gen.UpdateBlockRequest MapToUpdateRequest(UpdateBlockRequest request)
    {
        return new Gen.UpdateBlockRequest
        {
            Type = MapUpdateType(request.Type),
            Data = new Gen.BlockData
            {
                RichText = request.RichTextContent?.Select(RichTextMapper.MapToGen).ToList(),
                Checked = request.Checked,
                Language = request.Language,
                Url = request.Url
            },
            Archived = request.Archived
        };
    }

    private static Gen.UpdateBlockRequest_type MapUpdateType(string type) => type switch
    {
        "paragraph"          => Gen.UpdateBlockRequest_type.Paragraph,
        "heading_1"          => Gen.UpdateBlockRequest_type.Heading_1,
        "heading_2"          => Gen.UpdateBlockRequest_type.Heading_2,
        "heading_3"          => Gen.UpdateBlockRequest_type.Heading_3,
        "bulleted_list_item" => Gen.UpdateBlockRequest_type.Bulleted_list_item,
        "numbered_list_item" => Gen.UpdateBlockRequest_type.Numbered_list_item,
        "to_do"              => Gen.UpdateBlockRequest_type.To_do,
        "quote"              => Gen.UpdateBlockRequest_type.Quote,
        "toggle"             => Gen.UpdateBlockRequest_type.Toggle,
        "code"               => Gen.UpdateBlockRequest_type.Code,
        "divider"            => Gen.UpdateBlockRequest_type.Divider,
        "image"              => Gen.UpdateBlockRequest_type.Image,
        "embed"              => Gen.UpdateBlockRequest_type.Embed,
        _ => throw new ArgumentException($"Unknown block type for update: {type}")
    };

    private static Gen.BlockData MakeData(
        IReadOnlyList<RichText>? richText,
        bool? @checked = null,
        string? language = null)
    {
        return new Gen.BlockData
        {
            RichText = richText?.Select(RichTextMapper.MapToGen).ToList(),
            Checked = @checked,
            Language = language
        };
    }

    public static PaginatedList<Block> MapChildrenResponse(Gen.GetBlockChildrenResponse? gen)
    {
        if (gen is null) return new PaginatedList<Block>();

        var blocks = new List<Block>();
        if (gen.Results is UntypedArray array)
        {
            foreach (var item in array.GetValue())
            {
                if (item is null) continue;

                var element = MappingHelpers.SerializeToElement(item);
                var node = new JsonParseNode(element);
                var genBlock = node.GetObjectValue(Gen.Block.CreateFromDiscriminatorValue);
                if (genBlock is not null)
                    blocks.Add(Map(genBlock));
            }
        }

        return new PaginatedList<Block>
        {
            Results = blocks,
            HasMore = gen.HasMore ?? false,
            NextCursor = gen.NextCursor
        };
    }
}
