using System.Text.Json;
using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Markdown.Editing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Buildout.IntegrationTests.Cli;

public sealed class UpdateCommandTests
{
    private const string PageId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string Revision = "abcd1234";

    private static readonly ReconciliationSummary DefaultSummary = new()
    {
        PreservedBlocks = 5,
        UpdatedBlocks = 2,
        NewBlocks = 1,
        DeletedBlocks = 0,
        AmbiguousMatches = 0,
        NewRevision = "ef567890",
    };

    private static string WriteOpsFile(string json)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);
        return path;
    }

    private static string ValidOpsJson() =>
        """[{"op":"search_replace","old_str":"Hello","new_str":"World"}]""";

    private static (CommandApp app, TestConsole console) CreateApp(IPageEditor pageEditor)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(pageEditor);

        var testConsole = new TestConsole();
        services.AddSingleton<Spectre.Console.IAnsiConsole>(testConsole);

        var caps = new TerminalCapabilities(isAnsi: false, isOutputRedirected: true, noColorEnv: null);
        services.AddSingleton(caps);
        services.AddSingleton<MarkdownTerminalRenderer>();

        var pinnedTypes = new HashSet<Type> { typeof(Spectre.Console.IAnsiConsole) };
        var registrar = new TypeRegistrar(services, pinnedTypes);

        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.AddCommand<UpdateCommand>("update");
        });

        return (app, testConsole);
    }

    [Fact]
    public async Task PrintSummary_SuccessfulUpdate_WritesReconciliationLine()
    {
        var editor = Substitute.For<IPageEditor>();
        editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
              .Returns(DefaultSummary);

        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalOut = Console.Out;
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile]);

                Assert.Equal(0, exitCode);
                var output = outWriter.ToString();
                Assert.Contains($"Reconciled page {PageId}", output);
                Assert.Contains("5 preserved", output);
                Assert.Contains("2 updated", output);
                Assert.Contains("1 new", output);
                Assert.Contains("0 deleted", output);
                Assert.Contains($"Revision: {DefaultSummary.NewRevision}", output);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task PrintSummary_DryRun_AddsDryRunPrefix()
    {
        var editor = Substitute.For<IPageEditor>();
        editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
              .Returns(DefaultSummary);

        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalOut = Console.Out;
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile, "--dry-run"]);

                Assert.Equal(0, exitCode);
                Assert.StartsWith("[dry-run] Reconciled page", outWriter.ToString().TrimStart());
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task PrintJson_SuccessfulUpdate_WritesJsonBody()
    {
        var editor = Substitute.For<IPageEditor>();
        editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
              .Returns(DefaultSummary);

        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalOut = Console.Out;
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile, "--print", "json"]);

                Assert.Equal(0, exitCode);
                var doc = JsonDocument.Parse(outWriter.ToString());
                Assert.True(doc.RootElement.TryGetProperty("preservedBlocks", out var preserved));
                Assert.Equal(5, preserved.GetInt32());
                Assert.True(doc.RootElement.TryGetProperty("updatedBlocks", out var updated));
                Assert.Equal(2, updated.GetInt32());
                Assert.True(doc.RootElement.TryGetProperty("newBlocks", out var newBlocks));
                Assert.Equal(1, newBlocks.GetInt32());
                Assert.True(doc.RootElement.TryGetProperty("deletedBlocks", out var deleted));
                Assert.Equal(0, deleted.GetInt32());
                Assert.True(doc.RootElement.TryGetProperty("newRevision", out var rev));
                Assert.Equal(DefaultSummary.NewRevision, rev.GetString());
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task PrintJson_DryRun_IncludesPostEditMarkdown()
    {
        var summaryWithMarkdown = DefaultSummary with { PostEditMarkdown = "# Heading\n\nBody text." };
        var editor = Substitute.For<IPageEditor>();
        editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
              .Returns(summaryWithMarkdown);

        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalOut = Console.Out;
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile,
                     "--dry-run", "--print", "json"]);

                Assert.Equal(0, exitCode);
                var doc = JsonDocument.Parse(outWriter.ToString());
                Assert.True(doc.RootElement.TryGetProperty("postEditMarkdown", out var md));
                Assert.Equal(summaryWithMarkdown.PostEditMarkdown, md.GetString());
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task UpdateAsync_PassesDryRunFlag()
    {
        var editor = Substitute.For<IPageEditor>();
        editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
              .Returns(DefaultSummary);

        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalOut = Console.Out;
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            try
            {
                await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile, "--dry-run"]);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            await editor.Received(1).UpdateAsync(
                Arg.Is<UpdatePageInput>(i => i.DryRun == true),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task UpdateAsync_PassesAllowLargeDeleteFlag()
    {
        var editor = Substitute.For<IPageEditor>();
        editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
              .Returns(DefaultSummary);

        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalOut = Console.Out;
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            try
            {
                await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile,
                     "--allow-large-delete"]);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            await editor.Received(1).UpdateAsync(
                Arg.Is<UpdatePageInput>(i => i.AllowLargeDelete == true),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task StaleRevision_Exits7_WritesErrorClassAndCurrentRevision()
    {
        var editor = Substitute.For<IPageEditor>();
        editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
              .ThrowsAsync(new StaleRevisionException("newrev123"));

        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalErr = Console.Error;
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile]);

                Assert.Equal(7, exitCode);
                var errOutput = errWriter.ToString();
                Assert.Contains("patch.stale_revision", errOutput);
                Assert.Contains("Current revision: newrev123", errOutput);
            }
            finally
            {
                Console.SetError(originalErr);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task NoMatch_Exits7_WritesErrorClass()
    {
        var editor = Substitute.For<IPageEditor>();
        editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
              .ThrowsAsync(new NoMatchException("Hello"));

        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalErr = Console.Error;
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile]);

                Assert.Equal(7, exitCode);
                Assert.Contains("patch.no_match", errWriter.ToString());
            }
            finally
            {
                Console.SetError(originalErr);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task AmbiguousMatch_Exits7_WritesErrorClass()
    {
        var editor = Substitute.For<IPageEditor>();
        editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
              .ThrowsAsync(new AmbiguousMatchException("Hello", 3));

        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalErr = Console.Error;
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile]);

                Assert.Equal(7, exitCode);
                Assert.Contains("patch.ambiguous_match", errWriter.ToString());
            }
            finally
            {
                Console.SetError(originalErr);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task LargeDelete_Exits7_WritesErrorClass()
    {
        var editor = Substitute.For<IPageEditor>();
        editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
              .ThrowsAsync(new LargeDeleteException(500, 100));

        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalErr = Console.Error;
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile]);

                Assert.Equal(7, exitCode);
                Assert.Contains("patch.large_delete", errWriter.ToString());
            }
            finally
            {
                Console.SetError(originalErr);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task UnknownAnchor_Exits7_WritesErrorClass()
    {
        var editor = Substitute.For<IPageEditor>();
        editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
              .ThrowsAsync(new UnknownAnchorException("<!-- buildin:abc -->"));

        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalErr = Console.Error;
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile]);

                Assert.Equal(7, exitCode);
                Assert.Contains("patch.unknown_anchor", errWriter.ToString());
            }
            finally
            {
                Console.SetError(originalErr);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task PartialPatch_Exits6_WritesErrorClass()
    {
        var editor = Substitute.For<IPageEditor>();
        editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
              .ThrowsAsync(new PartialPatchException("partial_rev", 1, new InvalidOperationException("buildin write failed")));

        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalErr = Console.Error;
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile]);

                Assert.Equal(6, exitCode);
                Assert.Contains("patch.partial", errWriter.ToString());
            }
            finally
            {
                Console.SetError(originalErr);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task MissingPage_Exits2()
    {
        var editor = Substitute.For<IPageEditor>();
        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalErr = Console.Error;
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--revision", Revision, "--ops", opsFile]);

                Assert.Equal(2, exitCode);
            }
            finally
            {
                Console.SetError(originalErr);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task MissingRevision_Exits2()
    {
        var editor = Substitute.For<IPageEditor>();
        var opsFile = WriteOpsFile(ValidOpsJson());
        try
        {
            var (app, _) = CreateApp(editor);

            var originalErr = Console.Error;
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--page", PageId, "--ops", opsFile]);

                Assert.Equal(2, exitCode);
            }
            finally
            {
                Console.SetError(originalErr);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task MissingOps_Exits2()
    {
        var editor = Substitute.For<IPageEditor>();
        var (app, _) = CreateApp(editor);

        var originalErr = Console.Error;
        using var errWriter = new StringWriter();
        Console.SetError(errWriter);
        try
        {
            var exitCode = await app.RunAsync(
                ["update", "--page", PageId, "--revision", Revision]);

            Assert.Equal(2, exitCode);
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task OpsFileNotFound_Exits2()
    {
        var editor = Substitute.For<IPageEditor>();
        var (app, _) = CreateApp(editor);

        var originalErr = Console.Error;
        using var errWriter = new StringWriter();
        Console.SetError(errWriter);
        try
        {
            var exitCode = await app.RunAsync(
                ["update", "--page", PageId, "--revision", Revision,
                 "--ops", "/nonexistent/path/ops.json"]);

            Assert.Equal(2, exitCode);
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task InvalidOpsJson_Exits2()
    {
        var editor = Substitute.For<IPageEditor>();
        var opsFile = WriteOpsFile("this is not json");
        try
        {
            var (app, _) = CreateApp(editor);

            var originalErr = Console.Error;
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile]);

                Assert.Equal(2, exitCode);
            }
            finally
            {
                Console.SetError(originalErr);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    [Fact]
    public async Task EmptyOpsArray_Exits2()
    {
        var editor = Substitute.For<IPageEditor>();
        var opsFile = WriteOpsFile("[]");
        try
        {
            var (app, _) = CreateApp(editor);

            var originalErr = Console.Error;
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            try
            {
                var exitCode = await app.RunAsync(
                    ["update", "--page", PageId, "--revision", Revision, "--ops", opsFile]);

                Assert.Equal(2, exitCode);
            }
            finally
            {
                Console.SetError(originalErr);
            }
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    private sealed class TypeRegistrar : ITypeRegistrar
    {
        private readonly IServiceCollection _services;
        private readonly HashSet<Type> _pinnedTypes;

        public TypeRegistrar(IServiceCollection services, HashSet<Type>? pinnedTypes = null)
        {
            _services = services;
            _pinnedTypes = pinnedTypes ?? [];
        }

        public void Register(Type service, Type implementation) => _services.AddSingleton(service, implementation);
        public void RegisterInstance(Type service, object implementation)
        {
            if (!_pinnedTypes.Contains(service))
                _services.AddSingleton(service, implementation);
        }
        public void RegisterLazy(Type service, Func<object> factory)
        {
            if (!_pinnedTypes.Contains(service))
                _services.AddSingleton(service, _ => factory());
        }
        public ITypeResolver Build() => new TypeResolver(_services.BuildServiceProvider());
    }

    private sealed class TypeResolver : ITypeResolver
    {
        private readonly IServiceProvider _provider;
        public TypeResolver(IServiceProvider provider) => _provider = provider;
        public object? Resolve(Type? type) => type is null ? null : _provider.GetService(type);
    }
}
