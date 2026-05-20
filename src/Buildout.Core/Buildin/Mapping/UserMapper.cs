using Buildout.Core.Buildin.Models;
using Gen = Buildout.Core.Buildin.Generated.Models;

namespace Buildout.Core.Buildin.Mapping;

internal static class UserMapper
{
    public static UserMe Map(Gen.UserMe gen)
    {
        return new UserMe
        {
            Id = gen.Id?.ToString() ?? string.Empty,
            Name = gen.Name,
            AvatarUrl = gen.AvatarUrl,
            Type = gen.Type ?? "unknown",
            Email = gen.Person?.Email
        };
    }

    public static UserMe? Map(Gen.User? gen)
    {
        if (gen is null) return null;
        return new UserMe
        {
            Id = gen.Id?.ToString() ?? string.Empty,
            Type = "unknown"
        };
    }
}
