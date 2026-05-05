using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Conversion;

public sealed class BlockToMarkdownRegistry
{
    private readonly IReadOnlyDictionary<Type, IBlockToMarkdownConverter> _byClrType;

    public BlockToMarkdownRegistry(IEnumerable<IBlockToMarkdownConverter> converters)
    {
        var dict = new Dictionary<Type, IBlockToMarkdownConverter>();
        foreach (var c in converters)
        {
            if (!dict.TryAdd(c.BlockClrType, c))
                throw new InvalidOperationException(
                    $"Duplicate {nameof(IBlockToMarkdownConverter.BlockClrType)} registration: {c.BlockClrType}");
        }
        _byClrType = dict;
    }

    public IBlockToMarkdownConverter? Resolve(Block block)
        => _byClrType.GetValueOrDefault(block.GetType());
}
