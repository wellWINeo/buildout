using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Markdown.Conversion;

public interface IBlockToMarkdownConverter
{
    Type BlockClrType { get; }
    string BlockType { get; }
    bool RecurseChildren { get; }
    void Write(Block block, IReadOnlyList<BlockSubtree> children, IMarkdownRenderContext ctx);
}
