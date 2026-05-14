using System.Globalization;
using System.Text;
using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin;
using Buildout.Core.DependencyInjection;
using Buildout.IntegrationTests.Buildin;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Buildout.IntegrationTests.Cli;

[Collection("BuildinWireMock")]
public sealed class CreateCommandTests
{
    private readonly BuildinWireMockFixture _fixture;

    private const string ParentId = "00000000-0000-0000-0000-000000000001";
    private const string NewPageId = "00000000-0000-0000-0000-000000000002";

    public CreateCommandTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private void SetupStubs()
    {
        BuildinStubs.RegisterPageProbe(_fixture.Server, ParentId, new
        {
            id = ParentId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{ParentId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });

        BuildinStubs.RegisterCreatePage(_fixture.Server, new
        {
            id = NewPageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{NewPageId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });

        BuildinStubs.RegisterAppendBlockChildren(_fixture.Server, NewPageId, new
        {
            @object = "list",
            results = Array.Empty<object>(),
            has_more = false,
            next_cursor = (string?)null
        });
    }

    private static (CommandApp app, TestConsole console) CreateCliApp(IBuildinClient client)
    {
        var services = new ServiceCollection();
        services.AddBuildoutCore();
        services.AddSingleton(client);

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
            config.AddCommand<CreateCommand>("create");
        });

        return (app, testConsole);
    }

    private static string GenerateLargeMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Large Test Page");
        sb.AppendLine();

        for (int i = 0; i < 60; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Paragraph {i + 1}: Lorem ipsum dolor sit amet, consectetur adipiscing elit.");
            sb.AppendLine();

            sb.AppendLine(CultureInfo.InvariantCulture, $"- Bullet A{i + 1}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Bullet B{i + 1}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Bullet C{i + 1}");
            sb.AppendLine();

            sb.AppendLine(CultureInfo.InvariantCulture, $"1. Item {i + 1}");
            sb.AppendLine();
        }

        // Total blocks: 60 paragraphs + 180 bullets + 60 numbered = 300 blocks
        // Total lines: 60*(2+5+2) = 540 lines + blanks ~ 1000 lines total

        return sb.ToString();
    }

    [Fact]
    public async Task CreateCommand_LargeMarkdown_CompletesUnder4Seconds()
    {
        SetupStubs();

        var markdown = GenerateLargeMarkdown();
        var client = _fixture.CreateClient();
        var (app, console) = CreateCliApp(client);

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, markdown);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var exitCode = await app.RunAsync(["create", tempFile, "--parent", ParentId]);
            sw.Stop();

            Assert.Equal(0, exitCode);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(4),
                $"Expected create to complete in < 4s, took {sw.Elapsed.TotalSeconds:F2}s");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CreateCommand_PlainMarkdown_OutputsPageId()
    {
        BuildinStubs.RegisterPageProbe(_fixture.Server, ParentId, new
        {
            id = ParentId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{ParentId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });
        BuildinStubs.RegisterCreatePage(_fixture.Server, new
        {
            id = NewPageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{NewPageId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });
        BuildinStubs.RegisterGetBlockChildren(_fixture.Server);

        var client = _fixture.CreateClient();
        var (app, console) = CreateCliApp(client);

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "# My Page\n\nHello world.");

            var exitCode = await app.RunAsync(["create", tempFile, "--parent", ParentId]);

            Assert.Equal(0, exitCode);
            Assert.Equal(NewPageId, console.Output.Trim());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CreateCommand_PrintJson_OutputsValidJson()
    {
        BuildinStubs.RegisterPageProbe(_fixture.Server, ParentId, new
        {
            id = ParentId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{ParentId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });
        BuildinStubs.RegisterCreatePage(_fixture.Server, new
        {
            id = NewPageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{NewPageId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });
        BuildinStubs.RegisterGetBlockChildren(_fixture.Server);

        var client = _fixture.CreateClient();
        var (app, console) = CreateCliApp(client);

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "# Json Test\n\nContent.");

            var exitCode = await app.RunAsync(["create", tempFile, "--parent", ParentId, "--print", "json"]);

            Assert.Equal(0, exitCode);
            var rawJson = console.Output.Trim().Replace("\n", "").Replace("\r", "");
            var json = System.Text.Json.JsonDocument.Parse(rawJson).RootElement;
            Assert.Equal(NewPageId, json.GetProperty("id").GetString());
            Assert.Equal($"buildin://{NewPageId}", json.GetProperty("uri").GetString());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CreateCommand_PrintNone_OutputsNothing()
    {
        BuildinStubs.RegisterPageProbe(_fixture.Server, ParentId, new
        {
            id = ParentId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{ParentId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });
        BuildinStubs.RegisterCreatePage(_fixture.Server, new
        {
            id = NewPageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{NewPageId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });
        BuildinStubs.RegisterGetBlockChildren(_fixture.Server);

        var client = _fixture.CreateClient();
        var (app, console) = CreateCliApp(client);

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "# Silent Test\n\nContent.");

            var exitCode = await app.RunAsync(["create", tempFile, "--parent", ParentId, "--print", "none"]);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(console.Output), $"Expected no output but got: {console.Output}");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CreateCommand_NoTitle_ReturnsExitCode2()
    {
        BuildinStubs.RegisterPageProbe(_fixture.Server, ParentId, new
        {
            id = ParentId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{ParentId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });

        var client = _fixture.CreateClient();
        var (app, console) = CreateCliApp(client);

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "No leading heading here.");

            var exitCode = await app.RunAsync(["create", tempFile, "--parent", ParentId]);

            Assert.Equal(2, exitCode);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CreateCommand_ParentNotFound_ReturnsExitCode3()
    {
        BuildinStubs.RegisterPageProbeNotFound(_fixture.Server, ParentId);
        BuildinStubs.RegisterDatabaseProbeNotFound(_fixture.Server, ParentId);

        var client = _fixture.CreateClient();
        var (app, console) = CreateCliApp(client);

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "# Some Page\n\nContent.");

            var exitCode = await app.RunAsync(["create", tempFile, "--parent", ParentId]);

            Assert.Equal(3, exitCode);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CreateCommand_AppendFails_ReturnsExitCode6WithPartialId()
    {
        BuildinStubs.RegisterPageProbe(_fixture.Server, ParentId, new
        {
            id = ParentId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{ParentId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });
        BuildinStubs.RegisterCreatePage(_fixture.Server, new
        {
            id = NewPageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{NewPageId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });
        BuildinStubs.RegisterGetBlockChildren(_fixture.Server);
        BuildinStubs.RegisterAppendBlockChildrenFailure(_fixture.Server, NewPageId, 500);

        var client = _fixture.CreateClient();
        var (app, console) = CreateCliApp(client);

        var sb = new StringBuilder();
        sb.AppendLine("# Partial Page");
        sb.AppendLine();
        for (int i = 0; i < 110; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Paragraph {i + 1}: content line.");
            sb.AppendLine();
        }

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, sb.ToString());

            var exitCode = await app.RunAsync(["create", tempFile, "--parent", ParentId]);

            Assert.Equal(6, exitCode);
            Assert.Contains(NewPageId, console.Output);
        }
        finally
        {
            File.Delete(tempFile);
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
