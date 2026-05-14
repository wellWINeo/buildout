using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;

namespace Buildout.Core.Markdown.Authoring.Blocks;

public sealed class ToDoBlockParser : IMarkdownBlockParser
{
    public bool CanParse(Markdig.Syntax.Block block)
    {
        if (block is not ListItemBlock item) return false;
        if (item.Parent is not ListBlock { IsOrdered: false }) return false;
        return TryGetTaskList(item) is not null;
    }

    public BlockSubtreeWrite Parse(Markdig.Syntax.Block block, IInlineMarkdownParser inlineParser)
    {
        var item = (ListItemBlock)block;
        var richTexts = new List<RichText>();

        foreach (var child in item)
        {
            if (child is Markdig.Syntax.ParagraphBlock para && para.Inline is not null)
            {
                richTexts.AddRange(inlineParser.ParseInlines(para.Inline));
            }
        }

        // Markdig's task-list extension leaves a leading space in the first LiteralInline
        // (the separator between the checkbox marker and the task text). Strip it so that
        // "- [ ] Task" round-trips as "- [ ] Task" rather than "- [ ]  Task".
        if (richTexts.Count > 0 && richTexts[0].Content.StartsWith(' '))
            richTexts[0] = richTexts[0] with { Content = richTexts[0].Content.TrimStart() };

        var taskList = TryGetTaskList(item);
        return new BlockSubtreeWrite
        {
            Block = new ToDoBlock { RichTextContent = richTexts, Checked = taskList?.Checked == true },
            Children = []
        };
    }

    private static TaskList? TryGetTaskList(ListItemBlock item)
    {
        foreach (var child in item)
        {
            if (child is Markdig.Syntax.ParagraphBlock { Inline: not null } para)
            {
                foreach (var inline in para.Inline)
                {
                    if (inline is TaskList taskList)
                        return taskList;
                }
            }
        }
        return null;
    }
}
