using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Conversion;

public sealed class MentionToMarkdownRegistry
{
    private readonly IReadOnlyDictionary<Type, IMentionToMarkdownConverter> _byClrType;

    public MentionToMarkdownRegistry(IEnumerable<IMentionToMarkdownConverter> converters)
    {
        var dict = new Dictionary<Type, IMentionToMarkdownConverter>();
        foreach (var c in converters)
        {
            if (!dict.TryAdd(c.MentionClrType, c))
                throw new InvalidOperationException(
                    $"Duplicate {nameof(IMentionToMarkdownConverter.MentionClrType)} registration: {c.MentionClrType}");
        }
        _byClrType = dict;
    }

    public IMentionToMarkdownConverter? Resolve(Mention mention)
        => _byClrType.GetValueOrDefault(mention.GetType());
}
