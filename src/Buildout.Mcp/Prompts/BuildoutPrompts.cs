using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace Buildout.Mcp.Prompts;

[McpServerPromptType]
public sealed class BuildoutPrompts
{
    [McpServerPrompt(Name = "update")]
    [Description("Detailed instructions for the page update workflow")]
    public static ChatMessage UpdateWorkflow() =>
        new(ChatRole.User, PromptResourceLoader.Load("update"));
}
