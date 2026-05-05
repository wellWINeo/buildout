using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Gen = Buildout.Core.Buildin.Generated.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Buildin;

public sealed class ErrorMappingTests
{
    private readonly IRequestAdapter _adapter;
    private readonly IOptions<BuildinClientOptions> _options;
    private readonly ILogger<BotBuildinClient> _logger;
    private readonly BotBuildinClient _client;

    public ErrorMappingTests()
    {
        _adapter = Substitute.For<IRequestAdapter>();
        _adapter.BaseUrl.Returns("https://api.buildin.ai");
        _options = Options.Create(new BuildinClientOptions());
        _logger = Substitute.For<ILogger<BotBuildinClient>>();
        _client = new BotBuildinClient(_adapter, _options, _logger);
    }

    [Fact]
    public async Task HttpRequestException_MapsTo_TransportError()
    {
        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.UserMe>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<Gen.UserMe?>>(_ => throw new HttpRequestException("Connection refused"));

        var ex = await Assert.ThrowsAsync<BuildinApiException>(() => _client.GetMeAsync());

        Assert.NotNull(ex);
        Assert.IsType<TransportError>(ex.Error);
        var transportError = (TransportError)ex.Error;
        Assert.Equal("Connection refused", transportError.Cause.Message);
    }

    [Fact]
    public async Task ApiException_WithGeneratedError_MapsTo_ApiError()
    {
        var apiError = new Gen.Error
        {
            Status = 404,
            Code = Gen.Error_code.Not_found,
            MessageEscaped = "Page not found"
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.Page>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<Gen.Page?>>(_ => throw apiError);

        var ex = await Assert.ThrowsAsync<BuildinApiException>(() => _client.GetPageAsync("00000000-0000-0000-0000-000000000000"));

        Assert.NotNull(ex);
        Assert.IsType<ApiError>(ex.Error);
        var apiErr = (ApiError)ex.Error;
        Assert.Equal(404, apiErr.StatusCode);
        Assert.Equal("not_found", apiErr.Code);
        Assert.Equal("Page not found", apiErr.Message);
    }

    [Fact]
    public async Task ApiException_WithUnauthorized_MapsTo_ApiError()
    {
        var apiError = new Gen.Error
        {
            Status = 401,
            Code = Gen.Error_code.Unauthorized,
            MessageEscaped = "Invalid token"
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.UserMe>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<Gen.UserMe?>>(_ => throw apiError);

        var ex = await Assert.ThrowsAsync<BuildinApiException>(() => _client.GetMeAsync());

        Assert.IsType<ApiError>(ex.Error);
        var apiErr = (ApiError)ex.Error;
        Assert.Equal(401, apiErr.StatusCode);
        Assert.Equal("unauthorized", apiErr.Code);
        Assert.Equal("Invalid token", apiErr.Message);
    }

    [Fact]
    public async Task GenericApiException_MapsTo_ApiError()
    {
        var apiException = new ApiException("Something went wrong");

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.UserMe>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<Gen.UserMe?>>(_ => throw apiException);

        var ex = await Assert.ThrowsAsync<BuildinApiException>(() => _client.GetMeAsync());

        Assert.IsType<ApiError>(ex.Error);
    }

    [Fact]
    public async Task UnexpectedException_MapsTo_UnknownError()
    {
        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.UserMe>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<Gen.UserMe?>>(_ => throw new InvalidOperationException("Unexpected"));

        var ex = await Assert.ThrowsAsync<BuildinApiException>(() => _client.GetMeAsync());

        Assert.IsType<UnknownError>(ex.Error);
    }
}
