namespace Buildout.Core.Markdown.Authoring;

public interface IMarkdownToBlocksParser
{
    AuthoredDocument Parse(string markdown);
}
