using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LettrLabs.UrlShorterner.Core.Domain;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace LettrLabs.UrlShorterner.Functions.Functions
{
    public class UrlRedirect
    {
        private readonly ILogger _logger;
        private readonly ShortenerSettings _settings;

        public UrlRedirect(ILoggerFactory loggerFactory, ShortenerSettings settings)
        {
            _logger = loggerFactory.CreateLogger<UrlRedirect>();
            _settings = settings;
        }

        [Function("UrlRedirect")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{shortUrl}")]
            HttpRequestData req,
            string shortUrl,
            ExecutionContext context)
        {
            _logger.LogInformation("Looking for: {RedirectUrl} to redirect", shortUrl);
            string redirectUrl = "https://azure.com";

            if (!string.IsNullOrWhiteSpace(shortUrl))
            {
                redirectUrl = _settings.DefaultRedirectUrl ?? redirectUrl;

                StorageTableHelper stgHelper = new StorageTableHelper(_settings.DataStorage);

                var tempUrl = new ShortUrlEntity(string.Empty, shortUrl);
                ShortUrlEntity newUrl = await stgHelper.GetShortUrlEntityAsync(tempUrl);

                if (newUrl != null)
                {
                    _logger.LogInformation("Found redirect for: {RedirectUrl} to {DestinationUrl} for order {OrderId} for {OrderRecipientId} {OrderRecipientName}"
                        , shortUrl, newUrl.Url, newUrl.OrderId, newUrl.OrderRecipientId, newUrl.OrderRecipientName);
                    newUrl.Clicks++;
                    await stgHelper.SaveClickStatsEntityAsync(new ClickStatsEntity(newUrl.RowKey));
                    await stgHelper.SaveShortUrlEntityAsync(newUrl);
                    await UpdateOrderRecipientStatisticAsync(newUrl);
                    redirectUrl = WebUtility.UrlDecode(newUrl.ActiveUrl);
                }
            }
            else
            {
                _logger.LogInformation("Bad Link - or no link found for {RedirectUrl}, resorting to fallback.", shortUrl);
            }

            _logger.LogInformation("Redirecting to {RedirectUrl}", redirectUrl);
            var res = req.CreateResponse(HttpStatusCode.Redirect);
            res.Headers.Add("Location", redirectUrl);
            return res;
        }

        private async Task UpdateOrderRecipientStatisticAsync(ShortUrlEntity urlEntity)
        {
            try
            {
                var apiUrl = Environment.GetEnvironmentVariable("LettrLabsApp.ApiUrl");
                var apiKey = Environment.GetEnvironmentVariable("LettrLabsApp.ApiKey");
                var statisticName = Environment.GetEnvironmentVariable("LettrLabsApp.QrCodeScanCountStatistic");

                var jsonZap = JsonSerializer.Serialize(new { urlEntity.OrderId, urlEntity.OrderRecipientId, StatisticName = statisticName, StatisticValue = urlEntity.Clicks });
                StringContent content = new(jsonZap, Encoding.UTF8, "application/json");

                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
                await client.PostAsync($"{apiUrl}/v1/order-recipients-statistics", content);
            }
            catch
            {
                // It's bad if we don't log statistics in the main LettrLabs API, but we can't break the Redirect in case something happens while requesting
            }
        }
    }
}
