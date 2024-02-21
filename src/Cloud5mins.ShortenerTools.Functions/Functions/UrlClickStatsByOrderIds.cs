/*
```c#
Input:


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
using LettrLabs.UrlShorterner.Core.Messages;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LettrLabs.UrlShorterner.Functions.Functions.Archived
{
    public class UrlClickStatsByOrderIds
    {

        private readonly ILogger _logger;
        private readonly ShortenerSettings _settings;

        public UrlClickStatsByOrderIds(ILoggerFactory loggerFactory, ShortenerSettings settings)
        {
            _logger = loggerFactory.CreateLogger<UrlList>();
            _settings = settings;
        }

        [Function("UrlClickStatsByOrderIds")]
        public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "api/UrlClickStatsByOrderIds")] HttpRequestData req, ExecutionContext context)
        {
            _logger.LogInformation("Starting UrlClickStatsByOrderIds");

            var result = new ListResponse();
            string userId = string.Empty;
            UrlClickStatsByOrderIdsRequest input;

            StorageTableHelper stgHelper = new StorageTableHelper(_settings.DataStorage);

            try
            {
                using (var reader = new StreamReader(req.Body))
                {
                    var strBody = await reader.ReadToEndAsync();
                    input = JsonSerializer.Deserialize<UrlClickStatsByOrderIdsRequest>(strBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (input == null)
                    {
                        return req.CreateResponse(HttpStatusCode.NotFound);
                    }
                }

                if (input.OrderIds.Any())
                {
                    result.UrlList = await stgHelper.GetAllShortUrlEntitiesByOrderIdsAsync(input.OrderIds);
                    result.UrlList = result.UrlList.Where(p => !(p.IsArchived ?? false)).ToList();
                    var host = string.IsNullOrEmpty(_settings.CustomDomain) ? req.Url.Host : _settings.CustomDomain;
                    foreach (ShortUrlEntity url in result.UrlList)
                    {
                        url.ShortUrl = Utility.GetShortUrl(host, url.RowKey);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("An unexpected error was encountered {Exception}", ex);
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { ex.Message });
                return badRequest;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
    }
}
