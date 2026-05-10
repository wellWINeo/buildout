using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews;
using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Markdown.Conversion.Blocks;

internal sealed class ChildDatabaseConverter : IBlockToMarkdownConverter
{
    private readonly IDatabaseViewRenderer _renderer;

    public ChildDatabaseConverter(IDatabaseViewRenderer renderer)
    {
        _renderer = renderer;
    }

    public Type BlockClrType => typeof(ChildDatabaseBlock);
    public string BlockType => "child_database";
    public bool RecurseChildren => false;

    public void Write(Block block, IReadOnlyList<BlockSubtree> children, IMarkdownRenderContext ctx)
    {
        var db = (ChildDatabaseBlock)block;

        if (string.IsNullOrEmpty(db.DatabaseId))
        {
            ctx.Writer.WriteLine("[child database: malformed]");
            ctx.Writer.WriteBlankLine();
            return;
        }

        var title = db.Title ?? "(unknown)";

        try
        {
            var rendered = _renderer.RenderInlineAsync(db.DatabaseId, CancellationToken.None).GetAwaiter().GetResult();
            ctx.Writer.WriteLine(rendered);
            ctx.Writer.WriteBlankLine();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
        {
            ctx.Writer.WriteLine($"[child database: not found — {title}]");
            ctx.Writer.WriteBlankLine();
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
        {
            ctx.Writer.WriteLine($"[child database: access denied — {title}]");
            ctx.Writer.WriteBlankLine();
        }
        catch (BuildinApiException ex) when (ex.Error is TransportError)
        {
            ctx.Writer.WriteLine($"[child database: transport error — {title}]");
            ctx.Writer.WriteBlankLine();
        }
        catch (BuildinApiException)
        {
            ctx.Writer.WriteLine($"[child database: not accessible — {title}]");
            ctx.Writer.WriteBlankLine();
        }
    }
}
