using Buildout.Core.Buildin.Errors;
using Buildout.Core.PageTree;
using Buildout.Core.PageTree.Errors;
using Buildout.Core.PageTree.Rendering;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class TreeCommand : AsyncCommand<TreeSettings>
{
    private readonly IPageTreeService _service;
    private readonly IReadOnlyDictionary<TreeFormat, ITreeRenderer> _renderers;

    public TreeCommand(
        IPageTreeService service,
        IReadOnlyDictionary<TreeFormat, ITreeRenderer> renderers)
    {
        _service = service;
        _renderers = renderers;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, TreeSettings settings, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TreeFormat>(settings.Format, ignoreCase: true, out var format))
        {
            await Console.Error.WriteLineAsync($"format must be 'ascii' or 'json'; got '{settings.Format}'");
            return 2;
        }

        try
        {
            TreeDepth.Validate(settings.Depth);
        }
        catch (TreeDepthOutOfRangeException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return 2;
        }

        TreeNode root;
        try
        {
            root = await _service.BuildAsync(settings.PageId, settings.Depth, cancellationToken);
        }
        catch (TreeRootNotFoundException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return 3;
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return 4;
        }
        catch (BuildinApiException ex) when (ex.Error is TransportError)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return 5;
        }
        catch (BuildinApiException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return 6;
        }
        catch (TreeCycleDetectedException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return 7;
        }

        var renderer = _renderers[format];
        var output = renderer.Render(root);
        await Console.Out.WriteAsync(output);
        return 0;
    }
}
