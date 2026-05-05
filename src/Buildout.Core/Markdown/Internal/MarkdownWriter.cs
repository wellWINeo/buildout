using System.Text;
using Buildout.Core.Markdown.Conversion;

namespace Buildout.Core.Markdown.Internal;

internal sealed class MarkdownWriter : IMarkdownWriter
{
    private readonly StringBuilder _sb = new();
    private bool _lastWasBlank;

    public void WriteLine(string text)
    {
        _sb.AppendLine(text);
        _lastWasBlank = false;
    }

    public void WriteBlankLine()
    {
        if (!_lastWasBlank)
        {
            _sb.AppendLine();
            _lastWasBlank = true;
        }
    }

    public override string ToString() => _sb.ToString();
}
