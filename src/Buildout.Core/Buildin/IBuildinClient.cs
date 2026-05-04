using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Buildin;

public interface IBuildinClient
{
    Task<UserMe> GetMeAsync(CancellationToken cancellationToken = default);
    Task<Page> GetPageAsync(string pageId, CancellationToken cancellationToken = default);
    Task<Page> CreatePageAsync(CreatePageRequest request, CancellationToken cancellationToken = default);
    Task<Page> UpdatePageAsync(string pageId, UpdatePageRequest request, CancellationToken cancellationToken = default);
    Task<Block> GetBlockAsync(string blockId, CancellationToken cancellationToken = default);
    Task<Block> UpdateBlockAsync(string blockId, UpdateBlockRequest request, CancellationToken cancellationToken = default);
    Task DeleteBlockAsync(string blockId, CancellationToken cancellationToken = default);
    Task<PaginatedList<Block>> GetBlockChildrenAsync(string blockId, BlockChildrenQuery? query = null, CancellationToken cancellationToken = default);
    Task<AppendBlockChildrenResult> AppendBlockChildrenAsync(string blockId, AppendBlockChildrenRequest request, CancellationToken cancellationToken = default);
    Task<Database> CreateDatabaseAsync(CreateDatabaseRequest request, CancellationToken cancellationToken = default);
    Task<Database> GetDatabaseAsync(string databaseId, CancellationToken cancellationToken = default);
    Task<Database> UpdateDatabaseAsync(string databaseId, UpdateDatabaseRequest request, CancellationToken cancellationToken = default);
    Task<QueryDatabaseResult> QueryDatabaseAsync(string databaseId, QueryDatabaseRequest request, CancellationToken cancellationToken = default);
    Task<SearchResults> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);
    Task<PageSearchResults> SearchPagesAsync(PageSearchRequest request, CancellationToken cancellationToken = default);
}
