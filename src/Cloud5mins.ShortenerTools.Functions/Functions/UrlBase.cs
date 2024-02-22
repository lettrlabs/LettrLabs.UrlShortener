using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using LettrLabs.UrlShorterner.Core.Domain;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace LettrLabs.UrlShorterner.Functions;

public abstract class UrlBase
{
    protected readonly ILogger Logger;
    protected readonly ShortenerSettings Settings;
    protected readonly JsonSerializerOptions JsonSerializerOptions;
    protected readonly StorageTableHelper StorageTableHelper;

    protected UrlBase(ILogger logger,
        ShortenerSettings settings,
        JsonSerializerOptions jsonSerializerOptions,
        StorageTableHelper storageTableHelper)
    {
        Logger = logger;
        Settings = settings;
        JsonSerializerOptions = jsonSerializerOptions;
        StorageTableHelper = storageTableHelper;
    }

    protected async Task<HttpResponseData> GetExceptionBadResponse(HttpRequestData req, Exception ex)
    {
        Logger.LogError("An unexpected error was encountered. {Exception}", ex);

        var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
        await badResponse.WriteAsJsonAsync(new { ex.Message });
        return badResponse;
    }

    protected async Task<HttpResponseData> GetMalformedUrlBadResponse(HttpRequestData req, string url)
    {
        Logger.LogError("Input url '{DestinationUrl}' is not well formed", url);
        var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
        await badResponse.WriteAsJsonAsync(new { Message = $"{url} is not a valid absolute Url. The Url parameter must start with 'http://' or 'http://'." });
        return badResponse;
    }

    protected static string FixUrl(string url)
    {
        url = url.Trim();
        // Validates if input.url is a valid absolute url, aka is a complete reference to the resource, ex: http(s)://google.com
        //if input.Url does not begin with http:// or https://, add https:// to the beginning of input.Url
        if (!url.StartsWith("https://") && !url.StartsWith("http://"))
            url = "https://" + url;
        return url;
    }

    protected async Task<HttpResponseData> GetEmptyUrlBadResponse(HttpRequestData req)
    {
        Logger.LogError("Input url is null or empty");
        var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
        await badResponse.WriteAsJsonAsync(new { Message = "The url parameter can not be empty." });
        return badResponse;
    }
}