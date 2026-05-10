using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Buildout.UnitTests.Markdown.Blocks;

public sealed class ChildDatabaseConverterTests
{
    private static (ChildDatabaseConverter sut, IMarkdownWriter writer, IMarkdownRenderContext ctx, IDatabaseViewRenderer renderer)
        CreateSut()
    {
        var writer = Substitute.For<IMarkdownWriter>();
        var ctx = Substitute.For<IMarkdownRenderContext>();
        ctx.Writer.Returns(writer);

        var renderer = Substitute.For<IDatabaseViewRenderer>();
        var sut = new ChildDatabaseConverter(renderer);

        return (sut, writer, ctx, renderer);
    }

    [Fact]
    public void BlockClrType_ReturnsChildDatabaseBlock()
    {
        var (sut, _, _, _) = CreateSut();
        Assert.Equal(typeof(ChildDatabaseBlock), sut.BlockClrType);
    }

    [Fact]
    public void BlockType_ReturnsChildDatabase()
    {
        var (sut, _, _, _) = CreateSut();
        Assert.Equal("child_database", sut.BlockType);
    }

    [Fact]
    public void RecurseChildren_ReturnsFalse()
    {
        var (sut, _, _, _) = CreateSut();
        Assert.False(sut.RecurseChildren);
    }

    [Fact]
    public void Write_Success_WritesHeadingAndTable()
    {
        var (sut, writer, ctx, renderer) = CreateSut();
        const string tableContent = "| Name |\n|---|\n| Row 1 |";
        renderer.RenderInlineAsync("db-id-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult($"## My Database\n\n{tableContent}"));

        var block = new ChildDatabaseBlock { Id = "db-id-1", Title = "My Database" };

        sut.Write(block, [], ctx);

        writer.Received().WriteLine($"## My Database\n\n{tableContent}");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_NotFound_WritesPlaceholderWithTitle()
    {
        var (sut, writer, ctx, renderer) = CreateSut();
        renderer.RenderInlineAsync("db-id-2", Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(404, "not_found", "Not found", null)));

        var block = new ChildDatabaseBlock { Id = "db-id-2", Title = "SomeTitle" };

        sut.Write(block, [], ctx);

        writer.Received().WriteLine("[child database: not found — SomeTitle]");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_Unauthorized_WritesAccessDeniedPlaceholder()
    {
        var (sut, writer, ctx, renderer) = CreateSut();
        renderer.RenderInlineAsync("db-id-3", Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(401, "unauthorized", "Unauthorized", null)));

        var block = new ChildDatabaseBlock { Id = "db-id-3", Title = "SomeTitle" };

        sut.Write(block, [], ctx);

        writer.Received().WriteLine("[child database: access denied — SomeTitle]");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_Forbidden_WritesAccessDeniedPlaceholder()
    {
        var (sut, writer, ctx, renderer) = CreateSut();
        renderer.RenderInlineAsync("db-id-4", Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(403, "forbidden", "Forbidden", null)));

        var block = new ChildDatabaseBlock { Id = "db-id-4", Title = "SomeTitle" };

        sut.Write(block, [], ctx);

        writer.Received().WriteLine("[child database: access denied — SomeTitle]");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_TransportError_WritesTransportPlaceholder()
    {
        var (sut, writer, ctx, renderer) = CreateSut();
        renderer.RenderInlineAsync("db-id-5", Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new TransportError(new HttpRequestException("timeout"))));

        var block = new ChildDatabaseBlock { Id = "db-id-5", Title = "SomeTitle" };

        sut.Write(block, [], ctx);

        writer.Received().WriteLine("[child database: transport error — SomeTitle]");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_GenericBuildinApiException_WritesNotAccessiblePlaceholder()
    {
        var (sut, writer, ctx, renderer) = CreateSut();
        renderer.RenderInlineAsync("db-id-6", Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new UnknownError(500, "internal error")));

        var block = new ChildDatabaseBlock { Id = "db-id-6", Title = "SomeTitle" };

        sut.Write(block, [], ctx);

        writer.Received().WriteLine("[child database: not accessible — SomeTitle]");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_NullTitle_UsesFallback()
    {
        var (sut, writer, ctx, renderer) = CreateSut();
        renderer.RenderInlineAsync("db-id-7", Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(404, "not_found", "Not found", null)));

        var block = new ChildDatabaseBlock { Id = "db-id-7", Title = null };

        sut.Write(block, [], ctx);

        writer.Received().WriteLine("[child database: not found — (unknown)]");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_MalformedBlock_EmptyDatabaseId_WritesMalformedPlaceholder()
    {
        var (sut, writer, ctx, renderer) = CreateSut();

        var block = new ChildDatabaseBlock { Id = "", Title = "SomeTitle" };

        sut.Write(block, [], ctx);

        writer.Received().WriteLine("[child database: malformed]");
        writer.Received().WriteBlankLine();
        renderer.DidNotReceiveWithAnyArgs().RenderInlineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Write_OperationCanceled_Propagates()
    {
        var (sut, _, ctx, renderer) = CreateSut();
        using var cts = new CancellationTokenSource();
        renderer.RenderInlineAsync("db-id-8", Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        var block = new ChildDatabaseBlock { Id = "db-id-8", Title = "SomeTitle" };

        Assert.Throws<OperationCanceledException>(() => sut.Write(block, [], ctx));
    }
}
