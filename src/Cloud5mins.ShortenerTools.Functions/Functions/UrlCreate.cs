/*
```c#
Input:

    {
        // [Required] The url you wish to have a short version for
        "url": "https://docs.microsoft.com/en-ca/azure/azure-functions/functions-create-your-first-function-visual-studio",
        
        // [Optional] Title of the page, or text description of your choice.
        "title": "Quickstart: Create your first function in Azure using Visual Studio"

        // [Optional] the end of the URL. If nothing one will be generated for you.
        "vanity": "azFunc"
    }

Output:
    {
        "ShortUrl": "http://c5m.ca/azFunc",
        "LongUrl": "https://docs.microsoft.com/en-ca/azure/azure-functions/functions-create-your-first-function-visual-studio"
    }
*/

using LettrLabs.UrlShorterner.Core.Domain;
using LettrLabs.UrlShorterner.Core.Messages;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace LettrLabs.UrlShorterner.Functions;

public class UrlCreate : UrlBase
{
    public UrlCreate(ILoggerFactory loggerFactory,
        ShortenerSettings settings,
        JsonSerializerOptions jsonSerializerOptions,
        StorageTableHelper storageTableHelper) :
        base(loggerFactory.CreateLogger<UrlCreate>(),
            settings,
            jsonSerializerOptions,
            storageTableHelper)
    { }

    [Function("UrlCreate")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "api/UrlCreate")] HttpRequestData req)
    {
        Logger.LogInformation("Creating shortURL");

        var input = await JsonSerializer.DeserializeAsync<ShortRequest>(req.Body, JsonSerializerOptions);
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
            result = await CreateSingleUrl(req, input);
        }
        catch (Exception ex)
        {
            return await GetExceptionBadResponse(req, ex);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);

        return response;
    }

    [Function("UrlCreateList")]
    public async Task<HttpResponseData> CreateList(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "api/UrlCreateList")] HttpRequestData req)
    {
        Logger.LogInformation("Creating shortURL List");

        var inputList = await JsonSerializer.DeserializeAsync<List<ShortRequest>>(req.Body, JsonSerializerOptions);
        if (inputList == null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        var tasks = inputList.Select(request => ProcessSingleRequestAsync(req, request)).ToList();

        var urlResponses = await Task.WhenAll(tasks);
        var urlList = urlResponses.Where(response => response != null).ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(urlList);

        return response;
    }

    private async Task<ShortResponse> ProcessSingleRequestAsync(HttpRequestData req, ShortRequest request)
    {
        try
        {
            // If the Url parameter only contains whitespaces or is empty return with BadRequest.
            if (string.IsNullOrWhiteSpace(request.Url))
                await GetEmptyUrlBadResponse(req);

            request.Url = FixUrl(request.Url);

            if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
                await GetMalformedUrlBadResponse(req, request.Url);

            return await CreateSingleUrl(req, request);
        }
        catch
        {
            // If one qr code failed, skip it, consumer will handle the missing qr code.
            return null; // Return null to indicate failure, to be filtered out later
        }
    }

    private async Task<ShortResponse> CreateSingleUrl(HttpRequestData req, ShortRequest input)
    {
        string longUrl = input.Url;
        string title = string.IsNullOrWhiteSpace(input.Title) ? "" : input.Title.Trim();

        var newRow = new ShortUrlEntity(longUrl, await Utility.GetValidEndUrl(StorageTableHelper), title, input);

        await StorageTableHelper.SaveShortUrlEntityAsync(newRow);

        var host = string.IsNullOrEmpty(Settings.CustomDomain) ? req.Url.Host : Settings.CustomDomain;
        var result = new ShortResponse(req.Url.Scheme, host, newRow.Url, newRow.RowKey, newRow.Title, input.OrderRecipientId);

        Logger.LogInformation("Short Url created {RedirectUrl} redirecting to {DestinationUrl} for order {OrderId} for {CustomerName}",
            result.ShortUrl, longUrl, input.OrderId, input.OrderRecipientName);
        return result;
    }
}