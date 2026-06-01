using Buildout.AdminUI.Models;

namespace Buildout.AdminUI.Services;

public interface IApiKeyService
{
    IReadOnlyList<ApiKey> GetAll();
}
