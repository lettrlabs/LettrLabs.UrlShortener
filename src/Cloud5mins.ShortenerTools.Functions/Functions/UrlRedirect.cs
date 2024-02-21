using LettrLabs.UrlShorterner.Core.Domain;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LettrLabs.UrlShorterner.Functions.Functions.Archived
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
    }
}
