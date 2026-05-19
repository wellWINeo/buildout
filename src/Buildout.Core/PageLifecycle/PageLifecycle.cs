using System.Diagnostics.CodeAnalysis;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Diagnostics;
using Buildout.Core.Markdown.Authoring;
using Microsoft.Extensions.Logging;

namespace Buildout.Core.PageLifecycle;

[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Dynamic operation names prevent compile-time LoggerMessage definitions")]
public sealed class PageLifecycle : IPageLifecycle
{
    private readonly IBuildinClient _client;
    private readonly ILogger<PageLifecycle> _logger;

    public PageLifecycle(IBuildinClient client, ILogger<PageLifecycle> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<PageLifecycleOutcome> DeleteAsync(string pageId, CancellationToken cancellationToken = default)
    {
        using var recorder = OperationRecorder.Start(_logger, "page_delete");

        Page page;
        try
        {
            page = await _client.GetPageAsync(pageId, cancellationToken);
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
        {
            recorder.Fail("not_found");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.NotFound, UnderlyingException = ex };
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
        {
            recorder.Fail("auth");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.Auth, UnderlyingException = ex };
        }
        catch (BuildinApiException ex) when (ex.Error is TransportError)
        {
            recorder.Fail("transport");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.Transport, UnderlyingException = ex };
        }
        catch (BuildinApiException ex)
        {
            recorder.Fail("unexpected");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.Unexpected, UnderlyingException = ex };
        }

        if (page.Archived == true)
        {
            recorder.SetTag("changed", false);
            recorder.Succeed();
            return new PageLifecycleOutcome { PageId = pageId, Archived = true, Changed = false };
        }

        try
        {
            await _client.UpdatePageAsync(pageId, new UpdatePageRequest
            {
                Properties = new Dictionary<string, PropertyValue>(),
                Archived = true
            }, cancellationToken);
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
        {
            recorder.Fail("not_found");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.NotFound, UnderlyingException = ex };
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
        {
            recorder.Fail("auth");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.Auth, UnderlyingException = ex };
        }
        catch (BuildinApiException ex) when (ex.Error is TransportError)
        {
            recorder.Fail("transport");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.Transport, UnderlyingException = ex };
        }
        catch (BuildinApiException ex)
        {
            recorder.Fail("unexpected");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.Unexpected, UnderlyingException = ex };
        }

        recorder.SetTag("changed", true);
        recorder.SetTag("status_code", 200);
        recorder.Succeed();
        return new PageLifecycleOutcome { PageId = pageId, Archived = true, Changed = true };
    }

    public async Task<PageLifecycleOutcome> RestoreAsync(string pageId, CancellationToken cancellationToken = default)
    {
        using var recorder = OperationRecorder.Start(_logger, "page_restore");

        Page page;
        try
        {
            page = await _client.GetPageAsync(pageId, cancellationToken);
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
        {
            recorder.Fail("not_found");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.NotFound, UnderlyingException = ex };
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
        {
            recorder.Fail("auth");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.Auth, UnderlyingException = ex };
        }
        catch (BuildinApiException ex) when (ex.Error is TransportError)
        {
            recorder.Fail("transport");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.Transport, UnderlyingException = ex };
        }
        catch (BuildinApiException ex)
        {
            recorder.Fail("unexpected");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.Unexpected, UnderlyingException = ex };
        }

        if (page.Archived == false)
        {
            recorder.SetTag("changed", false);
            recorder.Succeed();
            return new PageLifecycleOutcome { PageId = pageId, Archived = false, Changed = false };
        }

        try
        {
            await _client.UpdatePageAsync(pageId, new UpdatePageRequest
            {
                Properties = new Dictionary<string, PropertyValue>(),
                Archived = false
            }, cancellationToken);
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
        {
            recorder.Fail("not_found");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.NotFound, UnderlyingException = ex };
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
        {
            recorder.Fail("auth");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.Auth, UnderlyingException = ex };
        }
        catch (BuildinApiException ex) when (ex.Error is TransportError)
        {
            recorder.Fail("transport");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.Transport, UnderlyingException = ex };
        }
        catch (BuildinApiException ex)
        {
            recorder.Fail("unexpected");
            return new PageLifecycleOutcome { PageId = pageId, Changed = false, FailureClass = FailureClass.Unexpected, UnderlyingException = ex };
        }

        recorder.SetTag("changed", true);
        recorder.SetTag("status_code", 200);
        recorder.Succeed();
        return new PageLifecycleOutcome { PageId = pageId, Archived = false, Changed = true };
    }
}
