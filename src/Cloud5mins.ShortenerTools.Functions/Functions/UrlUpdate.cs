/*
```c#
Input:
    {
        // [Required] New long Url where the user will be redirected to
        "ShortUrl": "https://SOME_URL"

         // [Required] Short Url to extract the Row_Key from
         "ShortUrl": "https://SOME_URL"
    }


Output:
    {
        "Url": "https://SOME_URL",
        "Clicks": 0,
        "PartitionKey": "d",
        "title": "Quickstart: Create your first function in Azure using Visual Studio"
        "RowKey": "doc",
        "Timestamp": "0001-01-01T00:00:00+00:00",
        "ETag": "W/\"datetime'2020-05-06T14%3A33%3A51.2639969Z'\""
    }
*/

using LettrLabs.UrlShorterner.Core.Domain;

// using Microsoft.Azure.WebJobs;
// using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using LettrLabs.UrlShorterner.Core.Messages;

namespace LettrLabs.UrlShorterner.Functions;

public class UrlUpdate : UrlBase
{
    public UrlUpdate(ILoggerFactory loggerFactory,
        ShortenerSettings settings,
        JsonSerializerOptions jsonSerializerOptions,
        StorageTableHelper storageTableHelper) :
        base(loggerFactory.CreateLogger<UrlUpdate>(),
            settings,
            jsonSerializerOptions,
            storageTableHelper)
    { }

    [Function("UrlUpdate")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "api/UrlUpdate")] HttpRequestData req)
    {
        Logger.LogInformation("Updating shortURL");

        var input = await JsonSerializer.DeserializeAsync<ShortUrlEntity>(req.Body, JsonSerializerOptions);
        if (input == null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        // If the Url parameter only contains whitespaces or is empty return with BadRequest.
        if (string.IsNullOrWhiteSpace(input.Url))
            return await GetEmptyUrlBadResponse(req);

        input.Url = FixUrl(input.Url);

        if (!Uri.IsWellFormedUriString(input.Url, UriKind.Absolute))
            return await GetMalformedUrlBadResponse(req, input.Url);

        ShortResponse result;

        try
        {
            result = await UpdateSingleUrl(req, input);
        }
        catch (Exception ex)
        {
            return await GetExceptionBadResponse(req, ex);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);

        return response;
    }

    [Function("UrlUpdateList")]
    public async Task<HttpResponseData> UpdateList(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "api/UrlUpdateList")] HttpRequestData req)
    {
        Logger.LogInformation("Updating shortURL List");

        var inputList = await JsonSerializer.DeserializeAsync<List<ShortUrlEntity>>(req.Body, JsonSerializerOptions);
        if (inputList == null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        var tasks = inputList.Select(request => ProcessSingleRequestAsync(req, request)).ToList();

        var urlResponses = await Task.WhenAll(tasks);
        var urlList = urlResponses.Where(response => response != null).ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(urlList);

        Logger.LogInformation("Updated shortURL List");

        return response;
    }

    private async Task<ShortResponse> ProcessSingleRequestAsync(HttpRequestData req, ShortUrlEntity request)
    {
        try
        {
            // If the Url parameter only contains whitespaces or is empty return with BadRequest.
            if (string.IsNullOrWhiteSpace(request.Url))
                await GetEmptyUrlBadResponse(req);

            request.Url = FixUrl(request.Url);

            if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
                await GetMalformedUrlBadResponse(req, request.Url);

            return await UpdateSingleUrl(req, request);
        }
        catch
        {
            // If one qr code failed, skip it, consumer will handle the missing qr code.
            return null; // Return null to indicate failure, to be filtered out later
        }
    }

    private async Task<ShortResponse> UpdateSingleUrl(HttpRequestData req, ShortUrlEntity input)
    {
        string longUrl = input.Url;
        input.SetKeys();
        input = await StorageTableHelper.UpdateShortUrlEntityUrlAsync(input);

        var host = string.IsNullOrEmpty(Settings.CustomDomain) ? req.Url.Host : Settings.CustomDomain;
        var result = new ShortResponse(req.Url.Scheme, host, input.Url, input.RowKey, input.Title, input.OrderRecipientId);

        Logger.LogInformation("Short Url updated {RedirectUrl} redirecting to {DestinationUrl} for order {OrderId} for {CustomerName}",
            result.ShortUrl, longUrl, input.OrderId, input.OrderRecipientName);
        return result;
    }
}