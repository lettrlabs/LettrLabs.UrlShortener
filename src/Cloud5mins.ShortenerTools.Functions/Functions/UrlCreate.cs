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
using LettrLabs.UrlShorterner.Functions.Functions.Archived;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LettrLabs.UrlShorterner.Functions
{

    public class UrlCreate
    {
        private readonly ILogger _logger;
        private readonly ShortenerSettings _settings;
        private ShortResponse result;

        public UrlCreate(ILoggerFactory loggerFactory, ShortenerSettings settings)
        {
            _logger = loggerFactory.CreateLogger<UrlList>();
            _settings = settings;
        }

        [Function("UrlCreate")]
        public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "api/UrlCreate")] HttpRequestData req,
            ExecutionContext context
        )
        {
            _logger.LogInformation("Creating shortURL");
            ShortRequest input;

            try
            {
                // Validation of the inputs
                if (req == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                using (var reader = new StreamReader(req.Body))
                {
                    var strBody = await reader.ReadToEndAsync();
                    input = JsonSerializer.Deserialize<ShortRequest>(strBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (input == null)
                    {
                        return req.CreateResponse(HttpStatusCode.NotFound);
                    }
                }

                // If the Url parameter only contains whitespaces or is empty return with BadRequest.
                if (string.IsNullOrWhiteSpace(input.Url))
                {
                    _logger.LogError("Input url is null or empty");
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { Message = "The url parameter can not be empty." });
                    return badResponse;
                }

                // Validates if input.url is a valid absolute url, aka is a complete reference to the resource, ex: http(s)://google.com
                //if input.Url does not begin with http:// or https://, add https:// to the beginning of input.Url
                if (!input.Url.StartsWith("https://") && !input.Url.StartsWith("http://"))
                {
                    input.Url = "https://" + input.Url;
                }

                if (!Uri.IsWellFormedUriString(input.Url, UriKind.Absolute))
                {
                    _logger.LogError("Input url '{DestinationUrl}' is not well formed", input.Url);
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { Message = $"{input.Url} is not a valid absolute Url. The Url parameter must start with 'http://' or 'http://'." });
                    return badResponse;
                }

                _logger.LogInformation("Creating shortURL for {DestinationUrl}", input.Url);
                StorageTableHelper stgHelper = new StorageTableHelper(_settings.DataStorage);

                string longUrl = input.Url.Trim();
                //string vanity = string.IsNullOrWhiteSpace(input.Vanity) ? "" : input.Vanity.Trim();
                string title = string.IsNullOrWhiteSpace(input.Title) ? "" : input.Title.Trim();

                ShortUrlEntity newRow;
                newRow = new ShortUrlEntity(longUrl, await Utility.GetValidEndUrl(stgHelper), title, input);

                await stgHelper.SaveShortUrlEntity(newRow);

                var host = string.IsNullOrEmpty(_settings.CustomDomain) ? req.Url.Host : _settings.CustomDomain.ToString();
                result = new ShortResponse(req.Url.Scheme, host, newRow.Url, newRow.RowKey, newRow.Title);

                _logger.LogInformation("Short Url created {RedirectUrl} redirecting to {DestinationUrl} for order {OrderId} for {CustomerName}",
                    result.ShortUrl, longUrl, input.OrderId, input.OrderRecipientName);
            }
            catch (Exception ex)
            {
                _logger.LogError("An unexpected error was encountered. {Exception}", ex);

                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { ex.Message });
                return badResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);

            return response;
        }
    }
}
