namespace Buildout.Core.Markdown.Conversion;

public interface IMarkdownWriter
{
    void WriteLine(string text);
    void WriteBlankLine();
    string ToString();
}
